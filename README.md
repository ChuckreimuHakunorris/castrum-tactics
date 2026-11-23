# Castrum Tactics

Castrum Tactics is a two-player, turn-based strategy game in development about controlling territory on a 2D grid. Two people can play a full match on one device, from the Unlimited Draft through simultaneous main-phase turns and final scoring. The demonstration map is 7 columns by 5 rows.

## Open and run

1. Install Godot 4.7.2 .NET and a .NET SDK that can build the project's `net8.0` desktop target and `net9.0` Android target. The .NET 10 SDK can build both targets.
2. Import `project.godot` in the Godot Project Manager and press **F5** to run the main scene. Follow the local match walkthrough below.
3. Run `dotnet build CastrumTactics.csproj` from the repository root to check the Godot C# build from the command line.

When running inside Godot's embedded Game view, select **Input** in the Game toolbar before testing the board. **2D** mode intercepts clicks to inspect nodes. Clicking the board in that mode can outline the full-window renderer. [Godot Game view interaction modes](https://docs.godotengine.org/en/4.7/tutorials/editor/game_embedding.html#interaction-mode)

The project uses C# and Godot's Compatibility renderer. Android export uses Godot's Gradle build template.

## Play a local match

Use left clicks on Windows or taps on Android.

Choose **Player One** (red) or **Player Two** (blue), select a marked square, and press **Submit selection**. The board updates once both players submit. The first player can choose again and press **Revise selection** while waiting. Switching players clears the pale provisional outline and refreshes the legal targets.

Gold dots mark shared draft targets. The main phase starts automatically after a successful draft and uses gold frames to show the active player's legal targets. Secured cells have a red or blue tint and are unavailable to both players. When the match ends, the board stays visible with both scores and the winner or draw.

### Windows and Android walkthrough

Start a fresh match. Count columns A–G from left to right and rows 1–5 from top to bottom. Submit both selections in each row.

| Turn | Player One | Player Two | What to check |
| --- | --- | --- | --- |
| Draft 1 | B2 | D3 | Each player gets a knight. Try revising Player One's choice to B2 before Player Two submits. |
| Draft 2 | E1 | F4 | Each player gets a second knight. |
| Draft 3 | B5 | B5 | A grave appears and the first main turn starts automatically. |
| Main 1 | C3 | E2 | Both players place a knight. |
| Main 2 | B3 | E1 | Player Two captures the knight at E1 and the ground beneath it degrades. |
| Main 3 | B1 | G3 | Both players place a knight. |
| Main 4 | A1 | D3 | Player Two upgrades D3 to a castle with battlements. |
| Main 5 | C3 | C3 | A direct collision creates a grave at C3. |
| Main 6 | A2 | D2 | Both players place a knight. |
| Main 7 | B3 | D4 | Player One upgrades B3 to a castle. Empty A3 is now tinted red and cannot be selected by either player. |
| Main 8 | C4 | C4 | The final collision ends the match. Player Two wins **18–8**. The final board stays visible and submission controls disappear. |

- On Windows, resize the window during a provisional selection. Check that tiles remain square and hit testing follows the board. Switch players and verify the old outline clears.
- On Android, tap near tile edges, in tile gaps, and on the player and submit buttons. A tap on a control should trigger only that control. Check that gold frames, territory tints, and the result remain readable at the device's screen size.
- To verify the draft retry, relaunch the scene and submit the same dot for both players in all five draft rounds. **Restart Draft** should restore all five dots and the initial ground. Complete a successful draft to confirm normal play resumes.

## Android export

1. Install the Godot 4.7.2 .NET export templates, JDK 17, Android SDK Platform 36, and Build Tools 36.1.0. Install Android SDK Platform-Tools for device deployment and configure the Java and Android SDK paths in Godot's editor settings. The checked-in template uses compile and target API 36 and minimum API 24. Gradle can install missing SDK packages when their licenses have already been accepted. [Gradle SDK downloads](https://developer.android.com/studio/intro/update#download-with-gradle)
2. On a fresh checkout, open the installed export templates' `android_source.zip` and copy `libs/debug/godot-lib.template_debug.aar` and `libs/release/godot-lib.template_release.aar` into their corresponding folders under `android/build/`. This preserves the project's Gradle scripts and Android resources. Godot generates the project assets and .NET runtime files during export.
3. In **Project > Export**, choose the **Android** preset to export an ARM64 debug APK. Choose a local output directory such as `exports/`. Keep signing keys and credentials local.
4. Connect a physical ARM64 Android device with USB debugging enabled. Run `adb install -r <path-to-apk>`, launch **CastrumTactics**, and follow the local match walkthrough with touch.

The template pins NDK `29.0.14206865` for native tooling. The verified debug export used prebuilt native libraries and packaged them without stripping symbols, so it succeeded without that NDK. The current Gradle project has no CMake build. Use the versions in [`config.gradle`](android/build/config.gradle) for this project. The general Godot 4.7 setup guide lists older SDK packages. [Godot 4.7 Android setup](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_android.html#download-the-android-sdk)

See the [game design](Documentation/GameDesign.md) and [art direction](Documentation/ArtDirection.md) for gameplay rules and visual direction.

## Developer documentation

With the .NET 10 SDK, run `dotnet build CastrumTactics.sln` to build all projects and `dotnet test CastrumTactics.sln` to run the domain, local session, and board geometry tests. The tests run without the Godot editor or runtime.

See the [documentation homepage source](Documentation/index.md) for architecture and API reference navigation, and the [documentation contributor guide](Documentation/contributing.md) for local builds, writing conventions, and GitHub Pages setup.
