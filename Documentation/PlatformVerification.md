# Run, play and export

The [README](https://github.com/ChuckreimuHakunorris/castrum-tactics#open-and-run) covers the Godot .NET setup, play controls and automated regressions. The walkthrough below uses the preserved Classic ruleset as a repeatable presentation test. See the [roadmap](DevelopmentRoadmap.md#platform-verification) for the status of native Windows and physical Android checks.

## Windows and Android walkthrough

Start on the demonstration map with `godot --path . -- --mode local --ruleset classic --map demonstration`. For an editor run or a deterministic Android debug export, set **Mode** to **Local Multiplayer**, **Rules** to **Classic**, and **Map Id Override** to `demonstration` on the main scene. Leave the map override blank for random startup. Count columns A–G from left to right and rows 1–5 from top to bottom. Submit both selections in each row with coordinates.

| Turn | Player One | Player Two | What to check |
| --- | --- | --- | --- |
| Draft 1 | B2 | D3 | Each player gets a knight. Try revising Player One's choice to B2 before Player Two submits. |
| Draft 2 | E1 | F4 | Each player gets a second knight. |
| Draft 3 | Automatic | Automatic | B5 becomes a grave immediately after Draft 2, and the first main turn begins. |
| Main 1 | C3 | E2 | Both players place a knight. |
| Main 2 | B3 | E1 | Player Two captures the knight at E1 and the ground beneath it degrades. |
| Main 3 | B1 | G3 | Both players place a knight. |
| Main 4 | A1 | D3 | Player Two upgrades D3 to a castle with battlements. |
| Main 5 | C3 | C3 | A direct collision creates a grave at C3. |
| Main 6 | A2 | D2 | Both players place a knight. |
| Main 7 | B3 | D4 | Player One upgrades B3 to a castle. Empty A3 is now tinted red and cannot be selected by either player. |
| Main 8 | C4 | C4 | The final collision ends the match. Player Two wins **18–8**. The final board stays visible and submission controls disappear. |

- On Windows, resize the window from tall to wide during a provisional selection. Check that tiles remain square, the outline stays selected, and hit testing follows the board. Switch players and verify the outline clears. Finish the walkthrough, click **Restart Match**, and check for an empty board, restored ground, five gold dots, and Player One active at Draft 1.
- To verify the draft retry, start a fresh match and submit the same dot for both players in four draft rounds. The fifth dot should resolve automatically. **Restart Draft** should restore all five dots and the initial ground. Complete a successful draft to confirm normal play resumes.

## Android rotation checks

The Android build is configured for automatic rotation between portrait and landscape. The gameplay scene fits its controls inside the available safe area, stacks them beneath the board on narrow screens, and puts them beside the board on wide screens.

1. Enable Android auto-rotate and launch the game in portrait. Complete Draft 1 from the walkthrough.
2. For Draft 2, submit Player One's choice, switch to Player Two, and highlight Player Two's square. Rotate to landscape before submitting. Check that the status still shows Player One's submission, Player Two is active, and the provisional outline stayed on the chosen square.
3. Submit Player Two's selection. Confirm that B5 becomes a grave and Main 1 begins. Select and submit some main turns in landscape.
4. Rotate back to portrait. Check that the board fits, stays centered in its area, and has square tiles. Tap a tile center, a tile gap, and the player and submit buttons to check hit testing. Confirm targets and status are readable in both orientations.
5. Finish the walkthrough and verify Player Two's 18–8 win and both visible scores. Tap **Restart Match** in portrait and confirm a fresh draft on the same map.
6. Enable the device's rotation lock and rotate the phone. Verify that the app follows the locked orientation where Android applies the preference.

## Android export

1. Install the Godot 4.7.2 .NET export templates, JDK 17, Android SDK Platform 36, and Build Tools 36.1.0. Install Android SDK Platform-Tools for device deployment and configure the Java and Android SDK paths in Godot's editor settings. The checked-in template uses compile and target API 36 and minimum API 24. Gradle can install missing SDK packages when their licenses have already been accepted. [Gradle SDK downloads](https://developer.android.com/studio/intro/update#download-with-gradle)
2. On a fresh checkout, open the installed export templates' `android_source.zip` and copy `libs/debug/godot-lib.template_debug.aar` and `libs/release/godot-lib.template_release.aar` into their corresponding folders under `android/build/`. This preserves the project's Gradle scripts and Android resources. Godot generates the project assets and .NET runtime files during export.
3. In **Project > Export**, choose the **Android** preset to export an ARM64 debug APK. Choose a local output directory such as `exports/`. Keep signing keys and credentials local.
4. Connect a physical ARM64 Android device through USB or wireless debugging. From the repository root on Windows, run `./scripts/Install-AndroidDebug.ps1 -Apk <path-to-apk>`. Use `-Serial <adb-device>` when multiple devices are connected. Launch **CastrumTactics** and test Versus CPU with the normal preset or use the editor overrides above for the deterministic two-human walkthrough.

Keep the Android preset's package ID `com.castrumtactics.prototype` and use the same signing key for repeat installs. The installer checks the APK's embedded package ID before copying it to the phone. The isolated 8C exports used `com.castrumtactics.milestone8c`, which Android installs alongside the standard game. If the installer detects that old copy, rerun with `-RemoveLegacy8C` to remove it after installing the standard APK. Android keeps the old package's data separately.

The template pins NDK `29.0.14206865` for native tooling. The verified debug export used prebuilt native libraries and packaged them without stripping symbols, so it succeeded without that NDK. The current Gradle project has no CMake build. Use the versions in [`config.gradle`](https://github.com/ChuckreimuHakunorris/castrum-tactics/blob/main/android/build/config.gradle) for this project. The general Godot 4.7 setup guide lists older SDK packages. [Godot 4.7 Android setup](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_android.html#download-the-android-sdk)

See the [game design](GameDesign.md) and [art direction](ArtDirection.md) for gameplay rules and visual direction.

## Windows x64 debug export

Install the matching Godot .NET export templates and the [pinned SDK](EngineeringVerification.md#toolchain-and-policy). Run from the repository root in PowerShell.

```powershell
New-Item -ItemType Directory -Force exports/windows | Out-Null
godot --headless --path . --import
godot --headless --path . --export-debug "Windows Desktop" exports/windows/CastrumTactics.exe
dumpbin /headers exports/windows/CastrumTactics.exe
Test-Path exports/windows/CastrumTactics.pck
Test-Path exports/windows/data_CastrumTactics_windows_x86_64/CastrumTactics.dll
./exports/windows/CastrumTactics.console.exe -- --mode local --ruleset classic --map demonstration
./exports/windows/CastrumTactics.console.exe -- --mode cpu --ruleset knight-support --map demonstration --seed 42
```

Use `dumpbin` from a Visual Studio Developer Shell to verify x64 (machine `8664`). On Linux, `file` can inspect the PE32+ package. Keep the pack and managed sidecars next to the executable for launch. Complete the Classic walkthrough, then play a short Knight Support CPU match. Start a replacement match while the CPU is thinking and confirm the new game accepts only its own actions.

## Android package validation

After configuring the SDK and templates as [above](#android-export), set `ANDROID_HOME` to the installed Android SDK path for the tool commands below. Use the existing Android preset. On Linux, the tracked `android/build/gradlew` needs its executable bit. On checkouts prior to 9C, set it locally with `chmod +x android/build/gradlew`.

```powershell
New-Item -ItemType Directory -Force exports/android | Out-Null
dotnet build CastrumTactics.csproj -p:GodotTargetPlatform=android -warnaserror
godot --headless --path . --import
godot --headless --path . --export-debug Android exports/android/CastrumTactics-debug.apk
$tools = Join-Path $env:ANDROID_HOME 'build-tools/36.1.0'
& "$tools/apksigner.bat" verify --verbose exports/android/CastrumTactics-debug.apk
& "$tools/zipalign.exe" -c -P 16 -v 4 exports/android/CastrumTactics-debug.apk
& "$tools/aapt.exe" dump badging exports/android/CastrumTactics-debug.apk
./scripts/Install-AndroidDebug.ps1 -Apk exports/android/CastrumTactics-debug.apk
```

Confirm signature, 16 KiB alignment, package ID `com.castrumtactics.prototype` and ARM64 libraries. On Linux, use the equivalent tools without file extensions. After installing on an authorized phone, launch the app. Follow the rotation and hit-testing walkthrough, then use the normal Knight Support CPU build to exercise New Match, restart and interruption while the CPU is thinking.

For optional device measurement, use the [instrumentation procedure](CpuDifficultyCalibration.md#physical-android-follow-up) with `CastrumDeviceTiming=true` and a separate local output. Record the revision, device and Android version. Discard warm-up samples before summarizing worker timing and thread allocation, and record process GC counts, Android frame timing and thermal observations separately. See the [verification record](EngineeringVerification.md#verification-record) for currently completed checks.
