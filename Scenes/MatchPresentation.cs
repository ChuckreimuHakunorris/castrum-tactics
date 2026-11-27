#nullable enable

using System.Collections.Generic;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

internal sealed class MatchPresentation(IGameSession session)
{
    internal ResolvedTurn? Latest { get; private set; }

    // A new composition owns a fresh observer even when the new session starts at the same round.
    internal void Observe(GameSessionSnapshot snapshot)
    {
        if (!ReferenceEquals(snapshot, session.Current)) return;
        if (session.LatestResolution is { } resolution && resolution.After.RoundId == snapshot.RoundId)
        {
            if (Latest?.RoundId != resolution.RoundId) Latest = resolution;
        }
        else
        {
            Latest = null;
        }
    }

    internal static (IEnumerable<BoardPosition> Active, IEnumerable<BoardPosition> Opposing)
        Targets(GameSessionSnapshot snapshot, PlayerId active)
    {
        if (snapshot.Phase == MatchPhase.Opening)
            return (snapshot.EligiblePositions, snapshot.EligiblePositions);

        var other = active == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
        return (snapshot.GetMainPhaseLegalSelections(active), snapshot.GetMainPhaseLegalSelections(other));
    }

    internal static LegalTargetKind Classify(bool active, bool opposing, bool showOpponent) =>
        (active ? LegalTargetKind.Active : LegalTargetKind.None) |
        (opposing && showOpponent ? LegalTargetKind.Opposing : LegalTargetKind.None);
}

internal enum LegalTargetKind { None = 0, Active = 1, Opposing = 2, Shared = 3 }
