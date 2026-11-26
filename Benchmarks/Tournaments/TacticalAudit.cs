using CastrumTactics.AI;
using CastrumTactics.Domain;
using static Program;

namespace CastrumTactics.Development;

internal static class TacticalAudit
{
    internal static void Record(StrategicPosition position, MainPhaseState before, MainPhaseState after, PlayerId side,
        DecisionSample decision, DecisionSample opposing, ref TacticalMetrics metrics, CancellationToken token)
    {
        var action = Position(decision.Move);
        // Alternatives face the realized reply. A better result here supplies retrospective evidence for this pair only.
        var reply = new MainPhaseSelection(Other(side), Position(opposing.Move));
        if (before.Board.GetCell(action).Piece == PieceType.None)
        {
            metrics.Placements++;
            if (IsInteriorPlacement(position, before, after, side, action, token))
            {
                metrics.Interior++;
                if (HasBetterPlacement(before, after, side, action, reply, token)) metrics.BetterAlternative++;
            }
        }
        if (after.Result?.Winner != Other(side)) return;
        foreach (var alternative in before.GetLegalSelections(side))
        {
            token.ThrowIfCancellationRequested();
            if (alternative != action && before.Advance(new(side, alternative), reply).Result?.Winner != Other(side))
            {
                metrics.AvoidableLoss = true;
                break;
            }
        }
    }

    private static bool HasBetterPlacement(MainPhaseState before, MainPhaseState after, PlayerId side,
        BoardPosition selected, MainPhaseSelection reply, CancellationToken token)
    {
        var legalBefore = before.GetLegalSelections(side);
        var (outcome, margin, newReach, formation) = Measure(after);
        foreach (var alternative in legalBefore)
        {
            token.ThrowIfCancellationRequested();
            if (alternative == selected || before.Board.GetCell(alternative).Piece != PieceType.None) continue;
            var (nextOutcome, nextMargin, nextReach, nextFormation) = Measure(before.Advance(new(side, alternative), reply));
            if (nextOutcome < outcome || nextMargin < margin || nextReach < newReach || nextFormation < formation) continue;
            if (nextOutcome > outcome || nextMargin > margin || nextReach > newReach || nextFormation > formation)
                return true;
        }
        return false;

        (int Outcome, int Margin, int NewReach, int Formation) Measure(MainPhaseState state)
        {
            int delta = state.Territory.PlayerOneScore - state.Territory.PlayerTwoScore;
            int margin = side == Red ? delta : -delta;
            int outcome = state.Result is null ? 0 : Math.Sign(margin);
            int formation = 0;
            for (int y = 0; y < state.Board.Height; y++)
            for (int x = 0; x < state.Board.Width; x++)
                if (state.Board.GetCell(new(x, y)).Owner == side) formation++;
            int reach = state.GetLegalSelections(side).Count(target => !legalBefore.Contains(target) &&
                state.Board.GetCell(target).Piece == PieceType.None);
            return (outcome, margin, reach, formation);
        }
    }

    internal struct TacticalMetrics
    {
        internal int Interior, Placements, BetterAlternative;
        internal bool AvoidableLoss;
    }
}
