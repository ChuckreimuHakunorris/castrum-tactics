using System.Diagnostics;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using static Program;

namespace CastrumTactics.Development;

internal static class TournamentMeasurement
{
    internal static DecisionSample Measure(TournamentOpponent opponent, GameSessionSnapshot state, PlayerId player,
        Random stream, bool diagnostics, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        var decision = opponent.Decide(state, player, stream, diagnostics, token);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return decision with { Measured = true, Milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            AllocatedBytes = bytes };
    }
}
