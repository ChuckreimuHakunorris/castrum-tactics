#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CastrumTactics.AI;
using CastrumTactics.Match;
using Godot;

namespace CastrumTactics.RenderingBenchmarks;

internal sealed class RenderBenchmarkCapture(int capacity)
{
    internal List<RenderFrame> Frames { get; } = new(capacity);
    internal List<double> StartupIntervalsMs { get; } = new(24);
    internal List<RenderEvent> Events { get; } = [];
    internal List<RenderCounters> Trends { get; } = [];
    private readonly ConcurrentQueue<CpuWork> _cpu = new();
    internal long Start { get; set; }

    internal void Mark(string name, long started, string? detail = null)
    {
        if (Start == 0) return;
        Events.Add(new RenderEvent(name, Frames.Count, Stopwatch.GetElapsedTime(Start, started).TotalMilliseconds,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds, detail));
    }

    internal CpuMatchCoordinator.DecisionSource CpuSource(bool diagnostics)
    {
        var engine = new CpuDecisionEngine(TimeSpan.FromMilliseconds(450));
        return (snapshot, player, profile, random, token) =>
        {
            long started = Stopwatch.GetTimestamp();
            long before = GC.GetAllocatedBytesForCurrentThread();
            CpuSearchReport? search = null;
            string status = "faulted";
            try
            {
                CpuDecision? decision;
                if (diagnostics)
                {
                    var report = engine.DecideWithDiagnostics(snapshot, player, profile, random, token);
                    search = report.Search;
                    decision = report.Decision;
                }
                else
                {
                    decision = engine.Decide(snapshot, player, profile, random, token);
                }
                status = decision.HasValue ? "selected" : "no-selection";
                return decision;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                status = "cancelled";
                throw;
            }
            finally
            {
                long ended = Stopwatch.GetTimestamp();
                _cpu.Enqueue(new CpuWork(started, ended, snapshot.RoundId, snapshot.Phase.ToString(), status,
                    GC.GetAllocatedBytesForCurrentThread() - before, search?.ResolvedPairs, search?.Depth,
                    search?.CompletedDepth, search?.TimeLimitReached));
            }
        };
    }

    internal object[] CompletedCpuSamples(long end) => _cpu.Where(work => work.Start >= Start && work.End <= end)
        .Select(work => (object)new
        {
            startMs = Stopwatch.GetElapsedTime(Start, work.Start).TotalMilliseconds,
            workerMs = Stopwatch.GetElapsedTime(work.Start, work.End).TotalMilliseconds,
            work.Round,
            work.Phase,
            work.Status,
            work.WorkerAllocatedBytes,
            work.ResolvedPairs,
            work.Depth,
            work.CompletedDepth,
            work.TimeLimitReached,
        }).ToArray();

    private readonly record struct CpuWork(long Start, long End, long Round, string Phase, string Status,
        long WorkerAllocatedBytes, int? ResolvedPairs, int? Depth, int? CompletedDepth, bool? TimeLimitReached);
}

internal sealed record RenderEvent(string Name, int NextFrameIndex, double StartMs, double DurationMs, string? Detail);

internal sealed record RenderCounters(double ElapsedMs, long ManagedHeapBytes, long ProcessAllocatedBytes,
    int Gen0, int Gen1, int Gen2, long? Nodes, long? Resources, long? GodotStaticBytes,
    long? TextureBytes, long? VideoBytes, double? LastProcessMs, double? ViewportRenderCpuMs, double? ViewportRenderGpuMs)
{
    internal static RenderCounters Take(Viewport viewport, long start, RenderBenchmarkOptions options, bool software) => new(
        Stopwatch.GetElapsedTime(start).TotalMilliseconds, GC.GetTotalMemory(false), GC.GetTotalAllocatedBytes(false),
        GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
        options.DetailedCounters ? Positive(Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)) : null,
        options.DetailedCounters ? Positive(Performance.GetMonitor(Performance.Monitor.ObjectResourceCount)) : null,
        options.DetailedCounters ? Positive(Performance.GetMonitor(Performance.Monitor.MemoryStatic)) : null,
        options.DetailedCounters ? Positive(RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed)) : null,
        options.DetailedCounters ? Positive(RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed)) : null,
        options.DetailedCounters ? PositiveMs(1000 * Performance.GetMonitor(Performance.Monitor.TimeProcess)) : null,
        options.ViewportTiming ? PositiveMs(RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport.GetViewportRid())) : null,
        options.ViewportTiming && !software ? PositiveMs(RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport.GetViewportRid())) : null);

    internal static long? Positive(double value) => value > 0 ? (long)value : null;
    private static double? PositiveMs(double value) => value > 0 && double.IsFinite(value) ? value : null;
}
