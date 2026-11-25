using System.Globalization;
using System.Text;
using CastrumTactics.Domain;

internal static partial class Program
{
    internal sealed class Scores
    {
        public int Matches { get; private set; }
        public int Wins { get; private set; }
        public int Draws { get; private set; }
        public int Losses { get; private set; }
        public int DraftRetries { get; private set; }
        public int DraftScores { get; private set; }
        public int MainMatches { get; private set; }
        public int Failed { get; private set; }
        public int Limited { get; private set; }
        public long DraftRounds { get; private set; }
        public long Collisions { get; private set; }
        public long ForcedCollisions { get; private set; }
        public long Castles { get; private set; }
        public long MainTurns { get; private set; }
        public int? MinMainTurns { get; private set; }
        public int? MaxMainTurns { get; private set; }
        public long Margin { get; private set; }
        public int AvoidableLosses { get; private set; }
        public long Placements { get; private set; }
        public long InteriorPlacements { get; private set; }
        public int Scored => Wins + Draws + Losses;
        public int Completed => Scored + DraftRetries;
        public double Points => Wins + Draws / 2d;
        public double? WinRate => Ratio(Wins, Scored);
        public double? MeanMargin => Ratio(Margin, Scored);
        public double? MeanMainTurns => Ratio(MainTurns, MainMatches);
        public long SubmittedDraftRounds => DraftRounds - ForcedCollisions;
        public double? DraftCollisionRate => Ratio(Collisions, SubmittedDraftRounds);
        public double? DraftRetryRate => Ratio(DraftRetries, Completed);
        public double? MainRate => Ratio(MainMatches, Completed);

        internal void Add(TournamentResult result, bool red)
        {
            Matches++;
            if (result.Status == "failed") { Failed++; return; }
            if (result.Status == "limit") { Limited++; return; }
            DraftRounds += result.DraftRounds;
            Collisions += result.Collisions;
            ForcedCollisions += result.ForcedCollisions;
            Castles += red ? result.RedCastles : result.BlueCastles;
            if (result.Opening == "retry") { DraftRetries++; return; }
            if (result.Opening == "draftscore") DraftScores++;
            else
            {
                MainMatches++;
                MainTurns += result.MainTurns;
                MinMainTurns = Math.Min(MinMainTurns ?? result.MainTurns, result.MainTurns);
                MaxMainTurns = Math.Max(MaxMainTurns ?? result.MainTurns, result.MainTurns);
            }
            int margin = result.RedScore - result.BlueScore;
            Margin += red ? margin : -margin;
            if (result.Winner is null) Draws++;
            else if (result.Winner == (red ? Red : Blue)) Wins++;
            else Losses++;
            if (red ? result.RedAvoidableLoss : result.BlueAvoidableLoss) AvoidableLosses++;
            Placements += red ? result.RedPlacements : result.BluePlacements;
            InteriorPlacements += red ? result.RedInterior : result.BlueInterior;
        }
    }

    private sealed class Timings
    {
        // Logarithmic buckets keep memory bounded even for long runs and slow opponents.
        private readonly SortedDictionary<int, int> buckets = new();
        private readonly SortedDictionary<int, int> depths = new();
        private readonly SortedDictionary<int, int> completedDepths = new();
        internal int Count { get; private set; }
        private int searchSamples, budgetSamples, timeSamples, fallbackSamples, depthSamples, budget, timed, fallback;
        private long pairs, depth, allocated, reused;
        private double total, max, searchMilliseconds;

        internal void Add(DecisionSample sample)
        {
            if (!sample.Measured) return;
            Count++;
            total += sample.Milliseconds;
            max = Math.Max(max, sample.Milliseconds);
            allocated += sample.AllocatedBytes;
            int bucket = (int)Math.Ceiling(Math.Log(1 + sample.Milliseconds * 1000, 1.02));
            buckets[bucket] = buckets.GetValueOrDefault(bucket) + 1;
            if (sample.Pairs is int p) { searchSamples++; pairs += p; searchMilliseconds += sample.Milliseconds; }
            if (sample.Depth is int d)
            {
                depthSamples++;
                depth += d;
                depths[d] = depths.GetValueOrDefault(d) + 1;
                int completed = sample.CompletedDepth ?? d;
                completedDepths[completed] = completedDepths.GetValueOrDefault(completed) + 1;
            }
            reused += sample.ReusedPairs ?? 0;
            if (sample.BudgetExhausted is bool b) { budgetSamples++; if (b) budget++; }
            if (sample.TimeLimitReached is bool t) { timeSamples++; if (t) timed++; }
            if (sample.Fallback is bool f) { fallbackSamples++; if (f) fallback++; }
        }

        private double Quantile(double fraction)
        {
            int target = (int)Math.Ceiling(fraction * Count);
            foreach (var (bucket, count) in buckets)
            {
                target -= count;
                if (target <= 0) return Math.Min(max, (Math.Pow(1.02, bucket) - 1) / 1000);
            }
            return 0;
        }

        internal PerformanceRow Row(string participant, string opponent, string phase) => new(participant, opponent,
            phase, Count, total / Count, Quantile(.5), Quantile(.95), max, allocated / (double)Count,
            searchSamples, pairs, Ratio(pairs, searchSamples), depthSamples, Ratio(depth, depthSamples), budgetSamples,
            budget, timeSamples, timed, fallbackSamples, fallback,
            DepthCounts(depths), DepthCounts(completedDepths), Ratio(pairs * 1000d, searchMilliseconds), reused);

        private static string DepthCounts(SortedDictionary<int, int> history) =>
            string.Join('/', history.Select(pair => $"{pair.Key}:{pair.Value}"));
    }

    private sealed record PerformanceRow(string Participant, string Opponent, string Phase, int Decisions, double MeanMs,
        double MedianMs, double P95Ms, double MaxMs, double MeanBytes, int PairSamples, long Pairs, double? MeanPairs,
        int DepthSamples, double? MeanDepth, int BudgetSamples, int BudgetExhausted, int TimeSamples, int TimeLimitReached,
        int FallbackSamples, int Fallbacks, string DepthDistribution, string CompletedDepthDistribution,
        double? PairsPerSecond, long ReusedPairs);
    internal sealed record Rating(string Participant, string Component, double? RelativeRating, int Evidence, string Precision);
    internal sealed record Comparison(string First, string Second, int DistinctScored, int UniqueBlocks,
        double? BlockPointRate, double? Low95, double? High95);
    private sealed record ScoreRow(string Group, string Key, string Participant, Scores Scores);
    private sealed record MatchEvidence(string First, string Second, string Map, int Seed, bool SeatsSwapped,
        bool StartsSwapped, string History, double FirstPoints, bool Distinct);

    private static double? Ratio(double numerator, double denominator) => denominator == 0 ? null : numerator / denominator;

    internal static Rating[] FitRatings(string[] participants, IEnumerable<(string First, string Second, double Points)> games)
    {
        var outcomes = games.Where(game => game.First != game.Second)
            .GroupBy(game => (game.First, game.Second)).Select(g => (g.Key.First, g.Key.Second, Count: g.Count(), Points: g.Sum(x => x.Points))).ToArray();
        var strength = participants.ToDictionary(id => id, _ => 0d);
        var evidence = participants.ToDictionary(id => id, _ => 0);
        foreach (var game in outcomes) { evidence[game.First] += game.Count; evidence[game.Second] += game.Count; }
        // Fractional wins for draws. Ridge regularization gives finite estimates for all-win pairings.
        for (int i = 0; i < 250; i++)
        {
            var gradient = strength.ToDictionary(pair => pair.Key, pair => -.5 * pair.Value);
            var curvature = participants.ToDictionary(id => id, _ => .5);
            foreach (var (first, second, count, points) in outcomes)
            {
                double probability = 1 / (1 + Math.Exp(strength[second] - strength[first]));
                double delta = points - count * probability;
                double slope = count * probability * (1 - probability);
                gradient[first] += delta;
                gradient[second] -= delta;
                curvature[first] += slope;
                curvature[second] += slope;
            }
            foreach (string id in participants) strength[id] += .45 * gradient[id] / curvature[id];
        }
        var component = participants.ToDictionary(id => id, id => id);
        foreach (var (first, second, _, _) in outcomes)
        {
            string from = component[second], to = component[first];
            if (from == to) continue;
            foreach (string id in participants.Where(id => component[id] == from)) component[id] = to;
        }
        return participants.Select(id => new Rating(id, component[id], evidence[id] == 0 ? null :
            Math.Round(1500 + 400 / Math.Log(10) * strength[id], 1), evidence[id],
            evidence[id] < 12 ? "insufficient evidence" : "see paired intervals")).ToArray();
    }

    private static Comparison[] Compare(IEnumerable<MatchEvidence> evidence)
    {
        return evidence.GroupBy(game => (game.First, game.Second)).Select(group =>
        {
            // Preserve all scored assignment variants together and discard exact copies of the whole block.
            double[] blocks = group.GroupBy(g => (g.Map, g.Seed)).Select(block => new
            {
                block.Key.Map,
                Trace = string.Join(',', block.Select(g => $"{g.SeatsSwapped}/{g.StartsSwapped}/{g.History}")),
                Rate = block.Average(g => g.FirstPoints)
            }).DistinctBy(block => (block.Map, block.Trace)).Select(block => block.Rate).ToArray();
            int distinct = group.Count(g => g.Distinct);
            if (blocks.Length < 5 || blocks.Distinct().Count() == 1)
                return new Comparison(group.Key.First, group.Key.Second, distinct, blocks.Length, blocks.Average(), null, null);
            var random = new Random(StreamSeed(1, group.Key.First, group.Key.Second));
            var rates = new double[2000];
            for (int i = 0; i < rates.Length; i++)
            {
                double total = 0;
                for (int j = 0; j < blocks.Length; j++) total += blocks[random.Next(blocks.Length)];
                rates[i] = total / blocks.Length;
            }
            Array.Sort(rates);
            return new Comparison(group.Key.First, group.Key.Second, distinct, blocks.Length, blocks.Average(), rates[50], rates[1949]);
        }).ToArray();
    }

    private static void WriteReports(string directory, Manifest manifest, Progress progress, Dictionary<string, string> states)
    {
        var bins = new Dictionary<(string Group, string Key, string Participant), Scores>();
        var performance = new Dictionary<(string Participant, string Phase), Timings>();
        var evidence = new List<MatchEvidence>();
        var histories = new HashSet<(string First, string Second, bool Swapped, string Digest)>();
        var maps = manifest.Maps.ToDictionary(map => map.Id);
        var specs = manifest.Config.Participants.ToDictionary(spec => spec.Id);
        var errors = new List<string>();
        int repeated = 0;
        foreach (var match in manifest.Matches)
        {
            if (!states.ContainsKey(match.Id)) continue;
            var result = LoadResult(directory, match.Id);
            var map = maps[match.Map];
            string size = Math.Max(map.Width, map.Height) switch { <= 7 => "small (<=7)", <= 9 => "medium (8–9)", _ => "large (>=10)" };
            string red = match.SeatsSwapped ? match.Second : match.First;
            string blue = match.SeatsSwapped ? match.First : match.Second;
            Add(red, blue, true);
            Add(blue, red, false);
            if (result.Error is not null) errors.Add($"{match.Id} {result.Error}");
            if (result.HistoryDigest is string digest)
            {
                bool distinct = histories.Add((match.First, match.Second, match.SeatsSwapped, digest));
                if (!distinct) repeated++;
                if (result.Opening != "retry" && match.First != match.Second)
                    evidence.Add(new(match.First, match.Second, match.Map, match.Seed, match.SeatsSwapped,
                        match.StartsSwapped, digest, result.Winner is null ? .5 :
                        result.Winner == (match.SeatsSwapped ? Blue : Red) ? 1 : 0, distinct));
            }

            void Add(string participant, string other, bool isRed)
            {
                Group("participant", participant);
                Group("pairing", other);
                Group("map", map.Id);
                Group("size", size);
                Group("seat", isRed ? "red" : "blue");
                if (manifest.Config.Opening.Mode == "fixed") Group("starting assignment", isRed ^ match.SeatsSwapped ^ match.StartsSwapped ? "first" : "second");
                foreach (var round in result.Rounds)
                {
                    var sample = isRed ? round.Red : round.Blue;
                    if (!sample.Measured) continue;
                    var key = (participant, round.Phase);
                    if (!performance.TryGetValue(key, out var timing)) performance.Add(key, timing = new());
                    timing.Add(sample);
                }

                void Group(string group, string key)
                {
                    var bucket = (group, key, participant);
                    if (!bins.TryGetValue(bucket, out var scores)) bins.Add(bucket, scores = new());
                    scores.Add(result, isRed);
                }
            }
        }
        foreach (string id in specs.Keys) bins.TryAdd(("participant", id, id), new());
        var rows = bins.Select(pair => new ScoreRow(pair.Key.Group, pair.Key.Key, pair.Key.Participant, pair.Value))
            .OrderBy(row => row.Group, StringComparer.Ordinal).ThenBy(row => row.Key, StringComparer.Ordinal)
            .ThenBy(row => row.Participant, StringComparer.Ordinal).ToArray();
        var timings = performance.OrderBy(pair => pair.Key.Participant, StringComparer.Ordinal).ThenBy(pair => pair.Key.Phase, StringComparer.Ordinal)
            .Select(pair => pair.Value.Row(pair.Key.Participant, specs[pair.Key.Participant].Opponent, pair.Key.Phase)).ToArray();
        var ratings = FitRatings(specs.Keys.Order(StringComparer.Ordinal).ToArray(),
            evidence.Where(game => game.Distinct).Select(game => (game.First, game.Second, game.FirstPoints)));
        var comparisons = Compare(evidence);
        SaveJson(Path.Combine(directory, "summary.json"), new { manifest.Config.Id, manifest.Config.Opening.Mode,
            manifest.DedicatedPerformance, progress, DistinctHistories = histories.Count, RepeatedHistories = repeated, rows, ratings, comparisons, timings, errors });
        var scoreProperties = typeof(Scores).GetProperties();
        Csv("scores", new[] { "Group", "Key", "Participant" }.Concat(scoreProperties.Select(p => p.Name)),
            rows.Select(row => new object?[] { row.Group, row.Key, row.Participant }.Concat(scoreProperties.Select(p => p.GetValue(row.Scores)))));
        Export("performance", timings);
        Export("ratings", ratings);
        Export("comparisons", comparisons);
        SaveText(Path.Combine(directory, "summary.md"), ReportMarkdown(manifest, progress, rows, timings, ratings, comparisons, histories.Count, repeated, errors));

        void Export<T>(string name, T[] items)
        {
            var properties = typeof(T).GetProperties();
            Csv(name, properties.Select(p => p.Name), items.Select(item => properties.Select(p => p.GetValue(item))));
        }
        void Csv(string name, IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> values)
        {
            var csv = new StringBuilder(string.Join(',', headers)).AppendLine();
            foreach (var row in values) csv.AppendLine(string.Join(',', row.Select(value =>
                $"\"{(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "").Replace("\"", "\"\"")}\"")));
            SaveText(Path.Combine(directory, $"{name}.csv"), csv.ToString());
        }
    }

    private static string ReportMarkdown(Manifest manifest, Progress progress, ScoreRow[] rows, PerformanceRow[] timings,
        Rating[] ratings, Comparison[] comparisons, int distinct, int repeated, List<string> errors)
    {
        var report = new StringBuilder($"# {manifest.Config.Id}").AppendLine().AppendLine()
            .AppendLine($"{manifest.Config.Opening.Mode} openings under {manifest.Config.Ruleset}. {manifest.Config.Format}, {manifest.Maps.Length} maps, {progress.Workers} workers.")
            .AppendLine($"{progress.Completed}/{progress.Scheduled} completed, {progress.Failed} failed, {progress.Limited} limited, {progress.Pending} pending. {distinct} distinct and {repeated} repeated completed histories.")
            .AppendLine($"Runtime {manifest.Runtime}, {manifest.OS}, {manifest.Architecture}, {manifest.Processor}, {manifest.LogicalProcessors} logical processors.")
            .AppendLine($"Revision `{manifest.Revision ?? "unavailable"}`. Configuration, schedule and binary identifiers are in `manifest.json`. Resume with `tournament {(manifest.DedicatedPerformance ? "performance" : "run")} --out <this-directory>`.")
            .AppendLine().AppendLine("## Leaderboard and openings").AppendLine()
            .AppendLine("|Participant|W–D–L|Points|Mean margin|Main|Draft scored|Draft retry|Collisions / rounds|Rating (evidence)|")
            .AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---|");
        foreach (var row in rows.Where(row => row.Group == "participant").OrderByDescending(row => row.Scores.Points))
        {
            var s = row.Scores;
            var rating = ratings.Single(r => r.Participant == row.Participant);
            report.AppendLine($"|{row.Participant}|{Wdl(s)}|{s.Points:F1}|{s.MeanMargin:F2}|{s.MainMatches}|{s.DraftScores}|{s.DraftRetries}|{s.Collisions}/{s.SubmittedDraftRounds}|{rating.RelativeRating} ({rating.Evidence}, {rating.Component}, {rating.Precision})|");
        }
        report.AppendLine().AppendLine("## Head-to-head (row player W–D–L)").AppendLine()
            .AppendLine($"|Participant|{string.Join('|', ratings.Select(r => r.Participant))}|")
            .AppendLine($"|---|{string.Concat(ratings.Select(_ => "---|"))}");
        foreach (var r in ratings)
            report.AppendLine($"|{r.Participant}|{string.Join('|', ratings.Select(other => Wdl(rows.FirstOrDefault(row => row.Group == "pairing" && row.Key == other.Participant && row.Participant == r.Participant)?.Scores)))}|");
        report.AppendLine().AppendLine("## Paired comparisons").AppendLine()
            .AppendLine("|First|Second|Distinct scored|Blocks|First points / block match|95% interval|")
            .AppendLine("|---|---|---:|---:|---:|---|");
        foreach (var c in comparisons) report.AppendLine($"|{c.First}|{c.Second}|{c.DistinctScored}|{c.UniqueBlocks}|{c.BlockPointRate:P1}|{(c.Low95 is null ? "insufficient evidence" : $"{c.Low95:P1}–{c.High95:P1}")}|");
        report.AppendLine().AppendLine("## Battlefield results").AppendLine()
            .AppendLine("|Map|Participant|W–D–L|Mean margin|Main|Retry|").AppendLine("|---|---|---|---:|---:|---:|");
        foreach (var row in rows.Where(row => row.Group == "map"))
            report.AppendLine($"|{row.Key}|{row.Participant}|{Wdl(row.Scores)}|{row.Scores.MeanMargin:F2}|{row.Scores.MainMatches}|{row.Scores.DraftRetries}|");
        report.AppendLine().AppendLine("## Decision performance").AppendLine()
            .AppendLine(manifest.DedicatedPerformance ? "Dedicated sequential measurements." : "Tournament timings include worker contention when run concurrently.")
            .AppendLine().AppendLine("|Participant|Phase|Decisions|Median ms|P95 ms|Max ms|Mean pairs|Pairs/s|Selected depth|Completed depth|Reused pairs|Budget exhausted|Time exhausted|Fallbacks|Mean bytes|")
            .AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---|---|---|---:|");
        foreach (var p in timings)
            report.AppendLine($"|{p.Participant}|{p.Phase}|{p.Decisions}|{p.MedianMs:F3}|{p.P95Ms:F3}|{p.MaxMs:F3}|{p.MeanPairs:F1}|{p.PairsPerSecond:F0}|{p.DepthDistribution}|{p.CompletedDepthDistribution}|{p.ReusedPairs}|{p.BudgetExhausted}/{p.BudgetSamples}|{p.TimeLimitReached}/{p.TimeSamples}|{p.Fallbacks}/{p.FallbackSamples}|{p.MeanBytes:F0}|");
        if (errors.Count != 0) report.AppendLine().AppendLine("## Technical failures").AppendLine().AppendLine(string.Join('\n', errors.Select(e => $"- {e.ReplaceLineEndings(" ")}")));
        return report.ToString();

        static string Wdl(Scores? s) => s is null ? "—" : $"{s.Wins}–{s.Draws}–{s.Losses}";
    }
}
