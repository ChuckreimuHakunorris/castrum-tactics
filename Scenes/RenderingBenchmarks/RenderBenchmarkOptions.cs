#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CastrumTactics.RenderingBenchmarks;

internal sealed record RenderBenchmarkOptions
{
    internal const int Schema = 1;
    internal const int ScenarioVersion = 1;
    internal static readonly string[] Scenarios =
        ["small", "large", "castles", "damage", "targets", "turns", "lifecycle", "monster", "extended"];

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    public int SchemaVersion { get; init; } = Schema;
    public string Profile { get; init; } = "quick";
    public string? Revision { get; init; }
    public string? DeviceNotes { get; init; }
    public string[]? Cases { get; init; }
    public int Seed { get; init; } = 42;
    public double? WarmupSeconds { get; init; }
    public double? MeasureSeconds { get; init; }
    public int? Repetitions { get; init; }
    public string Pacing { get; init; } = "normal";
    public bool ShowOpponent { get; init; } = true;
    public bool ReduceMotion { get; init; }
    public bool? AmbientMotion { get; init; }
    public bool? LegalMotion { get; init; }
    public bool? TubeMotion { get; init; }
    public bool DetailedCounters { get; init; } = true;
    public bool ViewportTiming { get; init; }
    public bool CpuDiagnostics { get; init; } = true;

    internal bool AnimateAmbient => AmbientMotion ?? !ReduceMotion;
    internal bool AnimateLegal => LegalMotion ?? !ReduceMotion;
    internal bool AnimateTube => TubeMotion ?? !ReduceMotion;

    internal static RenderBenchmarkOptions Parse(string json)
    {
        var options = JsonSerializer.Deserialize<RenderBenchmarkOptions>(json, Json)
            ?? throw new InvalidDataException("A benchmark request must be a JSON object.");
        var defaults = options.Profile switch
        {
            "quick" => (Warmup: 2, Measure: 4, Repeats: 1, Cases: new[] { "small", "turns", "monster" }),
            "standard" => (Warmup: 8, Measure: 20, Repeats: 3, Cases: Scenarios[..^1]),
            "sustained" => (Warmup: 30, Measure: 1200, Repeats: 1, Cases: new[] { "extended" }),
            _ => throw new InvalidDataException("Unknown profile. Choose quick, standard or sustained."),
        };
        if (options.SchemaVersion != Schema || options.Pacing is not ("normal" or "uncapped"))
            throw new InvalidDataException("Unsupported schema version or pacing mode.");

        double warmup = options.WarmupSeconds ?? defaults.Warmup;
        double measure = options.MeasureSeconds ?? defaults.Measure;
        int repetitions = options.Repetitions ?? defaults.Repeats;
        string[] cases = options.Cases ?? defaults.Cases;
        if (!double.IsFinite(warmup) || warmup is < 0 or > 300 ||
            !double.IsFinite(measure) || measure is < 1 or > 1800 || repetitions is < 1 or > 10)
            throw new InvalidDataException("Warm-up must be 0–300 s, measurement 1–1800 s and repetitions 1–10.");
        if (cases.Length is < 1 or > 9 || cases.Distinct().Count() != cases.Length ||
            Array.Exists(cases, scenario => !Scenarios.Contains(scenario)))
            throw new InvalidDataException("Provide distinct, known scenario IDs.");
        return options with { Cases = cases, WarmupSeconds = warmup, MeasureSeconds = measure, Repetitions = repetitions };
    }
}

internal sealed record FrameSummary(int Samples, double AverageFps, double MinMs, double MedianMs, double P95Ms, double P99Ms,
    double MaxMs, double StandardDeviationMs, int Over16_67Ms, double PercentOver16_67Ms, int Over33_33Ms, double PercentOver33_33Ms)
{
    internal static FrameSummary? Calculate(List<RenderFrame> frames)
    {
        if (frames.Count == 0) return null;

        var intervals = new double[frames.Count];
        double mean = 0;
        double deviationSum = 0;
        int over16 = 0, over33 = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            double interval = intervals[i] = frames[i].IntervalMs;
            // Online variance avoids subtracting two nearly equal large sums.
            double difference = interval - mean;
            mean += difference / (i + 1);
            deviationSum += difference * (interval - mean);
            if (interval > 16.67) over16++;
            if (interval > 33.33) over33++;
        }

        Array.Sort(intervals);
        return new FrameSummary(intervals.Length, 1000 / mean, intervals[0], Quantile(0.5), Quantile(0.95), Quantile(0.99),
            intervals[^1], Math.Sqrt(Math.Max(0, deviationSum / intervals.Length)),
            over16, 100d * over16 / intervals.Length, over33, 100d * over33 / intervals.Length);

        double Quantile(double fraction)
        {
            double rank = (intervals.Length - 1) * fraction;
            int low = (int)rank;
            return intervals[low] + (intervals[Math.Min(low + 1, intervals.Length - 1)] - intervals[low]) * (rank - low);
        }
    }
}

internal readonly record struct RenderFrame(double ElapsedMs, double IntervalMs, long? DrawCalls, long? Objects, long? Primitives);
