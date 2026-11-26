# Milestone 8E CPU difficulty calibration

The baseline is the integrated 8D.4 revision `7ec9a71c0f7a260c013fb08468db76af156692f3` (PR #22). All 15 AI source files from that revision are byte-for-byte copies in the separate `CastrumTactics.Frozen8D4` assembly. The game has no reference to that assembly. The tournament harness exposes the copy as `frozen8d4:<difficulty>` and fingerprints the shared Domain and each opponent binary.

Tests compare the seeded decision, legal action, ranking scores, sampling probabilities, resolved pair count and selected depth on both seats across 100 maps, five profiles, two rulesets and progressive resolved positions. In Knight Support, the reference uses unchanged 8D.4 settings. Classic uses the static policy in all five profiles. [CPU architecture](CpuArchitecture.md)

## Baseline profiles and trials

| Difficulty | Draft | Main cutoff and selection | Main depth / root own × replies / pairs | Draft / Main temperatures |
| --- | --- | --- | --- | --- |
| Easy | Legacy expected/adverse with diversity | Standard evaluation and broad score-based sampling | 1 / 5 × 4 / 64 | 0.90 / 1.00 |
| Normal | Legacy expected/adverse with diversity | Standard evaluation and narrower score-based sampling | 1 / 8 × 6 / 120 | 0.75 / 0.60 |
| Hard | Legacy expected/adverse with diversity | Standard evaluation and a narrow second comparison when it fits | 2 / 12 × 8 / 360 | 0.65 / 0.30 |
| Very Hard | Legacy expected/adverse with diversity | Standard evaluation in 8D.4. The candidate uses resilient territorial evaluation. Both use score-based sampling. | 2 / 14 × 9 / 500 | 0.55 / 0.12 |
| Monster | Terrain-aware equilibrium, depth 1, 16 starts, 256 pairs, no Main preview | Resilient evaluation, numerical matrix sampling, verified compact proof pass | 3 / 18 × 10 / 850 | 0.50 / 0, for score-based fallback |

The scene uses a 450 ms elapsed guard for every difficulty. Score-based openings use the original diversity shares of 0.20 for normal rounds and 0.70 when both players still need a foothold and only two starts remain. The Monster policy uses its mixed distribution when the numerical solve completes. Imported configuration, analysis commands and frozen implementations are for research. The production game continues to use public resolved snapshots, the worker thread, cancellation and session/round validation.

## Experimental design

The baseline round-robin used The Commons, Open Meadow, Honeystone Green and Twelve Towers with seeds 1229 and 1361. The first 8E tuning set used Alder Common, Banner Plain, Bellflower Reach and Twin Basins. Validation used Briar Commons, Crownfall Basin, Linnet Plain and Sentinel Pasture. Final holdout used Flint Crescent, Mossglass Plain, Osprey Heights and Whitehart Plain with seeds 4903, 5059 and 5279. Canonical terrain plus start layouts did not overlap between these partitions under rotation and reflection. Compact opening experiments have their own explicit archived map subset. The size study and the catalog audit have different research questions and were excluded from profile selection.

Natural games swap seats. Fixed games also swap physical assignments independently. Each contestant has an independent Main and draft stream. Reusing a stream alias across policy variants controls for changes in participant labels. Tournament comparisons usually omit elapsed limits to reproduce work exactly. The matched timed Monster validation uses the same 450 ms guard for both players. Scored points are 1 for a win, 0.5 for a draw and 0 for a loss, with restart-required openings and technical failures tracked separately. A rare adjudicated terminal draft remains scored.

The reviewed schedules produced 7,167 scored games and 205 restart-required drafts across 7,372 attempts, including controls and repeated verification. There were zero technical failures or round-limit cases. The compact export is `Benchmarks/Reference/cpu-calibration-8e.json`. It preserves raw W–D–L, score margins, playable-area-normalized margins, turns, collision counts, work and restart denominators. The history-aware summary normalizes by participant seat and applies geometry that preserves terrain and starts. Its exploratory 95% intervals resample maps and then independent seed blocks within each map after identical blocks are collapsed. They do not include uncertainty about unsampled policy matchups or human play, and endpoints of 0% or 100% can reflect a homogeneous small sample.

## Calibration result and final ladder

Very Hard now uses the resilient evaluator in Knight Support Main, preserving its 500-pair limit, legacy draft, risk, breadth, temperature and non-matrix sampler. The evaluator rewards support resilience, influence and invasion reach. Very Hard can plan a second comparable turn while Monster also uses mixed strategies and checks deeper terminal lines. Easy remains a broad tactical policy for learning, Normal retains its narrower tactical search, and Hard keeps the two-turn standard evaluator. These distinctions describe the policies. The evidence below measures CPU matchups.

On the first baseline, Normal vs Easy scored 7–1–6 (53.6%) natural and 20–0–12 (62.5%) fixed. Natural draft isolation favored Easy on the tuning maps when both sides used identical Hard Main. Very Hard also trailed Hard in draft isolation. This points to opening quality and matchup variation, while the fixed baseline shows that Main can compensate. Reducing Normal's Main temperature produced 37.5% against frozen Normal and no gain against Easy. Resilient Normal scored 93.8% against Easy and won every tuning match against frozen Normal. On the independent fixed validation ladder it tied Hard and scored 66.7% against Very Hard, while using the unchanged legacy draft. Resilient evaluation for all three intermediate levels also inverted the fixed Hard–Very Hard comparison. Those changes were rejected.

The advanced equilibrium Normal opening scored 84.4% against Easy with a shared Hard Main on tuning maps. On release validation, Monster and advanced Normal restarted 12 of 16 natural games, so their successful games are a selected subset. The pure and near-pure draft choices can coincide when both use the same opening evaluator. The release retains the legacy openings for Easy through Very Hard.

Very Hard's resilient Main defeated its frozen counterpart on the tuning fixed starts and preserved substantially better separation from Hard in validation. It was the sole pinned release candidate. The predeclared holdout gate required at least 55% against Hard in each opening mode, and Monster at least 45% in each mode and above 50% pooled. Very Hard scored 83.3% natural and 91.7% fixed against Hard. On the same maps and streams, the frozen profile scored 68.8% and 64.6%. Monster earned 83.3% natural and 54.2% fixed against calibrated Very Hard. The guard and deterministic budget remain shared across platforms. The production rerun reproduced all 720 candidate histories and retry outcomes exactly.

Each cell below gives W–D–L, raw points per scored match, canonical distinct histories, and the exploratory 95% map/seed block interval. A dash means there were too few usable independent blocks. The compact evidence also gives scored/restarted counts, raw margins and work by pairing. The remaining adjacent weakness is Normal versus Easy. Normal scored 62.5% natural and 43.8% fixed, with intervals that permit either ordering. The held-out natural Hard–Very Hard separation is larger than the original. Fixed Monster–Very Hard narrowed and its uncertainty includes a possible inversion. Human difficulty still needs separate validation.

| Higher vs lower | 8D.4 natural | 8E natural | 8D.4 fixed | 8E fixed |
| --- | --- | --- | --- | --- |
| Normal vs Easy | 14–2–8 · 62.5% · 24 · 22.9%–95.8% | 14–2–8 · 62.5% · 24 · 22.9%–95.8% | 20–2–26 · 43.8% · 24 · 22.9%–66.7% | 20–2–26 · 43.8% · 24 · 22.9%–66.7% |
| Hard vs Easy | 23–1–0 · 97.9% · 24 · 91.7%–100.0% | 23–1–0 · 97.9% · 24 · 91.7%–100.0% | 44–2–2 · 93.8% · 24 · 81.2%–100.0% | 44–2–2 · 93.8% · 24 · 81.2%–100.0% |
| Very Hard vs Easy | 24–0–0 · 100.0% · 24 · 100.0%–100.0% | 23–0–1 · 95.8% · 24 · 83.3%–100.0% | 42–0–6 · 87.5% · 24 · 70.8%–100.0% | 48–0–0 · 100.0% · 24 · 100.0%–100.0% |
| Monster vs Easy | 24–0–0 · 100.0% · 24 · 100.0%–100.0% | 24–0–0 · 100.0% · 24 · 100.0%–100.0% | 48–0–0 · 100.0% · 24 · 100.0%–100.0% | 48–0–0 · 100.0% · 24 · 100.0%–100.0% |
| Hard vs Normal | 22–0–2 · 91.7% · 24 · 79.2%–100.0% | 22–0–2 · 91.7% · 24 · 79.2%–100.0% | 38–0–10 · 79.2% · 24 · 58.3%–100.0% | 38–0–10 · 79.2% · 24 · 58.3%–100.0% |
| Very Hard vs Normal | 23–0–1 · 95.8% · 24 · 83.3%–100.0% | 24–0–0 · 100.0% · 24 · 100.0%–100.0% | 40–0–8 · 83.3% · 24 · 66.7%–100.0% | 48–0–0 · 100.0% · 24 · 100.0%–100.0% |
| Monster vs Normal | 24–0–0 · 100.0% · 24 · 100.0%–100.0% | 24–0–0 · 100.0% · 24 · 100.0%–100.0% | 48–0–0 · 100.0% · 24 · 100.0%–100.0% | 48–0–0 · 100.0% · 24 · 100.0%–100.0% |
| Very Hard vs Hard | 16–1–7 · 68.8% · 24 · 45.8%–89.6% | 20–0–4 · 83.3% · 24 · 58.3%–100.0% | 30–2–16 · 64.6% · 24 · 41.7%–85.4% | 44–0–4 · 91.7% · 24 · 75.0%–100.0% |
| Monster vs Hard | 22–1–1 · 93.8% · 24 · 81.2%–100.0% | 22–1–1 · 93.8% · 24 · 81.2%–100.0% | 48–0–0 · 100.0% · 24 · 100.0%–100.0% | 48–0–0 · 100.0% · 24 · 100.0%–100.0% |
| Monster vs Very Hard | 24–0–0 · 100.0% · 24 · 100.0%–100.0% | 20–0–4 · 83.3% · 24 · 70.8%–95.8% | 44–2–2 · 93.8% · 24 · 83.3%–100.0% | 24–4–20 · 54.2% · 24 · 16.7%–85.4% |

For adjacent replay examples, use the fixed production holdout and `tournament replay --out exports/8e/holdout-production-fixed --id <match-id>`.

| Matchup | Higher profile win | Lower profile win |
| --- | --- | --- |
| Normal vs Easy | flint-crescent `cb636016fc6478865dbac279` | flint-crescent `b2cd2fa3ce2ff2dbb03b16f7` |
| Hard vs Normal | flint-crescent `b204f4d5c1221776041ca32e` | flint-crescent `71d3ab34facbac01c76517be` |
| Very Hard vs Hard | flint-crescent `ffbc7b52fe9cb7a1428416b7` | mossglass-plain `19b9f60730348abff310c5a3` |
| Monster vs Very Hard | flint-crescent `20ced295992d7d8bcd3cd9b4` | flint-crescent `53b41e318ee476ddd4f200d8` |

## Monster work and coverage

The cap-only and search variants used common candidate and reference stream IDs, one fixed seed and four tuning layouts with four assignment variants per map. Each row scheduled 16 games. Four independent map/seed blocks give limited precision. Pairs are the candidate's mean new authoritative resolutions per Main decision.

| Trial | W–D–L | Scored points | Mean pairs |
| --- | --- | ---: | ---: |
| 850 control | 4–12–0 | 62.5% | 186 |
| 2,000 | 4–12–0 | 62.5% | 242 |
| 4,000 | 4–4–8 | 37.5% | 1666 |
| 8,000 | 2–6–8 | 31.2% | 2910 |
| 24 × 16, 850 | 10–6–0 | 81.2% | 354 |
| 18 × 24, 2,000 | 4–6–6 | 43.8% | 455 |
| 26 × 18, 2,000 | 10–4–2 | 75.0% | 473 |
| 32 × 24, 4,000 | 6–4–6 | 50.0% | 886 |
| Depth 2, 2,000 | 4–12–0 | 62.5% | 210 |
| 8 × 8 continuations, 4,000 | 4–4–8 | 37.5% | 1666 |
| 8 × 8 continuations, six finalists, 8,000 | 2–6–8 | 31.2% | 2916 |

The matched 850 control can deviate from 50% in a small comparison because participants use independent choices. The 2,000-pair ceiling produced no scored-point gain on that schedule over the 850 control. Larger caps enabled more continuations and reduced points. Broad 24 × 16 at the existing cap scored 81.25% in tuning. It fell to 22–0–26 in timed fixed validation and scored 6–1–0 with 17 retries in natural validation. The conditional natural estimate is based on very few successful openings. This combination did not justify promoting the broader profile. The complete comparable tier still controls selection, and a deeper proof still needs every legal reply. Wide roots commonly use most effective work in the first complete pass, before budget and comparable-coverage checks stop or reject a deeper one.

On the initial four-map integration check, live Monster against frozen 8D.4 scored 0–4–1 (40.0%) natural and 4–4–8 (37.5%) fixed. Live and frozen matched directly in catalog parity. Their aggregate match outcomes on that small schedule reflect the two assigned streams. Frozen 8D.3 Monster against 8D.4 scored 1–1–5 (21.4%) natural and 4–4–8 (37.5%) fixed. Its Main policy is shared, and the extra advanced opening has no effect on fixed starts. Monster's release profile and device guard retain the 8D.4 settings. No adaptive search algorithm or cache changes were promoted.

## Opening sensitivity

The one-round terrain-aware equilibrium draft was compared to frozen 8D.3 Monster draft under frozen 8D.4 Easy, Hard and Monster Main. All three tests used the same maps, seeds, policies' stream aliases and seats. This controls the schedule discrepancy in the earlier Easy sensitivity result. The controlled runs isolate the draft and keep the remainder of each match on the common Main policy. Deeper and preview trials use the same authoritative continuation rules and charge executed draft and Main previews to the draft budget.

| Trial | W–D–L | Points | Restarts | Submitted collisions / rounds |
| --- | --- | ---: | ---: | --- |
| Advanced vs legacy with Easy Main | 14–1–7 | 65.9% | 2 | 44 / 114 |
| Advanced vs legacy with Hard Main | 15–1–6 | 70.5% | 2 | 44 / 114 |
| Advanced vs legacy with Monster Main | 11–4–7 | 59.1% | 2 | 44 / 114 |
| Deeper draft matrix | 3–9–2 | 53.6% | 10 | 62 / 82 |
| Deeper matrix with one Main preview | 4–5–7 | 40.6% | 8 | 56 / 81 |

Both shared Easy and shared Hard now favor the advanced opening on the same schedule. Monster Main narrowed that advantage. The deeper draft and limited Main preview failed to establish a gain. Collision timing and the realized castle assignment still depend on the opposing draft. Deliberately replayed starts in the battlefield audit measure reach and first actions on comparable boards. They cannot establish optimal long-term expansion. [Battlefield audit](BattlefieldBalance.md)

## Mathematical regressions

Pocket Meadow, Corner Watch and Stone Center reproduce zero root outcome and margin values under the configured certified interval solves. Both players can independently guarantee win or draw on those specified roots, and neither can guarantee an exact draw. The solver's Boolean analysis establishes these statements separately from the matrix expectations. Pocket Meadow still has the unique B2 opening Main action for both sides. The exact/certified study measures a policy against all legal replies with solved continuations and reports the outcome and margin security gaps separately. Displayed production matrices use modeled replies and approximated continuations.

The historical Pocket Meadow line after A1/C3 and A2/B2, B2/A2, B1/B2 remains a forced Blue win. The historical B2/B1 ends 1–5 and B1/B1 continues at 1–1 in a still-lost position. The independent regression suite also covers the second-turn mixed defense, the objective-sign counterexample, and the 75/25 equilibrium. For Three Banners, A1/C1 certifies outcome and margin of 0. A1/B3 certifies −1 and −2. Reflection and player exchange yield the other off-diagonal entries. Under the neutral retry model these recover the unique initial B3 choice. [Opening matrix and retry assumptions](CpuOpeningStrategy.md#mathematical-draft-examples)

| Held-out map | Side–positions | Frozen Very Hard | Calibrated Very Hard | Monster |
| --- | ---: | ---: | ---: | ---: |
| Pocket Meadow | 96 | 0.162 / 0.224 | 0.172 / 0.235 | 0.138 / 0.200 |
| Corner Watch | 48 | 0.129 / 0.168 | 0.133 / 0.172 | 0.068 / 0.100 |
| Stone Center | 48 | 0.089 / 0.132 | 0.088 / 0.133 | 0.082 / 0.124 |

Entries give mean outcome / margin security gap. Smaller is better. Tuning and holdout positions use the solver's canonical geometry, player exchange and locked-health grouping. The compact samples reveal a small setback for Very Hard on Pocket Meadow and Corner Watch even though production pairing results improved. At the Pocket Meadow initial A1/C3 Main state, both live and frozen Monster gave B2 probability zero, selecting B1 for Red and B3 for Blue in this inspection. The release retains this known compact-board weakness.

### Tactical audit

Eight mirrored fixed games between calibrated and frozen Very Hard split 4–0–4. Calibrated won all four on Honeystone Green and lost all four on The Commons. There were no immediately avoidable losses under the recorded reply in either arm. Calibrated recorded 20/128 interior placements and 6/20 with a better realized-reply alternative. Frozen recorded 22/86 interior placements and 10/22 with a better realized-reply alternative. The interior flag follows the historical definition and the alternative holds the realized reply fixed. The different match lengths and opportunities limit interpretation.

Regression fixtures separately cover captures, castle upgrades, formation survival, scarce legal replies, secure territory, and the loss of a sole supporter. The measured differences in choice and margin are data for future human-facing calibration.

## Platform verification and measurement

The release harness ran on Debian 13.7 x64, AMD EPYC 9V74, with four CPUs of scheduler quota, 16 GiB memory and .NET 10.0.15 from SDK 10.0.404. Godot runtime tests use 4.7.2 .NET. Dedicated performance runs execute sequentially. Snapshot measurements replay the first successful match per map, then repeat early, middle and late resolved positions with the same decision seed. Raw worker-path runs call `Decide`. The variant pass calls the diagnostics path to record resolved pairs, selected and completed depth and elapsed stops. The full-match diagnostics also record coverage, policy support, matrix overhead, GC and peak process memory. Per-thread allocation and peak working set measure different quantities. Cancellation uses an approximately 2 ms timer on the widest snapshot. The total time from invocation includes timer scheduling and can exceed the requested delay. CPU worker timings do not measure application frame time.

| Raw worker | Warm sample count | First decision (ms) | Warm median / P95 / P99 / max (ms) | Mean allocation (KiB) |
| --- | ---: | ---: | --- | ---: |
| frozen8d4 draft | 240 | 45.26 | 0.70 / 8.87 / 14.05 / 15.86 | 146.5 |
| strategic draft | 240 | 48.13 | 0.66 / 8.46 / 16.74 / 23.80 | 146.5 |
| frozen8d4 main | 288 | 65.28 | 0.59 / 6.56 / 29.62 / 53.06 | 447.9 |
| strategic main | 288 | 64.07 | 0.68 / 9.58 / 27.96 / 52.90 | 449.4 |

| Diagnostic variant | Warm decisions | Mean pairs | Selected depths | Completed depths | Elapsed stops | Median / P95 / P99 / max (ms) |
| --- | ---: | ---: | --- | --- | ---: | --- |
| base | 96 | 141 | 1×80 / 2×16 | 1×80 / 2×16 | 0 | 0.55 / 5.61 / 27.20 / 27.20 |
| veryhard-base | 96 | 223 | 1×24 / 2×72 | 1×24 / 2×72 | 0 | 1.45 / 7.17 / 21.66 / 21.66 |
| veryhard-live | 96 | 225 | 1×24 / 2×72 | 1×24 / 2×72 | 0 | 1.91 / 9.71 / 12.43 / 12.43 |
| limit-850 | 96 | 141 | 1×80 / 2×16 | 1×80 / 2×16 | 0 | 0.48 / 4.55 / 5.09 / 5.09 |
| limit-2000 | 96 | 365 | 1×56 / 2×40 | 1×56 / 2×40 | 0 | 3.86 / 6.31 / 9.96 / 9.96 |
| limit-4000 | 96 | 842 | 1×40 / 2×56 | 1×40 / 2×56 | 0 | 4.33 / 48.90 / 63.76 / 63.76 |
| limit-8000 | 96 | 2198 | 1×24 / 2×72 | 1×24 / 2×72 | 0 | 18.54 / 77.95 / 83.90 / 83.90 |
| wide | 96 | 233 | 1×80 / 2×16 | 1×80 / 2×16 | 0 | 0.47 / 9.36 / 36.47 / 36.47 |

In the dedicated full-match diagnostic sample, live Monster averaged 201.9 new pairs in Main (26,376/s). Its modeled matrix included every legal root reply in 17/119 solved Main matrices, with mean support 2.24 and matrix work accounting for 0.2% of measured Main decision time. Opening included every legal reply in 35/35 solved matrices. Live Main reported 114 budget stops, 0 elapsed stops and 0 static fallbacks in 119 decisions. Per-process collections by generation were [18, 6, 3] and peak working set was 116.9 MiB.

On the saved widest root, the 2 ms timer cancelled 3/12 live and 11/12 frozen calls. The slowest cancelling calls returned after 2.79 and 5.83 ms from invocation. The 500 ms/2,000-pair and 1,000 ms/4,000- and 8,000-pair diagnostic configurations timed out in 0/96, 0/96 and 0/96 warm decisions respectively. These small saved workloads characterize the Cloud host and the sampled depths.

The full solution build passed with warnings treated as errors and all 361 tests passed. Both Godot headless regressions passed. The initial editor import exited successfully with a Godot Android-settings teardown message. Windows and Android managed targets compiled. Windows native launch and ARM64 Android APK export, install, runtime, frame timing and sustained or thermal measurements remain outstanding. The Cloud host had no connected phone, Android SDK deployment tool or native Windows runtime. The opt-in logger was verified in a Linux Godot run with 43 worker records. DocFX metadata and site verification were run with warnings treated as errors.

### Physical Android follow-up

A Windows workstation with the project's Godot .NET export templates and Android toolchain can build an opt-in instrumentation APK. The instrumented path still calls the standard production CPU with the 450 ms guard. It appends a CSV line after each worker call, including cancellation status, allocation and collection count deltas. File output occurs after the worker timer. Use the ordinary preset for the uninstrumented app and the instrumented preset invocation for worker measurements. [Android setup](https://github.com/ChuckreimuHakunorris/castrum-tactics/blob/main/README.md#android-export)

```powershell
New-Item -ItemType Directory -Force exports/8e-device | Out-Null
$env:CastrumDeviceTiming = 'true'
godot --headless --path . --export-debug Android exports/8e-device/CastrumTactics-debug.apk
Remove-Item Env:CastrumDeviceTiming
./scripts/Install-AndroidDebug.ps1 -Apk exports/8e-device/CastrumTactics-debug.apk
adb shell monkey -p com.castrumtactics.prototype -c android.intent.category.LAUNCHER 1
adb shell run-as com.castrumtactics.prototype find . -name m8e-cpu.csv
adb shell dumpsys thermalservice > exports/8e-device/thermal-before.txt
adb shell dumpsys gfxinfo com.castrumtactics.prototype reset
```

For the 8D.4 CPU control with the same logger, build commit `25706f5`. For repeatable startup on Windows, launch `godot --path . -- --mode cpu --ruleset knight-support --map honeystone-green --seed 42` and choose the difficulty from **New Match**.

On Android, start with a cold app process, then use **New Match** to select Monster and repeat short sessions on The Commons, Honeystone Green and Twelve Towers. Record the selected starts and actions if comparing builds. On a fresh or stopped process, launch using the preceding command. Repeat for at least three short blocks, rotating maps, and record surface temperature indicators if the phone exposes them. Pause or restart through the standard UI to test interruption and stale-move handling. Read the incremental file between blocks to retain partial results. The usual Android application data path is `files/m8e-cpu.csv` relative to `run-as`. If the path differs, use the result of `find`.

```powershell
adb shell run-as com.castrumtactics.prototype cat files/m8e-cpu.csv > exports/8e-device/worker.csv
adb shell dumpsys gfxinfo com.castrumtactics.prototype framestats > exports/8e-device/frames.txt
adb shell dumpsys thermalservice > exports/8e-device/thermal-after.txt
```

Record the APK revision, export mode, Android architecture and OS, rounds and collection intervals. Use aided replay on desktop for identical snapshots. The device harness observes normal matches and does not automate an identical-snapshot replay or estimate battery drain. The developer's qualitative Monster result on Honeystone Green was generally strong and responsive, with some apparently redundant placements. The history was unavailable for reproduction.

## Reproduction

```sh
dotnet build CastrumTactics.sln -warnaserror
dotnet test CastrumTactics.sln --no-build
dotnet build Benchmarks -c Release -warnaserror
bench=Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll
python3 Benchmarks/Examples/8e/make_experiments.py exports/8e/config
for study in baseline-natural baseline-fixed monster-natural monster-fixed catalog; do
  dotnet "$bench" tournament run --config "Benchmarks/Examples/8e/$study.json" --out "exports/8e/$study"
done
for spec in exports/8e/config/*.json; do
  study=$(basename "$spec" .json)
  if [ "$study" = snapshot-variants ]; then continue; fi
  dotnet "$bench" tournament run --config "$spec" --out "exports/8e/$study"
done
dotnet "$bench" tournament performance --config Benchmarks/Examples/8e/performance.json --out exports/8e/performance-paired
for phase in draft main; do
  for player in frozen8d4 strategic; do
    dotnet "$bench" snapshot-performance exports/8e/baseline-natural "$player" "$phase" 24 450 "exports/8e/snapshot-$phase-$player.jsonl"
  done
done
dotnet "$bench" snapshot-performance exports/8e/baseline-natural exports/8e/config/snapshot-variants.json main 8 450 exports/8e/snapshot-variants.jsonl
godot --headless --path . --import
godot --headless --path . --script Tests/Presentation/DraftLayoutCheck.gd
godot --headless --path . --script Tests/Presentation/CpuMatchCheck.gd -- --seed 42
dotnet tool restore
dotnet docfx metadata Documentation/docfx.json --warningsAsErrors
dotnet docfx build Documentation/docfx.json --warningsAsErrors
```

For the tactical audit and Pocket Meadow root inspection:

```sh
dotnet "$bench" tournament run --id 8e-tactical --format head-to-head \
  --participants 'final=strategic:VeryHard,baseline=frozen8d4:VeryHard' \
  --maps 'the-commons,honeystone-green' --opening fixed --swap-starts true \
  --seeds 7331 --strategic-metrics true --out exports/8e/tactical
dotnet "$bench" tournament inspect --out exports/8e/size-fixed \
  --id a0cbc9d49579fd23a4e3c937 --round 1 --opponents 'live=strategic:Monster,base=frozen8d4:Monster'
```

Use the [solver commands](CpuStrategicEvaluation.md#reproduction) with analysis sample sizes of 48 on Pocket Meadow and 24 on Corner Watch and Stone Center. For a stopped tournament, rerun with the same engine build, manifest and directory. Histories can be selected with `tournament find`, reproduced with `tournament replay`, and inspected with `tournament inspect`. [Tournament guide](CpuTournaments.md)
