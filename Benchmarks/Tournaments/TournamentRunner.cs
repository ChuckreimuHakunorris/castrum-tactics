using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using static CastrumTactics.Development.TournamentHistory;

using static Program;
using static CastrumTactics.Development.TournamentPlanner;

namespace CastrumTactics.Development;

internal static class TournamentRunner
{
    internal static TournamentResult PlayTournamentMatch(TournamentPlan plan, ScheduledMatch match, CancellationToken token)
    {
        var config = plan.Config;
        var definition = StudyBattlefields.Resolve(match.Map).Definition;
        var session = new LocalGameSession(definition, config.Ruleset);
        var red = plan.Opponents[match.SeatsSwapped ? match.Second : match.First];
        var blue = plan.Opponents[match.SeatsSwapped ? match.First : match.Second];
        var redRandom = new Random(match.SeatsSwapped ? match.SecondStream : match.FirstStream);
        var blueRandom = new Random(match.SeatsSwapped ? match.FirstStream : match.SecondStream);
        bool separatePhases = config.SeparatePhaseStreams || red.SeparatePhases || blue.SeparatePhases;
        var redDraftRandom = separatePhases ? new Random(StreamSeed(match.Seed, red.Spec.StreamId ?? red.Spec.Id, "opening-red")) : redRandom;
        var blueDraftRandom = separatePhases ? new Random(StreamSeed(match.Seed, blue.Spec.StreamId ?? blue.Spec.Id, "opening-blue")) : blueRandom;
        var rounds = new List<TournamentRound>();
        var audit = config.StrategicMetrics
            ? new StrategicPosition(session.Current.Board, RuleSetDefinition.Get(config.Ruleset), token) : null;
        var metrics = new TacticalAudit.TacticalMetrics[2];
        int draftIndex = 0, collisions = 0, forced = 0, redCastles = 0, blueCastles = 0;
        string opening = "unfinished", status = "completed";
        string? error = null, openingBoardDigest = null;
        int limit = config.MaxRounds ?? checked(8 * definition.Width * definition.Height);

        try
        {
            foreach (var opponent in new[] { red, blue })
                if (opponent.TablebaseMap is { } map && (map != match.Map || config.Ruleset != RuleSet.KnightSupport))
                    throw new InvalidOperationException("The tournament map or rules are outside the reference tablebase scope.");
            while (IsPlayable(session.Current.Phase))
            {
                token.ThrowIfCancellationRequested();
                if (rounds.Count - forced >= limit)
                {
                    status = "limit";
                    break;
                }
                var before = session.Current;
                bool draft = before.Phase == MatchPhase.Opening;
                DecisionSample redMove, blueMove;
                if (draft && config.Opening.Mode == "fixed")
                {
                    var position = before.EligiblePositions[0];
                    var (first, second) = draftIndex < match.Draft.Length ? ParsePair(match.Draft[draftIndex]) : (position, position);
                    // Seat and physical assignment swaps are independent. Swapping both restores the original color-to-coordinate pairing.
                    if (match.StartsSwapped ^ match.SeatsSwapped) (first, second) = (second, first);
                    redMove = new(Coordinate(first));
                    blueMove = new(Coordinate(second));
                }
                else
                {
                    redMove = TournamentMeasurement.Measure(red, before, Red, draft ? redDraftRandom : redRandom, config.Diagnostics, token);
                    blueMove = TournamentMeasurement.Measure(blue, before, Blue, draft ? blueDraftRandom : blueRandom, config.Diagnostics, token);
                }
                token.ThrowIfCancellationRequested();
                Play(session, redMove.Move, blueMove.Move, match.BlueSubmitsFirst);
                var after = session.Current;
                rounds.Add(new(draft ? "draft" : "main", draft ? before.ResolvedRounds + 1 : before.ResolvedMainTurns + 1,
                    redMove, blueMove, after.PlayerOneScore, after.PlayerTwoScore));
                if (draft)
                {
                    draftIndex++;
                    if (redMove.Move == blueMove.Move) collisions++;
                    if (after.ResolvedRounds == before.ResolvedRounds + 2)
                    {
                        var last = before.EligiblePositions.Single(p => p != Position(redMove.Move) && p != Position(blueMove.Move));
                        var move = new DecisionSample(Coordinate(last));
                        rounds.Add(new("auto-draft-collision", after.ResolvedRounds, move, move,
                            after.PlayerOneScore, after.PlayerTwoScore));
                        forced++;
                    }
                    if (after.Phase != MatchPhase.Opening)
                    {
                        opening = OpeningOutcome(after);
                        openingBoardDigest = OpeningDigest(after);
                        (redCastles, blueCastles) = CountCastles(after.Board);
                    }
                }
                else if (audit is not null)
                {
                    TacticalAudit.Record(audit, before.MainState!, after.MainState!, Red, redMove, blueMove, ref metrics[0], token);
                    TacticalAudit.Record(audit, before.MainState!, after.MainState!, Blue, blueMove, redMove, ref metrics[1], token);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            status = "failed";
            error = $"{exception.GetType().Name}: {exception.Message}";
        }
        var final = session.Current;
        return new(match.Id, status, opening, status == "completed" ? final.FinalResult?.Winner : null,
            final.PlayerOneScore, final.PlayerTwoScore, final.ResolvedMainTurns, final.ResolvedRounds,
            collisions, forced, redCastles, blueCastles, metrics[0].AvoidableLoss, metrics[1].AvoidableLoss,
            metrics[0].Interior, metrics[1].Interior, metrics[0].Placements, metrics[1].Placements,
            status == "completed" ? HistoryDigest(match.Map, config.Ruleset, rounds) : null, rounds, error)
        {
            OpeningBoardDigest = openingBoardDigest,
            RedInteriorWithBetterAlternative = audit is null ? null : metrics[0].BetterAlternative,
            BlueInteriorWithBetterAlternative = audit is null ? null : metrics[1].BetterAlternative
        };
    }

    internal static (int Red, int Blue) CountCastles(BoardState board)
    {
        int red = 0, blue = 0;
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            var cell = board.GetCell(new(x, y));
            if (cell.Piece != PieceType.Castle) continue;
            if (cell.Owner == Red) red++; else blue++;
        }
        return (red, blue);
    }
}
