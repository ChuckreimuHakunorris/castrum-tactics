#if CASTRUM_DEVICE_TIMING
#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Godot;

namespace CastrumTactics.Match;

// Compile into an instrumentation export only. The timed interval calls the usual decision source.
internal static class DeviceTiming
{
    private static readonly object FileLock = new();

    internal static CpuMatchCoordinator.DecisionSource Wrap(CpuMatchCoordinator.DecisionSource decide)
    {
        string path = ProjectSettings.GlobalizePath("user://m8e-cpu.csv");
        return (snapshot, player, profile, random, token) =>
        {
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            string result = "faulted";
            try
            {
                var decision = decide(snapshot, player, profile, random, token);
                result = decision.HasValue ? "selected" : "no-selection";
                return decision;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                result = "cancelled";
                throw;
            }
            finally
            {
                double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                int gc0 = GC.CollectionCount(0) - gen0, gc1 = GC.CollectionCount(1) - gen1, gc2 = GC.CollectionCount(2) - gen2;
                string row = string.Create(CultureInfo.InvariantCulture,
                    $"{DateTimeOffset.UtcNow:O},{snapshot.RuleSet},{snapshot.Phase},{snapshot.RoundId},{snapshot.Board.Width},{snapshot.Board.Height},{profile.Search?.PairBudget},{profile.OpeningSearch?.PairBudget},{result},{milliseconds:F4},{allocated},{gc0},{gc1},{gc2}");
                try
                {
                    lock (FileLock)
                    {
                        using var log = new StreamWriter(path, append: true);
                        if (log.BaseStream.Length == 0)
                            log.WriteLine("utc,rules,phase,round,width,height,mainCap,openingCap,result,workerMs,allocatedBytes,gc0,gc1,gc2");
                        log.WriteLine(row);
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // A missing diagnostics file must not discard a valid game decision.
                    GD.PushWarning("Castrum Tactics device timing could not write its CSV.");
                }
            }
        };
    }
}
#endif
