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
        // Measure the synchronous decision and its diagnostics on this thread. Match resolution and tactical auditing happen outside this interval.
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        var decision = opponent.Decide(state, player, stream, diagnostics, token);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return decision with { Measured = true, Milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            AllocatedBytes = bytes };
    }
}
