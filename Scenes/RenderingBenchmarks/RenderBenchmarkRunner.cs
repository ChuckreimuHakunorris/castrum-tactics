#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Godot;

namespace CastrumTactics.RenderingBenchmarks;

internal sealed partial class RenderBenchmarkRunner(Main game, RenderBenchmarkRequest request) : Node
{
    private const int MaximumSamples = 1_000_000;
    private const double TrendSeconds = 15;
    private readonly List<string> _completed = [];
    private readonly StringBuilder _summary = new("Castrum Tactics rendering benchmark\n");
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
    private RenderBenchmarkOptions Options => request.Options;
    private string Scenario => Options.Cases![_scenarioIndex];

    private int _scenarioIndex;
    private int _repetition = 1;
    private int _initialFps;
    private DisplayServer.VSyncMode _initialVsync;
    private bool _initialKeepOn;
    private bool _attached;
    private bool _needsCase = true;
    private bool _stopping;
    private string? _interruption;
    private RenderBenchmarkCapture? _capture;
    private long _caseStarted;
    private long _lastFrame;
    private double _nextAction;
    private double _nextTrend;
    private double _setupMs;
    private object? _startBoard;
    private object? _display;
    private Vector2I _size;
    private int _step;
    private bool _synthetic;
    private bool _software;

    internal void Mark(string name, long start, string? detail = null) => _capture?.Mark(name, start, detail);

    public override void _Ready()
    {
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("Use a renderer, without --headless.");
            _initialFps = Engine.MaxFps;
            _initialVsync = DisplayServer.WindowGetVsyncMode();
            _initialKeepOn = DisplayServer.ScreenIsKeptOn();
            string adapter = RenderingServer.GetVideoAdapterName();
            _software = adapter.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase) ||
                adapter.Contains("softpipe", StringComparison.OrdinalIgnoreCase) ||
                adapter.Contains("SwiftShader", StringComparison.OrdinalIgnoreCase) ||
                adapter.Contains("Basic Render Driver", StringComparison.OrdinalIgnoreCase);
            DisplayServer.ScreenSetKeepOn(true);
            if (Options.Pacing == "uncapped")
            {
                Engine.MaxFps = 0;
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            }
            _attached = true;
            RenderingServer.FramePostDraw += SampleFrame;
            if (Options.ViewportTiming)
                RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
            WriteIndex("running");
            GD.Print($"Rendering benchmark output: {request.DirectoryPath}");
        }
        catch (Exception exception)
        {
            Stop("invalid", exception.Message);
        }
    }

    public override void _Process(double delta)
    {
        if (_stopping) return;
        try
        {
            if (_interruption is { } interruption) throw new InvalidOperationException(interruption);
            if (_needsCase) BeginCase();
            var capture = _capture!;
            double elapsed = Stopwatch.GetElapsedTime(_caseStarted).TotalSeconds;
            if (capture.Start == 0 && elapsed >= Options.WarmupSeconds!.Value)
            {
                _startBoard = game.BenchmarkBoardDigest();
                _size = DisplayServer.WindowGetSize();
                _display = DisplayMetadata();
                capture.Start = Stopwatch.GetTimestamp();
                capture.Trends.Add(RenderCounters.Take(GetViewport(), capture.Start, Options, _software));
                _lastFrame = 0;
                _nextTrend = TrendSeconds;
            }

            if (capture.Start != 0)
            {
                if (DisplayServer.WindowGetSize() != _size)
                    throw new InvalidOperationException("Window size or device orientation changed during measurement.");
                double measured = Stopwatch.GetElapsedTime(capture.Start).TotalSeconds;
                if (capture.Frames.Count >= MaximumSamples)
                    throw new InvalidOperationException("Frame buffer reached its limit. Shorten the run or use normal pacing.");
                if (measured >= Options.MeasureSeconds!.Value)
                {
                    CompleteCase();
                    return;
                }
                if (measured >= _nextTrend)
                {
                    capture.Trends.Add(RenderCounters.Take(GetViewport(), capture.Start, Options, _software));
                    _nextTrend += TrendSeconds;
                }
            }

            if (!_synthetic && elapsed >= _nextAction && Scenario is "turns" or "lifecycle" or "monster" or "extended")
            {
                game.AdvanceBenchmarkMatch(Scenario, _step++);
                _nextAction = elapsed + (Scenario == "lifecycle" ? 0.25 : 0.35);
            }
        }
        catch (Exception exception)
        {
            Stop("invalid", exception.Message);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationPaused || (what == NotificationApplicationFocusOut && _capture?.Start > 0))
            _interruption = "The application lost focus or was suspended.";
    }

    public override void _ExitTree()
    {
        if (!_attached) return;
        RenderingServer.FramePostDraw -= SampleFrame;
        if (Options.ViewportTiming)
            RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), false);
        Engine.MaxFps = _initialFps;
        DisplayServer.WindowSetVsyncMode(_initialVsync);
        DisplayServer.ScreenSetKeepOn(_initialKeepOn);
    }

    public override void _Input(InputEvent @event) => GetViewport().SetInputAsHandled();

    private void BeginCase()
    {
        _needsCase = false;
        _step = 0;
        _nextAction = 0.20;
        _synthetic = Scenario is "castles" or "damage" or "targets";
        int nominalRate = Options.Pacing == "uncapped" ? 360 : 90;
        _capture = new RenderBenchmarkCapture(Math.Min(MaximumSamples, (int)(Options.MeasureSeconds!.Value * nominalRate) + 1));
        long started = Stopwatch.GetTimestamp();
        game.PrepareBenchmarkCase(Scenario, Options, _capture);
        _setupMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _lastFrame = 0;
        _caseStarted = Stopwatch.GetTimestamp();
    }

    private void SampleFrame()
    {
        if (_stopping || _needsCase || _capture is not { } capture) return;

        long now = Stopwatch.GetTimestamp();
        long previous = _lastFrame;
        _lastFrame = now;
        if (previous == 0 || now <= previous) return;
        double interval = Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds;
        if (capture.Start == 0)
        {
            if (capture.StartupIntervalsMs.Count < 24) capture.StartupIntervalsMs.Add(interval);
            return;
        }
        if (capture.Frames.Count >= MaximumSamples) return;

        var viewport = GetViewport();
        capture.Frames.Add(new RenderFrame(Stopwatch.GetElapsedTime(capture.Start, now).TotalMilliseconds, interval,
            Options.DetailedCounters ? CanvasCounter(viewport, Viewport.RenderInfo.DrawCallsInFrame) : null,
            Options.DetailedCounters ? CanvasCounter(viewport, Viewport.RenderInfo.ObjectsInFrame) : null,
            Options.DetailedCounters ? CanvasCounter(viewport, Viewport.RenderInfo.PrimitivesInFrame) : null));
    }

    private static long? CanvasCounter(Viewport view, Viewport.RenderInfo counter) =>
        RenderCounters.Positive(view.GetRenderInfo(Viewport.RenderInfoType.Canvas, counter));

    private void CompleteCase()
    {
        var capture = _capture!;
        // Sampling has ended. Keep post-processing and serialization outside the measured allocation window.
        _needsCase = true;
        long end = Stopwatch.GetTimestamp();
        capture.Trends.Add(RenderCounters.Take(GetViewport(), capture.Start, Options, _software));
        var frames = FrameSummary.Calculate(capture.Frames);
        if (frames is null) throw new InvalidOperationException("No rendered frame intervals were captured.");

        string file = $"{_scenarioIndex + 1:00}-{Scenario}-{_repetition:00}.json";
        var first = capture.Trends[0];
        var last = capture.Trends[^1];
        RenderBenchmarkRequest.Write(request.DirectoryPath, file, new
        {
            schemaVersion = RenderBenchmarkOptions.Schema,
            scenarioVersion = RenderBenchmarkOptions.ScenarioVersion,
            scenario = Scenario,
            synthetic = _synthetic,
            repetition = _repetition,
            status = "complete",
            display = _display,
            setupMs = _setupMs,
            startBoard = _startBoard,
            endBoard = game.BenchmarkBoardDigest(),
            configuration = Options,
            actualMeasureSeconds = Stopwatch.GetElapsedTime(capture.Start, end).TotalSeconds,
            frameSummary = frames,
            rawFrames = capture.Frames,
            startupIntervalsMs = capture.StartupIntervalsMs,
            events = capture.Events,
            trends = capture.Trends,
            processAllocationEstimateBytes = last.ProcessAllocatedBytes - first.ProcessAllocatedBytes,
            collections = new[] { last.Gen0 - first.Gen0, last.Gen1 - first.Gen1, last.Gen2 - first.Gen2 },
            cpuDecisions = capture.CompletedCpuSamples(end),
        });
        _completed.Add(file);
        _summary.AppendLine(FormattableString.Invariant(
            $"{Scenario} #{_repetition}: {frames.Samples} intervals, {frames.AverageFps:F2} FPS, median {frames.MedianMs:F2} ms, p95 {frames.P95Ms:F2} ms, p99 {frames.P99Ms:F2} ms"));
        WriteIndex("running");
        if (++_repetition <= Options.Repetitions!.Value) return;
        _repetition = 1;
        if (++_scenarioIndex == Options.Cases!.Length) Stop("complete");
    }

    private object DisplayMetadata()
    {
        Vector2 visible = GetViewport().GetVisibleRect().Size;
        Vector2I client = DisplayServer.WindowGetSize();
        Vector2 render = GetViewport().GetTexture().GetSize();
        float refresh = DisplayServer.ScreenGetRefreshRate();
        return new
        {
            logicalSize = new[] { visible.X, visible.Y },
            renderSize = new[] { render.X, render.Y },
            windowPixels = new[] { client.X, client.Y },
            orientation = client.X >= client.Y ? "landscape" : "portrait",
            refreshHz = refresh > 0 ? (float?)refresh : null,
            engineMaxFps = Engine.MaxFps,
            engineVsync = DisplayServer.WindowGetVsyncMode().ToString(),
        };
    }

    private void WriteIndex(string status, string? error = null)
    {
        RenderBenchmarkRequest.Write(request.DirectoryPath, "run.json", new
        {
            schemaVersion = RenderBenchmarkOptions.Schema,
            scenarioVersion = RenderBenchmarkOptions.ScenarioVersion,
            status,
            error,
            startedUtc = _startedUtc,
            configuration = Options,
            presentation = new { Options.ShowOpponent, Options.AnimateAmbient, Options.AnimateLegal, Options.AnimateTube },
            build = typeof(Main).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            buildType = OS.IsDebugBuild() ? "debug" : "release",
            godot = Engine.GetVersionInfo()["string"].AsString(),
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            platform = OS.GetName(),
            osVersion = OS.GetVersion(),
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            device = Available(OS.GetModelName()),
            processor = Available(OS.GetProcessorName()),
            logicalProcessors = OS.GetProcessorCount(),
            renderer = RenderingServer.GetCurrentRenderingMethod(),
            backend = RenderingServer.GetCurrentRenderingDriverName(),
            softwareRenderer = _software,
            adapter = Available(RenderingServer.GetVideoAdapterName()),
            adapterVendor = Available(RenderingServer.GetVideoAdapterVendor()),
            graphicsApi = Available(RenderingServer.GetVideoAdapterApiVersion()),
            activeScenario = _scenarioIndex < Options.Cases!.Length ? Scenario : null,
            activeRepetition = _scenarioIndex < Options.Cases!.Length ? (int?)_repetition : null,
            completed = _completed,
        });
        File.WriteAllText(Path.Combine(request.DirectoryPath, "summary.txt"), _summary.ToString());
    }

    private static string? Available(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void Stop(string status, string? error = null)
    {
        _stopping = true;
        if (error is not null)
        {
            _summary.AppendLine(error);
            GD.PushError($"Rendering benchmark stopped: {error}");
        }
        try
        {
            WriteIndex(status, error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GD.PushError($"Rendering benchmark could not write its results: {exception.Message}");
            status = "invalid";
        }
        GetTree().Quit(status == "complete" ? 0 : 1);
    }
}
