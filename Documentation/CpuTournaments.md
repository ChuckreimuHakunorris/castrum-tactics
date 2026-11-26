# CPU tournaments

The development-only .NET 10 harness plays CPU opponents through the same `LocalGameSession` used by the application. It plans matches before starting workers. Each worker owns its session and random streams, and both opponents decide from the same published snapshot before either command is submitted. Benchmarks use the authoritative resolver for both Knight Support and Classic. Knight Support is the default.

## Quick start

Run from the repository root. The following examples use PowerShell.

```powershell
dotnet build Benchmarks/CastrumTactics.Benchmarks.csproj -c Release
$bench = 'Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll'
dotnet $bench tournament dry-run --config Benchmarks/Examples/round-robin.json
dotnet $bench tournament run --config Benchmarks/Examples/round-robin.json
dotnet $bench tournament run --config Benchmarks/Examples/baseline.json
dotnet $bench tournament dry-run --config Benchmarks/Examples/budget-comparison.json
dotnet $bench tournament report --out exports/8d1-final/five
dotnet $bench tournament performance --id monster-latency --format head-to-head --participants 'live=strategic:Monster,baseline=frozen8c:Monster' --maps twin-basins --opening fixed --out exports/monster-latency
```

The JSON examples cover a five-difficulty round robin, explicit fixed drafts against frozen controls, and two budgets for the same strategic CPU with sampled tuning maps and two generated castle pairs. CLI options override their corresponding JSON settings. Boolean CLI options take `true` or `false`.

## Participants and configuration

Use `frozen8d2:<difficulty>` for the immutable 8D.2 policy. A live `strategic` participant without a `search` object uses the built-in settings. An explicit `search` object uses the 8D.2 evaluator and sampler by default, with independent overrides for `resilientEvaluation`, `boundedUtility` and `equilibrium` (`off`, `expected`, `root` or `continuations`). Matrix experiments beyond two turns require proof-only deepening. With strategic metrics enabled, the match record also counts historic interior placements for which a legal alternative improved immediate audited terms against the realized reply. Dedicated performance runs record process collection counts and peak working set in `process-gc.json`. [8D.3 configurations and reproduction](CpuStrategicEvaluation.md)

| Opponent ID | Implementation and provenance |
| --- | --- |
| `strategic:<difficulty>` | Current production policy and profile. Optional `pairBudget`, `weights` (a complete [CpuEvaluationWeights](xref:CastrumTactics.AI.CpuEvaluationWeights) object), `timeLimitMs`, and `search` overrides apply only to that participant. |
| `frozen8d4:<difficulty>` | The integrated 8D.4 AI from `7ec9a71c0f7a260c013fb08468db76af156692f3`, including opening and Main policies. Accepts an optional `timeLimitMs` for controlled timed runs. |
| `frozen8c:<difficulty>` | Eight original AI sources from `6d5c1355e1f119a42feb5f2e7f848bc63d66b145`, the corrected 8C draft collision implementation merged in `1682d48`. `Benchmarks/Frozen8C` builds a separate assembly, including the evaluator, search options, and all difficulty profiles. |
| `static:<difficulty>` | The original five static AI sources from `afb0dcbab5b789ed8820017c84648ed4dd3bbe14` in `Benchmarks/Frozen`. |
| `probe` | Dot's frozen One-Turn Probe from the original Knight Support benchmark archive for `afb0dcbab5b789ed8820017c84648ed4dd3bbe14`. It uses frozen Normal's shortlist and plays frozen Normal during the draft. |

Set `separatePhaseStreams` to true to keep Main draws independent of the draft even when neither participant uses phase composition. An optional participant `streamId` holds the seed identity constant across alternative labels. Live strategic opponents also accept an optional `mainTemperature` for score-based sampling experiments. Official map selection excludes study layouts unless their IDs are supplied explicitly. [8E calibration and reproduction](CpuDifficultyCalibration.md)

The difficulty names are `Easy`, `Normal`, `Hard`, `VeryHard`, and `Monster`. Each participant has a unique ID, even when several entries share an implementation. A future CPU can register a versioned adapter in `CreateOpponent` that takes a published snapshot, seat, seeded random source, diagnostics flag, and cancellation token. The runner owns timing, session validation, and history. All adapters should return an action from the supplied snapshot and report only the search counters their implementation exposes.

An optional `search` object overrides `depth`, `ownCandidates`, `opponentCandidates`, `continuationOwnCandidates`, `continuationOpponentCandidates`, `deepFinalists`, `risk`, `replyTemperature`, `verifyTerminalWins`, `requireFullRootReplies`, and `proofOnlyDeepening` independently. Omitted values inherit the selected difficulty. Set `proofOnlyDeepening` to false to compare heuristic deepening for Monster, and set continuation widths explicitly when comparing against an older configuration. All supplied overrides are part of the manifest fingerprint. The performance exports include selected and completed depth counts, simulated pairs per second and reused pair counts. [Example configurations and measured results](CpuSearchOptimization.md)

The frozen policies use the checkout's shared domain assembly, with its build identified in every manifest. The 8C.1 catalog update retained the 8C Knight Support gameplay behavior. For exact reproduction of Dot's original benchmark, including its pinned 8B.1 Domain, use the [archived reproduction workflow](CpuBenchmarks.md#frozen-controls-and-provenance). The original fixtures and legacy commands remain available.

| JSON setting | Values and corresponding CLI options |
| --- | --- |
| `id`, `participants` | Lowercase IDs of up to 64 letters, digits, or hyphens, starting with a letter or digit. CLI uses `--id NAME --participants 'id=opponent,id=opponent'`. Per-participant overrides use JSON. |
| `format`, `references` | `round-robin` creates every unordered distinct pair. `baseline` pairs every non-reference participant against all `references`. `head-to-head` requires exactly two participants. `self-play` schedules each participant against itself with independent streams. Use `--format` and `--references`. |
| `maps` | Defaults to all 100 production entries. Use `ids` or `--maps` for explicit selections, including historical IDs. JSON filters include `minWidth`, `maxWidth`, `minHeight`, `maxHeight`, `minRocks`, `maxRocks`, `minStarts`, `maxStarts` and `square`. `heldOut` marks production IDs, and `split` selects `all`, `tuning` or `held-out`. `sample` takes a stable hash-ranked subset after filtering, with `sampleSeed`. CLI also supports `--split`, `--sample`, and `--sample-seed`. |
| `ruleset` | `knightSupport` or deprecated `classic`. Use `--ruleset`. |
| `opening` | `natural` lets both competitors draft. In `fixed`, choose generated `pairs` and `seed` or set `fixed` to a per-map array of coordinate pairs such as `["A1/L12"]`. CLI supports `--opening`, `--pairs`, and `--opening-seed`. The single-pair generator uses established starts where available and otherwise finds a separated pair that reaches Main. Multi-pair generation uses a seeded shuffle and verifies a playable Main. Remaining positions collide in eligibility order. |
| `seeds`, `firstSeed`, `seedCount` | Use an explicit set of integers or a consecutive range, defaulting to seed 1. CLI uses `--seeds`, `--first-seed`, and `--seed-count`. |
| `swapSeats`, `swapStarts`, `alternateSubmission` | Seat swapping and paired submission-order reversal default to true. In fixed drafts, a participant keeps its assigned start when changing seats. `swapStarts` independently exchanges physical starts. CLI uses `--swap-seats`, `--swap-starts`, and `--alternate-submission`. |
| `workers`, `outputDirectory` | `--workers` defaults to at most two. `performance` forces one worker. `--out` defaults to `exports/tournaments/<id>`. |
| `maxMatches`, `maxRounds` | `--max-matches` truncates the canonical schedule and may leave an assignment pair incomplete. `--max-rounds` caps submitted rounds across both phases. The default cap is eight times the board area. |
| `diagnostics`, `strategicMetrics` | `--diagnostics true` stores candidate and search reports where available. `--strategic-metrics false` skips the retrospective tactical audit. Defaults are false and true respectively. |

Pairs are scheduled by ordinal map ID, ascending seed, pairing and assignment variants. The match ID is derived from the tournament ID, rules, opening mode, map, participants, seed, assignment variants and fixed actions. It addresses a schedule slot and can align comparisons in separate runs with matching spec IDs. Manifest fingerprints bind those slots to the actual opponents and settings. SHA-256 derives two distinct nonnegative .NET random seeds from the match seed, participant ID, and pairing slot. Streams follow the participant across the seat and start swaps. Each match gets fresh streams.

## Results, resumption and replay

- `manifest.json` stores the exact schedule, selected map fingerprints, configuration, opponent versions, binary digests, the Git revision when available, and runtime and hardware information. Keep the build and manifest together when archiving a run. A Git revision alone may omit local changes.
- The coordinator writes checksummed JSON shards under `matches/` via same-directory rename. Results contain both selections and scores on every resolved round, including forced collisions. Match records persist as they finish. One process at a time owns the output directory. Workers hold one match history each. The report reads shards one at a time and keeps aggregate bins, compact evidence, and error messages.
- Press Ctrl+C to cancel, or use `--stop-after N` to checkpoint after up to N pending matches. Resume with `tournament run --out <directory>` or the original `performance` command for a dedicated run. Resume validates the manifest and existing shard checksums. It skips completed or limited matches, retries technical failures, and restarts any in-flight match from its original seed. It can use a different worker count. A failed record preserves the partial history until the next attempt replaces it. An unsupported manifest, changed binaries or map definitions, or a corrupt result stops reuse of that directory.
- `summary.md`, `summary.json`, and the scores, comparisons, performance, and ratings CSVs are regenerated from persisted shards. Use `report` after an interruption to generate a snapshot without playing new matches.

```powershell
dotnet $bench tournament find --out exports/8d1-final/five --map banner-plain --opponent monster --seed 42 --result win
dotnet $bench tournament find --out exports/8d1-final/five --opening retry
dotnet $bench tournament replay --out exports/8d1-final/five --id <match-id>
dotnet $bench tournament inspect --out exports/8d1-final/five --id <match-id> --phase main --round 1 --opponents 'current=strategic:Monster,baseline=frozen8c:Monster'
```

`find` accepts participant IDs or exact implementation IDs. Result filters include `win`, `loss`, `red`, `blue`, `draw`, `retry`, `limit`, `failed` and `pending`. Opening filters include `main`, `draftscore`, `retry` and `unfinished`. Replay checks phase order, scores and the final result against the domain. Inspect reconstructs the input board for a recorded submitted round and analyzes each requested opponent in both seats. It uses a fresh deterministic random stream for each analysis. Use the stored history or original full diagnostics to inspect the exact random action of the original match.

## Statistics and limitations

One win earns one point and a draw earns half. The W–D–L denominator is scored matches, including immediate scored openings. Draft retries, limit stops, technical failures and pending matches are separate. Main and opening retry frequencies divide by completed matches. Main-turn averages and ranges use matches that reached Main. Collision frequency uses submitted draft rounds, with forced collisions recorded separately. Self-play participant totals count both seats and ratings exclude self-pairings.

Ratings fit a ridge-regularized Bradley–Terry logistic model in batch, treating each draw as a half win and half loss. The output centers neutral strength at 1500 on a 400-point log-odds scale. Only distinct scored histories per pairing and seat assignment contribute. The distinct key includes map, rules and recorded physical action history. Each rating lists its distinct evidence and connected component. Components and unrelated tournaments have independent ratings. Small or disconnected pools often yield weak estimates. A rating with fewer than 12 distinct scored observations is flagged as insufficient evidence.

Head-to-head uncertainty resamples map/seed blocks with seat and start variants kept together, using 2,000 deterministic bootstrap replicates. Exact repeat blocks on the same map are counted once. Intervals are suppressed below five blocks or when block rates show no variation. Confidence estimates describe the sampled comparisons. Shared map features and partly repeated traces can still correlate results. Use full assignment blocks for comparative runs. Natural and fixed openings run in separate tournaments, with separate ratings.

Performance mode is sequential and reports draft and Main separately. It records sample counts, mean, median, P95 and max wall time, mean or total pair counts, depth, fallback and budget/time exhaustion, and calling-thread allocation traffic. Percentiles use approximately 2% logarithmic bins at microsecond granularity. The measured decision includes diagnostic collection needed for strategic search counters. Full diagnostic retention affects storage. Fixed openings submit recorded coordinates so they produce no draft decision samples. The Probe reports pairs and depth in Main, with its internal tactical terms available only in archive code. This mode includes first-call compilation and runtime effects. Use comparable Release builds and repeated runs for hardware comparisons. Parallel match throughput is a separate measurement. Elapsed-time caps can change search outcomes, including when comparing worker counts. Deterministic pair caps are preferred for strength comparisons. Android can use the same benchmark positions in future measurements.

The avoidable-defeat audit asks whether another legal selection would have survived the actual revealed reply. The interior-placement proxy uses the current strategic position evaluator, including the opponent's resolved movement, and is a diagnostic rather than a direct measure of good play. A rules or evaluator change can require new reference measurements. [Original metric definitions and retained tactical cases](CpuBenchmarks.md#interpretation)

Start future searches with the tuning split and compare against frozen 8C under fixed openings. Follow with natural drafts and held-out maps. Use paired histories to investigate when a weight or budget change affects play, and use sequential measurements before tuning for mobile. Keep large results in the ignored `exports/` directory.

## 8D.1 reference runs

The runs used Knight Support and both seats, with no physical start swap. All scored totals below are wins–draws–losses. Counts refer to scheduled matches, with draft retries shown separately.

| Run | Configuration | Result |
| --- | --- | --- |
| Difficulty round robin | Example `round-robin.json`. Open Meadow, Twin Basins and Banner Plain. Natural, seed 42, 60 matches, two workers. | Easy 4–0–18, Normal 6–2–14, Hard 8–4–12, Very Hard 14–2–8, Monster 22–0–2. Two drafts retried, both Easy versus Normal. |
| Monster versus original controls, fixed | Example `baseline.json`. Pocket Meadow and Banner Plain, one castle pair, seed 42, eight matches, two workers. | Monster 2–2–0 against static Monster and 2–0–2 against Probe. All eight reached Main. |
| Monster versus original controls, natural | Same participants, maps, seed and workers as the fixed run. Select natural and omit the explicit pairs. Eight matches. | Monster 4–0–0 against static Monster and 2–0–2 against Probe. All eight reached Main. |
| Full production catalog | Static Easy versus static Normal on all 100 maps. Generated single-pair fixed drafts, seed 42, 200 matches, four workers. | Normal 172–6–22. All 200 reached Main. Easy earned 12.5% of match points, with a paired 95% interval of 7–19% across 100 map/seed blocks. |
| 8C parity | Run strategic Monster versus static Monster, then use frozen 8C Monster in the same participant slot. Natural, Pocket Meadow and Banner Plain, seed 42, four matches per implementation, two workers. | The four corresponding histories, final results and search counters matched exactly. Both runs went 4–0–0 for the Monster in the candidate slot. |
| Throughput and resumption | Strategic Monster against static Normal and Probe. Twin Basins and Banner Plain, generated single-pair fixed drafts, seeds 42 and 43, 16 matches. Tactical audit disabled. | Sequential, four-worker and resumed runs agreed on every action, result and search counter. The first five checkpoint files remained byte-for-byte identical after resuming with four workers. |
| Dedicated timing | Strategic Monster versus static Monster. Twin Basins and Banner Plain, generated single-pair fixed drafts, seed 42, four matches, one worker. Tactical audit disabled. | 340 Main decisions per participant. Monster won all four matches. |

Representative commands for the natural baseline, full catalog and reproducibility sample follow. Use a fresh value for `$out` to make new measurements.

```powershell
$out = 'exports/8d1-repeat'
dotnet $bench tournament run --id 8d1-monster-natural --format baseline --participants 'monster=strategic:Monster,static=static:Monster,dot=probe' --references static,dot --maps pocket-meadow,banner-plain --seeds 42 --workers 2 --out "$out/monster-natural"
dotnet $bench tournament run --id 8d1-catalog --format head-to-head --participants 'easy=static:Easy,normal=static:Normal' --opening fixed --seeds 42 --workers 4 --out "$out/catalog"
$sample = @('--id','8d1-throughput','--format','baseline','--participants','monster=strategic:Monster,normal=static:Normal,dot=probe','--references','normal,dot','--maps','twin-basins,banner-plain','--opening','fixed','--seeds','42,43','--strategic-metrics','false')
dotnet $bench tournament run @sample --workers 1 --out "$out/sequential"
dotnet $bench tournament run @sample --workers 4 --out "$out/parallel"
dotnet $bench tournament run @sample --workers 2 --out "$out/resumed" --stop-after 5
dotnet $bench tournament run --out "$out/resumed" --workers 4
```

The local Release results are in `exports/8d1-final/`. The catalog sweep used about 6.2 MiB including reports and the schedule. The five-difficulty run used about 1.6 MiB. The throughput runs used about 0.41 MiB each. These runs retained actions and counters with full diagnostics disabled. Storage grows with the number of recorded rounds and retained candidate reports.

The 16-match throughput sample took 26.78 s with one worker and 14.75 s with four workers, a 1.82× speedup. Measurements used Windows x64, a four-core Intel Core i5-7500 at 3.40 GHz, and .NET runtime 10.0.15. Dedicated timing used a separate sequential pass.

| Main-phase opponent | Decisions | Median / P95 / max latency | Mean pairs | Mean allocation traffic |
| --- | ---: | ---: | ---: | ---: |
| Strategic Monster | 340 | 22.47 / 40.71 / 118.95 ms | 597.5 | 5.52 MiB |
| Static Monster | 340 | 0.053 / 0.117 / 12.34 ms | 0 | 18.8 KiB |

Strategic Monster averaged 1.99 completed search depth. No strategic decision in that pass exhausted its budget, hit a time limit, or fell back to static evaluation. The time-limited configuration was disabled for this reference. Timing includes first-call overhead.

For 8D.2, pair each candidate configuration with frozen 8C on tuning maps, test against Probe, and repeat the most promising variants on held-out maps. Pocket Meadow is a useful tactical fixture for the Probe losses. Carry over the same opening and stream assignments when isolating changes to Main. For 8D.3, replay representative early and late Main positions on ARM64 Android and measure P95, allocation traffic and sustained latency before choosing mobile budgets.

## Certified tablebase opponents

Use a solver:<tablebase-id> opponent with a tablebase path and fixed openings within the certified scope. The manifest records the tablebase checksum, and unavailable positions are explicit failures. See the [solver commands and tablebase format](SmallBoardSolver.md).

Use `frozen8d3:<difficulty>` to select the immutable 8D.3 CPU. On a participant with `mainOpponent`, `opponent` and its options supply the draft policy and the named main opponent supplies the built-in Main policy. If either participant composes policies, both receive separate draft streams so draft random consumption cannot perturb Main. Live participants can set `openingSearch` or `legacyOpening: true`. New match records include a resolved opening-board digest alongside the full history digest. [Opening experiments](CpuOpeningStrategy.md)

The `frozen8e:<difficulty>` adapter uses the integrated 8E AI at `b63e7fb`. Planning, replay and persistence have explicit owners under `Benchmarks/Tournaments`. The scheduler and result formats remain compatible with historical reports and replay. Resume still requires the original executable fingerprint. [9A compatibility checks and reproduction](ArchitectureRefactoring.md)
