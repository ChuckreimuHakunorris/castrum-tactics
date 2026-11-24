using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.AI;

/// <summary>Ranks authoritative legal selections using static opening and main-phase heuristics.</summary>
/// <remarks>The engine is stateless. Supply independent random sources for concurrent callers.</remarks>
public sealed class CpuDecisionEngine
{
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
        CancellationToken cancellationToken = default) =>
        Evaluate(snapshot, player, profile, random, cancellationToken).Decision;

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
        var (decision, candidates) = Evaluate(snapshot, player, profile, random, cancellationToken);
        return new CpuDecisionReport(decision, candidates);
    }

    private static (CpuDecision? Decision, CpuCandidate[] Candidates) Evaluate(GameSessionSnapshot snapshot,
        PlayerId player, CpuProfile profile, Random random, CancellationToken token)
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

        bool opening = snapshot.Phase == MatchPhase.Opening;
        IReadOnlySet<BoardPosition>? ownLegal = snapshot.Phase == MatchPhase.Main
            ? snapshot.GetMainPhaseLegalSelections(player) : null;
        IEnumerable<BoardPosition> legal = opening ? snapshot.EligiblePositions : ownLegal?.AsEnumerable() ?? [];
        BoardPosition[] positions = legal.OrderBy(position => position.Y).ThenBy(position => position.X).ToArray();
        token.ThrowIfCancellationRequested();
        if (positions.Length == 0)
        {
            return (null, []);
        }

        var board = new TacticalBoard(snapshot.Board, opening, token);
        bool noKnights = board.OneKnights == 0 && board.TwoKnights == 0;
        var candidates = new CpuCandidate[positions.Length];
        if (opening)
        {
            RankOpening(board, positions, player, opponent, noKnights, profile, candidates, token);
        }
        else
        {
            IReadOnlySet<BoardPosition> enemyLegal = snapshot.GetMainPhaseLegalSelections(opponent);
            for (int i = 0; i < positions.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                CpuFeatures features = MainFeatures(board, positions[i], player, opponent, snapshot.Territory!, ownLegal!, enemyLegal);
                candidates[i] = new CpuCandidate(positions[i], features, features.Score(profile.Weights), 0);
            }
        }

        Array.Sort(candidates, static (a, b) =>
        {
            int compare = b.Score.CompareTo(a.Score);
            if (compare != 0) return compare;
            compare = a.Position.Y.CompareTo(b.Position.Y);
            return compare != 0 ? compare : a.Position.X.CompareTo(b.Position.X);
        });

        token.ThrowIfCancellationRequested();
        double temperature = opening ? profile.OpeningTemperature : profile.MainTemperature;
        int poolSize = Math.Min(candidates.Length, Math.Max(opening ? 2 : 1, profile.CandidateLimit));
        if (temperature == 0) poolSize = 1;
        double diversity = opening ? (noKnights && candidates.Length == 2 ? 0.7 : 0.2) : 0;
        int selected = Sample(candidates, poolSize, temperature, diversity, random);
        token.ThrowIfCancellationRequested();
        return (new CpuDecision(player, candidates[selected].Position, snapshot.Phase, snapshot.RoundId), candidates);
    }

    private static void RankOpening(TacticalBoard board, BoardPosition[] positions, PlayerId player, PlayerId opponent,
        bool noKnights, CpuProfile profile, CpuCandidate[] candidates, CancellationToken token)
    {
        var opponentAppeal = new double[positions.Length];
        double mostAppealing = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            int index = board.Index(positions[i]);
            CpuFeatures ours = OpeningFeatures(board, index, player, opponent);
            CpuFeatures theirs = OpeningFeatures(board, index, opponent, player);
            // Use a public, profile-independent model of opponent appeal, with no submission flags.
            opponentAppeal[i] = (theirs.Expansion + theirs.Territory + theirs.Spread) / 3;
            mostAppealing = Math.Max(mostAppealing, opponentAppeal[i]);
            candidates[i] = new CpuCandidate(positions[i], ours, 0, 0);
        }

        double urgency = noKnights ? (positions.Length <= 3 ? 1.0 : 0.7) : 0.4;
        for (int i = 0; i < candidates.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            CpuFeatures features = candidates[i].Features;
            double appeal = mostAppealing > 0 ? opponentAppeal[i] / mostAppealing : 1;
            // Denying a prized start can be worthwhile after knights are established. A blocked gate adds cost.
            double denial = noKnights ? 0 : opponentAppeal[i] * 0.2;
            double risk = Math.Clamp(appeal * (urgency + features.Bottleneck * 0.2 - denial) /
                Math.Sqrt(positions.Length / 2.0), 0, 1);
            features = features with { Risk = risk };
            candidates[i] = candidates[i] with { Features = features, Score = features.Score(profile.Weights) };
        }
    }

    internal static CpuFeatures OpeningFeatures(TacticalBoard board, int index, PlayerId player, PlayerId opponent)
    {
        int free = board.Neighbors(index).Count(neighbor => board.Cells[neighbor].IsPlayable &&
            board.Cells[neighbor].Piece == PieceType.None);
        TacticalBoard.Region region = board.RegionAt(index);
        double extent = (double)region.Size / board.PlayableCount;
        double spread = 1.0 / (1 + region.Count(player) + board.Support(index, player));
        double territory = extent * (region.Count(opponent) > region.Count(player) ? 1.0 : 0.8);
        double gate = board.Bottleneck(index);
        return new CpuFeatures(free / 8.0, territory, 0, 0, 0, gate, spread * extent, 0);
    }

    internal static CpuFeatures MainFeatures(TacticalBoard board, BoardPosition position, PlayerId player, PlayerId opponent,
        TerritoryAnalysis territory, IReadOnlySet<BoardPosition> ownLegal, IReadOnlySet<BoardPosition> enemyLegal)
    {
        int index = board.Index(position);
        CellState cell = board.Cells[index];
        bool capture = cell.Piece == PieceType.Knight && cell.Owner == opponent;
        bool upgrade = cell.Piece == PieceType.Knight && cell.Owner == player;
        bool lethal = capture && cell.GroundHealth == 1;
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

        double captureValue = capture ? 0.5 + (lethal ? 0 : 0.25 + 0.125 * Math.Clamp(cell.GroundHealth - 2, 0, 2)) : 0;
        double fortification = upgrade ? (shared ? 0.8 : 0.25) : 0;
        double gate = board.Bottleneck(index) * (upgrade || lethal ? 1.0 : 0.35);
        double risk = shared ? (capture ? (lethal ? 0.05 : 0.25) : 0.65) : 0;
        if (gainsReach) risk += 0.35 * nearbyThreats / 8.0;

        return new CpuFeatures(newReach / 8.0, contestedInfluence / 8, captureValue, fortification,
            denied / 8.0, gate, 0, risk);
    }

    internal static int Sample(CpuCandidate[] candidates, int poolSize, double temperature, double diversity, Random random)
    {
        if (poolSize == 1)
        {
            candidates[0] = candidates[0] with { Probability = 1 };
            return 0;
        }

        double best = candidates[0].Score;
        double total = 0;
        for (int i = 0; i < poolSize; i++)
        {
            double weight = Math.Exp((candidates[i].Score - best) / temperature);
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
