using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CastrumTactics.AI;
using CastrumTactics.Domain;

internal static partial class Program
{
    internal sealed record Manifest(int Schema, TournamentConfig Config, MapIdentity[] Maps, ScheduledMatch[] Matches,
        Dictionary<string, string> Opponents, Dictionary<string, string> Binaries, string? Revision,
        string Runtime, string OS, string Architecture, string Processor, int LogicalProcessors,
        bool DedicatedPerformance, string Signature, DateTimeOffset Created);

    internal sealed record Progress(int Scheduled, int Completed, int Failed, int Limited, int Pending,
        string State, int Workers, double LastRunSeconds);

    private sealed record ResultEnvelope(int Schema, string Checksum, JsonElement Payload);

    internal static Manifest MakeManifest(TournamentPlan plan, bool dedicated = false)
    {
        var assemblies = new[] { typeof(Program).Assembly, typeof(CpuDecisionEngine).Assembly,
            typeof(RuleSet).Assembly }.Concat(typeof(Program).Assembly.GetReferencedAssemblies()
            .Where(name => name.Name is "CastrumTactics.FrozenAI" or "CastrumTactics.Frozen8C" or "CastrumTactics.Frozen8D2" or "CastrumTactics.Frozen8D3" or "CastrumTactics.Frozen8D4" or "CastrumTactics.Frozen8E")
            .Select(System.Reflection.Assembly.Load));
        var binaries = assemblies.OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .ToDictionary(assembly => assembly.GetName().Name!, assembly =>
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location))));
        var opponents = plan.Opponents.ToDictionary(pair => pair.Key, pair => pair.Value.Version);
        var config = plan.Config;
        string signature = Digest(JsonSerializer.Serialize(new
        {
            Schema = TournamentSchema,
            Config = config with { OutputDirectory = null, Workers = 1, Participants = config.Participants.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray() },
            plan.Maps, plan.Matches, opponents, binaries, RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture, dedicated
        }, TournamentJson));
        return new(TournamentSchema, config, plan.Maps, plan.Matches, opponents, binaries, ReadRevision(),
            RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(), Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unavailable",
            Environment.ProcessorCount, dedicated, signature, DateTimeOffset.UtcNow);
    }

    private static string? ReadRevision()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            });
            if (process is null) return null;
            string revision = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? revision : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    internal static T ReadJson<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), TournamentJson)
        ?? throw new InvalidDataException($"Empty JSON in '{path}'.");

    private static void SaveJson<T>(string path, T value) =>
        SaveText(path, JsonSerializer.Serialize(value, TournamentJson) + Environment.NewLine);

    private static void SaveText(string path, string content)
    {
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, content);
            // A Windows reader can briefly deny replacement of the current checkpoint.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(temporary, path, overwrite: true);
                    break;
                }
                catch (Exception error) when (OperatingSystem.IsWindows() && attempt < 6 &&
                    error is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(20 << attempt);
                }
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string ManifestPath(string directory) => Path.Combine(directory, "manifest.json");
    private static string ResultsPath(string directory) => Path.Combine(directory, "matches");
    private static string ResultPath(string directory, string id) => Path.Combine(ResultsPath(directory), $"{id}.json");
    private static FileStream LockTournament(string directory) =>
        new(Path.Combine(directory, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    internal static Dictionary<string, string> CheckResults(string directory, Manifest manifest)
    {
        if (manifest.Schema != TournamentSchema || manifest.Signature != ManifestSignature(manifest))
            throw new InvalidDataException("Unsupported or corrupted tournament manifest.");
        var scheduled = manifest.Matches.Select(match => match.Id).ToHashSet(StringComparer.Ordinal);
        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(ResultsPath(directory), "*.json"))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            if (!scheduled.Contains(id)) throw new InvalidDataException($"Unscheduled result '{path}'.");
            states.Add(id, LoadResult(directory, id).Status);
        }
        return states;
    }

    internal static TournamentResult LoadResult(string directory, string id)
    {
        string path = ResultPath(directory, id);
        try
        {
            var envelope = ReadJson<ResultEnvelope>(path);
            if (envelope.Schema != TournamentSchema || envelope.Payload.ValueKind != JsonValueKind.Object ||
                envelope.Checksum != Digest(envelope.Payload.GetRawText())) throw new InvalidDataException("Checksum or version mismatch.");
            var result = envelope.Payload.Deserialize<TournamentResult>(TournamentJson);
            if (result is null || result.Id != id || result.Rounds is null ||
                result.Status is not ("completed" or "failed" or "limit") ||
                result.Opening is not ("main" or "draftscore" or "retry" or "unfinished") ||
                (result.Status == "completed" && (result.HistoryDigest is null || result.Opening == "unfinished")) ||
                (result.Status == "failed" && string.IsNullOrEmpty(result.Error)))
                throw new InvalidDataException("Invalid result record.");
            return result;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            throw new InvalidDataException($"Invalid tournament result '{path}': {exception.Message}", exception);
        }
    }

    internal static Progress ExecuteTournament(string directory, TournamentPlan plan, bool dedicated, int? stopAfter,
        CancellationToken token, Func<TournamentPlan, ScheduledMatch, CancellationToken, TournamentResult>? simulate = null)
    {
        if (stopAfter is <= 0) throw new ArgumentException("Stop-after must be positive.");
        Directory.CreateDirectory(directory);
        using var directoryLock = LockTournament(directory);
        Directory.CreateDirectory(ResultsPath(directory));
        var current = MakeManifest(plan, dedicated);
        var manifest = current;
        if (File.Exists(ManifestPath(directory)))
        {
            manifest = ReadJson<Manifest>(ManifestPath(directory));
            if (current.Signature != manifest.Signature || current.Signature != ManifestSignature(manifest))
                throw new InvalidDataException("Incompatible tournament manifest. Configuration, runtime, maps, opponents, or executable versions differ.");
        }
        else
        {
            if (Directory.EnumerateFiles(ResultsPath(directory), "*.json").Any())
                throw new InvalidDataException("Result files exist without a manifest.");
            SaveJson(ManifestPath(directory), manifest);
        }

        var states = CheckResults(directory, manifest);
        var pending = plan.Matches.Where(match => !states.TryGetValue(match.Id, out var state) || state == "failed")
            .Take(stopAfter ?? int.MaxValue).ToArray();
        var counts = states.Values.GroupBy(state => state).ToDictionary(group => group.Key, group => group.Count());
        var gate = new object();
        var watch = Stopwatch.StartNew();
        Checkpoint("running");
        try
        {
            Parallel.ForEach(pending, new ParallelOptions { MaxDegreeOfParallelism = plan.Config.Workers, CancellationToken = token }, match =>
            {
                TournamentResult result;
                try { result = (simulate ?? PlayTournamentMatch)(plan, match, token); }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    result = FailedMatch(match.Id, exception);
                }
                var payload = JsonSerializer.SerializeToElement(result, TournamentJson);
                SaveJson(ResultPath(directory, match.Id), new ResultEnvelope(TournamentSchema, Digest(payload.GetRawText()), payload));
                lock (gate)
                {
                    if (states.TryGetValue(match.Id, out string? previous)) counts[previous]--;
                    states[match.Id] = result.Status;
                    counts[result.Status] = counts.GetValueOrDefault(result.Status) + 1;
                    Checkpoint("running");
                }
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { watch.Stop(); }
        var progress = Checkpoint(token.IsCancellationRequested ? "cancelled" : states.Count < plan.Matches.Length ? "paused" : "finished");
        WriteReports(directory, manifest, progress, states);
        return progress;

        Progress Checkpoint(string state)
        {
            var progress = new Progress(plan.Matches.Length, counts.GetValueOrDefault("completed"),
                counts.GetValueOrDefault("failed"), counts.GetValueOrDefault("limit"), plan.Matches.Length - states.Count,
                state, plan.Config.Workers, watch.Elapsed.TotalSeconds);
            SaveJson(Path.Combine(directory, "progress.json"), progress);
            return progress;
        }
    }

    private static TournamentResult FailedMatch(string id, Exception exception) =>
        new(id, "failed", "unfinished", null, 0, 0, 0, 0, 0, 0, 0, 0, false, false, 0, 0, 0, 0, null, [],
            $"{exception.GetType().Name}: {exception.Message}");

    private static string ManifestSignature(Manifest manifest) => Digest(JsonSerializer.Serialize(new
    {
        manifest.Schema,
        Config = manifest.Config with { OutputDirectory = null, Workers = 1,
            Participants = manifest.Config.Participants.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray() },
        manifest.Maps, manifest.Matches, opponents = manifest.Opponents, binaries = manifest.Binaries,
        FrameworkDescription = manifest.Runtime,
        ProcessArchitecture = Enum.Parse<Architecture>(manifest.Architecture), dedicated = manifest.DedicatedPerformance
    }, TournamentJson));
}
