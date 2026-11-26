using CastrumTactics.Domain;

namespace CastrumTactics.AI;

internal static class TacticalCandidates
{
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

    internal static void Sort(CpuCandidate[] candidates) => Array.Sort(candidates, static (a, b) =>
    {
        int compare = b.Score.CompareTo(a.Score);
        if (compare != 0) return compare;
        compare = a.Position.Y.CompareTo(b.Position.Y);
        return compare != 0 ? compare : a.Position.X.CompareTo(b.Position.X);
    });

    internal static void RankOpening(TacticalBoard board, BoardPosition[] positions, PlayerId player, PlayerId opponent,
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
}
