#nullable enable

using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace CastrumTactics.RenderingBenchmarks;

internal sealed record RenderBenchmarkRequest(RenderBenchmarkOptions Options, string DirectoryPath)
{
    internal static RenderBenchmarkRequest? Take()
    {
        string source = ProjectSettings.GlobalizePath("user://render-benchmark.json");
        if (!File.Exists(source)) return null;

        string directory = Path.Combine(ProjectSettings.GlobalizePath("user://render-benchmarks"),
            $"{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string consumed = Path.Combine(directory, "request.json");
        File.Move(source, consumed);

        try
        {
            if (new FileInfo(consumed).Length > 64 * 1024)
                throw new InvalidDataException("Benchmark requests must be at most 64 KiB.");
            return new RenderBenchmarkRequest(RenderBenchmarkOptions.Parse(File.ReadAllText(consumed)), directory);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Write(directory, "run.json", new { schemaVersion = RenderBenchmarkOptions.Schema, status = "invalid", error = exception.Message });
            throw;
        }
    }

    internal static void Write(string directory, string name, object data)
    {
        string filename = Path.Combine(directory, name);
        string temporary = filename + ".tmp";
        using (var output = File.Create(temporary))
            JsonSerializer.Serialize(output, data, RenderBenchmarkOptions.Json);
        File.Move(temporary, filename, overwrite: true);
    }
}
