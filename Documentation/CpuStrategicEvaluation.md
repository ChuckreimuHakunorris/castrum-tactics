# Milestone 8D.3 strategic evaluation and simultaneous policies

This is the 8D.3 strategic evaluation study. Use the [current CPU architecture](CpuArchitecture.md) for live settings and [8E calibration](CpuDifficultyCalibration.md) for the selected ladder. Its device observations apply to the 8D.3 builds.

The study tests whether better territorial estimates, better response modeling, or more search improve Knight Support. Frozen 8D.2 uses the source at `9ef5ad05409efdc7d00cf112bd5b65cdc7ba5474` with the shared Domain assembly. Before changing the live CPU, the five difficulty profiles reproduced the frozen opponent’s complete match histories, moves and search counts. The tournament manifest records the frozen assemblies and the authoritative map fingerprints.

## Position evaluation

Authoritative secured territory supplies the score component. The 8D.2 evaluator projects the strongest castle or knight source through each contested component. This gives diminishing returns for adjacent sources, but a route through a damaged enemy knight has the same propagation cost as an empty route. Its material, formation and mobility terms can also reward placement in an already favorable interior.

The resilient evaluator assigns both players static route costs in half-turn units. It seeds from public legal placements and captures, backing castles at full reliability and delaying threatened or singly supported knight launches. Each successive step needs placement and survival reach from the preceding hypothetical knight. A capture that leaves a knight costs extra. A health-one enemy knight blocks a route because taking it creates a grave. Castles, graves, terrain and secured cells stop traversal. The model pays extra for entering a square exposed to an opposing legal selection and stops after eight estimated turns. A cell earns prospective credit from the difference in the two approach costs, tapered by the quicker approach. Cells outside both horizons earn zero prospective credit.

The resilient evaluation credits castles through secured score and reliable launch points. It discounts vulnerable knights and awards extra formation value where castle backing or redundancy matters under immediate threat. A mutually supporting pair can launch without a castle. The separate mobility term is disabled in this variant because legal launch reach is already part of the forecast. Ground degradation discounts the same projected territory estimate. The historical shortlist still orders tactical actions and reserves geographic alternatives, so the evaluation comparison retains the same candidate-ranking inputs. Draft previews retain the old evaluation.

The graph estimates route feasibility through a fixed state. It allows several fronts to project simultaneously even though a player can act on only one per turn. Threat costs approximate opposition and cannot prove that a route survives moving defenses, collisions or changes in territory. A knight can still be worth sacrificing when the resolved reply matrix shows a greater gain elsewhere.

The existing interior-placement proxy remains comparable with previous tournaments. A supplementary audit keeps the realized reply fixed and tests other legal empty placements. It counts a better alternative when it preserves terminal outcome tier, secured margin, surviving friendly pieces and new legal empty reach and strictly improves at least one. This measures immediate alternatives against that particular reply.

## Bounded outcome values and matrix policies

The bounded experiment maps a nonterminal score \(h\) to `h / (max(3, 0.60 × sqrt(area)) + abs(h))`. A resolved win, draw or loss supplies +1, 0 or −1. The transformation retains score order on a fixed board and prevents the large terminal numbers and score margins from swamping expected utility. It is a monotone heuristic with a board-size scale. The experiments measure its error and sign agreement against solved outcome utility. Margin is measured in a separate study column.

Each matrix uses authoritative simultaneous resolutions. The one-turn experiment covers all legal opposing replies if the root fits the pair budget and the reply count is at most 32. At other roots and selected continuation nodes, common replies include affordable same-square collisions as columns. The two-turn experiment compares all retained root rows at the same horizon, falling back to the completed first tier when the wider comparison exceeds the budget. Root-only search uses the risk-weighted continuation. Continuation mode estimates the value of a simultaneous matrix at each searched child. The expected control uses the same root coverage and bounded utilities and retains the expected/adverse decision policy. A terminal proof still requires a winning action against every legal reply at each proved state.

The production-side numerical component uses a pure-saddle pass and a positive-shift binary64 simplex for mixed cases. It normalizes primal and dual weights and recomputes the lower and upper security levels on the original matrix. It stops after 256 pivots or on the same elapsed guard and cancellation token used by search. A gap above `1e-7` causes a fallback. The engine draws from accepted row weights with the caller’s seeded stream. It preserves zero probability outside the support. The solver occupies no space in the Domain assembly and imports no tablebase or exact-arithmetic dependency.

[`CpuSearchReport.Matrix`](xref:CastrumTactics.AI.CpuSearchReport.Matrix) records numerical security bounds, pivot count, covered root actions, whether all legal opposing replies were covered, and matrix construction and solving time. The bounds describe the scored matrix. Approximate continuation payoffs and omitted own actions can still affect the result. The solver study separately measures policy security against all legal replies and certified continuation values.

## Solved-position results

The split study canonicalizes locked-health-equivalent states across geometry and player exchange before assigning a stable hash partition. It samples early, middle and late positions separately and checks each side. The verified Pocket Meadow graph contains 641,567 representatives after these reductions in the solve. The study uses 72 held-out Pocket Meadow positions and 36 positions on each of Corner Watch and Stone Center. The original rank-third study stays available with `--analysis-split all`. Reported means below use the midpoint of certified intervals. The study also writes lower and upper mean gap bounds using exact dot products with the normalized binary64 policy.

| Monster configuration | Pocket outcome / margin gap | Corner outcome / margin gap | Stone outcome / margin gap |
| --- | ---: | ---: | ---: |
| Frozen 8D.2 | .197 / .272 | .185 / .201 | .100 / .117 |
| Built-in 8D.3 | .141 / .213 | .079 / .105 | .112 / .149 |
| Resilient | .175 / .249 | .164 / .194 | .097 / .128 |
| Bounded expected/adverse | .164 / .246 | .151 / .191 | .100 / .138 |
| Root matrix | .136 / .192 | .075 / .116 | .103 / .145 |
| Continuation matrices | .143 / .202 | .051 / .077 | .082 / .122 |
| Resilient with continuation matrices | .143 / .212 | .085 / .119 | .123 / .167 |
| Wider 2,000-pair 8D.2 | .189 / .267 | .185 / .215 | .100 / .131 |
| Depth 3, 4,000 pairs | .170 / .267 | .172 / .202 | .096 / .112 |
| Dot’s Probe | .149 / .236 | .162 / .185 | .095 / .109 |

Across the 288 held-out seat decisions, the built-in Monster’s mean outcome gap was .118 versus .170 for 8D.2. Pocket Meadow P95 fell from .738 to .500 and Corner Watch from .500 to .289. Stone Center regressed on both utilities, and production assigned about 26% of its action mass to weakly dominated moves there versus 14% for 8D.2. The solved report includes each stage, pure optimal mass, both policy-gap bounds and omitted-response checks. Each sampled small-board root could cover the known adverse replies.

The resilient evaluator’s bounded mean absolute error rose from .361 to .459 on Pocket Meadow, .322 to .408 on Corner Watch and .350 to .483 on Stone Center. Certified sign reversals changed from 9 to 16, 7 to 7 and 2 to 10 respectively. Better action ordering in some searches can coexist with worse absolute leaf calibration. Material remains a useful predictor on the solved boards. The results do not establish a production-wide material weight.

- **Pocket Meadow start.** The one-turn matrix chooses B2 with probability 1. Frozen 8D.2, the resilient evaluator at depth two and the two-turn matrix variants leave B2 out of their policies. The resulting outcome gap is approximately .695 in all of those depth-two cases.
- **Second-turn defense.** After A2/B2, the two-turn root variant mixes approximately 86% A2 and 14% B2. Its outcome gap still equals the frozen policy’s .305. The combined policy also mixes and retains that gap. The search has not recovered the certified defense.
- **Archived collision.** The documented turn-four fixture is a forced loss for Red under optimal play. Resilient evaluation and the combined variant select C2 in the tested orientation and attain the solved margin policy’s security. The match-outcome value stays −1.
- **Sign reversal and 75/25 mixture.** The older evaluator reads the position as favorable for Red. Exact outcome is −3/4 and exact margin is −9/11. The numerical tests recover the distinct policies from their solved payoff matrices. In a two-turn search with heuristic leaves, root mixing gives B1/B2 approximately 84%/16% and lowers the outcome gap from .250 to .094. The combined experiment stays on B1 with a .250 gap.
- **Independent guarantees.** Pocket Meadow, Corner Watch and Stone Center each have a zero initial outcome value in these starts. In each case neither side can force a drawn terminal score, while each side has a policy guaranteeing a win or draw. The Boolean analyses check every legal opposing reply and treat terminal wins separately from terminal draws.

## Production battlefield results

The first fixed-opening cohort used Banner Plain (12 × 12, open), Bellflower Reach (8 × 8, six rocks) and Twin Basins (10 × 10, 24 rocks). Amber Fallow (9 × 12), Broadshield Field (12 × 10) and Silver Bracken (12 × 12) stayed held out. Each candidate played 24 matches per cohort against frozen 8D.2 with two seeds, independent seat and physical-start swaps and fixed successful openings. The two-castle holdout varied the opening regime as well as map geometry, and allowed multiple fronts from the first Main turn.

| Experiment versus frozen 8D.2 | Tuning W–D–L | Held-out one castle W–D–L | Held-out two castles W–D–L | Two-castle mean margin |
| --- | ---: | ---: | ---: | ---: |
| Resilient evaluator only | 8–8–8 | 20–0–4 | 16–0–8 | +16.17 |
| Bounded expected/adverse | 6–8–10 | 18–0–6 | 8–0–16 | −4.83 |
| Root matrix | 0–0–24 | 2–0–22 | 10–0–14 | −13.50 |
| Continuation matrices | 0–0–24 | 0–0–24 | 8–2–14 | −12.33 |
| Resilient with continuation matrices | 16–8–0 | 24–0–0 | 18–0–6 | +15.42 |
| Wider 2,000 pairs | 12–8–4 | 16–0–8 | 12–0–12 | +5.92 |
| Resilient, wider 2,000 pairs | 16–8–0 | 24–0–0 | 16–0–8 | +18.25 |
| Deeper 2,000 pairs | 16–8–0 | 16–0–8 | 12–0–12 | +3.75 |
| Frozen 8C | 4–16–4 | 12–0–12 | 12–0–12 | 0.00 |
| Frozen static | 0–0–24 | 0–0–24 | 12–0–12 | −8.50 |
| Dot’s Probe | 2–6–16 | 6–0–18 | 10–0–14 | −9.58 |

The combined variant took 18/24 points in the two-castle holdout, with 24 distinct scored histories and a block bootstrap interval of 58.3%–91.7% across six blocks. The tuning result had 20 distinct histories and five blocks. The one-castle holdout swept all six blocks, so the harness omits an interval for that constant sample. On three further multi-castle maps, Granite Orchard, Twelve Towers and Wrenfield, the combined variant went 16–0–8 against each frozen control. Evaluator-only went 20–0–4 against each on those maps, evidence that the contribution from matrix selection depends on the opening and map. The actual built-in Monster, including its compact three-turn proof and all-reply immediate-win verification, repeated 16–0–8 against each control in 48 distinct histories. Mean margin was +9.67 against 8D.2 and +9.33 against 8C. Each of those point rates was 66.7%, with a wide 33.3%–100% block interval.

The separate choices that improved small-board security often failed on production maps. In particular, the matrix-only policy still made poor territorial choices with the baseline leaf evaluator. Bounded utility alone also traded gains on one-castle holdouts for losses in the two-castle conditions. The results support the interaction of the resilient evaluation and matrix selection more strongly than either individual change across the full set. The wider resilient variant remains competitive, but doubling the pair limit yielded mixed tournament gains.

In the final holdout against 8D.2, Monster recorded 92 historical interior flags in 648 empty placements (14.2%). Frozen 8D.2 recorded 70 in 458 (15.3%). The realized-reply audit found a strictly better alternative for 32 Monster placements (4.9% of all empty placements) and 44 frozen placements (9.6%). Match lengths and placement opportunities differed. The immediate audit measures a local comparison with a fixed reply. It cannot establish long-term dominance.

The 120-game natural-opening ladder produced 120 distinct histories. Monster went 42–0–6 across the four lower levels, compared with 34–0–14 for 8D.2 on the same schedule. The other standings were Very Hard 28–0–20, Hard 24–2–22, Easy 16–0–32 and Normal 8–2–38. Easy won eight of its 12 games against Normal, and Hard split its 12 games with Very Hard. Final difficulty calibration remains open.

Against the certified expectation-policy opponent on Pocket Meadow, production, frozen 8D.2, frozen 8C and frozen static each scored 2–0–14 in 16 assignments. The one-turn matrix drew all 16, and Probe drew 14 and lost two. The six pairings produced 96 distinct scheduled histories by the tournament definition. The expectation policy can risk a loss after a deviation and still secure its certified expected value. The separate win-or-draw analysis asks whether a policy exists that avoids such an outcome against every reply.

Monster uses the combined policy at 850 pairs with 18 own candidates and 10 ordinary replies. It retains the 450 ms scene guard and the compact three-turn proof pass. Easy through Very Hard retain their earlier settings. Classic retains its static policy. The opening still uses the same evaluator and seeded sampler.

## Windows performance

The dedicated worker ran 60 multi-castle games on Bellflower Reach, Banner Plain and Silver Bracken. It measured elapsed time and thread-local allocations for each decision with diagnostics enabled. Pair counts exclude the audit and report serialization. These are wall-clock measurements on the development Windows host.

| Profile | Median / P95 / maximum ms | Allocated MB per decision | Mean resolved pairs | Mean selected depth |
| --- | ---: | ---: | ---: | ---: |
| Frozen 8D.2, 850 | 23.4 / 54.8 / 316.2 | 3.70 | 609 | 1.99 |
| Resilient, 850 | 36.1 / 61.7 / 124.8 | 3.75 | 605 | 2.00 |
| Bounded expected, matrix coverage, 850 | 4.3 / 21.6 / 72.9 | 0.97 | 210 | 1.11 |
| Root matrix, 850 | 4.3 / 17.0 / 112.7 | 0.99 | 213 | 1.14 |
| Combined, 850 | 7.7 / 31.5 / 296.7 | 0.97 | 190 | 1.08 |
| Resilient, 2,000 | 79.8 / 139.0 / 217.3 | 8.41 | 1,347 | 1.98 |

Combined completed 716 first-tier and 64 second-tier decisions, with 704 comparability-budget stops and zero static fallbacks. The benchmark disables the elapsed limit to preserve deterministic work. Both elapsed interruption and cancellation are covered by the tests. Throughput was about 17,300 pairs/s for combined and 23,900 for frozen 8D.2. Route evaluation costs more per pair. The all-row staging budget uses fewer pairs on wide boards because many second-tier comparisons cannot fit.

There were 762 combined decisions with a numerical matrix policy, including 144 with complete opposing root coverage. Across these decisions construction and solving averaged 0.017 ms, with a 0.033 ms P95 and a 4.50 ms maximum. The largest observed numerical gap was approximately \(4.5 × 10^{-16}\), with at most ten pivots at the selected root and up to 56 solves per decision. The test suite also compares against the exact development solver on the archived payoff matrices, pure saddles, degenerate ties and 75 seeded rectangular matrices. The complete benchmark process, including match storage and diagnostics, recorded 4,948 generation-0, 3,081 generation-1 and 249 generation-2 collections and a peak working set of 165 MB. These counts describe the harness as a whole.

The separate built-in head-to-head measured 626 Main decisions per engine. Monster used a 7.0 ms median, 16.4 ms P95 and 479.2 ms maximum, versus 22.0, 44.1 and 749.9 ms for frozen 8D.2. Mean allocated bytes were 0.98 MB versus 3.79 MB. Monster resolved 193 pairs per decision on average versus 604. Selected depths were 572 at one turn, 48 at two turns and six at three turns. The extra proof pass reached three turns twice more and kept the two-turn choice. This run used deterministic budgets with the elapsed guard disabled.

## Verification

The .NET 10.0.404 solution build passed with warnings treated as errors, and all 347 tests passed. The suite covers stale-session protection and legal decisions for both players at all five difficulty levels across all 100 production maps in Knight Support and Classic. Both Godot 4.7.2 .NET runtime regressions passed, covering restarts and authoritative score presentation. DocFX 2.81.0 metadata and site builds passed with zero warnings. NuGet's online audit was disabled on the offline build host.

The Windows x64 debug export launched Knight Support on Wrenfield. The ARM64 Android debug export packages `com.castrumtactics.prototype`. The standard installer verified the update over wireless ADB on an XQ-DQ54 running Android 15, and the production build loaded Thistlewater Plain in the foreground.

The on-device timing harness reached 12 Main snapshots from four maps. Android closed its task before it reported the 850- and 2,000-pair timings. The production settings keep the conservative budget while Android latency and thermals remain unmeasured.

## Reproduction

Run from the repository root. Use independent output directories for binaries or settings that change. Tablebases, generated reports, traces and export artifacts belong under ignored `exports/`.

```powershell
dotnet build Benchmarks -c Release -warnaserror
$bench = 'Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll'
New-Item -ItemType Directory -Force exports/8d3 | Out-Null
dotnet $bench tournament run --config Benchmarks/Examples/strategic-evaluation.json
dotnet $bench tournament run --config Benchmarks/Examples/strategic-evaluation.json --split held-out --seeds '119,233' --out exports/8d3/held-out
dotnet $bench tournament run --config Benchmarks/Examples/strategic-evaluation.json --split held-out --pairs 2 --seeds '119,233' --out exports/8d3/held-out-two-pairs
dotnet $bench tournament run --config Benchmarks/Examples/strategic-holdout.json
dotnet $bench tournament run --id 8d3-default --format baseline --participants 'live=strategic:Monster,8d2=frozen8d2:Monster,8c=frozen8c:Monster' --references '8d2,8c' --maps 'granite-orchard,twelve-towers,wrenfield' --opening fixed --pairs 2 --opening-seed 932 --seeds '131,349' --swap-starts true --out exports/8d3/production
dotnet $bench tournament run --id 8d3-production-ladder --format round-robin --participants 'easy=strategic:Easy,normal=strategic:Normal,hard=strategic:Hard,very-hard=strategic:VeryHard,monster=strategic:Monster' --maps 'broadshield-field,amber-fallow,silver-bracken' --opening natural --seeds '127,229' --out exports/8d3/production-ladder
dotnet $bench tournament performance --config Benchmarks/Examples/strategic-performance.json
dotnet $bench tournament performance --id 8d3-default-latency --format head-to-head --participants 'live=strategic:Monster,8d2=frozen8d2:Monster' --maps 'banner-plain,granite-orchard' --opening fixed --pairs 2 --seeds 67 --swap-starts true --strategic-metrics false --diagnostics true --out exports/8d3/production-performance
dotnet $bench solver --ignore-locked-health true --interval-scale 100000000 --fast-policy true --max-states 3200000 --max-seconds 650 --max-mib 3500 --fixture Benchmarks/Reference/pocket-meadow.json --analysis 72 --analysis-split both --output exports/8d3/pocket-split.json --tablebase exports/8d3/pocket-meadow.ctb
dotnet $bench solver --map corner-watch --ignore-locked-health true --analysis 36 --analysis-split both --output exports/8d3/corner.json --tablebase exports/8d3/corner-watch.ctb
dotnet $bench solver --map stone-center --ignore-locked-health true --interval-scale 100000000 --fast-policy true --analysis 36 --analysis-split both --output exports/8d3/stone.json --tablebase exports/8d3/stone-center.ctb
dotnet $bench solver-query exports/8d3/pocket-meadow.ctb --opponent study --side red --path 'A2/B2,B2/A2,B1/B2,C1/A3,C1/B1'
```

The certified-opponent tournament uses the Pocket Meadow example with `exports/8d3/pocket-meadow.ctb` as the tablebase path. Add `frozen8d2:Monster` and a depth-one `strategic:Monster` with `equilibrium` set to `root` to reproduce the comparisons alongside the current production profile. [Tournament example and configuration](CpuTournaments.md)

## Roadmap

The following were the 8D.3 plans. See the completed [8D.4 opening strategy](CpuOpeningStrategy.md), [8E ladder calibration](CpuDifficultyCalibration.md) and [battlefield balance study](BattlefieldBalance.md).

8D.4 will compare natural and fixed starts with the same Main opponent and characterize multi-castle openings before redesigning the draft. Track first foothold security, collision costs and disconnected fronts independently from their final match result.

8E will calibrate the difficulty ladder and battlefield mix. Test whether larger battlefields reduce the influence of individual simultaneous outcomes and increase the consistency with which stronger players win. Isolate board area, geometry, starting-position distribution, match length, computational limits and strategic depth. The small-board study cannot resolve those effects.

Milestone 8D.4 adds an independent Monster draft policy. [Opening strategy](CpuOpeningStrategy.md) records the paired tests with Main fixed at frozen 8D.3 and the original engine parity.

[8E calibration](CpuDifficultyCalibration.md) evaluates the resilient evaluator at lower levels, compares production and frozen policies, and reruns the compact reference study.
