extern alias benchmarks;

using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using System.Text.Json;
using Xunit;
using static benchmarks::Program;
using static benchmarks::CastrumTactics.Development.TournamentPlanner;
using static benchmarks::CastrumTactics.Development.TournamentStore;
using static benchmarks::CastrumTactics.Development.TournamentRunner;
using static benchmarks::CastrumTactics.Development.TournamentReplay;

namespace CastrumTactics.AI.Tests;

public sealed class TournamentTests
{
    private static TournamentConfig Config => new()
    {
        Id = "test-suite", Format = "head-to-head", Participants = [new("alpha", "static:Easy"), new("beta", "static:Normal")],
        Maps = new() { Ids = ["pocket-meadow"] }, Opening = new() { Mode = "fixed" },
        Workers = 1, StrategicMetrics = false
    };

    [Fact]
    public void ScheduleUsesDistinctPairsAndIndependentMirroredStreams()
    {
        var config = Config with { Format = "round-robin", Participants = [.. Config.Participants, new("gamma", "probe")],
            Seeds = [4, 2], SwapStarts = true };
        var plan = PlanTournament(config);
        Assert.Equal(24, plan.Matches.Length);
        Assert.Equal(3, plan.Matches.Select(m => (m.First, m.Second)).Distinct().Count());
        Assert.Equal(24, plan.Matches.Select(m => m.Id).Distinct().Count());
        Assert.Equal(plan.Matches.Select(m => m.Id), PlanTournament(config with { Participants = config.Participants.Reverse().ToArray() }).Matches.Select(m => m.Id));
        foreach (var group in plan.Matches.GroupBy(m => (m.First, m.Second, m.Seed)))
        {
            Assert.Equal(4, group.Select(m => (m.SeatsSwapped, m.StartsSwapped)).Distinct().Count());
            Assert.Single(group.Select(m => m.FirstStream).Distinct());
            Assert.Single(group.Select(m => m.SecondStream).Distinct());
            Assert.NotEqual(group.First().FirstStream, group.First().SecondStream);
            Assert.All(group, match => Assert.Equal(match.SeatsSwapped, match.BlueSubmitsFirst));
        }
        Assert.Equal(4, plan.Matches.Select(m => m.FirstStream).Distinct().Count());
        var games = plan.Matches.Take(4).Select(match => PlayTournamentMatch(plan, match, default).Rounds[0]).ToArray();
        Assert.Equal(games[0].Red.Move, games[1].Blue.Move);
        Assert.Equal(games[0].Red.Move, games[2].Blue.Move);
        Assert.Equal(games[0].Red.Move, games[3].Red.Move);
    }

    [Fact]
    public void StreamAliasesPreservePairedRandomDrawsAcrossCandidateLabels()
    {
        var config = Config with { SeparatePhaseStreams = true, Opening = new() };
        var first = PlanTournament(config);
        var second = PlanTournament(config with
        {
            Participants = [config.Participants[0] with { Id = "alternative", StreamId = "alpha" }, config.Participants[1]]
        });
        foreach (var (a, b) in first.Matches.Zip(second.Matches))
        {
            Assert.Equal((a.FirstStream, a.SecondStream), (b.FirstStream, b.SecondStream));
            Assert.Equal(PlayTournamentMatch(first, a, default).HistoryDigest,
                PlayTournamentMatch(second, b, default).HistoryDigest);
        }
    }

    [Fact]
    public void CatalogAuditUsesReachablePairedCastlesAndKeepsResearchMapsOutOfProduction()
    {
        var rows = PredefinedMapCatalog.BuiltIn.Maps.Select(AuditBattlefield).ToArray();
        Assert.Equal(100, rows.Length);
        Assert.All(rows, row =>
        {
            Assert.Equal(row.Playable, row.Regions.Sum());
            Assert.Contains(0, row.Symmetries);
            Assert.Contains(row.Openings, opening => opening.Phase == MatchPhase.Main);
            Assert.All(row.Openings, opening => Assert.Equal(opening.RedCastles, opening.BlueCastles));
        });
        foreach (int size in new[] { 4, 6, 8, 10, 12 })
        foreach (string family in new[] { "open", "block" })
        {
            string id = $"study-{family}-{size}";
            var map = benchmarks::StudyBattlefields.Resolve(id);
            var row = AuditBattlefield(map);
            Assert.Equal(size, row.Width);
            Assert.Equal(4, row.Starts.Length);
            Assert.True(family == "open" ? row.Rocks == 0 : row.Rocks > 0);
            Assert.DoesNotContain(rows, production => production.Id == id);
            var plan = PlanTournament(Config with { Maps = new() { Ids = [id] } });
            Assert.Equal("completed", PlayTournamentMatch(plan, plan.Matches[0], default).Status);
        }
    }

    [Fact]
    public void MapSubsetsHoldOutAndHistoryRequireExplicitSelection()
    {
        Assert.True(Assert.Single(PlanTournament(Config).Maps).Historical);
        var config = Config with { Opening = new(), Maps = new() };
        var production = PlanTournament(config).Maps;
        Assert.Equal(100, production.Length);
        Assert.DoesNotContain(production, map => map.Historical);
        var filtered = new MapSubset { MinWidth = 8, MaxHeight = 12, Sample = 3, SampleSeed = 7,
            HeldOut = ["banner-plain"], Split = "tuning" };
        var maps = PlanTournament(config with { Maps = filtered }).Maps;
        Assert.Equal(3, maps.Length);
        Assert.DoesNotContain(maps, map => map.Id == "banner-plain");
        Assert.Equal(maps, PlanTournament(config with { Maps = filtered }).Maps);
        Assert.True(Assert.Single(PlanTournament(config with { Maps = filtered with { Split = "held-out", Sample = 1 } }).Maps).HeldOut);
        Assert.ThrowsAny<ArgumentException>(() => PlanTournament(Config with { Seeds = [1, 1] }));
        Assert.ThrowsAny<ArgumentException>(() => PlanTournament(Config with { Opening = new(), SwapStarts = true }));
        Assert.ThrowsAny<ArgumentException>(() => PlanTournament(Config with { Workers = 0 }));
        Assert.ThrowsAny<ArgumentException>(() => PlanTournament(Config with { Maps = new() { Sample = 101 } }));
        Assert.ThrowsAny<ArgumentException>(() => PlanTournament(Config with { Opening = new() { Mode = "fixed",
            Fixed = new() { ["pocket-meadow"] = ["Z1/Z2"] } } }));
    }

    [Fact]
    public void CancelsResumesAndReplaysWithoutChangingCompletedShards()
    {
        string directory = NewPath(), sequential = NewPath();
        try
        {
            var plan = PlanTournament(Config with { Seeds = [1, 2] });
            using var cancellation = new CancellationTokenSource();
            int attempted = 0;
            var paused = ExecuteTournament(directory, plan, false, null, cancellation.Token, (run, match, token) =>
            {
                if (++attempted == 2) cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return PlayTournamentMatch(run, match, token);
            });
            Assert.Equal("cancelled", paused.State);
            Assert.Equal(1, paused.Completed);
            Assert.Equal(3, paused.Pending);
            string path = Assert.Single(Directory.GetFiles(Path.Combine(directory, "matches"), "*.json"));
            byte[] original = File.ReadAllBytes(path);
            var resumed = ExecuteTournament(directory, plan with { Config = plan.Config with { Workers = 2 } }, false, null, default);
            Assert.Equal(4, resumed.Completed);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(4, Directory.GetFiles(Path.Combine(directory, "matches"), "*.json").Length);
            Assert.Equal(4, ExecuteTournament(sequential, plan, false, null, default).Completed);
            var manifest = MakeManifest(plan);
            foreach (var match in plan.Matches)
            {
                var left = LoadResult(directory, match.Id);
                var right = LoadResult(sequential, match.Id);
                Assert.Equal(left.HistoryDigest, right.HistoryDigest);
                Assert.Equal(left.Winner, right.Winner);
                Assert.Equal(MatchPhase.Finished, Reconstruct(manifest, match, left).Phase);
                Assert.Equal(MatchPhase.Main, Reconstruct(manifest, match, left, "main", 1).Phase);
            }
            Assert.Throws<InvalidDataException>(() => ExecuteTournament(directory, PlanTournament(Config), false, null, default));
            File.AppendAllText(path, "corruption");
            Assert.Throws<InvalidDataException>(() => ExecuteTournament(directory, plan, false, null, default));
        }
        finally { Remove(directory); Remove(sequential); }
    }

    [Fact]
    public void RetriesAndTechnicalFailuresDoNotBecomeScoredDraws()
    {
        string directory = NewPath();
        try
        {
            var plan = PlanTournament(Config with { Opening = new() });
            foreach (var opponent in plan.Opponents.Values.ToArray())
                plan.Opponents[opponent.Spec.Id] = opponent with { Decide = (state, _, _, _, _) =>
                {
                    var p = state.EligiblePositions[0];
                    return new($"{(char)('A' + p.X)}{p.Y + 1}");
                } };
            var progress = ExecuteTournament(directory, plan, false, null, default, (run, match, token) =>
                match.SeatsSwapped ? throw new InvalidOperationException("test failure") : PlayTournamentMatch(run, match, token));
            Assert.Equal(1, progress.Completed);
            Assert.Equal(1, progress.Failed);
            var retry = LoadResult(directory, plan.Matches[0].Id);
            var failed = LoadResult(directory, plan.Matches[1].Id);
            Assert.Equal("retry", retry.Opening);
            Assert.Equal(MatchPhase.OpeningRestartRequired, Reconstruct(MakeManifest(plan), plan.Matches[0], retry).Phase);
            Assert.Contains("test failure", failed.Error);
            var scores = new Scores();
            scores.Add(retry, true);
            scores.Add(failed, true);
            var limited = PlanTournament(Config with { MaxRounds = 1 });
            scores.Add(PlayTournamentMatch(limited, limited.Matches[0], default), true);
            Assert.Equal(3, scores.Matches);
            Assert.Equal(0, scores.Scored);
            Assert.Equal(1, scores.Failed);
            Assert.Equal(1, scores.Limited);
            Assert.Equal(1, scores.DraftRetries);
            Assert.Equal(1, scores.DraftCollisionRate);
            var resumed = ExecuteTournament(directory, plan, false, null, default);
            Assert.Equal(2, resumed.Completed);
            Assert.Equal(0, resumed.Failed);
        }
        finally { Remove(directory); }
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("Normal")]
    [InlineData("Hard")]
    [InlineData("VeryHard")]
    [InlineData("Monster")]
    public void FrozenEightCMatchesEquivalentProductionSettings(string difficulty)
    {
        SearchOverrides? search = difficulty switch
        {
            "Monster" => new(Depth: 2),
            "VeryHard" => new(ResilientEvaluation: false),
            _ => null
        };
        var config = Config with { Participants = [new("alpha", $"strategic:{difficulty}", Search: search), new("beta", "static:Normal")] };
        var frozenConfig = config with { Participants = [new("alpha", $"frozen8c:{difficulty}"), config.Participants[1]] };
        var live = PlanTournament(config);
        var frozen = PlanTournament(frozenConfig);
        var a = PlayTournamentMatch(live, live.Matches[0], default);
        var b = PlayTournamentMatch(frozen, frozen.Matches[0], default);
        Assert.Equal("completed", a.Status);
        Assert.Equal(a.HistoryDigest, b.HistoryDigest);
        Assert.Equal(a.Rounds.Select(r => (r.Red.Move, r.Red.Pairs, r.Red.Depth, r.Red.BudgetExhausted)),
            b.Rounds.Select(r => (r.Red.Move, r.Red.Pairs, r.Red.Depth, r.Red.BudgetExhausted)));
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("Normal")]
    [InlineData("Hard")]
    [InlineData("VeryHard")]
    [InlineData("Monster")]
    public void FrozenEightD2MatchesExplicitLegacySearch(string difficulty)
    {
        var config = Config with
        {
            Participants = [new("alpha", $"strategic:{difficulty}", Search: new()), new("beta", "static:Normal")]
        };
        var frozenConfig = config with { Participants = [new("alpha", $"frozen8d2:{difficulty}"), config.Participants[1]] };
        var live = PlanTournament(config);
        var frozen = PlanTournament(frozenConfig);
        var a = PlayTournamentMatch(live, live.Matches[0], default);
        var b = PlayTournamentMatch(frozen, frozen.Matches[0], default);
        Assert.Equal("completed", a.Status);
        Assert.Equal(a.HistoryDigest, b.HistoryDigest);
        Assert.Equal(a.Rounds.Select(r => (r.Red.Move, r.Red.Pairs, r.Red.Depth, r.Red.BudgetExhausted)),
            b.Rounds.Select(r => (r.Red.Move, r.Red.Pairs, r.Red.Depth, r.Red.BudgetExhausted)));
    }

    [Fact]
    public void SearchOverridesRoundTripAndArePartOfTheManifestIdentity()
    {
        string file = NewPath();
        try
        {
            File.WriteAllText(file, """
                {"id":"override-test","format":"head-to-head","maps":{"ids":["pocket-meadow"]},
                 "participants":[{"id":"cpu","opponent":"strategic:Monster","pairBudget":8000,"timeLimitMs":1500,
                  "search":{"depth":4,"ownCandidates":22,"opponentCandidates":14,"continuationOwnCandidates":3,
                  "continuationOpponentCandidates":2,"deepFinalists":2,"risk":0.35,"replyTemperature":1.2,
                  "verifyTerminalWins":true,"requireFullRootReplies":false,"proofOnlyDeepening":false}},
                  {"id":"frozen","opponent":"frozen8c:Monster"}]}
                """);
            var config = ReadJson<TournamentConfig>(file);
            var plan = PlanTournament(config);
            Assert.Equal(4, plan.Opponents["cpu"].Spec.Search!.Depth);
            Assert.Equal(8000, plan.Opponents["cpu"].Spec.PairBudget);
            Assert.Equal(1500, plan.Opponents["cpu"].Spec.TimeLimitMs);
            var changed = config.Participants[0] with { Search = config.Participants[0].Search! with { Risk = 0.5 } };
            var other = PlanTournament(config with { Participants = [changed, config.Participants[1]] });
            Assert.NotEqual(MakeManifest(plan).Signature, MakeManifest(other).Signature);
            Assert.Throws<ArgumentException>(() =>
                PlanTournament(config with { Participants = [changed, config.Participants[1] with { Search = new(Depth: 2) }] }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PlanTournament(config with { Participants = [changed with { Search = new(Depth: 7) }, config.Participants[1]] }));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void GeneratedAndExplicitMultiCastleOpeningsReplay()
    {
        var config = Config with { Maps = new() { Ids = ["banner-plain"] }, MaxRounds = 3 };
        var generated = PlanTournament(config with { Opening = new() { Mode = "fixed", Pairs = 2, Seed = 13 } });
        var draft = generated.Matches[0].Draft;
        Assert.Equal(2, draft.Length);
        var explicitPlan = PlanTournament(config with
        {
            Opening = new() { Mode = "fixed", Fixed = new() { ["banner-plain"] = draft } }
        });
        Assert.Equal(generated.Matches[0].Id, explicitPlan.Matches[0].Id);
        foreach (var match in explicitPlan.Matches)
        {
            var result = PlayTournamentMatch(explicitPlan, match, default);
            Assert.Equal("main", result.Opening);
            Assert.Equal(2, result.RedCastles);
            Assert.Equal(2, result.BlueCastles);
            Assert.Equal(MatchPhase.Main, Reconstruct(MakeManifest(explicitPlan), match, result).Phase);
        }
    }

    [Fact]
    public void RatingUsesFractionalDrawsAndFlagsMissingEvidence()
    {
        var ratings = FitRatings(["a", "b", "c"], [("a", "b", 1), ("a", "b", .5), ("a", "b", 0)]);
        Assert.Equal(1500, ratings[0].RelativeRating);
        Assert.Equal(1500, ratings[1].RelativeRating);
        Assert.Null(ratings[2].RelativeRating);
        Assert.Equal("insufficient evidence", ratings[0].Precision);
        Assert.Equal(3, ratings[0].Evidence);
        var separated = FitRatings(["a", "b"], [("a", "b", 1), ("a", "b", 1)]);
        Assert.True(separated[0].RelativeRating > 1500);
        Assert.True(separated[1].RelativeRating < 1500);
    }

    [Fact]
    public void SummarySeparatesScoredOpeningsAndDiscountsRepeatedHistories()
    {
        string directory = NewPath();
        try
        {
            var plan = PlanTournament(Config with { Opening = new(), SwapSeats = false, Seeds = [1, 2, 3] });
            ExecuteTournament(directory, plan, false, null, default, (_, match, _) =>
                new(match.Id, "completed", "draftscore", PlayerId.PlayerOne, 3, 1, 0, 2, 0, 1, 1, 1,
                    false, false, 0, 0, 0, 0, "same-physical-actions", []));
            using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "summary.json")));
            var data = summary.RootElement;
            Assert.Equal(1, data.GetProperty("distinctHistories").GetInt32());
            Assert.Equal(2, data.GetProperty("repeatedHistories").GetInt32());
            var alpha = data.GetProperty("rows").EnumerateArray().Single(row =>
                row.GetProperty("group").GetString() == "participant" && row.GetProperty("participant").GetString() == "alpha")
                .GetProperty("scores");
            Assert.Equal(3, alpha.GetProperty("scored").GetInt32());
            Assert.Equal(3, alpha.GetProperty("draftScores").GetInt32());
            Assert.Equal(0, alpha.GetProperty("mainMatches").GetInt32());
            Assert.Equal(2, alpha.GetProperty("meanMargin").GetDouble());
            Assert.Equal(1, data.GetProperty("ratings")[0].GetProperty("evidence").GetInt32());
            Assert.Equal(1, data.GetProperty("comparisons")[0].GetProperty("uniqueBlocks").GetInt32());
        }
        finally { Remove(directory); }
    }

    private static string NewPath() => Path.Combine(Path.GetTempPath(), "castrum-tournament-" + Guid.NewGuid().ToString("N"));
    private static void Remove(string path) { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
}
