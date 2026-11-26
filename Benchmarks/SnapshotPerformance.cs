extern alias frozen8d4;

using CastrumTactics.Development;
using System.Diagnostics;
using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Frozen = frozen8d4::CastrumTactics.AI;
using static CastrumTactics.Development.TournamentStore;

internal static partial class Program
{
    private sealed record PerformanceSnapshot(string Map, string Match, string Phase, int Round, GameSessionSnapshot State);
    private sealed record SnapshotMeasurement(string Map, string Match, string Phase, int Round, int Iteration, string Opponent,
        string? Move, double Milliseconds, long AllocatedBytes, bool Cancelled, int? Pairs, int? Depth, int? CompletedDepth, bool? TimeLimitReached);

    private static int RunSnapshotPerformance(string[] args)
    {
        if (args.Length != 6 ||
            args[2] is not ("draft" or "main") || !int.TryParse(args[3], out int repetitions) ||
            repetitions is < 1 or > 10000 || !int.TryParse(args[4], out int guard) || guard is < 1 or > 60000)
            throw new ArgumentException("Usage: snapshot-performance <tournament-dir> <frozen8d4|strategic|config.json> <draft|main> <repetitions> <guard-ms> <output.jsonl>");

        TournamentOpponent[] opponents = args[1] is "frozen8d4" or "strategic" ? [] :
            ReadJson<TournamentConfig>(args[1]).Participants.Select(TournamentOpponentFactory.Create).ToArray();
        if (opponents.Length == 0 && args[1] is not ("frozen8d4" or "strategic"))
            throw new ArgumentException("The snapshot configuration must name at least one opponent.");
        var manifest = ReadJson<Manifest>(ManifestPath(args[0]));
        var states = CheckResults(args[0], manifest);
        var snapshots = new List<PerformanceSnapshot>();
        foreach (var group in manifest.Matches.GroupBy(m => m.Map))
        {
            foreach (var match in group)
            {
                if (states.GetValueOrDefault(match.Id) != "completed") continue;
                var result = LoadResult(args[0], match.Id);
                if (result.Opening != "main") continue;
                var rounds = result.Rounds.Where(r => r.Phase == args[2]).ToArray();
                if (rounds.Length == 0) continue;
                foreach (int i in new[] { 0, rounds.Length / 2, rounds.Length - 1 }.Distinct())
                {
                    int round = rounds[i].Number;
                    snapshots.Add(new(match.Map, match.Id, args[2], round,
                        TournamentReplay.Reconstruct(manifest, match, result, args[2], round)));
                }
                break;
            }
        }
        if (snapshots.Count == 0) throw new ArgumentException("No playable snapshots in the chosen phase.");

        var time = TimeSpan.FromMilliseconds(guard);
        var live = new CpuDecisionEngine(time);
        var frozen = new Frozen.CpuDecisionEngine(time);
        var liveProfile = CpuDifficultyProfiles.Get(CpuDifficulty.Monster);
        var frozenProfile = Frozen.CpuDifficultyProfiles.Get(Frozen.CpuDifficulty.Monster);
        string output = Path.GetFullPath(args[5]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var log = new StreamWriter(output, append: false) { AutoFlush = true };
        int[] gcBefore = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        log.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            manifest.Revision, manifest.Signature, opponent = args[1], phase = args[2], guard,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            diagnosticParticipants = opponents.Select(o => o.Spec),
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            processorCount = Environment.ProcessorCount, repetitions
        }, Json));
        for (int iteration = 0; iteration <= repetitions; iteration++)
        foreach (var snapshot in snapshots)
        {
            if (opponents.Length == 0)
                WriteMeasurement(snapshot, iteration, CancellationToken.None);
            foreach (var opponent in opponents)
            {
                var decision = TournamentMeasurement.Measure(opponent, snapshot.State, Red, new Random(7319), diagnostics: false, CancellationToken.None);
                Write(new(snapshot.Map, snapshot.Match, snapshot.Phase, snapshot.Round, iteration, opponent.Spec.Id,
                    decision.Move, decision.Milliseconds, decision.AllocatedBytes, false, decision.Pairs,
                    decision.Depth, decision.CompletedDepth, decision.TimeLimitReached));
            }
        }

        if (args[2] == "main" && opponents.Length == 0)
        {
            var wide = snapshots.MaxBy(s => s.State.GetMainPhaseLegalSelections(Red).Count)!;
            for (int i = 0; i < 12; i++)
            {
                using var cancellation = new CancellationTokenSource();
                cancellation.CancelAfter(TimeSpan.FromMilliseconds(2));
                WriteMeasurement(wide, -1, cancellation.Token);
            }
        }
        using var process = Process.GetCurrentProcess();
        log.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            gc = gcBefore.Select((count, gen) => GC.CollectionCount(gen) - count),
            process.PeakWorkingSet64
        }, Json));
        return 0;

        void WriteMeasurement(PerformanceSnapshot snapshot, int iteration, CancellationToken token)
        {
            var random = new Random(7319);
            string? move = null;
            bool cancelled = false;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            try
            {
                BoardPosition? decision = args[1] == "strategic"
                    ? live.Decide(snapshot.State, Red, liveProfile, random, token)?.Position
                    : frozen.Decide(snapshot.State, Red, frozenProfile, random, token)?.Position;
                if (decision is { } chosen) move = Coordinate(chosen);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                cancelled = true;
            }
            Write(new(snapshot.Map, snapshot.Match, snapshot.Phase, snapshot.Round, iteration, args[1],
                move, Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - allocated, cancelled, null, null, null, null));
        }

        void Write(SnapshotMeasurement measurement) =>
            log.WriteLine(System.Text.Json.JsonSerializer.Serialize(measurement, Json));
    }
}
