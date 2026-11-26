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
    // Scores combine a bounded set of weighted features. This absolute margin covers arithmetic roundoff.
    private const double ScoreEquivalenceTolerance = 1e-12;

    private const double OpeningFirstPieceUrgency = 1.0;
    private const double OpeningEarlyCollisionCost = 0.7;
    private const double OpeningEstablishedCollisionCost = 0.4;
    private const double OpeningDenialCredit = 0.2;
    private const double OpeningGateCollisionCost = 0.2;
    private const double OpeningBalancedRegionFactor = 0.8;

    private const double CaptureRemovalValue = 0.5;
    private const double CaptureRetainedKnightValue = 0.25;
    private const double CaptureValuePerAdditionalHealth = 0.125;
    private const double ThreatenedFortificationValue = 0.8;
    private const double UnthreatenedFortificationValue = 0.25;
    private const double TraversableGateFactor = 0.35;
    private const double PlacementOrUpgradeCollisionCost = 0.65;
    private const double RetainedKnightCollisionCost = 0.25;
    private const double GraveCaptureCollisionCost = 0.05;
    private const double MaximumNewKnightExposure = 0.35;
    private const double StartingCastleGateValue = 0.25;
    private const double MultipleSupportValue = 0.25;
    private const double CastleSupportValue = 0.35;
    private const double DependentKnightValue = 0.25;
    private const double GroundExhaustionFactor = 0.5;
    private const double SoleSupportThreatRisk = 0.5;
    private const double OpponentResponseDiscount = 0.5;
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
        bool strategic = knightSupport && profile.Search is not null;
        var board = new TacticalBoard(snapshot.Board, opening, token, snapshot.RuleSet);
        bool noStartingPieces = board.StartingPieceCount == 0;
        CpuCandidate[] candidates;
        CpuCandidate[]? replies = null;
        if (opening)
        {
            BoardPosition[] positions = snapshot.EligiblePositions.OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
            candidates = new CpuCandidate[positions.Length];
            RankOpening(board, positions, player, opponent, noStartingPieces, knightSupport, profile, candidates, token);
            Sort(candidates);
            if (strategic)
            {
                replies = new CpuCandidate[positions.Length];
                RankOpening(board, positions, opponent, player, noStartingPieces, knightSupport, profile, replies, token);
                Sort(replies);
            }
        }
        else
        {
            IReadOnlySet<BoardPosition> enemyLegal = snapshot.GetMainPhaseLegalSelections(opponent);
            candidates = RankMain(board, snapshot.Territory!, player, opponent, ownLegal!, enemyLegal, profile,
                knightSupport, token);
            if (strategic)
                replies = RankMain(board, snapshot.Territory!, opponent, player, enemyLegal, ownLegal!, profile,
                    knightSupport, token);
        }

        CpuSearchReport? searchReport = null;
        int selectable = candidates.Length;
        bool usesMatrix = false;
        if (replies is not null)
        {
            var result = new SimultaneousSearch(snapshot, player, opponent, profile, token, start, _searchTimeLimit)
                .Analyze(snapshot, candidates, replies, diagnostics);
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
        int poolSize = temperature == 0 ? 1 : GetPoolSize(sampling, limit, opening ? 2 : 1, maximumGap);
        double diversity = opening
            ? (noStartingPieces && candidates.Length == 2 ? profile.UrgentOpeningDiversity : profile.OpeningDiversity)
            : 0;
        int selected = usesMatrix ? SampleMatrix(sampling, random) : Sample(sampling, poolSize, temperature, diversity, random);
        return (new CpuDecision(player, candidates[selected].Position, snapshot.Phase, snapshot.RoundId), candidates,
            searchReport);
    }

    private static int SampleMatrix(ReadOnlySpan<CpuCandidate> policy, Random random)
    {
        int last = 0;
        int support = 0;
        for (int i = 0; i < policy.Length; i++)
        {
            if (policy[i].Probability <= 0) continue;
            last = i;
            support++;
        }
        if (support == 1) return last;
        double draw = random.NextDouble();
        for (int i = 0; i <= last; i++)
        {
            draw -= policy[i].Probability;
            if (draw < 0) return i;
        }
        return last;
    }

    internal static CpuCandidate[] RankMain(TacticalBoard board, TerritoryAnalysis territory, PlayerId player,
        PlayerId opponent, IReadOnlySet<BoardPosition> ownLegal, IReadOnlySet<BoardPosition> enemyLegal,
        CpuProfile profile, bool knightSupport, CancellationToken token)
    {
        var candidates = new CpuCandidate[ownLegal.Count];
        int i = 0;
        foreach (BoardPosition position in ownLegal)
        {
            token.ThrowIfCancellationRequested();
            CpuFeatures features = MainFeatures(board, position, player, opponent, territory, ownLegal, enemyLegal,
                knightSupport);
            candidates[i++] = new(position, features, features.Score(profile.Weights), 0);
        }
        Sort(candidates);
        return candidates;
    }

    private static void Sort(CpuCandidate[] candidates) => Array.Sort(candidates, static (a, b) =>
    {
        int compare = b.Score.CompareTo(a.Score);
        if (compare != 0) return compare;
        compare = a.Position.Y.CompareTo(b.Position.Y);
        return compare != 0 ? compare : a.Position.X.CompareTo(b.Position.X);
    });

    private static void RankOpening(TacticalBoard board, BoardPosition[] positions, PlayerId player, PlayerId opponent,
        bool noStartingPieces, bool knightSupport, CpuProfile profile, CpuCandidate[] candidates, CancellationToken token)
    {
        var opponentAppeal = new double[positions.Length];
        double mostAppealing = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            int index = board.Index(positions[i]);
            CpuFeatures ours = OpeningFeatures(board, index, player, opponent, knightSupport);
            CpuFeatures theirs = OpeningFeatures(board, index, opponent, player, knightSupport);
            // Use a public, profile-independent model of opponent appeal, with no submission flags.
            opponentAppeal[i] = (theirs.Expansion + theirs.Territory + theirs.Spread) / 3;
            mostAppealing = Math.Max(mostAppealing, opponentAppeal[i]);
            candidates[i] = new CpuCandidate(positions[i], ours, 0, 0);
        }

        double urgency = noStartingPieces
            ? (positions.Length <= 3 ? OpeningFirstPieceUrgency : OpeningEarlyCollisionCost)
            : OpeningEstablishedCollisionCost;
        for (int i = 0; i < candidates.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            CpuFeatures features = candidates[i].Features;
            double appeal = mostAppealing > 0 ? opponentAppeal[i] / mostAppealing : 1;
            // Denying a prized start can be worthwhile after footholds are established. A blocked gate adds cost.
            double denial = noStartingPieces ? 0 : opponentAppeal[i] * OpeningDenialCredit;
            double risk = Math.Clamp(appeal * (urgency + features.Bottleneck * OpeningGateCollisionCost - denial) /
                Math.Sqrt(positions.Length / 2.0), 0, 1);
            features = features with { Risk = risk };
            candidates[i] = candidates[i] with { Features = features, Score = features.Score(profile.Weights) };
        }
    }

    internal static CpuFeatures OpeningFeatures(TacticalBoard board, int index, PlayerId player, PlayerId opponent,
        bool knightSupport = false)
    {
        int free = 0;
        foreach (int neighbor in board.Neighbors(index))
            if (board.Cells[neighbor].IsPlayable && board.Cells[neighbor].Piece == PieceType.None) free++;
        TacticalBoard.Region region = board.RegionAt(index);
        double extent = (double)region.Size / board.PlayableCount;
        double spread = 1.0 / (1 + region.Count(player) + board.Support(index, player));
        double territory = extent * (region.Count(opponent) > region.Count(player) ? 1.0 : OpeningBalancedRegionFactor);
        double gate = board.Bottleneck(index);
        if (knightSupport)
        {
            territory = Math.Min(1, territory + gate * StartingCastleGateValue);
        }
        return new CpuFeatures(free / 8.0, territory, 0, 0, 0, gate, spread * extent, 0);
    }

    internal static CpuFeatures MainFeatures(TacticalBoard board, BoardPosition position, PlayerId player, PlayerId opponent,
        TerritoryAnalysis territory, IReadOnlySet<BoardPosition> ownLegal, IReadOnlySet<BoardPosition> enemyLegal,
        bool knightSupport = false)
    {
        int index = board.Index(position);
        CellState cell = board.Cells[index];
        bool capture = cell.Piece == PieceType.Knight && cell.Owner == opponent;
        bool upgrade = cell.Piece == PieceType.Knight && cell.Owner == player;
        bool lethal = capture && cell.GroundHealth <= 1;
        bool gainsReach = cell.Piece == PieceType.None || (capture && !lethal);
        bool shared = enemyLegal.Contains(position);
        int newReach = 0;
        int denied = 0;
        int nearbyThreats = 0;
        double contestedInfluence = 0;

        foreach (int neighbor in board.Neighbors(index))
        {
            BoardPosition target = board.Position(neighbor);
            if (territory.GetControl(target) != TerritoryControl.Contested) continue;

            bool enemyOption = enemyLegal.Contains(target);
            if (enemyOption) nearbyThreats++;
            if (capture && enemyOption && board.Support(neighbor, opponent) == 1 &&
                board.Cells[neighbor].Owner != opponent)
            {
                denied++;
            }

            if (gainsReach && board.Cells[neighbor].Piece == PieceType.None)
            {
                if (!ownLegal.Contains(target)) newReach++;
                contestedInfluence += 1.0 / (1 + board.Support(neighbor, player) + board.Support(neighbor, opponent));
            }
        }

        double captureValue = capture ? CaptureRemovalValue : 0;
        if (capture && gainsReach)
        {
            captureValue += CaptureRetainedKnightValue +
                CaptureValuePerAdditionalHealth * Math.Min(cell.GroundHealth - 2, 2);
        }

        double fortification = upgrade ? (shared ? ThreatenedFortificationValue : UnthreatenedFortificationValue) : 0;
        double gate = board.Bottleneck(index) * (upgrade || lethal ? 1.0 : TraversableGateFactor);
        // A collision leaves a grave on the target, including when our knight was attempting to upgrade.
        double risk = shared
            ? (capture ? (lethal ? GraveCaptureCollisionCost : RetainedKnightCollisionCost) : PlacementOrUpgradeCollisionCost)
            : 0;
        if (gainsReach) risk += MaximumNewKnightExposure * nearbyThreats / 8.0;

        var features = new CpuFeatures(newReach / 8.0, contestedInfluence / 8, captureValue, fortification,
            denied / 8.0, gate, 0, risk);
        return knightSupport ? AddSupportFeatures(features, board, index, player, opponent, enemyLegal,
            gainsReach, upgrade, capture, shared) : features;
    }

    private static CpuFeatures AddSupportFeatures(CpuFeatures features, TacticalBoard board, int index,
        PlayerId player, PlayerId opponent, IReadOnlySet<BoardPosition> enemyLegal, bool gainsReach, bool upgrade,
        bool capture, bool shared)
    {
        int friendlyNeighbors = board.SurvivalSupport(index, player);
        double formation = gainsReach && friendlyNeighbors > 1 ? MultipleSupportValue : 0;
        double supportRisk = 0;

        if (gainsReach)
        {
            foreach (int neighbor in board.Supporters(index, player))
            {
                if (board.Cells[neighbor].Piece == PieceType.Castle) formation += CastleSupportValue;
                if (friendlyNeighbors == 1 && enemyLegal.Contains(board.Position(neighbor)))
                {
                    // The opponent can remove the newly placed knight's only supporter.
                    bool exhaustsGround = board.Cells[index].GroundHealth <= (capture ? 2 : 1);
                    supportRisk += SoleSupportThreatRisk * (exhaustsGround ? 1 + GroundExhaustionFactor : 1);
                }
            }
        }
        if (upgrade || gainsReach)
        {
            foreach (int target in board.SupportedTargets(index, upgrade ? PieceType.Castle : PieceType.Knight))
            {
                if (board.Cells[target] is { Piece: PieceType.Knight } knight && knight.Owner == player &&
                    board.SurvivalSupport(target, player) == 1)
                {
                    formation += DependentKnightValue;
                    if (upgrade && shared && board.InSupportRange(index, target, PieceType.Knight))
                        supportRisk += DependentKnightValue * OpponentResponseDiscount;
                }
            }
        }
        if (capture)
        {
            foreach (int target in board.SupportedTargets(index, PieceType.Knight))
            {
                if (board.Cells[target] is not { Piece: PieceType.Knight } knight || knight.Owner != opponent ||
                    board.SurvivalSupport(target, opponent) != 1) continue;
                // The opponent can add a supporter or upgrade the dependent knight in the same turn.
                double groundValue = knight.GroundHealth <= 1 ? 1 + GroundExhaustionFactor : 1;
                formation += DependentKnightValue * groundValue * OpponentResponseDiscount;
            }
        }

        if (shared) formation *= OpponentResponseDiscount;
        return features with { Formation = Math.Min(1, formation), SupportRisk = Math.Min(1, supportRisk) };
    }

    internal static int GetPoolSize(ReadOnlySpan<CpuCandidate> candidates, int nominalLimit, int minimum, double? maximumGap)
    {
        minimum = Math.Min(candidates.Length, minimum);
        int poolSize = Math.Min(candidates.Length, Math.Max(nominalLimit, minimum));
        if (maximumGap is double gap)
        {
            double best = candidates[0].Score;
            while (poolSize > minimum && best - candidates[poolSize - 1].Score > gap) poolSize--;
        }

        return IncludeCutoffTies(candidates, poolSize);
    }

    private static int IncludeCutoffTies(ReadOnlySpan<CpuCandidate> candidates, int poolSize)
    {
        // Compare to the original cutoff so a chain of near-ties cannot extend the margin.
        double cutoff = candidates[poolSize - 1].Score;
        while (poolSize < candidates.Length && cutoff - candidates[poolSize].Score <= ScoreEquivalenceTolerance)
        {
            poolSize++;
        }

        return poolSize;
    }

    internal static int Sample(Span<CpuCandidate> candidates, int poolSize, double temperature, double diversity, Random random)
    {
        if (temperature == 0) poolSize = IncludeCutoffTies(candidates, 1);
        for (int i = poolSize; i < candidates.Length; i++)
        {
            candidates[i] = candidates[i] with { Probability = 0 };
        }

        if (poolSize == 1)
        {
            candidates[0] = candidates[0] with { Probability = 1 };
            return 0;
        }

        double best = candidates[0].Score;
        double total = 0;
        for (int i = 0; i < poolSize; i++)
        {
            double weight = temperature == 0 ? 1 : Math.Exp((candidates[i].Score - best) / temperature);
            candidates[i] = candidates[i] with { Probability = weight };
            total += weight;
        }

        double draw = random.NextDouble();
        double cumulative = 0;
        int selected = -1;
        for (int i = 0; i < poolSize; i++)
        {
            double probability = (1 - diversity) * candidates[i].Probability / total + diversity / poolSize;
            candidates[i] = candidates[i] with { Probability = probability };
            cumulative += probability;
            if (selected < 0 && draw < cumulative) selected = i;
        }

        // Absorb rounding at the upper boundary without selecting an underflowed zero-weight tail.
        if (selected < 0)
        {
            selected = poolSize - 1;
            while (candidates[selected].Probability == 0) selected--;
        }

        return selected;
    }
}
