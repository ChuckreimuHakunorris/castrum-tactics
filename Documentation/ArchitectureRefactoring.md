# Milestone 9A architecture refactoring

The baseline is integrated 8E at `b63e7fbbb5450b4f9a5b6bb4047579bc978669c5` (PR #23). The feature branch is `codex/m9a-architecture-refactoring`. Initial verification passed the warnings-as-errors build, all 361 tests and both Godot presentation regressions. Linux builds used Debian 13.7, .NET SDK 10.0.404 and runtime 10.0.15, and Godot 4.7.2 .NET. The solution and presentation checks used Debug. The benchmark comparisons used Release. The application defaults to Knight Support, Versus CPU and Normal. Classic remains available through the existing paths.

## Ownership changes

| Area | 8E location | 9A responsibility |
| --- | --- | --- |
| Draft transitions | `LocalGameSession.ResolveRound` and `GameSessionSnapshot.PreviewDraft` | `OpeningDraft.ResolveTransition` calculates the next draft, evaluates completed openings and counts resolved rounds. The session retains validation, pending submissions and publication. |
| CPU decisions | `AI/CpuDecisionEngine.cs` | `TacticalCandidates.cs` owns feature scoring and tactical order. `CandidateSampling.cs` owns cutoffs, ties and both samplers. The engine still selects the phase path and returns the round-tagged legal proposal. |
| Legacy draft search | Mixed into `SimultaneousSearch.Analyze` | `LegacyDraftSearch.cs` has the legacy entry point and preview loop. Main has an explicit entry point in `SimultaneousSearch.cs`. They share completed results, reply weights and accounting within the decision. Advanced openings still run through `OpeningSearch`. |
| Match lifecycle | `Scenes/Main.cs` | `Match/MatchLifetime.cs` owns construction, CPU lifetime and observer detachment. Main binds nodes, publishes captions, attaches its observer and displays the first snapshot in the existing order. |
| New Match picker | `NewMatchDialog.Initialize` | Separate list and preview construction methods preserve the same nodes and signals. `MatchCaptions` owns the difficulty display name used by both scene and picker. |
| Benchmarks | Partial `Program` and `TournamentMatch.cs` | `Program.cs` dispatches. `LegacyMatchCommands.cs` retains legacy command behavior. `TournamentOpponentFactory`, `TournamentPlanner`, `TournamentRunner`, `TournamentReplay`, `TournamentHistory`, `TournamentStore`, `TournamentMeasurement`, and `TacticalAudit` own their respective operations. The shared serialized contracts are in `Tournaments/TournamentContracts.cs`. |

The Main and legacy draft paths remain in one internal partial search type because they use the same row rating and completed-tier assembly. The advanced opening path has a different charging and interruption model. Keeping its accounting local avoids changing what counts as a pair, when cancellation is checked, and when a completed matrix can be selected. The strategic and numerical evaluators retain their existing data structures and arithmetic order. The exact solver remains part of Benchmarks. Research commands with established contracts retain their existing study-specific modules.

`BoardRenderer` retains ownership of pointer/touch selection and its drawing layers because selection changes affect indicator rendering and both use `BoardGeometry`. `MatchLayout` and `StableInstructionLayout` already separate layout calculations from scene updates. The scene continues to adapt those calculations to the controls it owns. The picker remains built in code because its list is generated from the catalog and the two construction methods express the existing hierarchy without adding a scene resource.

## Equivalence evidence

The 15 frozen C# files in `Benchmarks/Frozen8E` match the integrated AI sources byte for byte. The assembly is a benchmark and test dependency and uses the shared Domain. The `frozen8e:<difficulty>` adapter and the manifest record its provenance. In tests, all five difficulties use the same seed on both seats, both rulesets, all 100 production maps and four compact historical maps. They compare the complete decision and diagnostics payload after setting elapsed fields to zero, plus the RNG state following selection. The cases include draft collisions, successful and failed openings, restarts, nonplayable snapshots and early, middle and late Main positions. Search widths and thresholds remain the 8E production settings, including Monster's 850/256 pair limits, Very Hard's resilient evaluation at 500 pairs and the scene's 450 ms guard. [CPU architecture](CpuArchitecture.md)

The domain tests independently pin full public snapshot histories for one seeded match per ruleset on Honeystone Green. Those hashes include terrain, ground health, pieces, owners, territory, eligible and legal sets, counters, flags, scores and outcomes. Live/preview characterization also checks both submission orders, collision accounting, forced final collisions, restart, immediate completion on a divided custom map and snapshots retained across later mutations.

The baseline and current CLI agree on **204 complete histories** and **12,688 recorded rounds**, including 60 natural and 120 fixed Knight Support games and 12 Classic games per opening mode. Schedules and normalized result payloads matched. The schedules use three production maps and all five difficulties. Both modes swap seats and separate phase streams. Fixed mode drafts two castle pairs and independently swaps physical starts. Dedicated timing fields are excluded from exact parity. The checked-in `Benchmarks/Reference/refactoring-9a.json` records schedule, result, board-history and source digests. CLI smoke checks cover dry-run, report, inspection, reading 8E shards, pausing after four matches, resuming with the same build, historical replay, the map audit, a six-match opening study and a compact Corner Watch solver run. The audit and opening study output matched byte for byte. All 30 legacy fixture records matched after excluding timing, and certified solver output matched after excluding process measurements. The binary-fingerprint protection still rejects resuming with a different build. Start a fresh output directory to run the same plan on 9A. Existing shards remain available for report, inspect and replay. [CPU tournaments](CpuTournaments.md)

## Verification

The warnings-as-errors solution and Release benchmark builds, **369 xUnit tests**, both Godot headless regressions, and DocFX metadata and site checks passed. The Godot regressions exercised Classic and Knight Support, both local input modes, restarts, map picker, resize and the score banner. The seeded Classic CPU walkthrough ended 11–12 after 30 Main turns in both builds.

Windows x64 and Android ARM64 debug exports both succeeded. The Windows package contains an x64 PE executable, pack and managed sidecar assemblies. Android signature and 16 KiB ZIP alignment checks passed, and the package remains `com.castrumtactics.prototype`. The Linux host had no native Windows runtime or connected Android phone. Windows launch, Android install and launch, and physical timings remain outstanding. Godot emitted its existing Android settings teardown diagnostic with successful editor exit.

### Warmed performance

The same Release builds ran sequentially on one Cloud host (AMD EPYC 9V74, four CPUs of scheduler quota) with the same nine saved snapshots per phase, RNG seed and 450 ms guard. Each build ran twice in 8E, 9A, 9A, 8E order with 432 warmed observations per phase per run. These observations repeat the same positions and estimate runtime variability on this host.

| Phase | 8E median ms / P95 ms / KiB per decision | 9A median ms / P95 ms / KiB per decision |
| --- | --- | --- |
| Main | 0.59–0.82 / 6.53–27.46 / 403.17–403.46 | 0.68–1.32 / 6.96–25.24 / 402.75–403.28 |
| Draft | 1.13–1.13 / 13.98–14.56 / 159.03–159.04 | 1.14–1.14 / 14.90–15.19 / 159.03–159.04 |

Ranges span the two runs. Allocations were effectively unchanged. Main timing varied substantially between repeats, so these measurements cannot isolate a small latency change. A sequential 120-match fixed tournament took 35.60 s on 8E and 35.46 s on 9A. Both resolved exactly 1,901,708 search pairs across 11,328 measured Main decisions. This run includes match orchestration and reporting. The raw snapshot runs time only decision execution. Renderer geometry and drawing were left in place and Linux headless checks provide layout evidence. Native frame timing remains unmeasured.

## Reproduction

From a checkout of 9A, use an independent worktree at the pinned 8E revision. Build both benchmark projects in Release. Keep full output in ignored `exports/9a`.

```sh
dotnet build CastrumTactics.sln -warnaserror
dotnet test CastrumTactics.sln --no-build
dotnet build Benchmarks -c Release -warnaserror
git worktree add --detach ../castrum-8e b63e7fbbb5450b4f9a5b6bb4047579bc978669c5
dotnet build ../castrum-8e/Benchmarks -c Release -warnaserror
base=../castrum-8e/Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll
bench=Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll
for mode in natural fixed; do
  dotnet "$base" tournament run --config "Benchmarks/Examples/9a/$mode.json" --out "exports/9a/baseline-$mode"
  dotnet "$bench" tournament run --config "Benchmarks/Examples/9a/$mode.json" --out "exports/9a/current-$mode"
  python3 Benchmarks/Examples/9a/compare.py "exports/9a/baseline-$mode" "exports/9a/current-$mode"
  dotnet "$base" tournament run --config "Benchmarks/Examples/9a/$mode.json" --id "9a-classic-$mode" --ruleset classic --max-matches 12 --out "exports/9a/baseline-classic-$mode"
  dotnet "$bench" tournament run --config "Benchmarks/Examples/9a/$mode.json" --id "9a-classic-$mode" --ruleset classic --max-matches 12 --out "exports/9a/current-classic-$mode"
  python3 Benchmarks/Examples/9a/compare.py "exports/9a/baseline-classic-$mode" "exports/9a/current-classic-$mode"
done
for phase in draft main; do
  dotnet "$base" snapshot-performance exports/9a/baseline-natural strategic "$phase" 48 450 "exports/9a/baseline-$phase.jsonl"
  dotnet "$bench" snapshot-performance exports/9a/baseline-natural strategic "$phase" 48 450 "exports/9a/current-$phase.jsonl"
done
godot --headless --path . --import
godot --headless --path . --script Tests/Presentation/DraftLayoutCheck.gd
godot --headless --path . --script Tests/Presentation/CpuMatchCheck.gd -- --seed 42
dotnet tool restore
dotnet docfx metadata Documentation/docfx.json --warningsAsErrors
dotnet docfx build Documentation/docfx.json --warningsAsErrors
```

For 9B, inventory public API docs and complex comments against the resulting ownership, including snapshot and solver invariants. For 9C, consider isolated hardware timing, a pinned SDK and export workflow, and physical Android and Windows smoke tests. Those follow-ups belong in separate changes.
