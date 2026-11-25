# Milestone 8C CPU benchmarking

The [8D.2 search optimization report](CpuSearchOptimization.md) records equal-effort comparisons, wider and deeper experiments, and the final production policy.

For new CPU comparisons, use the [8D.1 tournament runner](CpuTournaments.md). The commands and results below remain available to reproduce the original 8C work.

## Reproduction

From the repository root with the .NET 10 SDK, build the development-only harness in Release. The executable supports `strategic:<difficulty>`, `static:<difficulty>`, and `probe`. The five labels are Easy, Normal, Hard, VeryHard, and Monster.

```sh
dotnet build Benchmarks/CastrumTactics.Benchmarks.csproj -c Release
dotnet Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll fixtures
dotnet Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll match --maps pocket-meadow,little-crossing,twin-basins,banner-plain,broadshield-field,amber-fallow,barrow-ring --first strategic:Monster --second probe --seed 5 --count 3 > probe.jsonl
dotnet Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll replay probe.jsonl
python Benchmarks/summarize.py probe.jsonl
```

Swap `--second probe` for `--second static:Monster` to compare against the original Monster. `--count` is the number of consecutive seed indices **per map and seat**. Both seats run by default. The tested opponent keeps its seed stream and fixed starting position when it changes seats. The archived seed formulas are `index × 104729 + 67` for the tested opponent and `index × 10949 + 1109` for its rival. Use `--mode draft` for natural drafts or `--starts A1/C3` to supply a fixed successful pair on one map. Fixed drafts use Dot's starts where available and otherwise find a widely separated pair that reaches playable Main. Remaining starts collide. In a natural draft, the Probe uses frozen Normal. The log distinguishes failed drafts, openings that score immediately, and matches that play Main.

JSON Lines include map, ruleset, opponents, seats and seeds, phase outcomes, final scores, decisions, latency, allocations, pair count, completed depth, and legal-action count. Actions and outcomes reproduce with the same runtime and inputs. Timing and allocation samples can vary with runtime and hardware. The replay command checks the complete action and score history, including automatic collisions. Keep local output files out of version control.

The local run's replayable records, replay fixtures, and rendered Windows captures are in the ignored `exports/m8c-verification` directory.

### Retired battlefield fixtures

Milestone 8C.1 moved retired layouts byte-for-byte into `Benchmarks/Reference/Maps/`. The development harness resolves historical IDs for `match`, `replay`, and `fixtures`. The commands above continue to accept Pocket Meadow, Little Crossing, and Barrow Ring. The application catalog and map selector exclude these fixtures. Historical results still refer to the layouts and opponents originally recorded. [Retired battlefield list](MapFormat.md#retired-battlefields)

The 8C.1 verification replayed 596 saved match histories. It also reran the 18 fixed-draft Monster versus Probe matches on Pocket Meadow, Little Crossing, and Barrow Ring with seed indices 5–7 in both seats. Actions, results, and search effort matched the earlier records. Timing and allocation samples were excluded from that comparison.

### Frozen controls and provenance

The benchmark sources under `Benchmarks/Frozen` are the original five AI files from `afb0dcbab5b789ed8820017c84648ed4dd3bbe14`. A separate assembly alias keeps frozen Normal's rankings independent of the production evaluator. `Benchmarks/ArchivedProbe.cs` preserves the archived Probe functions and counts their pair resolutions. Probe takes all own actions up to ten, otherwise the top ten from frozen Normal. It takes all replies up to eight, otherwise the top six from frozen Normal, and adds a legal omitted collision. Its original score is 65% mean and 35% worst with seeded ties. The nonterminal weights are 0.95 secured lead, 0.65 knight lead, 0.50 anchor lead, and 0.25 legal-move lead. Terminal values are ±150 plus margin, or zero for a draw.

The comparison harness uses the current domain for every opponent on the same map. For exact historical reproduction, use the owner-provided archive in the external project files directory recorded in `AGENTS.md`. The archive holds the frozen Domain and AI assemblies and exact historical harness. From an extracted copy, direct `CASTRUM_TRACE_JSONL` to a fresh file and run `dotnet run -c Release --project bench -- normal probe duel`. Compare the sorted result rows and trace objects to the archived references. The repository retains the reference CSVs, manual moves, Pocket Meadow pair fixture, and archive README. The large historical trace and compiled assemblies stay in the original archive.

On Windows with .NET SDK 10.0.404, the archive harness reproduced **1,968 of 1,968** CSV results and full action histories, including 1,440 Normal trials, 240 Probe trials, and 288 Monster–Very Hard duels. The bundled assembly hashes matched the archived manifest. The new fixture runner also reproduces the frozen Monster selections and scores throughout both recorded manual matches.

### Interpretation

The report counts an avoidable immediate defeat when a different legal choice would have prevented a terminal loss against the actual revealed reply. This retrospective count neither predicts what a different opponent would have selected nor proves a universally safe action. An interior-placement proxy counts surviving placements where pre-turn friendly graph strength already leads by at least three and the resolved pair gives no secured gain, at most 0.10 extra weighted influence or mobility, and at most 0.25 extra formation. Both players move in that comparison, so opponent choices can affect the proxy.

## Fixed-draft results

All results below give wins, draws, and losses from the first opponent's perspective. Each map uses seed indices 5–7 in both seats, with the same fixed starts across pairings. Pocket Meadow, Little Crossing, Twin Basins, and Banner Plain were used during development. Broadshield Field, Amber Fallow, and Barrow Ring were held out from the initial tuning. Some seeds take identical branches because Monster often has a unique best choice.

| Map | Strategic Monster vs frozen Monster | Strategic Monster vs Probe | Frozen Monster vs Probe |
| --- | --- | --- | --- |
| Pocket Meadow | 6–0–0 | 0–0–6 | 0–0–6 |
| Little Crossing | 6–0–0 | 0–0–6 | 0–0–6 |
| Twin Basins | 6–0–0 | 6–0–0 | 0–6–0 |
| Banner Plain | 6–0–0 | 6–0–0 | 0–0–6 |
| Broadshield Field | 6–0–0 | 6–0–0 | 0–0–6 |
| Amber Fallow | 6–0–0 | 6–0–0 | 6–0–0 |
| Barrow Ring | 2–4–0 | 4–2–0 | 0–0–6 |
| **Total** | **38–4–0** | **28–2–12** | **6–6–30** |

Against the same Probe, the new Monster had zero immediately avoidable losses in 42 matches, versus 14 for the frozen Monster. The interior-placement proxy fell from 432 of 1,042 placements (41%) to 142 of 680 (21%). Match lengths and positions differed across policies. The two small maps remain Probe weaknesses. The strategic CPU's losses on them were already forced by the time an immediate defeat could occur against the revealed reply.

Reversing which opponent received each physical start in Little Crossing changed the outcome to four wins in four games against Probe, using seed indices 8–9. Pocket Meadow remained 0–0–4 and Twin Basins remained 4–0–0 after reversing the starts. The two-map small-board result includes a starting-position sensitivity.

On the catalog expansion's 12 × 12 Quillstone Lawn and Silver Bracken, Monster went 4–0–0 per map against each control with seed indices 8–9. These add sparse terrain and scattered rock formations, interior starts, and several expansion routes to the rectangular and open-map suites.

The separate natural-draft sample used seed indices 8–9 on Pocket Meadow, Twin Basins, Banner Plain and Dawnstone Green. Strategic Monster won all 12 matches that reached Main. All four Pocket Meadow drafts collided and needed a restart. In a larger Pocket Meadow-only sample with seed indices 1–24, 26 of 48 drafts needed a restart and the Probe won the other 22. Probe used frozen Normal for the draft. The larger sample confirms Pocket Meadow remains a Main-phase weakness and illustrates the high variance of two-start drafts.

### Recorded positions

- **Pocket Meadow, Main turn 4.** The fixture enumerates all four own choices against all six opponent choices. The historical B2 versus B1 branch ends 1–5. B1 versus B1 stays at 1–1. The production Monster chooses C1, which survives every immediate reply on this board. The two-turn comparison finds a worse continuation for B1 even though B1 survives the recorded reply.
- **Open Meadow, 11–9.** Frozen Monster and both scores reproduce across all 19 Main turns. Strategic Monster diverges at 11 turns. On turn 10 it moves from the central engagement at E4 to D1. On turn 17 it selects C1 instead of D1 against A2, keeping the score at 1–1 where the recorded pair produced 1–9. Each branch starts from the original position. The one-turn branches leave their eventual results unmeasured.
- **Little Crossing, 5–6.** Frozen Monster and both scores reproduce across all eight Main turns. Strategic Monster diverges at five turns. On turn 7 it selects C1 instead of B3 against C2, giving 2–2 where the original turn produced 2–6. On the recorded final board it chooses the same C2. The dedicated boundary fixture separately verifies legal castle construction can secure a region immediately.
- **Redundant expansion and Runaway Knight.** The AI regressions contrast two legal, formally contested interior placements with a productive frontier move. They also check a beneficial interior upgrade, invasion surviving with mutual support, and indirect elimination after the last supporter is captured.

### Difficulty ladder

After an initial one-turn Hard lost its adjacent comparison with Normal (2–4–6), Hard gained a narrower second-turn pass. The fixed-draft ladder sample used Pocket Meadow, Twin Basins, and Banner Plain, with each seed side-swapped.

| Matchup | Seed indices | W–D–L |
| --- | --- | --- |
| Normal vs Easy | 8–9 | 6–4–2 |
| Hard vs Normal | 8–9 | 10–2–0 |
| Very Hard vs Hard | 8–9 | 10–2–0 |
| Monster vs Very Hard | 8–9 | 4–8–0 |

Each stronger level led overall in this sample. Normal split Twin Basins with Easy. Monster beat Very Hard on Banner Plain and drew on Pocket Meadow and Twin Basins. These matches establish a provisional ladder with substantial map and position effects.

## Draft collision correction

The paired comparison uses the initial 8C implementation at `f152749` and the corrected draft search. Easy, Normal, and Monster each played four seed indices (8–11) in both seats on five maps, for 120 matches per version. The rival used frozen Normal in the draft and the archived Probe in Main. The maps were Pocket Meadow (2 starts), Silver Bracken (10), Broadshield Field (11), Vesper Barrows (12), and Sixteen Standards (16).

Build the Release harness from each revision, then run the following commands once for each difficulty and version. Use the matching values for `difficulty` and `version`.

```sh
dotnet build Benchmarks/CastrumTactics.Benchmarks.csproj -c Release
dotnet Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll match --maps pocket-meadow,broadshield-field,silver-bracken,vesper-barrows --first "strategic:$difficulty" --second probe --seed 8 --count 4 --mode draft > "$version-$difficulty.jsonl"
dotnet Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll match --maps sixteen-standards --first "strategic:$difficulty" --second probe --seed 8 --count 4 --mode draft > "$version-sixteen-$difficulty.jsonl"
python Benchmarks/summarize.py "$version-$difficulty.jsonl" "$version-sixteen-$difficulty.jsonl"
```

Collision rates use submitted draft rounds. Automatically resolved final collisions are forced by the rules and excluded from that denominator. A failed draft requires a restart. Opening latency and preview counts refer to the strategic CPU. Latency is wall-clock time on the Windows system described below. Draft paths and the mix of positions can differ between versions.

| Difficulty | Collisions before → after | Failed drafts | Reached Main | Median / P95 / max latency before → after | Median / max previews before → after |
| --- | ---: | ---: | ---: | --- | --- |
| Easy | 32/208 → 38/210 | 4/40 → 4/40 | 36/40 → 36/40 | 1.14 / 5.99 / 11.53 → 1.15 / 7.02 / 13.23 ms | 42 / 64 → 43 / 64 |
| Normal | 54/216 → 54/216 | 4/40 → 4/40 | 36/40 → 36/40 | 1.42 / 7.71 / 16.67 → 1.37 / 7.15 / 22.08 ms | 49 / 112 → 49 / 118 |
| Monster | 60/220 → 60/220 | 4/40 → 4/40 | 36/40 → 36/40 | 1.28 / 6.60 / 36.91 → 1.28 / 5.97 / 27.80 ms | 49 / 192 → 49 / 196 |

Overall, collisions rose from 146/644 submitted rounds (22.7%) to 152/646 (23.5%). All 12 failed drafts in each version were on Pocket Meadow, where the common shortlist already covered both starts. There were 108 transitions to Main and zero immediately scored openings in each version. Easy changed 10 draft histories on Sixteen Standards and Vesper Barrows. All six additional collisions were on Sixteen Standards. Normal and Monster reproduced their original draft histories. A collision can deny a valuable start, so its frequency alone does not establish draft quality.

On the first Sixteen Standards decision, Easy narrowed the common replies from four to three and used 61 previews instead of 64. Normal narrowed from seven to six and used 106 instead of 112. Monster kept 12 common replies and used 196 instead of 192. These include the omitted same-square previews. Median times were close across the sample, while the high-percentile and maximum times moved in both directions. The configured pair caps stayed fixed.

All 240 histories replayed through the domain. The local JSON Lines are in the ignored `exports/m8c-draft-collision` directory.

## Windows search performance

The following Release-build measurements use Windows x64, Intel Core i5-7500 at 3.40 GHz, .NET SDK 10.0.404 and runtime 10.0.15. The harness warms on Pocket Meadow and times successive decisions in full fixed-draft matches with diagnostics enabled. These samples include early and late states. Allocations are the calling thread's bytes per decision, including simulated boards and diagnostics. They measure allocation traffic rather than retained heap size.

| Monster against frozen Monster | Decisions | Median / P95 / max latency | Median / max pairs | Median allocation |
| --- | ---: | ---: | ---: | ---: |
| Pocket Meadow, 3 × 3 | 30 | 6.39 / 16.24 / 22.03 ms | 335 / 801 | 388 KiB |
| Twin Basins, 10 × 10 | 108 | 14.07 / 65.83 / 73.85 ms | 630 / 667 | 3,766 KiB |
| Broadshield Field, 12 × 10 | 464 | 19.13 / 26.67 / 78.60 ms | 630 / 742 | 5,029 KiB |
| Banner Plain, 12 × 12 | 756 | 23.25 / 30.07 / 67.70 ms | 630 / 745 | 6,599 KiB |

Across the two strategic fixed-draft suites, 3,248 of 3,304 decisions completed depth two. The highest observed latency across these measurement passes was 113.70 ms. The final pass's 1,348 decisions with at least 16 legal actions against frozen Monster took 20.69 / 29.84 / 78.60 ms median / P95 / max. Compact domain selection sets, allocation-free neighbor iteration, and reuse of candidate-ordering scratch data reduced allocation traffic while the benchmark actions stayed identical. The 12 × 12 sample still allocated about 6.4 MiB per median decision.

The complete solution build, 290 automated tests, both Godot 4.7.2 headless regressions, and DocFX metadata and site builds with warnings treated as errors passed. The CPU match regression also passed with the Windows OpenGL renderer and saved rendered gameplay frames.

## Android verification

The ARM64 .NET publish and signed Godot debug APK export passed. The isolated package `com.castrumtactics.milestone8c` installed alongside the existing game on a Sony XQ-DQ54 running Android 15. Godot startup appeared in the device log behind the lock screen. On-device gameplay and decision latency remain unverified while the handset is locked.

## Follow-up for 8D and 8E

- Stratify 8D competition by draft mode, map family, and position size. Increase seed diversity and retain match histories for tactical audits. Investigate small-board Probe losses first, where a continuation can become forced several turns before the final score moves.
- Measure allocation pressure and sustained response time on Android across dense late-game positions. Tune pair budgets independently of strategic weights when the mobile measurements warrant it.
- In 8E, use several opponent models and a held-out map set to distinguish map imbalance from CPU-specific blind spots. Separate draft restart rates from scored outcomes and compare natural versus fixed openings.

Use the [small-board solver](SmallBoardSolver.md) to certify reachable positions, inspect decision payoffs and compare seeded CPU policies against optimal play. Store generated reports and tablebases under exports/.
