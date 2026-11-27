# Native rendering benchmarks

Milestone 10C.0 starts from integrated 10C `3e0fbb66378101bbb7c95a7c5639df932a99e1e6` (PR #29). This milestone supplies the benchmark runner. A native baseline requires the owner’s Windows and physical Android results. Historical 10B and 10C software-rendered evidence remains available. [10B art verification](BoardArtVerification.md) · [10C interaction verification](InteractionVerification.md)

## Start here

Fetch and check out the reviewed work, then record the exact revision.

```powershell
git fetch origin
git switch --track origin/feat/m10c0-performance-baseline
$revision = (git rev-parse HEAD).Trim()
```

An existing local branch can be opened with `git switch feat/m10c0-performance-baseline` and updated with `git pull --ff-only`. Run each native comparison from the same window size or device orientation and keep the request JSON with the results.

### Windows x64

Use Godot 4.7.2 .NET, matching export templates and the pinned .NET SDK. Set `$godot` to the Godot .NET executable on your machine.

```powershell
New-Item -ItemType Directory -Force exports/windows | Out-Null
& $godot --headless --path . --import
& $godot --headless --path . --export-debug "Windows Desktop" exports/windows/CastrumTactics.exe

$userData = Join-Path $env:APPDATA 'Godot\app_userdata\CastrumTactics'
New-Item -ItemType Directory -Force $userData | Out-Null
$request = @{ profile = 'quick'; revision = $revision; deviceNotes = 'Windows, monitor refresh, power mode and driver' }
$request | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $userData 'render-benchmark.json')
Start-Process -Wait -FilePath (Resolve-Path exports/windows/CastrumTactics.exe) -ArgumentList '--resolution','900x650'
Get-ChildItem (Join-Path $userData 'render-benchmarks') -Directory | Sort-Object LastWriteTime -Descending
```

Start with `quick`. Then set `$request.profile = 'standard'` and repeat the request and launch commands. A second standard run at `--resolution 720x1000` provides a portrait-layout sample. The configured window can be limited by desktop scaling or display size. Check the dimensions recorded in each result. Use the same V-Sync, monitor, renderer and build type when comparing revisions.

### Android ARM64

Use the existing Android export prerequisites, template libraries and owner signing key. In the editor export an Android ARM64 **debug** APK, or use the equivalent command below after configuring the SDK and key. The normal debug build contains the benchmark. Leave `CastrumDeviceTiming` off so the separate milestone 8E logger does not add I/O to these measurements. [Android export prerequisites](PlatformVerification.md#android-export)

```powershell
New-Item -ItemType Directory -Force exports/android | Out-Null
& $godot --headless --path . --export-debug Android exports/android/CastrumTactics-debug.apk
./scripts/Install-AndroidDebug.ps1 -Apk exports/android/CastrumTactics-debug.apk
adb shell run-as com.castrumtactics.prototype ls files

$profile = 'quick'
@{ profile = $profile; revision = $revision; deviceNotes = 'Device, orientation, refresh and charge conditions' } |
    ConvertTo-Json | Set-Content -Encoding ASCII exports/android/render-benchmark.json
adb shell am force-stop com.castrumtactics.prototype
Get-Content -Raw exports/android/render-benchmark.json |
    adb shell -T 'run-as com.castrumtactics.prototype tee files/render-benchmark.json >/dev/null'
adb shell monkey -p com.castrumtactics.prototype -c android.intent.category.LAUNCHER 1
```

Connect the device through the existing USB or wireless ADB workflow first. If the private `files` directory is absent on a fresh install, launch the app once and close it before staging the request. With multiple devices, pass the same serial to the installer and to each ADB invocation. An exported APK signed with a different key cannot update the owner’s existing install. Use a locally signed export for that device.

Set the device orientation before launch. Keep the app foregrounded and the orientation fixed until it exits. Repeat with `$profile = 'standard'` in both orientations. Use `$profile = 'sustained'` for a continuous 20-minute Monster workload. Do the sustained run in the owner’s normal play orientation and start from a known temperature and battery level. Capture the external observations before staging the run and again after it exits.

```powershell
adb shell dumpsys thermalservice > exports/android/thermal-before.txt
adb shell dumpsys battery > exports/android/battery-before.txt
# Run the selected profile here, then wait for the app to exit.
adb shell dumpsys thermalservice > exports/android/thermal-after.txt
adb shell dumpsys battery > exports/android/battery-after.txt
adb shell run-as com.castrumtactics.prototype ls files/render-benchmarks
cmd /c "adb exec-out run-as com.castrumtactics.prototype tar -C files -cf - render-benchmarks > exports\android\render-benchmarks.tar"
tar -tf exports/android/render-benchmarks.tar
```

The archive command uses binary redirection through `cmd` so it works from Windows PowerShell as well. If device `tar` is unavailable, retrieve individual files with `adb exec-out run-as com.castrumtactics.prototype cat files/render-benchmarks/<run>/<file>`. Optional external GPU/system traces depend on the device and toolchain. Prefer battery sampling over a whole interval with known power conditions. Wireless ADB makes an unplugged test possible. Device battery percentage changes and thermal status are coarse observations. USB power, screen brightness, radios and background processes can dominate a short comparison.

Return the complete run folders plus any thermal and battery files. Record the device model and OS version, GPU driver if available, refresh rate, resolution, orientation, power mode, brightness, battery range, charging status and ambient conditions. Note any external tracing that might add overhead. The benchmark requires no network on the test device.

## Profiles and scenarios

| Profile | Warm-up and measurement per case | Cases |
| --- | --- | --- |
| Quick | 2 s + 4 s, once | Small, turns, Monster |
| Standard | 8 s + 20 s, three independent repetitions | The nine non-extended cases, about 13 minutes plus setup and export |
| Sustained | 30 s + 1,200 s, once | Extended |

Quick checks activation and output. Standard repeats the same initial state and settings to expose run-to-run variance and reduce the influence of early asset loading. Sustained records a trend every 15 seconds. Warm-up discards the early frame intervals from steady-state summaries and includes scripted activity for dynamic cases.

| ID | Version 2 workload |
| --- | --- |
| `small` | Demonstration (7 × 5), Classic, fixed row-major draft into a settled board. |
| `large` | Great Divide (12 × 12), Classic, the same fixed draft rule and production sidecar. |
| `castles` | Synthetic 12 × 12, 64 pieces and 80 legal tiles, based on the 10C dense fixture. |
| `damage` | A reachable Classic Great Divide midgame with damage on both factions’ knights and worn ground beneath permanent pieces. Uses authoritative draft and Main resolutions with a fixed replay seed of 163. |
| `damage-extreme` | The version 1 `damage` board, preserved byte-for-byte as a synthetic stress test. Its deliberately extreme health pattern includes zero-health knights and empty cells. |
| `targets` | Synthetic checkerboard knights and shared legal frames, evaluated with the existing Classic rules. |
| `turns` | Demonstration with scripted choices for both sides and repeated authoritative resolutions. |
| `lifecycle` | Fixed opening collisions cause a restart, then replacement alternates Demonstration and Open Meadow. |
| `monster` | Great Divide, Knight Support, asynchronous Monster CPU with a scripted human opponent and the production elapsed guard. |
| `extended` | Sustained repetition of the Monster workload, including normal match restarts. |

Static scenarios finish setup before sampling. For dynamic cases, each scripted step picks from authoritative legal targets in stable order. The CPU uses the saved seed and the production 450 ms elapsed guard. The guard and frame pacing can change how far a match progresses in a fixed wall-clock window. Match identity, events and search metrics make those differences visible. Compare steady scenes for frame-only A/B tests and compare dynamic runs with similar action counts. Synthetic fixtures never enter the production map catalog.

### Request controls

The request is a single JSON object. `schemaVersion` defaults to 1. A request can override `cases` (unique IDs), `seed` (default 42), `warmupSeconds` (0–300), `measureSeconds` (1–1,800) and `repetitions` (1–10). `revision` identifies the build explicitly and `deviceNotes` holds optional owner observations. The assembly version is also recorded when available.

```json
{
  "profile": "quick",
  "cases": ["castles", "targets"],
  "pacing": "normal",
  "showOpponent": true,
  "reduceMotion": false,
  "ambientMotion": false,
  "legalMotion": true,
  "tubeMotion": false,
  "detailedCounters": true,
  "viewportTiming": false,
  "cpuDiagnostics": false
}
```

The three motion overrides are optional and take precedence over `reduceMotion` independently. Opposing targets default to on. The knobs affect the benchmark only. Normal pacing keeps current engine settings. Uncapped pacing requests a zero FPS cap and disabled V-Sync, and the results include the operating values as reported by Godot. A compositor, driver or Android surface may still pace presentation. Match window size, viewport, refresh and recorded engine settings before interpreting an uncapped result.

## Output and metric definitions

The app consumes `user://render-benchmark.json` once and moves it into `user://render-benchmarks/<run>/request.json`. Normal launches skip the collector when there is no request. The runner writes `run.json` with status, platform, adapter, OS, build, effective request and an index of completed cases. Each completed repetition gets its own JSON file and summary line. `invalid` includes the failure reason. If the process is killed, `running` marks an unfinished run and completed repetition files remain readable. Resize, orientation change or loss of focus during measurement invalidates the active run. The 1,000,000-sample buffer limit also invalidates a run rather than silently truncating it.

- **Frames.** `rawFrames` records monotonic wall-clock intervals between Godot `FramePostDraw` callbacks. `frameSummary` reports FPS as samples divided by the total sampled interval, min, max, median, p95, p99, population standard deviation and counts and percentages strictly above 16.67 and 33.33 ms. Quantiles use linear interpolation. Startup intervals are kept separately. These wall times include scheduling, waits and frame pacing.
- **Renderer.** Detailed mode samples canvas draw calls, reported object/primitive counts, Godot nodes, resources and static memory. Trend rows also contain engine process time. Set `viewportTiming` to true in a separate run to ask the driver for viewport render CPU/GPU timing. The default leaves those timestamp queries off because they can affect rendering. Positive texture/video bytes are engine estimates. A null value denotes disabled or unavailable data, including nonpositive counters from Compatibility backends. Known software adapters report no GPU execution time even if Godot provides a backend timing value. A render interval never stands in for GPU execution time, and static allocations never stand in for video memory.
- **Managed memory.** Start, periodic and end trend samples include estimated managed heap bytes, process-wide cumulative `GC.GetTotalAllocatedBytes(false)`, and collection counts by generation. The delta estimates managed allocations across threads, including instrumentation. No explicit collection runs. Per-decision CPU allocation uses the worker thread counter around the decision.
- **Gameplay events.** Events identify the next raw frame index, starting offset and inclusive duration. A submission can include a nested resolved publication and sprite synchronization. Durations are nested, so adding them would double-count. Compare the tagged interval and nearby intervals with the rest of the distribution. Replacement and restart are recorded separately.
- **Monster work.** The game uses its existing search profile and command path. Worker elapsed time excludes queue wait and main-thread submission. Detailed CPU search diagnostics are off by default. Set `cpuDiagnostics: true` in the request to use the existing diagnostic engine path, which adds resolved pairs, depth and elapsed-limit flags where available. The diagnostics can add work and slightly affect where the same elapsed guard triggers. With it disabled the worker uses ordinary `Decide` and continues to measure elapsed time and allocations. Samples crossing the measured window boundaries are excluded.
- **Thermals and power.** The app exports no inferred thermal or battery statistics. Pair the results with external readings and controlled conditions when comparing sustained behavior.

Detailed sampling buffers records in memory and writes only between repetitions. For an overhead check on the same revision, alternate short settled runs with `detailedCounters` on and off. The lighter mode retains the frame sampler, gameplay event tagging and managed boundary counters. Keep `viewportTiming` off in both runs to isolate the ordinary counter reads. The lighter mode still has a sampler, so this comparison measures the added counters rather than all instrumentation. Compare timestamp queries in their own paired experiment.

## Original cloud verification (scenario version 1)

The benchmark implementation, cloud runs and package exports use source revision `7787ceccfaeb132b2155eeac393c13ff6627a7ea`. The review-only documentation commit follows it. Candidate builds used .NET SDK 10.0.404 and Godot 4.7.2 .NET on Debian 13.7 x86-64.

- Local solution and Release benchmark builds passed with zero warnings. The 437-case xUnit suite and both Godot presentation regressions passed, including the existing seeded CPU outcome. A normal launch under both regressions created no benchmark directory. Release CLI smoke, 96 historic archive hashes, formatting and DocFX local-link checks passed. Domain, AI, maps, sidecars, shaders and artwork are byte-identical to 10C.
- All nine rendered cases wrote raw intervals and summaries. A paired portrait run repeated 12 × 12 cases with opposing targets hidden and Reduce motion on, while leaving ambient motion on. Board hashes matched across repeats. The runner also rejected an invalid scenario request without starting play.
- A shortened 17-second sustained test crossed the periodic memory sampling boundary. This verifies the trend mechanism. A physical 20-minute sample is still outstanding.
- The Windows x64 and Android ARM64 debug packages include the activation logic in their managed assemblies. Android ordinary and device-timing managed builds passed. The APK retained `com.castrumtactics.prototype` and verified with APK Signature Scheme v2 and 16 KiB ZIP alignment. It uses a disposable cloud key. Godot emitted its existing editor-settings teardown diagnostic after export.

The 900 × 650 cloud window reported a 1465 × 1057 render texture. The engine reported a 60 FPS cap and V-Sync disabled. These were short runs in a shared software-rendered host with varied scheduling load. Treat them as instrumentation checks.

| Scenario | Median frame interval | Median draw calls | Diagnostic coverage |
| --- | --- | --- | --- |
| Small | 22.8 ms | 55 | Settled Demonstration opening |
| Large | 23.6 ms | 71 | Great Divide and the production sidecar |
| Castles | 25.5 ms | 176 | 64 pieces |
| Damage | 68.0 ms | 1,443 | Synthetic severe damage and graves |
| Targets | 25.6 ms | 59 | 95 shared targets |
| Turns | 27.5 ms | 59 | 5 authoritative publications |
| Lifecycle | 20.1 ms | 39 | 2 restarts and 2 replacements |
| Monster | 23.8 ms | 96 | 5 completed worker samples |
| Extended | 21.1 ms | 97 | 5 worker samples in the short suite run |

Two alternating uncapped runs per counter mode, with viewport timing off, produced the following ranges of per-run medians. Engine cap was zero and Godot reported V-Sync disabled. Both scenes kept identical start and end board hashes.

| Uncapped scene | Lighter sampler | Detailed counters |
| --- | --- | --- |
| Small | 7.96–9.29 ms | 9.79–10.57 ms |
| Castles | 12.46–12.56 ms | 12.46–12.49 ms |

These short cloud windows establish an overhead comparison procedure. Load varied across windows, so the results do not isolate total instrumentation cost or establish native pacing. The independent viewport-timing probe exercised opt-in CPU render timing and kept physical GPU time null on llvmpipe.

Native Windows and physical Android runs remain to be collected. Use the same profile, seed, orientation and settings on later revisions. Compare repetitions and trends before attributing a change to an optimization.

## PR #30 review follow-up (scenario version 2)

Ground-health marks appear only on damaged empty playable squares and damaged knight squares. Both factions and rulesets use the same presentation rule. Grass, sand, snow and bridge wear continue to reflect authoritative health beneath castles and graves. The rendering benchmark now records the visible marker count with each board digest. JSON schema version 1 remains readable and the scenario collection advances to version 2.

Classic resolution never leaves a worn empty square. A collision creates a grave, a surviving capture leaves a knight, and an upgrade produces a castle. The representative fixture therefore exercises damaged knights in a reachable position. The synthetic extreme fixture exercises the worn-empty rendering case and the upper end of the marker workload.

Matched Compatibility runs compared the original PR head `62d6840` to the visibility change (source committed as `a36d80f`) on Mesa llvmpipe. Each workload has four repetitions per revision, collected in before/after/after/before order with two seconds of warm-up and four seconds of measurement. The 900 × 650 window used the same render texture, static presentation settings, and an uncapped engine on both revisions. Board hashes matched across revisions. Before-marker counts are calculated from the version 1 renderer rule and fixture. Each mark uses two antialiased circles.

| Case | Visible markers | Median draw calls | Median interval, ms | p95, ms | p99, ms |
| --- | --- | --- | --- | --- | --- |
| Extreme damage | 288 → 154 | 1,443 → 907 | 21.27–24.64 → 20.06–23.98 | 27.69–34.96 → 26.97–34.98 | 32.80–63.39 → 34.52–48.00 |
| Castles control | 0 → 0 | 176 → 176 | 10.19–10.76 → 9.36–10.78 | 12.35–13.68 → 12.52–15.57 | 14.95–17.13 → 16.65–25.04 |

Frame interval columns show the range of per-repetition statistics. The median and percentile ranges overlap, so the cloud runs establish a draw-call reduction and leave the frame-time benefit inconclusive. The undamaged castle control helps show software-host variation. The reachable damage replay rendered 10 visible marks and 237 median draw calls in a short smoke run. The remaining damage workload still warrants a 10C.2 investigation, with the reachable fixture as the first comparison and the synthetic extreme as an upper bound. Default Monster samples recorded worker time and allocations with null search counts. Opting in added resolved pair counts. [Before and after](assets/10c0/damage-visibility.jpg)

The original 1,443-draw-call damage result remains above as historical evidence. 10C.2 should compare batching or replacing antialiased marker primitives, using both the representative and extreme cases to judge priority. Also consider fixed action-sequence workloads and a reflection-free constructor for the remaining synthetic fixtures. [Visual roadmap](VisualDevelopmentRoadmap.md)
