# 10C.0 native rendering performance baseline

On 27 November 2026, all seven requested native runs at revision `f6add32346e3f6b3c81b67ea76692015ba979962` completed, yielding 115 accepted repetitions with scenario version 2 and JSON schema version 1. Three Windows attempts stopped after losing focus. Their partial results are retained as evidence and excluded from the baseline. The completed retries covered both requested window sizes. [Benchmark procedure and scenario definitions](RenderingBenchmarks.md)

The current renderer performs satisfactorily on the tested Windows PC and Xperia. Artwork refinement can proceed. Android turn-resolution efficiency is the principal opportunity for 10C.2, with incremental piece sprite synchronization the first candidate to investigate. [Visual development roadmap](VisualDevelopmentRoadmap.md#10c2--evidence-driven-rendering-optimization)

## Configuration

| | Windows | Physical Android |
| --- | --- | --- |
| Hardware | Intel Core i5-7500, NVIDIA GeForce GTX 1060 3 GB | Sony Xperia 1 V (XQ-DQ54), Adreno 740 |
| System | Windows 10 Home x64, build 19045, High performance power plan | Android 15, API 35, ARM64 |
| Graphics | OpenGL 3.3, NVIDIA driver 560.94, 59.973 Hz display | OpenGL ES 3.2, 60 Hz display |
| Runtime | .NET 8.0.34 | .NET 9.0.23 |

Both native debug exports use Godot 4.7.2 .NET, Compatibility/OpenGL, normal pacing with V-Sync enabled and an engine FPS cap of 0. Opposing targets and ordinary motion were enabled. Detailed rendering counters were enabled. CPU search diagnostics and viewport CPU/GPU timing were disabled. Each run used seed 42. Some profiles ran concurrently on separate devices, with one profile at a time on each device. Android device access was over wireless ADB.

The phone reported manual brightness at setting 45 and battery saver off. The portrait Standard run had USB power, and the landscape Standard and Sustained runs were unplugged. Orientation comparisons therefore also include a power-connection difference.

| Run layout | Window reported by Godot | Render texture reported by Godot |
| --- | ---: | ---: |
| Windows landscape, including Quick | 900 × 650 | 1465 × 1057 |
| Windows portrait | 720 × 1000 | 1080 × 1502 |
| Android portrait, including Quick and Sustained | 1096 × 2560 | 2503 × 5847 |
| Android landscape | 2560 × 1096 | 7017 × 3004 |

The reported textures are unexpectedly large, especially on Android. Establish the physical framebuffer and actual render-target workload before inferring pixel throughput from these dimensions.

## Frame times by scenario

Quick verified activation and output with one repetition each of Small, Turns and Monster on both devices. Each Standard run measured nine scenarios for three independent repetitions after warm-up. The table shows the median of the three per-repetition P95 and P99 values, in milliseconds. Draw calls are representative medians from the accepted Standard samples. Dynamic workloads can vary with the board reached during the run.

| Scenario | Draw calls | Windows 900 × 650 P95 / P99 | Windows 720 × 1000 P95 / P99 | Android portrait P95 / P99 | Android landscape P95 / P99 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Small | 51 | 16.79 / 16.86 | 16.83 / 16.90 | 19.66 / 21.41 | 19.82 / 21.50 |
| Large | 71 | 16.79 / 16.83 | 16.81 / 16.91 | 20.05 / 21.41 | 19.91 / 21.65 |
| Dense castles | 176 | 16.78 / 16.84 | 16.85 / 16.94 | 21.81 / 23.97 | 21.44 / 24.02 |
| Reachable damage | 237 | 16.78 / 16.85 | 16.80 / 16.86 | 21.73 / 24.85 | 21.24 / 24.07 |
| Extreme damage | 907 | 16.87 / 16.97 | 16.83 / 16.94 | 27.77 / 30.28 | 27.53 / 30.61 |
| Dense legal targets | 59 | 16.78 / 16.84 | 16.78 / 16.83 | 20.24 / 21.69 | 20.14 / 22.00 |
| Scripted turns | 92 Win / 91 Android | 16.90 / 18.67 | 16.92 / 18.64 | 22.48 / 35.25 | 22.48 / 35.15 |
| Lifecycle | 44 | 17.53 / 18.07 | 17.45 / 18.26 | 24.01 / 30.03 | 24.08 / 29.32 |
| Monster | 195 | 17.18 / 19.91 | 17.20 / 19.88 | 20.35 / 25.92 | 19.70 / 25.21 |

Windows ran at approximately 59.97 FPS across representative and extreme scenes. **2 of 64,734** Standard intervals exceeded 33.33 ms (0.0031%), both in Monster at 900 × 650. Android’s per-scenario median average FPS ranged from 59.70 to 60.16, with greater frame-time variation. **149 of 64,856** Standard intervals exceeded 33.33 ms (0.23%). Scripted turns accounted for 118 of those 149 intervals (79.2%), extreme damage for 14, lifecycle for 10 and Monster for 7. Extreme damage remains substantially more demanding than the reachable fixture. Both retained approximately 60 FPS under the tested frame pacing.

## Turn resolution and sprite synchronization

The values below pool Standard event durations across both layouts per platform using linear-interpolated percentiles. Sprite synchronization can be nested inside resolved publication and both can be nested inside submission. These timings should be read separately to avoid double-counting.

| Scenario and event | Windows events | Windows median / P95 ms | Android events | Android median / P95 ms |
| --- | ---: | ---: | ---: | ---: |
| Scripted turns, sprite synchronization | 342 | 0.32 / 0.48 | 330 | 4.45 / 7.75 |
| Scripted turns, resolved publication | 336 | 0.41 / 0.63 | 324 | 5.48 / 9.15 |
| Monster, sprite synchronization | 341 | 0.78 / 1.34 | 330 | 2.94 / 7.43 |
| Monster, resolved publication | 341 | 0.90 / 1.54 | 330 | 3.61 / 8.61 |

All 118 slow Android scripted-turn intervals share a next-frame index with a sprite-sync and resolved-publication event. The seven slow Monster intervals also share those tags. These associations and the event durations justify profiling incremental synchronization first. The event timing covers synchronization work as a whole. It does not isolate the cost of sprite recreation or establish the cause of each delayed frame.

The raw Standard data give **7.75 ms** and **7.43 ms** for Android sprite-sync P95, correcting the review figures of approximately 7.58 ms and 7.68 ms respectively. The archive identifies each event and its next-frame index, so future comparisons can use the same pooling rule. The other reviewed frame counts and draw-call medians agree with the raw data.

## Sustained Android portrait

The unplugged Monster workload measured 1,200.02 seconds after a 30-second warm-up. It averaged **60.12 FPS**, with a median interval of **16.66 ms**, **19.68 ms P95** and **25.69 ms P99**. **56 of 72,148** intervals exceeded 33.33 ms (0.078%), with a maximum interval of 50.23 ms. The first and last measured minutes each averaged 60.13 FPS, with P95 at 19.72 and 19.82 ms respectively. These observations show no meaningful sustained frame-rate deterioration in this run.

The in-window record contains **3,245 completed CPU decisions and 48 match restarts**. Worker time had a **63.86 ms median**, **91.29 ms P95**, **151.73 ms P99** and **241.37 ms maximum**. Worker timing excludes queue wait and main-thread submission. The ordinary decision path was used. Detailed search diagnostics were off, so these durations cannot establish search depth or the number of resolved pairs.

Across 81 trend snapshots, the managed heap estimate ranged from **13.64 to 37.52 MB**, starting at 23.39 MB and ending at 20.20 MB. Process-wide cumulative managed allocations increased by **3.66 GB** (decimal), about **3.05 MB/s**, including instrumentation. This is allocation traffic over time. The heap readings describe simultaneously occupied memory. Engine texture and video memory estimates grew approximately **1.40 MB** and **1.44 MB** respectively, and reported resources increased from 79 to 95. The modest growth is consistent with resource caching. The traces show no obvious leak in this workload. A single run cannot rule out leaks in other workloads or over longer sessions.

| External observation | Before | After |
| --- | ---: | ---: |
| Battery gauge | 74% | 69% |
| Battery temperature | 27.0 °C | 29.0 °C |
| Changing Android skin-sensor reading | 29.316 °C | 32.030 °C |
| Android thermal status | 0 | 0 |
| OS charge counter | 3,037,563 µAh | 2,848,905 µAh |
| Battery voltage | 3.976 V | 3.923 V |

Battery percentage is a coarse gauge. Temperatures, voltage, charge counter and thermal status are Android-reported observations at the boundaries. The service also lists an unchanged 31.223 °C skin reading. The table tracks the skin reading that changed. Thermal status 0 was observed before and after. Intermediate thermal status was not sampled.

The charge-counter decrease is **188.658 mAh**. Multiplying it by the mean endpoint voltage gives an estimated **0.745 Wh**. Dividing by the **1,233.19 seconds between external observations**, which include warm-up and setup, gives about **2.18 W** (approximately 2.2 W). This is an estimate of whole-device average power. It includes the display, radios and background activity and cannot isolate the app’s energy use.

## Limitations and comparisons

- V-Sync at approximately 60 Hz governs the primary baseline. The results leave throughput above 60 FPS unmeasured. Detailed GPU execution timing was disabled. Frame intervals include pacing and scheduling.
- The Xperia 1 V is a capable device. Lower-end Android devices may behave differently. Thermal and battery results apply to the recorded unplugged session. Room temperature was unavailable.
- The sustained scripted workload advances matches more quickly than typical human-paced play. Dynamic cases can reach different board states and action counts at different execution speeds.
- Benchmark instrumentation contributes measurement and allocation overhead. The reported render texture dimensions need independent framebuffer validation.

For comparisons after artwork refinement, use the same device and driver, build type, scenarios, seed, layouts, frame pacing and presentation settings. Verify window and render metadata, compare repetitions and slow-frame rates above the same threshold, and check event counts and board digests before interpreting dynamic results. Keep uncapped throughput, isolated visual-effect comparisons and opt-in viewport timing in separate runs. Match display settings and unplugged conditions for sustained phone comparisons, and record before-and-after thermal and battery data. [Provisional regression targets](VisualDevelopmentRoadmap.md#provisional-performance-targets)

## Evidence retention

This page commits the reviewed numerical summary. The raw ZIP remains outside version control under the local ignored exports directory.

- Filename `native-10c0-f6add32.zip`
- SHA-256 `b07622df61f3855836ee891f70df82ccb848e184e58aa260950ef604fd41777a`
- The archive contains the complete accepted runs, three interrupted Windows attempts, raw frame samples, event and worker timings, settings and trend data, and external observations. `verification.json` identifies accepted runs, and `frame-summary.csv` indexes per-repetition frame statistics. ZIP integrity and the recorded source revision, layout, settings and scenario version were checked.
