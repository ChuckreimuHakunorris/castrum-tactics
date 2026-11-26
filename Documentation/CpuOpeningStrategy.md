# Advanced opening strategy

Milestone 8D.4 gives Monster an opening-specific evaluator and a simultaneous policy. The selected production configuration completes one draft matrix of up to 16 starts, uses a 256-pair cap, and enables no Main preview. It sits behind the public snapshot boundary and the existing worker and elapsed-time safeguard. The [Main policy](CpuStrategicEvaluation.md) continues to use its 8D.3 evaluator, profiles, and selection path. Classic and the four other difficulties retain their existing draft policies.

## Baseline and experimental design

The immutable `frozen8d3:<difficulty>` implementation is the C# AI source at `b4c77ea7553486f6043cd60822f46f9b75dd42fa` in a separate development assembly. Before changing live AI, the frozen copy matched seeded live decisions, candidate scores and probabilities, selected depths, and pair counts across all five difficulties and the 100 production maps under each ruleset. The parity test runs the live built-in profiles on resolved Main states and on Classic; it also checks Knight Support drafts with an explicit legacy profile.

The 8D.3 opening ranked immediate empty neighbors, regional size and friendly spread. It evaluated one authoritative pair per retained row and reply, with a special adverse-only check for an omitted same-square reply. Later castles influenced the strongest-source field, but the score combined local appeal and projected influence on unfinished boards. Expected and adverse estimates used a softmax/uniform reply model. Its final diversity floor deliberately kept lower-rated starts in the distribution. Even a full reply model cannot protect a policy that gives an opponent good responses to its sampled mix. The baseline assigned a separate negative score to a restart.

The initial control scheduled 96 games on six maps, with both draft policies using the same 8D.3 engine and with frozen 8D.3 Hard on both sides in Main. The independent seat streams and policy-ID streams led to some variance between identical algorithms. There were 50 restarts on this intentionally tiny-map-heavy schedule and 46 scored games. Live 8D.3 went 12–24–10. This control establishes parity of the engine via the direct tests and illustrates the variation expected in a seeded tournament.

## Evaluator and sequencing

The advanced evaluator starts from the domain preview's resolved board. It uses authoritative secured territory plus a signed combination of marginal coverage, access to reachable regions, exclusive immediate footholds, and the neighboring space around those footholds. A multi-source eight-neighbor wave propagates through empty ground, respecting terrain, graves, and occupied cells. Each player's nearest arrival contributes once per square. Two nearby castles add little if they give access to the same area at the same time. Castles that reach across an obstruction or give an independent approach add their own coverage. Opposing arrival estimates are subtracted. Empty starts and their immediate surroundings receive a discount while the location is still contestable in the draft.

The static score is bounded and scales with playable area. It is a model of projected reach and reinforcement options. It does not forecast exact legal expansion timing, future simultaneous captures, or win probability. The current wave uses the official Knight Support adjacency and does not predict a route through occupied cells. Main previews can assess a few tactical consequences on compact boards through authoritative `Advance`. The test for complementary coverage uses a synthetic board with a permanent terrain divider. All opening-quality study samples instead come from legal draft histories.

The search sees current castles, graves, and eligible starts. Both players receive a common candidate set formed from their tactical ranks. All modeled rows have their same-square collision column. Production maps fit the complete root width. An explicitly narrower experiment models a restricted game and reports its coverage. Scores distinguish incomplete draft states, successful Main states, scored endings, and restart-required outcomes. A scored win or loss is valued at ±1. A restart uses an acting-player modeled utility of −0.08, representing a small heuristic retry penalty around a neutral new opportunity. It is separate from a scored draw. An opponent in the matrix is modeled as minimizing the acting player's utility. The retry penalty is a local preference for getting to Main, so the production model is not an exact shared utility for repeated drafts.

[CpuOpeningOptions](xref:CastrumTactics.AI.CpuOpeningOptions) enables one to six draft tiers, bounded width and work, optional Main preview, and expected/adverse, pure worst-case, or equilibrium selection. Candidate order is deterministic and sampling uses the caller's independent stream. The equilibrium policy adds no variety floor. The numerical solver reports lower and upper guarantees for the modeled matrix, with the same residual checks as 8D.3. A deterministic policy is appropriate where the model finds a pure solution. On an empty two-start symmetric opening, the retry penalty gives a robust fair mix and the worst-case probability of successful completion of one half.

The optional deeper pass resolves fresh pairs with `PreviewDraft` and reads the new eligibility before continuing. It reuses pairs and child values by the owner, piece, and full health of every initially eligible square, with terrain and other cells fixed per decision. Candidate filtering uses the root priority among starts still eligible in each child. Search only returns the last completed comparable matrix if a later tier runs out of pairs or time. If no tier finishes, the engine retains its legal static sampler. Cancellation is checked at each pair and inside wave propagation and numerical solving. A separate diagnostic counts visited restart outcomes, Main preview pairs, full root coverage, and reused child evaluations. Main lookahead is experimental, uses complete matrices up to 36 pairs per node, and shares the same decision budget. Wide Main leaves retain the static opening estimate.

## Fixed-configuration study

The `opening-study` command exhaustively visits draft states with up to five starts and samples larger schedules with a reproducible range of collision rates. It stratifies resolved openings by total paired castle count. For deduplication it uses color exchange and only board geometries that preserve both terrain and initial eligibility. It also merges equivalent orderings and inert castle/grave health. Drafts that score immediately or require a retry are counted separately from Main rollouts. Production rules place one castle for each player on a distinct pair and neither on a collision, so legal starts always have an equal number of castles per side. Asymmetric locations and routes can still produce substantial advantages.

The study ran **2,320 scored matches**, covering **145 unique legal opening configurations** on **13 maps**. Each configuration used both physical assignments, four seeds, and two identical-policy self-play controls, frozen 8D.3 Easy and Normal. The split is by map. The held-out group has seven production maps and 98 unique configurations. Training and held-out maps never share an opening group. Individual outcomes are oriented back to the same castle assignment and averaged per configuration before calculating correlations.

| Held-out predictor | Easy points Pearson | Normal points Pearson | Easy margin Pearson | Normal margin Pearson |
| --- | ---: | ---: | ---: | ---: |
| 8D.3 positional score | 0.482 | 0.560 | 0.690 | 0.755 |
| Proposed draft score | 0.626 | 0.659 | 0.684 | 0.703 |
| Proposed score without marginal reach and access | 0.452 | 0.448 | 0.345 | 0.371 |
| Marginal coverage alone | 0.581 | 0.631 | 0.717 | 0.757 |
| Exclusive immediate footholds | 0.421 | 0.410 | 0.306 | 0.331 |
| Relative friendly geometric spread | 0.164 | 0.097 | 0.141 | 0.025 |

The proposed match-point ranking agreed with held-out results on about 72% of comparable within-map pairs for both controls. Its margin correlation was lower than the legacy full score. Marginal coverage itself was the strongest individual held-out term. Closest opposing distance had points correlations below 0.07. Initial secured territory and exclusive access had no variation across the sampled held-out assignments, so this study gives no estimate of their independent effect. The opening's number of legal first actions and exclusive footholds agreed on this sample. Score and surviving formation leads after three Main turns were weak proxies for final match points. These are associations in seeded self-play, with only four seeds per physical assignment. They do not establish theoretical position values, causality, or a calibrated win probability.

## Controlled policy tests and promotion

Every tuning row scheduled 84 natural games on seven maps, using both seats and six seeds. Both sides in Main used frozen 8D.3 Hard, and the draft and Main random streams were separate. Reported point rates exclude adjudication-free retries and use unique map/seed blocks with their seat variants paired. Exact repeat histories are collapsed by the tournament reporter.

| Draft against frozen 8D.3 | W–D–L | Restarts | Block point rate |
| --- | ---: | ---: | ---: |
| Frozen static Monster | 20–11–29 | 24 | 42.9% |
| Frozen 8C Monster | 29–10–19 | 26 | 56.4% |
| Frozen 8D.2 Monster | 20–8–22 | 34 | 48.4% |
| Legacy positional score, expected/adverse | 29–12–25 | 18 | 54.1% |
| New draft score, expected/adverse | 24–16–30 | 14 | 46.3% |
| New draft score, pure worst case | 24–13–18 | 29 | 52.3% |
| One-round equilibrium, coverage ablated | 28–19–19 | 18 | 56.1% |
| One-round equilibrium, complete evaluator | 31–17–13 | 23 | 60.4% |
| Up to two draft rounds | 29–17–17 | 21 | 56.9% |
| Up to three draft rounds | 24–15–20 | 25 | 50.8% |
| Two draft rounds plus up to one Main preview | 27–20–18 | 19 | 53.9% |
| Two draft rounds plus up to two Main previews | 26–10–18 | 30 | 52.9% |

The evaluator comparison within expected/adverse shares coverage and the uniform reply model. It isolates the leaf evaluation within the new harness. The old production path uses a different reply model and scaling. Improved fixed-opening prediction did not translate to improved play in the expected/adverse ablation. Equilibrium selection and combined coverage together gave the highest tuning point estimate. Deeper draft tiers and Main previews changed the played histories and scored lower in the tuning aggregate. Those variants remain experimental. The one-round policy's tuning interval was 48.6–71.5%.

The selected policy then played 224 more draft-only matches on seven held-out production maps. Against frozen 8D.3 it went **81–2–29**, scored **73.2%** of points with a paired 95% interval of **64.3–81.7%**, and averaged **+7.14** secured territory. Against frozen 8C it went **77–3–32**, scored **70.1%** with an interval of **62.1–78.1%**, and averaged **+5.66** territory. Neither pairing required a restart. Against 8D.3 there were 179 submitted collisions in 583 submitted rounds (30.7%), plus 85 automatic collisions. Matches averaged 5.96 total draft rounds. The distribution of castles per side was 4 / 16 / 37 / 22 / 29 / 4 matches with one through six castles. There were 107 distinct resolved opening boards and 112 distinct complete histories. Against 8C there were 111 distinct openings and 112 histories. A repeat with the same seeds reproduced those histories.

With production Monster and frozen Monster using their normal policies, the live engine went **22–2–8** in 32 games on four of the held-out maps, scoring **71.9%** with a paired 95% interval of **50.0–90.6%**, mean margin **+5.13**, and zero restarts. Both engines used their 8D.3 Main policy. The independent physical-start swap belongs to fixed-opening experiments, because natural drafts themselves determine the assignment. Seat swaps and independent streams are used in both settings.

In a small separate four-map sensitivity set with frozen 8D.3 Easy in Main for both sides, the live Monster draft went 8–2–19 with three retries. This run used a different map/seed schedule from the main Hard study. The result cautions against treating any one rollout policy as a complete measure of opening strength. The four other difficulties ran in the same sensitivity set using their legacy draft implementations. Direct parity verifies their algorithm equivalence, while the empirical live points estimates ranged from 36.7% to 43.3% with distinct independent participant streams. The study does not establish a calibrated difficulty ordering. Production adopts the one-round equilibrium configuration only for Monster.

## Mathematical draft examples

On archived Pocket Meadow, either successful assignment has exact outcome and margin values of zero. An equal submission is an opening restart. With free retries from the same decision and independent fair choices on every attempt, a player can assure almost-sure completion and a neutral continuation. The model's retry penalty is what distinguishes the two restart diagonals from the successful choices in the production matrix.

On archived **Three Banners**, one pair of distinct starts assigns both castles and the remaining start becomes an automatic grave. The exact outcome and margin from top A1 against top C1 are **0 and 0**. From top A1 against bottom B3 they are **−1 and −2**. Reflection identifies C1 with A1 and color exchange negates the values for reversed assignments. Although the full continuation tables use certified intervals for some states, both cited root bounds are exact. The bottom castle can force victory after obtaining it against a top castle. When both top castles are established, the Boolean solver verifies a unilateral win-or-draw guarantee for either side and finds that neither side can force a draw. The specified equilibrium pair draws with probability one.

With a neutral restart continuation, a top/top collision leaves a top and bottom start. Both participants can choose the bottom and deny the other a winning assignment; this gives that subgame a neutral value. If both select B3 first, the remaining starts are the two equivalent top positions. The complete opening outcome matrix for A1, C1, B3 in that order is therefore

| Red / Blue | A1 | C1 | B3 |
| --- | ---: | ---: | ---: |
| A1 | 0 | 0 | −1 |
| C1 | 0 | 0 | −1 |
| B3 | +1 | +1 | 0 |

Margin has the same signs with magnitude 2. Both sides uniquely select B3 initially in this model. Following B3/B3 with a fair choice among the top starts avoids a predictable retry loop. Against any opponent, choosing B3 then using that fair continuation provides at least a one-half chance of reaching a successful opening per attempt, and every successful assignment lets that player avoid a loss through the corresponding Main policy. This pathwise guarantee uses the separate Boolean result, together with the forced win when B3 is obtained against a top castle. A different retry or abandonment objective would need its own analysis. A neutral opening value alone gives no guarantee about whether the eventual Main result will be a win or draw.

## Performance

A sequential Release run on Linux x64 used 24 games across Granite Orchard, Twelve Towers, and Wrenfield with frozen 8D.3 Normal in Main. It retained diagnostics and used a 450 ms safeguard for the live draft. Measurements include first-call runtime and compilation overhead.

| Draft policy | Decisions | Median | P95 | Max | Mean pairs | Mean allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Frozen 8D.3 Monster | 131 | 0.98 ms | 5.29 ms | 64.08 ms | 48.84 | 242,123 B |
| Selected 8D.4 Monster | 131 | 0.98 ms | 5.96 ms | 69.57 ms | 48.84 | 228,591 B |

Peak process working set was 255 MiB across the mixed draft and Main workload. All measured matrices had complete root coverage and selected depth one. There were no budget stops, elapsed stops or static fallbacks in the live sample. The reported numerical solve had median 0.0058 ms and maximum 3.20 ms, with 239 pivots and 17 reused child evaluations. The 256-pair cap covers the largest catalog root. Multi-round variants reuse pair snapshots and memoized outcomes and can still consume their larger cap on an interrupted tier. An interrupted deeper search is covered by the deterministic budget test. There were no on-device measurements or sustained thermal tests.

## Reproduction and verification

From the repository root, use the same Release benchmark build for comparisons. Keep raw results under ignored `exports/`. The compact reference file at `Benchmarks/Reference/opening-research-8d4.json` records the principal study, performance figures, and mathematical results.

```sh
dotnet build Benchmarks -c Release
bench=Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll
dotnet "$bench" opening-study Benchmarks/Examples/opening-quality-study.json exports/8d4/quality
python3 Benchmarks/opening_report.py exports/8d4/quality/outcomes.jsonl
dotnet "$bench" tournament run --config Benchmarks/Examples/opening-control.json --out exports/8d4/control
dotnet "$bench" tournament run --config Benchmarks/Examples/opening-strategy.json --out exports/8d4/tuning
dotnet "$bench" tournament run --config Benchmarks/Examples/opening-holdout.json --out exports/8d4/holdout
dotnet "$bench" tournament run --config Benchmarks/Examples/opening-integration.json --out exports/8d4/integration
dotnet "$bench" solver --map three-banners --starts A1/B3 --ignore-locked-health true --interval-scale 100000000 --fast-policy true
dotnet "$bench" solver --map three-banners --starts A1/C1 --ignore-locked-health true --interval-scale 100000000 --fast-policy true
```

The difficulty sensitivity configuration uses the integration example with map IDs alder-common, pocket-meadow, corvid-park, and wrenfield, seeds 79, 313, 653, and 827, the two participants set to matching difficulties, and `mainOpponent` set to `frozen8d3:Easy` for both.

The performance run uses the integration example on Granite Orchard, Twelve Towers, and Wrenfield, with frozen 8D.3 Normal as `mainOpponent` on both sides, live `timeLimitMs` set to 450, and `diagnostics` enabled. Run it via `tournament performance` to use one worker.

The solution build and **357 tests** passed. The tests include legal decisions across all five difficulties and 100 maps, Knight Support and Classic parity, combined coverage on blocked terrain, shared collision columns, seeded mixing, authoritative automatic resolution, restart classification, bounded Main previews, transposition reuse, interruption fallback, cancellation, and phase-stream isolation. Both Godot 4.7.2 .NET runtime checks passed on Linux. DocFX generated metadata from the built assemblies and built the site with warnings treated as errors. Windows- and Android-targeted managed compilation passed. Native Windows runtime verification, an Android export or install, and ARM64 timings require a supported Godot and device setup.

## Priorities for 8E

Use fixed assignment swaps to distinguish area from terrain layout, initial legal options, castle geometry, opponent strength, and match duration. Include openings with the same count and sharply different marginal reach, and repeat conclusions with several fixed Main policies. Keep outcomes and territory margins as separate targets. The current measurements do not determine whether larger maps reduce sensitivity to individual simultaneous outcomes. Collect warmed and sustained Android latency, allocations and thermal measurements before expanding the production draft budget.
