using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using System.Diagnostics;

namespace CastrumTactics.AI;

/// <summary>Ranks authoritative legal selections using tactical heuristics and optional simultaneous search.</summary>
/// <remarks>
/// Computation is synchronous and stateless. Concurrent calls may share the engine, snapshots, and immutable
/// profiles. Supply an independent random source per concurrent call. Equivalent resolved inputs and random
/// sequences reproduce results with the same engine and runtime when the elapsed-time safeguard is disabled.
/// </remarks>
public sealed class CpuDecisionEngine
{
    private readonly TimeSpan? _searchTimeLimit;

    /// <summary>Creates a decision engine that uses deterministic search pair limits.</summary>
    public CpuDecisionEngine() { }

    /// <summary>Creates a decision engine with an additional elapsed-time safeguard for strategic search.</summary>
    /// <param name="searchTimeLimit">A positive finite duration. Search checks the limit between authoritative resolutions.</param>
    /// <exception cref="ArgumentOutOfRangeException">The duration is zero or negative.</exception>
    /// <remarks>A time-limited call can fall back to a completed shallower pass or a static legal decision.
    /// Omit the limit when comparing seeded benchmark runs.</remarks>
    public CpuDecisionEngine(TimeSpan searchTimeLimit)
    {
        if (searchTimeLimit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(searchTimeLimit));
        _searchTimeLimit = searchTimeLimit;
    }

    /// <summary>Chooses one selection without writing to the snapshot or submitting a command.</summary>
    /// <param name="snapshot">The public snapshot supplying the board and legal selections.</param>
    /// <param name="player">The player for whom to choose.</param>
    /// <param name="profile">The immutable evaluation and sampling configuration.</param>
    /// <param name="random">The caller-owned random source used for sampling.</param>
    /// <param name="cancellationToken">A token observed during analysis and before returning.</param>
    /// <returns>A legal, round-tagged decision, or null if the phase has no decision or the legal set is empty.</returns>
    /// <exception cref="ArgumentNullException">The snapshot, profile, or random source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The player is undefined.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public CpuDecision? Decide(GameSessionSnapshot snapshot, PlayerId player, CpuProfile profile, Random random,
        CancellationToken cancellationToken = default)
    {
        var (decision, _, _) = Evaluate(snapshot, player, profile, random, cancellationToken, diagnostics: false);
        cancellationToken.ThrowIfCancellationRequested();
        return decision;
    }

    /// <summary>Chooses a selection and returns the ranked feature values and sampling probabilities.</summary>
    /// <param name="snapshot">The public snapshot supplying the board and legal selections.</param>
    /// <param name="player">The player for whom to choose.</param>
    /// <param name="profile">The immutable evaluation and sampling configuration.</param>
    /// <param name="random">The caller-owned random source used for sampling.</param>
    /// <param name="cancellationToken">A token observed during analysis and before returning.</param>
    /// <returns>A decision report. When no decision is available the candidate list is empty.</returns>
    /// <exception cref="ArgumentNullException">The snapshot, profile, or random source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The player is undefined.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public CpuDecisionReport DecideWithDiagnostics(GameSessionSnapshot snapshot, PlayerId player, CpuProfile profile,
        Random random, CancellationToken cancellationToken = default)
    {
        long start = Stopwatch.GetTimestamp();
        var (decision, candidates, search) = Evaluate(snapshot, player, profile, random, cancellationToken, diagnostics: true);
        var report = new CpuDecisionReport(decision, candidates,
            search is null ? null : search with { Elapsed = Stopwatch.GetElapsedTime(start) });
        cancellationToken.ThrowIfCancellationRequested();
        return report;
    }

    private (CpuDecision? Decision, CpuCandidate[] Candidates, CpuSearchReport? Search) Evaluate(
        GameSessionSnapshot snapshot, PlayerId player, CpuProfile profile, Random random, CancellationToken token,
        bool diagnostics)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(random);
        PlayerId opponent = player switch
        {
            PlayerId.PlayerOne => PlayerId.PlayerTwo,
            PlayerId.PlayerTwo => PlayerId.PlayerOne,
            _ => throw new ArgumentOutOfRangeException(nameof(player))
        };
        token.ThrowIfCancellationRequested();
        long start = _searchTimeLimit.HasValue ? Stopwatch.GetTimestamp() : 0;

        bool opening = snapshot.Phase == MatchPhase.Opening;
        IReadOnlySet<BoardPosition>? ownLegal = snapshot.Phase == MatchPhase.Main
            ? snapshot.GetMainPhaseLegalSelections(player) : null;
        if (opening ? snapshot.EligiblePositions.Count == 0 : ownLegal is null || ownLegal.Count == 0)
        {
            return (null, [], null);
        }

        bool knightSupport = RuleSetDefinition.Get(snapshot.RuleSet).RequiresKnightSupport;
        var openingOptions = opening && knightSupport ? profile.OpeningSearch : null;
        bool strategic = knightSupport && (profile.Search is not null || openingOptions is not null);
        var board = new TacticalBoard(snapshot.Board, opening, token, snapshot.RuleSet);
        bool noStartingPieces = board.StartingPieceCount == 0;
        CpuCandidate[] candidates;
        CpuCandidate[]? replies = null;
        if (opening)
        {
            BoardPosition[] positions = snapshot.EligiblePositions.OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
            candidates = new CpuCandidate[positions.Length];
            TacticalCandidates.RankOpening(board, positions, player, opponent, noStartingPieces, knightSupport, profile, candidates, token);
            TacticalCandidates.Sort(candidates);
            if (strategic)
            {
                replies = new CpuCandidate[positions.Length];
                TacticalCandidates.RankOpening(board, positions, opponent, player, noStartingPieces, knightSupport, profile, replies, token);
                TacticalCandidates.Sort(replies);
            }
        }
        else
        {
            IReadOnlySet<BoardPosition> enemyLegal = snapshot.GetMainPhaseLegalSelections(opponent);
            candidates = TacticalCandidates.RankMain(board, snapshot.Territory!, player, opponent, ownLegal!, enemyLegal, profile,
                knightSupport, token);
            if (strategic)
                replies = TacticalCandidates.RankMain(board, snapshot.Territory!, opponent, player, enemyLegal, ownLegal!, profile,
                    knightSupport, token);
        }

        CpuSearchReport? searchReport = null;
        int selectable = candidates.Length;
        bool usesMatrix = false;
        if (replies is not null)
        {
            SimultaneousSearch.SearchResult result;
            if (openingOptions is not null)
            {
                result = new OpeningSearch(snapshot, player, openingOptions, token, start, _searchTimeLimit)
                    .Analyze(candidates, replies, diagnostics);
            }
            else
            {
                var search = new SimultaneousSearch(snapshot, player, opponent, profile, token, start, _searchTimeLimit);
                result = opening
                    ? search.AnalyzeDraft(snapshot, candidates, replies, diagnostics)
                    : search.AnalyzeMain(snapshot, candidates, replies, diagnostics);
            }
            candidates = result.Candidates;
            selectable = result.Selectable;
            searchReport = result.Report;
            usesMatrix = result.UsesMatrix;
        }

        token.ThrowIfCancellationRequested();
        Span<CpuCandidate> sampling = candidates.AsSpan(0, selectable);
        double temperature = opening ? profile.OpeningTemperature : profile.MainTemperature;
        int limit = opening ? profile.OpeningCandidateLimit : profile.MainCandidateLimit;
        double? maximumGap = opening ? profile.OpeningMaximumScoreDifference : profile.MainMaximumScoreDifference;
        int poolSize = temperature == 0 ? 1 : CandidateSampling.GetPoolSize(sampling, limit, opening ? 2 : 1, maximumGap);
        double diversity = opening
            ? (noStartingPieces && candidates.Length == 2 ? profile.UrgentOpeningDiversity : profile.OpeningDiversity)
            : 0;
        int selected = usesMatrix ? CandidateSampling.SampleMatrix(sampling, random) : CandidateSampling.Sample(sampling, poolSize, temperature, diversity, random);
        return (new CpuDecision(player, candidates[selected].Position, snapshot.Phase, snapshot.RoundId), candidates,
            searchReport);
    }
}
