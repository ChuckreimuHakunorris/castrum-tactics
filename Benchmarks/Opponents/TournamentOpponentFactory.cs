extern alias frozen;
extern alias frozen8c;
extern alias frozen8d2;
extern alias frozen8d3;
extern alias frozen8d4;
extern alias frozen8e;

using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Frozen = frozen::CastrumTactics.AI;
using EightE = frozen8e::CastrumTactics.AI;
using EightD4 = frozen8d4::CastrumTactics.AI;
using EightD3 = frozen8d3::CastrumTactics.AI;
using EightD2 = frozen8d2::CastrumTactics.AI;
using EightC = frozen8c::CastrumTactics.AI;

using static Program;
using static CastrumTactics.Development.TournamentPlanner;

namespace CastrumTactics.Development;

internal static class TournamentOpponentFactory
{
    internal static TournamentOpponent Create(ParticipantSpec spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Opponent);
        if (spec.MainOpponent is { } mainName)
        {
            var draft = Create(spec with { MainOpponent = null });
            var main = Create(new(spec.Id, mainName));
            if (draft.TablebaseMap is not null)
                throw new ArgumentException("A tablebase cannot supply a natural draft policy.");
            // Report both policy versions and separate phase streams so draft draws cannot shift Main randomness.
            return new(spec, $"{draft.Version}/{main.Version}", (state, side, random, diagnostics, token) =>
                (state.Phase == MatchPhase.Opening ? draft : main).Decide(state, side, random, diagnostics, token),
                main.TablebaseMap, SeparatePhases: true);
        }
        if (spec.TimeLimitMs is double time && (!double.IsFinite(time) || time <= 0 || time > 60_000))
            throw new ArgumentException($"'{spec.Id}' has an invalid time limit. Use a positive value up to 60000 ms.");
        string[] parts = spec.Opponent.Split(':');
        string kind = parts[0];
        if (kind != "strategic" && (spec.PairBudget is not null || spec.Weights is not null ||
            (kind is not ("frozen8d4" or "frozen8e") && spec.TimeLimitMs is not null) || spec.Search is not null ||
            spec.OpeningSearch is not null || spec.LegacyOpening is not null || spec.MainTemperature is not null))
            throw new ArgumentException($"'{spec.Id}' can only tune production strategic opponents.");
        if (kind == "solver")
        {
            if (parts.Length != 2 || spec.Tablebase is null)
                throw new ArgumentException("Solver opponents require solver:<tablebase-id> and a tablebase path.");
            Identifier(parts[1], "Tablebase ID");
            if (parts[1] != Path.GetFileNameWithoutExtension(spec.Tablebase))
                throw new ArgumentException("Tablebase ID must match its filename without extension.");
            var tablebase = LoadSolverTablebase(spec.Tablebase);
            return new(spec, tablebase.Digest, (state, player, random, diagnostics, token) =>
            {
                token.ThrowIfCancellationRequested();
                var move = tablebase.Decide(state, player, random, out var game);
                return new(Coordinate(move), Diagnostics: diagnostics ? new
                {
                    game.IsExact, redValueLower = game.Lower.ToString(), redValueUpper = game.Upper.ToString(),
                    optimalityGapBound = (game.Upper - game.Lower).ToString()
                } : null);
            }, tablebase.Info.Map);
        }
        if (spec.Tablebase is not null)
            throw new ArgumentException("Only solver opponents accept a tablebase path.");
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
                if (spec.LegacyOpening == true && spec.OpeningSearch is not null)
                    throw new ArgumentException("Legacy opening and advanced draft options are mutually exclusive.");
                var defaults = CpuDifficultyProfiles.Get(level);
                var search = defaults.Search!;
                var overrides = spec.Search;
                int depth = overrides?.Depth ?? search.Depth;
                var equilibrium = overrides is null ? search.Equilibrium : overrides.Equilibrium ?? MatrixPolicy.Off;
                bool proofOnly = overrides?.ProofOnlyDeepening ?? search.ProofOnlyDeepening;
                if (equilibrium != MatrixPolicy.Off && depth > 2 && !proofOnly)
                    throw new ArgumentException("Equilibrium search beyond two turns requires proof-only deepening.");
                var profile = new CpuProfile(spec.Weights ?? defaults.Weights, defaults.OpeningCandidateLimit,
                    defaults.MainCandidateLimit, spec.MainTemperature ?? defaults.MainTemperature, defaults.OpeningTemperature,
                    defaults.OpeningDiversity, defaults.UrgentOpeningDiversity, defaults.OpeningMaximumScoreDifference,
                    defaults.MainMaximumScoreDifference)
                {
                    OpeningSearch = spec.LegacyOpening == true ? null : spec.OpeningSearch ?? defaults.OpeningSearch,
                    Search = new(depth, overrides?.OwnCandidates ?? search.OwnCandidates,
                        overrides?.OpponentCandidates ?? search.OpponentCandidates, spec.PairBudget ?? search.PairBudget,
                        overrides?.Risk ?? search.Risk, overrides?.ReplyTemperature ?? search.ReplyTemperature,
                        overrides?.ContinuationOwnCandidates ?? search.ContinuationOwnCandidates,
                        overrides?.ContinuationOpponentCandidates ?? search.ContinuationOpponentCandidates,
                        overrides?.DeepFinalists ?? search.DeepFinalists,
                        overrides?.VerifyTerminalWins ?? (level != CpuDifficulty.Monster ||
                            overrides is null && search.VerifyTerminalWins),
                        overrides?.RequireFullRootReplies ?? search.RequireFullRootReplies,
                        proofOnly)
                    {
                        ResilientEvaluation = overrides is null ? search.ResilientEvaluation : overrides.ResilientEvaluation ?? false,
                        BoundedUtility = overrides is null ? search.BoundedUtility : overrides.BoundedUtility ?? false,
                        Equilibrium = equilibrium
                    }
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
            case "frozen8d2":
                var frozenD2Engine = new EightD2.CpuDecisionEngine();
                var frozenD2Profile = EightD2.CpuDifficultyProfiles.Get((EightD2.CpuDifficulty)level);
                return new(spec, "9ef5ad05409efdc7d00cf112bd5b65cdc7ba5474",
                    (state, player, random, diagnostics, token) =>
                    {
                        var decision = frozenD2Engine.DecideWithDiagnostics(state, player, frozenD2Profile, random, token);
                        var report = decision.Search;
                        return new(Coordinate(decision.Decision!.Value.Position), Pairs: report?.ResolvedPairs,
                            Depth: report?.Depth, BudgetExhausted: report?.BudgetExhausted,
                            TimeLimitReached: report?.TimeLimitReached, Fallback: report is null ? null : report.Depth == 0,
                            Diagnostics: diagnostics ? decision : null, ReusedPairs: report?.ReusedPairs, CompletedDepth: report?.CompletedDepth);
                    });
            case "frozen8d3":
                var frozenD3Engine = new EightD3.CpuDecisionEngine();
                var frozenD3Profile = EightD3.CpuDifficultyProfiles.Get((EightD3.CpuDifficulty)level);
                return new(spec, "b4c77ea7553486f6043cd60822f46f9b75dd42fa",
                    (state, player, random, diagnostics, token) =>
                    {
                        var decision = frozenD3Engine.DecideWithDiagnostics(state, player, frozenD3Profile, random, token);
                        var report = decision.Search;
                        return new(Coordinate(decision.Decision!.Value.Position), Pairs: report?.ResolvedPairs,
                            Depth: report?.Depth, BudgetExhausted: report?.BudgetExhausted,
                            TimeLimitReached: report?.TimeLimitReached, Fallback: report is null ? null : report.Depth == 0,
                            Diagnostics: diagnostics ? decision : null, ReusedPairs: report?.ReusedPairs, CompletedDepth: report?.CompletedDepth);
                    });
            case "frozen8d4":
                var frozenD4Engine = spec.TimeLimitMs is double frozenTime
                    ? new EightD4.CpuDecisionEngine(TimeSpan.FromMilliseconds(frozenTime)) : new EightD4.CpuDecisionEngine();
                var frozenD4Profile = EightD4.CpuDifficultyProfiles.Get((EightD4.CpuDifficulty)level);
                return new(spec, "7ec9a71c0f7a260c013fb08468db76af156692f3",
                    (state, player, random, diagnostics, token) =>
                    {
                        var decision = frozenD4Engine.DecideWithDiagnostics(state, player, frozenD4Profile, random, token);
                        var report = decision.Search;
                        return new(Coordinate(decision.Decision!.Value.Position), Pairs: report?.ResolvedPairs,
                            Depth: report?.Depth, BudgetExhausted: report?.BudgetExhausted,
                            TimeLimitReached: report?.TimeLimitReached, Fallback: report is null ? null : report.Depth == 0,
                            Diagnostics: diagnostics ? decision : null, ReusedPairs: report?.ReusedPairs, CompletedDepth: report?.CompletedDepth);
                    });
            case "frozen8e":
                var frozen8EEngine = spec.TimeLimitMs is double frozen8ETime
                    ? new EightE.CpuDecisionEngine(TimeSpan.FromMilliseconds(frozen8ETime)) : new EightE.CpuDecisionEngine();
                var frozen8EProfile = EightE.CpuDifficultyProfiles.Get((EightE.CpuDifficulty)level);
                return new(spec, "b63e7fbbb5450b4f9a5b6bb4047579bc978669c5",
                    (state, player, random, diagnostics, token) =>
                    {
                        var decision = frozen8EEngine.DecideWithDiagnostics(state, player, frozen8EProfile, random, token);
                        var report = decision.Search;
                        return new(Coordinate(decision.Decision!.Value.Position), Pairs: report?.ResolvedPairs,
                            Depth: report?.Depth, BudgetExhausted: report?.BudgetExhausted,
                            TimeLimitReached: report?.TimeLimitReached, Fallback: report is null ? null : report.Depth == 0,
                            Diagnostics: diagnostics ? decision : null, ReusedPairs: report?.ReusedPairs, CompletedDepth: report?.CompletedDepth);
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
}
