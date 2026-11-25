extern alias frozen;
extern alias frozen8c;

using System.Diagnostics;
using CastrumTactics.AI;
using CastrumTactics.Development;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Frozen = frozen::CastrumTactics.AI;
using EightC = frozen8c::CastrumTactics.AI;

internal static partial class Program
{
    internal delegate DecisionSample TournamentDecision(GameSessionSnapshot state, PlayerId player, Random random,
        bool diagnostics, CancellationToken token);

    internal sealed record TournamentOpponent(ParticipantSpec Spec, string Version, TournamentDecision Decide);
    internal sealed record DecisionSample(string Move, double Milliseconds = 0, long AllocatedBytes = 0,
        int? Pairs = null, int? Depth = null, bool? BudgetExhausted = null, bool? TimeLimitReached = null,
        bool? Fallback = null, object? Diagnostics = null, bool Measured = false, int? ReusedPairs = null,
        int? CompletedDepth = null);
    internal sealed record TournamentRound(string Phase, int Number, DecisionSample Red, DecisionSample Blue,
        int RedScore, int BlueScore);
    internal sealed record TournamentResult(string Id, string Status, string Opening, PlayerId? Winner,
        int RedScore, int BlueScore, int MainTurns, int DraftRounds, int Collisions, int ForcedCollisions,
        int RedCastles, int BlueCastles, bool RedAvoidableLoss, bool BlueAvoidableLoss, int RedInterior,
        int BlueInterior, int RedPlacements, int BluePlacements, string? HistoryDigest, List<TournamentRound> Rounds,
        string? Error = null);

    private static TournamentOpponent CreateOpponent(ParticipantSpec spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Opponent);
        if (spec.TimeLimitMs is double time && (!double.IsFinite(time) || time <= 0 || time > 60_000))
            throw new ArgumentException($"'{spec.Id}' has an invalid time limit. Use a positive value up to 60000 ms.");
        string[] parts = spec.Opponent.Split(':');
        string kind = parts[0];
        if (kind != "strategic" && (spec.PairBudget is not null || spec.Weights is not null ||
            spec.TimeLimitMs is not null || spec.Search is not null))
            throw new ArgumentException($"'{spec.Id}' can only tune production strategic opponents.");
        if (spec.Opponent == "probe")
        {
            return new(spec, "archived-one-turn-probe", (state, player, random, diagnostics, token) =>
            {
                if (state.Phase != MatchPhase.Main)
                    return StaticDecision(state, player, random, diagnostics, token, Frozen.CpuDifficulty.Normal);
                var move = ArchivedProbe.Probe(state, player, random, out int pairs);
                return new(Coordinate(move), Pairs: pairs, Depth: 1);
            });
        }
        if (parts.Length != 2 || !Enum.TryParse(parts[1], true, out CpuDifficulty level) ||
            !Enum.IsDefined(level) || !level.ToString().Equals(parts[1], StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unknown opponent '{spec.Opponent}'.");
        switch (kind)
        {
            case "strategic":
                var defaults = CpuDifficultyProfiles.Get(level);
                var search = defaults.Search!;
                var overrides = spec.Search;
                var profile = new CpuProfile(spec.Weights ?? defaults.Weights, defaults.OpeningCandidateLimit,
                    defaults.MainCandidateLimit, defaults.MainTemperature, defaults.OpeningTemperature,
                    defaults.OpeningDiversity, defaults.UrgentOpeningDiversity, defaults.OpeningMaximumScoreDifference,
                    defaults.MainMaximumScoreDifference)
                {
                    Search = new(overrides?.Depth ?? search.Depth, overrides?.OwnCandidates ?? search.OwnCandidates,
                        overrides?.OpponentCandidates ?? search.OpponentCandidates, spec.PairBudget ?? search.PairBudget,
                        overrides?.Risk ?? search.Risk, overrides?.ReplyTemperature ?? search.ReplyTemperature,
                        overrides?.ContinuationOwnCandidates ?? search.ContinuationOwnCandidates,
                        overrides?.ContinuationOpponentCandidates ?? search.ContinuationOpponentCandidates,
                        overrides?.DeepFinalists ?? search.DeepFinalists,
                        overrides?.VerifyTerminalWins ?? search.VerifyTerminalWins,
                        overrides?.RequireFullRootReplies ?? search.RequireFullRootReplies,
                        overrides?.ProofOnlyDeepening ?? search.ProofOnlyDeepening)
                };
                var engine = spec.TimeLimitMs is double milliseconds
                    ? new CpuDecisionEngine(TimeSpan.FromMilliseconds(milliseconds)) : Engine;
                return new(spec, "production", (state, player, random, diagnostics, token) =>
                {
                    var decision = engine.DecideWithDiagnostics(state, player, profile, random, token);
                    var report = decision.Search;
                    return new(Coordinate(decision.Decision!.Value.Position), Pairs: report?.ResolvedPairs,
                        Depth: report?.Depth, BudgetExhausted: report?.BudgetExhausted,
                        TimeLimitReached: report?.TimeLimitReached, Fallback: report is null ? null : report.Depth == 0,
                        Diagnostics: diagnostics ? decision : null, ReusedPairs: report?.ReusedPairs,
                        CompletedDepth: report?.CompletedDepth);
                });
            case "frozen8c":
                var frozenEngine = new EightC.CpuDecisionEngine();
                var frozenProfile = EightC.CpuDifficultyProfiles.Get((EightC.CpuDifficulty)level);
                return new(spec, "6d5c1355e1f119a42feb5f2e7f848bc63d66b145",
                    (state, player, random, diagnostics, token) =>
                    {
                        var decision = frozenEngine.DecideWithDiagnostics(state, player, frozenProfile, random, token);
                        var report = decision.Search;
                        return new(Coordinate(decision.Decision!.Value.Position), Pairs: report?.ResolvedPairs,
                            Depth: report?.Depth, BudgetExhausted: report?.BudgetExhausted,
                            TimeLimitReached: report?.TimeLimitReached, Fallback: report is null ? null : report.Depth == 0,
                            Diagnostics: diagnostics ? decision : null);
                    });
            case "static":
                return new(spec, "afb0dcbab5b789ed8820017c84648ed4dd3bbe14",
                    (state, player, random, diagnostics, token) => StaticDecision(state, player, random, diagnostics, token,
                        (Frozen.CpuDifficulty)level));
            default:
                throw new ArgumentException($"Unknown opponent '{spec.Opponent}'.");
        }

        static DecisionSample StaticDecision(GameSessionSnapshot state, PlayerId player, Random random,
            bool diagnostics, CancellationToken token, Frozen.CpuDifficulty difficulty)
        {
            var profile = Frozen.CpuDifficultyProfiles.Get(difficulty);
            if (!diagnostics)
                return new(Coordinate(Baseline.Decide(state, player, profile, random, token)!.Value.Position), Pairs: 0, Depth: 0);
            var report = Baseline.DecideWithDiagnostics(state, player, profile, random, token);
            return new(Coordinate(report.Decision!.Value.Position), Pairs: 0, Depth: 0, Diagnostics: report);
        }
    }

    private static DecisionSample Measure(TournamentOpponent opponent, GameSessionSnapshot state, PlayerId player,
        Random stream, bool diagnostics, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        var decision = opponent.Decide(state, player, stream, diagnostics, token);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return decision with { Measured = true, Milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            AllocatedBytes = bytes };
    }

    internal static TournamentResult PlayTournamentMatch(TournamentPlan plan, ScheduledMatch match, CancellationToken token)
    {
        var config = plan.Config;
        var definition = HistoricalBattlefields.Resolve(match.Map).Definition;
        var session = new LocalGameSession(definition, config.Ruleset);
        var red = plan.Opponents[match.SeatsSwapped ? match.Second : match.First];
        var blue = plan.Opponents[match.SeatsSwapped ? match.First : match.Second];
        var redRandom = new Random(match.SeatsSwapped ? match.SecondStream : match.FirstStream);
        var blueRandom = new Random(match.SeatsSwapped ? match.FirstStream : match.SecondStream);
        var rounds = new List<TournamentRound>();
        var audit = config.StrategicMetrics
            ? new StrategicPosition(session.Current.Board, RuleSetDefinition.Get(config.Ruleset), token) : null;
        var metrics = new TacticalMetrics[2];
        int draftIndex = 0, collisions = 0, forced = 0, redCastles = 0, blueCastles = 0;
        string opening = "unfinished", status = "completed";
        string? error = null;
        int limit = config.MaxRounds ?? checked(8 * definition.Width * definition.Height);

        try
        {
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
                    if (match.StartsSwapped ^ match.SeatsSwapped) (first, second) = (second, first);
                    redMove = new(Coordinate(first));
                    blueMove = new(Coordinate(second));
                }
                else
                {
                    redMove = Measure(red, before, Red, redRandom, config.Diagnostics, token);
                    blueMove = Measure(blue, before, Blue, blueRandom, config.Diagnostics, token);
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
                        (redCastles, blueCastles) = CountCastles(after.Board);
                    }
                }
                else if (audit is not null)
                {
                    Audit(audit, before.MainState!, after.MainState!, Red, redMove, blueMove, ref metrics[0], token);
                    Audit(audit, before.MainState!, after.MainState!, Blue, blueMove, redMove, ref metrics[1], token);
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
            status == "completed" ? HistoryDigest(match.Map, config.Ruleset, rounds) : null, rounds, error);
    }

    private static (int Red, int Blue) CountCastles(BoardState board)
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

    private static void Audit(StrategicPosition position, MainPhaseState before, MainPhaseState after, PlayerId side,
        DecisionSample decision, DecisionSample opposing, ref TacticalMetrics metrics, CancellationToken token)
    {
        var action = Position(decision.Move);
        if (before.Board.GetCell(action).Piece == PieceType.None)
        {
            metrics.Placements++;
            if (IsInteriorPlacement(position, before, after, side, action, token)) metrics.Interior++;
        }
        if (after.Result?.Winner != Other(side)) return;
        var reply = new MainPhaseSelection(Other(side), Position(opposing.Move));
        foreach (var alternative in before.GetLegalSelections(side))
        {
            token.ThrowIfCancellationRequested();
            if (alternative != action && before.Advance(new(side, alternative), reply).Result?.Winner != Other(side))
            {
                metrics.AvoidableLoss = true;
                break;
            }
        }
    }

    private struct TacticalMetrics
    {
        internal int Interior, Placements;
        internal bool AvoidableLoss;
    }

    private static bool IsPlayable(MatchPhase phase) => phase is MatchPhase.Opening or MatchPhase.Main;
    private static string OpeningOutcome(GameSessionSnapshot state) => state.Phase switch
    {
        MatchPhase.Opening => "unfinished",
        MatchPhase.OpeningRestartRequired => "retry",
        MatchPhase.Finished when state.ResolvedMainTurns == 0 => "draftscore",
        _ => "main"
    };

    private static string HistoryDigest(string map, RuleSet rules, IEnumerable<TournamentRound> rounds) =>
        Digest($"{map}|{rules}|{string.Join(';', rounds.Select(round => $"{round.Phase}|{round.Red.Move}/{round.Blue.Move}"))}");
}
