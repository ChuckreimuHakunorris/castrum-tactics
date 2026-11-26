using CastrumTactics.Development;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using static CastrumTactics.Development.TournamentPlanner;
using static CastrumTactics.Development.TournamentStore;

internal static partial class Program
{
    private sealed record Continuation(int Result, int Margin, int Turns, int? RecoveryTurn);

    private static int RunActionSensitivity(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: action-sensitivity <tournament-dir> <output.jsonl>");
        var manifest = ReadJson<Manifest>(ManifestPath(args[0]));
        var states = CheckResults(args[0], manifest);
        var plan = PlanTournament(manifest.Config);
        string output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var log = new StreamWriter(output, append: false) { AutoFlush = true };
        foreach (var games in manifest.Matches.Where(m => m.First == "monster" && m.Second == "hard")
            .GroupBy(m => m.Map))
        {
            // One seed block per layout, with all seat and physical assignment variants.
            foreach (var match in games.Where(m => m.Seed == games.Min(g => g.Seed)))
            {
                if (states.GetValueOrDefault(match.Id) != "completed") continue;
                var result = LoadResult(args[0], match.Id);
                if (result.Opening != "main") continue;
                var red = plan.Opponents[match.SeatsSwapped ? match.Second : match.First];
                var blue = plan.Opponents[match.SeatsSwapped ? match.First : match.Second];
                var stronger = match.SeatsSwapped ? Blue : Red;
                var turns = result.Rounds.Where(r => r.Phase == "main").ToArray();
                foreach (int index in new[] { 0, Math.Min(2, turns.Length - 1) }.Distinct())
                {
                    var round = turns[index];
                    var before = TournamentReplay.Reconstruct(manifest, match, result, "main", round.Number).MainState!;
                    var action = Position(stronger == Red ? round.Red.Move : round.Blue.Move);
                    var reply = Position(stronger == Red ? round.Blue.Move : round.Red.Move);
                    var alternatives = before.GetLegalSelections(stronger).Where(p => p != action)
                        .OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
                    if (alternatives.Length == 0) continue;
                    // Choose one reproducible legal alternative while holding the realized reply fixed.
                    int alternativeIndex = StreamSeed(match.Seed, match.Id, $"action-{round.Number}") % alternatives.Length;
                    var alternate = alternatives[alternativeIndex];
                    int sign = stronger == Red ? 1 : -1;
                    string redMove = round.Red.Move, blueMove = round.Blue.Move;
                    string altRed = stronger == Red ? Coordinate(alternate) : redMove;
                    string altBlue = stronger == Blue ? Coordinate(alternate) : blueMove;
                    var selected = before.Advance(new(Red, Position(redMove)), new(Blue, Position(blueMove)));
                    var changed = before.Advance(new(Red, Position(altRed)), new(Blue, Position(altBlue)));
                    int localReference = sign * (selected.Territory.PlayerOneScore - selected.Territory.PlayerTwoScore);
                    int localAlternate = sign * (changed.Territory.PlayerOneScore - changed.Territory.PlayerTwoScore);
                    var map = manifest.Maps.Single(m => m.Id == match.Map);
                    int playable = map.Width * map.Height - map.Rocks;

                    foreach (int seed in new[] { 5839, 5987, 6151 })
                    {
                        var control = Continue(redMove, blueMove);
                        var treatment = Continue(altRed, altBlue);
                        log.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                        {
                            match.Map, match.Id, round.Number, seed, playable, action = Coordinate(action), reply = Coordinate(reply),
                            alternate = Coordinate(alternate), localReference, localAlternate, control, treatment
                        }, Json));

                        Continuation Continue(string first, string second)
                        {
                            var session = new LocalGameSession(StudyBattlefields.Resolve(match.Map).Definition, manifest.Config.Ruleset);
                            foreach (var prefix in result.Rounds.TakeWhile(r => r != round))
                                if (prefix.Phase != "auto-draft-collision") Play(session, prefix.Red.Move, prefix.Blue.Move, match.BlueSubmitsFirst);
                            Play(session, first, second, match.BlueSubmitsFirst);
                            int? recovery = null;
                            while (true)
                            {
                                var state = session.Current;
                                int margin = sign * (state.PlayerOneScore - state.PlayerTwoScore);
                                if (margin >= 0) recovery ??= state.ResolvedMainTurns;
                                if (state.Phase == MatchPhase.Finished)
                                    return new(state.FinalResult?.Winner is { } winner ? winner == stronger ? 1 : -1 : 0,
                                        margin, state.ResolvedMainTurns, recovery);
                                int turn = state.ResolvedMainTurns + 1;
                                if (turn > 8 * state.Board.Width * state.Board.Height)
                                    throw new InvalidOperationException($"Continuation exceeded the round limit in {match.Id}.");
                                string a = red.Decide(state, Red, new Random(StreamSeed(seed, "red", turn.ToString(System.Globalization.CultureInfo.InvariantCulture))), false, default).Move;
                                string b = blue.Decide(state, Blue, new Random(StreamSeed(seed, "blue", turn.ToString(System.Globalization.CultureInfo.InvariantCulture))), false, default).Move;
                                Play(session, a, b, match.BlueSubmitsFirst);
                            }
                        }
                    }
                }
            }
        }
        return 0;
    }
}
