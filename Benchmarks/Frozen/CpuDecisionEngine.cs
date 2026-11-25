using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.AI;

/// <summary>Ranks authoritative legal selections using static opening and main-phase heuristics.</summary>
/// <remarks>
/// Computation is synchronous and stateless. Concurrent calls may share the engine, snapshots, and immutable
/// profiles. Supply an independent random source per concurrent call. Equivalent resolved inputs and random
/// sequences reproduce results with the same engine and runtime.
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
        var (decision, _) = Evaluate(snapshot, player, profile, random, cancellationToken);
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
        var (decision, candidates) = Evaluate(snapshot, player, profile, random, cancellationToken);
        var report = new CpuDecisionReport(decision, candidates);
        cancellationToken.ThrowIfCancellationRequested();
        return report;
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

        bool knightSupport = snapshot.RuleSet == RuleSet.KnightSupport;
        var board = new TacticalBoard(snapshot.Board, opening, token, snapshot.RuleSet);
        bool noStartingPieces = board.StartingPieceCount == 0;
        var candidates = new CpuCandidate[positions.Length];
        if (opening)
        {
            RankOpening(board, positions, player, opponent, noStartingPieces, knightSupport, profile, candidates, token);
        }
        else
        {
            IReadOnlySet<BoardPosition> enemyLegal = snapshot.GetMainPhaseLegalSelections(opponent);
            for (int i = 0; i < positions.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                CpuFeatures features = MainFeatures(board, positions[i], player, opponent, snapshot.Territory!, ownLegal!,
                    enemyLegal, knightSupport);
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
        int limit = opening ? profile.OpeningCandidateLimit : profile.MainCandidateLimit;
        double? maximumGap = opening ? profile.OpeningMaximumScoreDifference : profile.MainMaximumScoreDifference;
        int poolSize = temperature == 0 ? 1 : GetPoolSize(candidates, limit, opening ? 2 : 1, maximumGap);
        double diversity = opening
            ? (noStartingPieces && candidates.Length == 2 ? profile.UrgentOpeningDiversity : profile.OpeningDiversity)
            : 0;
        int selected = Sample(candidates, poolSize, temperature, diversity, random);
        return (new CpuDecision(player, candidates[selected].Position, snapshot.Phase, snapshot.RoundId), candidates);
    }

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
        int free = board.Neighbors(index).Count(neighbor => board.Cells[neighbor].IsPlayable &&
            board.Cells[neighbor].Piece == PieceType.None);
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
        int friendlyNeighbors = board.Support(index, player);
        double formation = gainsReach && friendlyNeighbors > 1 ? MultipleSupportValue : 0;
        double supportRisk = 0;

        foreach (int neighbor in board.Neighbors(index))
        {
            CellState piece = board.Cells[neighbor];
            if (piece.Owner == player)
            {
                if (gainsReach && piece.Piece == PieceType.Castle) formation += CastleSupportValue;
                if (gainsReach && friendlyNeighbors == 1 && enemyLegal.Contains(board.Position(neighbor)))
                {
                    // A legal placement or capture starts in friendly range. The opponent can remove its only source.
                    bool exhaustsGround = board.Cells[index].GroundHealth <= (capture ? 2 : 1);
                    supportRisk += SoleSupportThreatRisk * (exhaustsGround ? 1 + GroundExhaustionFactor : 1);
                }

                // An upgrade anchors a dependent knight; a placement gives a knight another neighbor.
                if ((upgrade || gainsReach) && piece.Piece == PieceType.Knight && board.Support(neighbor, player) == 1)
                {
                    formation += DependentKnightValue;
                    if (upgrade && shared) supportRisk += DependentKnightValue * OpponentResponseDiscount;
                }
            }
            else if (capture && piece.Owner == opponent && piece.Piece == PieceType.Knight && board.Support(neighbor, opponent) == 1)
            {
                // The opponent can add a supporter or upgrade the dependent knight in the same turn.
                double groundValue = piece.GroundHealth <= 1 ? 1 + GroundExhaustionFactor : 1;
                formation += DependentKnightValue * groundValue * OpponentResponseDiscount;
            }
        }

        if (shared) formation *= OpponentResponseDiscount;
        return features with { Formation = Math.Min(1, formation), SupportRisk = Math.Min(1, supportRisk) };
    }

    internal static int GetPoolSize(CpuCandidate[] candidates, int nominalLimit, int minimum, double? maximumGap)
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

    private static int IncludeCutoffTies(CpuCandidate[] candidates, int poolSize)
    {
        // Compare to the original cutoff so a chain of near-ties cannot extend the margin.
        double cutoff = candidates[poolSize - 1].Score;
        while (poolSize < candidates.Length && cutoff - candidates[poolSize].Score <= ScoreEquivalenceTolerance)
        {
            poolSize++;
        }

        return poolSize;
    }

    internal static int Sample(CpuCandidate[] candidates, int poolSize, double temperature, double diversity, Random random)
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
