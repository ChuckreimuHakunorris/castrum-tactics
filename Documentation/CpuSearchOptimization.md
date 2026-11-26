# Milestone 8D.2 search optimization

This is the 8D.2 search study. Later evaluator, draft and ladder changes are described in the [current CPU architecture](CpuArchitecture.md). The commands below retain their original study settings.

The search engine can compare up to six simultaneous turns in experiments. Production Monster adds a third-turn pass for verified wins on compact tactical positions. It retains its 850-pair budget and the game's cooperative 450 ms safeguard. Easy through Very Hard retain their existing search profiles. The strategic evaluation weights, Knight Support rules, map definitions, and frozen opponent sources are preserved. [Search architecture and profile settings](CpuArchitecture.md)

## Measuring the original search

The baseline is the 8D.1 tree at `b759f59` with the original 8C behavior. The baseline and allocation-only builds played six deterministic fixed-opening matches per difficulty against frozen static Normal on Bellflower Reach (8 × 8), Twin Basins (10 × 10), and Banner Plain (12 × 12), with seed 42 and both seats. The timing mode ran one match at a time. All 30 histories, results, per-turn depths, and pair counts matched. The measurements use Release on a four-core i5-7500, Windows x64 and .NET 10.0.15.

On six early, middle and late Banner Plain replay positions, isolated sampling attributed about 2.95 KB per hypothetical resolution to board mutation and support processing. Territory plus legal generation added about 2.1 KB before optimization and 0.9 KB after. Tactical-board setup and ranking of both sides allocated about 42 KB per sampled child before optimization and 18 KB after. Influence and feature accumulation reused scratch arrays with zero measured per-call allocation. It remained a material part of CPU time, as did territory traversal and ranking. Isolated timings varied across runs. Full-match comparisons below include all search overhead.

Territory classification now uses a compact traversal buffer on catalog-sized maps and rents it for larger boards. Its classification and legal sets use the same traversal and rules. Tactical ranking walks neighboring cells with a value iterator. Retaining root children avoids a second authoritative resolution. The deeper pair cache reuses both the child state and its immediate score. The cache deliberately omits general cross-branch board hashing and cached depth-specific ratings. They would need complete state and search context keys and have yet to show a benefit in this workload.

| Difficulty | Main decisions | Baseline median / P95 / max ms | Allocation-only median / P95 / max ms | Mean pairs | Baseline → optimized pairs/s | Mean bytes before → after |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Easy | 348 | 2.21 / 5.29 / 39.72 | 2.21 / 4.89 / 39.30 | 19.1 | 7,902 → 8,610 | 213,999 → 173,179 |
| Normal | 366 | 1.43 / 8.86 / 41.43 | 1.52 / 8.69 / 39.92 | 42.6 | 19,437 → 19,919 | 349,220 → 284,054 |
| Hard | 268 | 8.02 / 23.38 / 60.46 | 7.56 / 28.50 / 59.42 | 230.1 | 25,806 → 27,053 | 1,857,278 → 1,301,151 |
| Very Hard | 284 | 12.65 / 19.18 / 70.33 | 11.69 / 17.03 / 70.90 | 331.2 | 25,904 → 28,227 | 3,051,210 → 2,074,649 |
| Monster | 228 | 13.43 / 25.31 / 116.76 | 12.41 / 24.33 / 117.16 | 563.0 | 31,204 → 33,236 | 4,425,814 → 3,071,362 |

Allocation traffic fell and simulated-pair throughput rose across all five levels. Normal median and Hard P95 rose slightly in this sample. Easy and Normal always completed depth one. Hard, Very Hard, and Monster reached depth two on 264 of 268, 280 of 284, and 226 of 228 decisions respectively. There were zero budget or fallback stops in these fixed-opening timing runs. Allocation traffic measures bytes allocated on the decision thread. Pause duration and heating require on-device measurements.

## Wider and deeper configurations

The following comparisons used fixed openings, both seats, seed 42, and the three tuning maps above. The narrow and large settings used the 18/10 root limits, with 2/2 and 3/2 continuation breadth respectively. The four-turn configuration used 2/2 continuation breadth. All deeper settings compared two root finalists. The wide setting used a 22/14 root matrix. Results are wins–draws–losses from the tested side.

| Monster variant | Depth / pair cap | W–D–L versus frozen 8C | Points | Main depth mean | Mean new pairs per decision |
| --- | --- | --- | ---: | ---: | ---: |
| Wider | 2 / 2,000 | 4–2–0 | 83.3% | 1.98 | 1,336 |
| Narrower deep | 3 / 2,000 | 4–0–2 | 66.7% | 2.95 | 1,190 |
| Larger deep | 3 / 4,000 | 4–2–0 | 83.3% | 2.97 | 1,661 |
| Deeper | 4 / 8,000 | 2–2–2 | 50.0% | 3.94 | 2,629 |

There were six distinct histories per row and three map/seed blocks. The sample is too small for paired intervals. In the first held-out suite, the 4,000-pair variant scored 0–2–8 versus frozen 8C and 2–0–8 versus wider depth two, on ten matches per opponent. The five maps were Silver Bracken, Vesper Barrows, Quillstone Lawn, Wrenfield and Granite Orchard, with seed 43, fixed openings and seat swaps. This suite adds scattered rocks and interior-heavy draft layouts. In the follow-up on the same assignments, wider depth two went 4–0–6 versus frozen 8C. Narrow depth three and depth three gated by full root replies each went 2–0–8. The corresponding match-point rates were 40%, 20%, and 20%, with paired intervals of 0–80%, 0–60%, and 0–60%.

The seat-only held-out schedule kept each participant on the same physical start. With that assignment and seeds 43–44, production scored 4–0–16 against frozen 8C. Substituting frozen 8C into the production slot preserved all 20 outcomes and 18 complete histories. In the balanced follow-up with seed 43, swapping both seats and physical starts independently, production went 10–0–10 and wider depth two went 14–0–6 against frozen 8C. There were 20 distinct histories per pairing. The wider profile earned 70% of points with a paired 95% interval of 50–90%. Production scored 50% in every block, so the report suppresses its interval. The wider profile is the strongest larger-budget candidate from this sample. Its increased allocation and the absence of device measurements favor retaining the current budget.

In six fixed-opening tuning matches, a 1,300-pair Very Hard depth-three experiment went 2–2–2 against frozen Very Hard with a mean margin of +8.67. That sample did not establish a reason to increase the Very Hard default. In eight separate fixed-opening matches per lower difficulty with seat and start swaps, Easy went 8–0–0, Normal 6–0–2, Hard 0–4–4 and Very Hard 0–8–0 versus their frozen counterparts. These small direct comparisons gave each participant a different random stream. The equivalent-slot comparisons above control for that assignment and reproduced the same seeded moves at every level.

The deeper variants used narrower successor models than the two-turn pass. That makes continuations sensitive to omitted replies even with complete root coverage. This is a plausible contributor to the results, alongside starting-position effects and the small sample. Production accepts a deeper policy change only when it can prove a win against all legal defenses. The regression fixture finds a third-turn win from D3 in a dense 5 × 4 support position using 78 new resolutions and 33 reused pairs. Exhaustive independent resolution proves that no action can win in two turns and that the shallower selection, C2, cannot win in three. This establishes a tactical improvement on the fixture. The balanced tournament did not establish a broader strength gain for production.

## Probe and historical positions

The 4,000-pair variant went 4–0–2 against Dot's Probe in six natural-opening matches, and 6–0–6 over twelve fixed-opening matches with both seat and physical-start swaps. Both runs used seed 42 on Pocket Meadow, Little Crossing and Banner Plain. It lost both scored natural-opening matches against frozen 8C and four additional drafts required a restart.

Production Monster went 6–2–4 against Probe in the same twelve fixed-opening assignments. It went 0–0–4 on Pocket Meadow, 4–0–0 on Little Crossing, and 2–2–0 on Banner Plain. The historical small-map results differ sharply even under the same fixed-opening design. The regression suite retains the recorded Pocket Meadow loss and the redundant interior-placement and vulnerable support fixtures. The larger-board replays include multiple fronts and dense late positions. The 8D.3 evaluator can use the recorded initial moves leading to the Pocket Meadow losses and compare interior and frontier choices before the result becomes forced.

For example, match `eba31ae0f743f30ca3155783` on Pocket Meadow was level at 1–1 after five Main turns before the B3 collision ended it 4–1 to Probe. Every legal final move lost against the observed reply. Banner Plain flagged 54 of the production opponent's 178 placements as redundant by the existing interior proxy. The proxy counts post-resolution changes under the actual reply and depends on both players' choices.

## Responsiveness

These dedicated sequential samples used the same three tuning maps and fixed-start schedule against static Normal. Depth counts show the completed comparison used for selection. The selected and completed counts matched in these samples. Pair throughput divides new simulations by total decision time.

| Monster variant | Decisions | Median / P95 / max ms | New pairs per decision | Pairs/s | Mean allocation | Depth 1 / 2 / 3 | Reused pairs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Production, 450 ms | 228 | 13.43 / 53.71 / 168.82 | 588 | 33,296 | 3.01 MiB | 2 / 226 / 0 | 224 |
| Wider depth two, 450 ms | 216 | 29.07 / 61.70 / 238.20 | 1,179 | 35,700 | 5.98 MiB | 6 / 210 / 0 | 0 |
| 4,000-pair depth three, 1,500 ms | 254 | 61.70 / 79.82 / 235.95 | 1,625 | 30,932 | 10.90 MiB | 4 / 0 / 250 | 25,444 |

Production reached its pair limit eight times and kept a completed lower tier. The other profiles had zero budget stops. All three recorded zero elapsed-time stops and zero static fallbacks. The tactical proof fixture reached depth three under the production budget. The broader timing sample shows the cost of unsuccessful refinement in its higher P95.

On a 12 × 12 simultaneous position, a deliberately short 25 ms safeguard stopped all 12 warmed decisions with 0.074 ms median and 0.807 ms maximum overshoot. Solicited cancellation during a depth-six workload stopped all 12 attempts, with 0.075 ms median and 1.881 ms maximum acknowledgment after the request. These are cooperative limits on the tested Windows host.

In a separate 30-decision loop on the same board, production allocated 104.1 MiB and recorded 18/15/0 generation 0/1/2 collections. The experimental depth-three profile allocated 318.6 MiB and recorded 63/15/7 collections. These counts describe a repeated-position desktop stress sample.

## Verification and next experiment

The Windows solution build passed with warnings treated as errors. All 316 automated tests passed, including checks for all five difficulties on all 100 maps, deterministic work limits, terminal proofs, selected depth, cancellation, independent cached work, and tournament manifest overrides. Both Godot runtime regression scripts passed. An 8D.1 tournament match and ten legacy harness histories replayed through the current resolver.

The ARM64 .NET publish and signed debug APK export succeeded. ADB found zero devices or wireless debugging services, so Android gameplay, sustained responsiveness, pauses and heating remain unmeasured.

For 8D.3, start with the recorded Pocket Meadow loss and the Banner Plain interior placements. Improve the evaluator in isolation against fixed 8C and 8D.2 search settings, then test wider opponent coverage on fresh maps with both assignment swaps. Measure the promising 2,000-pair configuration on the target Android device before changing the production budget.

## Reproduce

```powershell
dotnet build Benchmarks/CastrumTactics.Benchmarks.csproj -c Release
$bench = 'Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll'
dotnet $bench tournament dry-run --config Benchmarks/Examples/search-comparison.json
dotnet $bench tournament run --config Benchmarks/Examples/search-comparison.json
dotnet $bench tournament performance --id 8d2-equivalent-monster --format head-to-head --participants 'candidate=frozen8c:Monster,reference=static:Normal' --maps bellflower-reach,twin-basins,banner-plain --opening fixed --seeds 42 --strategic-metrics false --out exports/8d2-reproduction
```

The local runs saved to `exports/8d2/` include JSON manifests, performance exports, match histories, and the exact experimental configurations. Save output from different builds to separate directories. The frozen evaluator runs through the current domain, so measuring the original territory allocation requires using the baseline build. The 8D.1 commands and historical fixtures remain available. [Tournament configuration and replay](CpuTournaments.md)
