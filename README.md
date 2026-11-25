# Castrum Tactics

Castrum Tactics is a two-player, turn-based strategy game about controlling territory on a 2D grid. Play against the CPU or share one device with another player, from the Unlimited Draft through simultaneous main-phase turns and final scoring.

## Open and run

1. Install Godot 4.7.2 .NET and a .NET SDK that can build the project's `net8.0` desktop target and `net9.0` Android target. The .NET 10 SDK can build both targets.
2. Import `project.godot` in the Godot Project Manager and press **F5** to play Knight Support as Player One against a Normal CPU on one of 100 randomly selected built-in maps. [Map catalog](Documentation/MapFormat.md)
3. Run `dotnet build CastrumTactics.csproj` from the repository root to check the Godot C# build from the command line.

When running inside Godot's embedded Game view, select **Input** in the Game toolbar before testing the board. **2D** mode intercepts clicks to inspect nodes. Clicking the board in that mode can outline the full-window renderer. [Godot Game view interaction modes](https://docs.godotengine.org/en/4.7/tutorials/editor/game_embedding.html#interaction-mode)

The project uses C# and Godot's Compatibility renderer. Android export uses Godot's Gradle build template.

The modern interface uses a shared Godot theme around the pixel-art board. To preview reduced motion, open `Scenes/main.tscn` and turn off **Motion Enabled** on the **AnimatedBackground** instance in the Inspector. See the [art direction](Documentation/ArtDirection.md#9-interface-and-typography) for theme reuse, font licensing, and background customization.

## Play against the CPU

Use left clicks on Windows or taps on Android.

Select a marked square, then click or tap it again or press **Submit selection**. Selecting a different square moves the highlight. You can choose while the CPU is thinking and revise a submission until the CPU submits. The board shows both outcomes together when the round resolves.

The CPU card shows the active difficulty. In **New Match**, choose Easy, Normal, Hard, Very Hard, or Monster and select a battlefield before confirming.

The **Secured Territory** banner starts at 0–0 and updates when the board resolves. It counts secured squares, including empty ones, and keeps the final totals above the board. The left and right scores always represent Player One and Player Two.

## Choose a battlefield

In either input mode, scroll or filter the alphabetical **New Match** catalog and select a battlefield to preview the terrain and draft positions. **Random Map** draws from all 100 battlefields. The dialog remembers the last confirmed choice, including Random Map. A startup map override supplies the initial choice. Confirm to begin a fresh match or cancel to return to the active one. The header shows the active battlefield and its dimensions. **Restart Draft** and **Restart Match** preserve the current battlefield, ruleset, and difficulty.

On a touchscreen, swipe over the map rows to scroll and tap a row to preview its battlefield.

## Ruleset and development configuration

**Knight Support** is the official ruleset and starts with castles. At the end of each main turn, a knight survives if at least one friendly knight or castle is adjacent, including diagonally. Unsupported knights become neutral graves and lose one ground health. The header shows the active ruleset. [Complete rules and design rationale](Documentation/GameDesign.md#55-knight-support-ruleset)

To play **Classic** with starting knights, set **Rules** to **Classic** in the main scene Inspector, pass `RuleSet.Classic` in a programmatic match configuration, or run with `--ruleset classic`. The configured ruleset carries forward when you start a new match.

```sh
godot --path . -- --mode cpu --ruleset knight-support --map demonstration --seed 42
godot --path . -- --mode local --ruleset classic --map open-meadow
```

## Play a local two-human match

Choose **Player One** (red) or **Player Two** (blue) and select a marked square. Click or tap it again or press **Submit selection**. The board updates once both players submit. While waiting, select your revised square and confirm it the same way or use **Revise selection**. Switching players clears the pale provisional outline and refreshes the legal targets.

Gold dots mark shared draft targets. When only one start remains, it automatically becomes an unowned grave and loses one ground health. Main uses gold frames for the active player's legal targets. Secured cells have a red or blue tint and are unavailable to both players. At the end of a match, both scores and the winner or draw stay visible. **Restart Match** starts a fresh opening on the same map with the initial terrain, health, and starting positions.

### Windows and Android walkthrough

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

### Android rotation checks

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
4. Connect a physical ARM64 Android device with USB debugging enabled. Run `adb install -r <path-to-apk>` and launch **CastrumTactics**. Test Versus CPU with the normal preset and use the editor overrides above for the deterministic two-human walkthrough.

The template pins NDK `29.0.14206865` for native tooling. The verified debug export used prebuilt native libraries and packaged them without stripping symbols, so it succeeded without that NDK. The current Gradle project has no CMake build. Use the versions in [`config.gradle`](android/build/config.gradle) for this project. The general Godot 4.7 setup guide lists older SDK packages. [Godot 4.7 Android setup](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_android.html#download-the-android-sdk)

See the [game design](Documentation/GameDesign.md) and [art direction](Documentation/ArtDirection.md) for gameplay rules and visual direction.

## Developer documentation

With the .NET 10 SDK, run `dotnet build CastrumTactics.sln` to build all projects and `dotnet test CastrumTactics.sln` to run the domain, local session, CPU, and presentation tests. The tests run without the Godot editor or runtime. All five CPU difficulties use simultaneous search under Knight Support. Deprecated Classic is available for development and uses the legacy static policy. See the [CPU architecture](Documentation/CpuArchitecture.md), [benchmark commands and results](Documentation/CpuBenchmarks.md), and [session architecture](Documentation/SessionArchitecture.md).

The [CPU tournament guide](Documentation/CpuTournaments.md) covers headless round robins, frozen baseline comparisons, fixed and natural drafts, resumable parallel runs, and replay. Preview the example schedule with the .NET 10 SDK.

```sh
dotnet run -c Release --project Benchmarks -- tournament dry-run --config Benchmarks/Examples/round-robin.json
```

Monster can prove wins up to three simultaneous turns ahead on compact tactical positions. See the [8D.2 search optimization report](Documentation/CpuSearchOptimization.md) for the production settings, tournament results, and CLI examples for wider and deeper experiments.

After building and importing assets with `godot --headless --path . --import`, run the same two Godot 4.7.2 .NET regressions as CI.

```sh
godot --headless --path . --script Tests/Presentation/DraftLayoutCheck.gd
godot --headless --path . --script Tests/Presentation/CpuMatchCheck.gd -- --seed 42
```

The tests replay complete Classic local and CPU matches, exercise Knight Support gameplay, and check map previews, replacement, scores, and restarts at different viewport sizes. For development launches, use `--mode cpu` or `--mode local`, `--ruleset classic` or `--ruleset knight-support`, `--map <id>`, and an optional `--seed <int>` after the Godot `--` separator. The seed controls CPU randomness. The map override fixes the initial map.

See the [documentation homepage source](Documentation/index.md) for architecture and API reference navigation, and the [documentation contributor guide](Documentation/contributing.md) for local builds, writing conventions, and GitHub Pages setup.
