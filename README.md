# Castrum Tactics

Castrum Tactics is a two-player, turn-based strategy game in development about controlling territory on a 2D grid. The Godot main scene runs a local two-human Unlimited Draft on a 7-by-5 demonstration map with five starting positions. The engine-independent C# library validates and resolves the draft.

## Open and run

1. Install Godot 4.7.2 .NET and a .NET SDK that can build the project's `net8.0` desktop target and `net9.0` Android target. The .NET 10 SDK can build both targets.
2. Import `project.godot` in the Godot Project Manager and press **F5** to run the main scene. Follow the local draft walkthrough below.
3. Run `dotnet build CastrumTactics.csproj` from the repository root to check the Godot C# build from the command line.

When running inside Godot's embedded Game view, select **Input** in the Game toolbar before testing the board. **2D** mode intercepts clicks to inspect nodes. Clicking the board in that mode can outline the full-window renderer. [Godot Game view interaction modes](https://docs.godotengine.org/en/4.7/tutorials/editor/game_embedding.html#interaction-mode)

The project uses C# and Godot's Compatibility renderer. Android export uses Godot's Gradle build template.

## Test the local opening draft

Use left clicks on Windows or taps on Android.

1. Choose **Player One** (red) or **Player Two** (blue). Select a gold dot and verify that the provisional outline appears. Try an unmarked playable square, an inaccessible square, and a gap. Each should leave the outline where it was.
2. Click **Submit selection**. The board stays intact and the status indicates that the opponent is still choosing. To revise, select another gold dot for the same player and click **Revise selection**. Switch players and verify the outline clears, including when switching with a provisional choice still highlighted.
3. Submit a different dot for the other player. Verify that the two knights appear together on green ground. Check that occupied starts can no longer be highlighted. With three dots left, submit the same dot for both players and verify that an unowned grave appears on tan ground and can no longer be selected. Use the final two starts for another pair of knights. The draft should report completion with two knights per player.
4. Relaunch the scene and select the same dot for both players in each of the five rounds. Click **Restart Draft** after the restart message appears. Verify that all five dots and the initial terrain colors return, then complete the draft with at least one pair of different selections.
5. On Windows, resize the window during a provisional selection. Verify that the board stays centered above the controls, cells stay square, and the outline still follows the selected cell. On Android, check taps near tile edges and the player and submission buttons. Each tap should trigger only its intended control.

## Android export

1. Install the Godot 4.7.2 .NET export templates, JDK 17, Android SDK Platform 36, and Build Tools 36.1.0. Install Android SDK Platform-Tools for device deployment and configure the Java and Android SDK paths in Godot's editor settings. The checked-in template uses compile and target API 36 and minimum API 24. Gradle can install missing SDK packages when their licenses have already been accepted. [Gradle SDK downloads](https://developer.android.com/studio/intro/update#download-with-gradle)
2. On a fresh checkout, open the installed export templates' `android_source.zip` and copy `libs/debug/godot-lib.template_debug.aar` and `libs/release/godot-lib.template_release.aar` into their corresponding folders under `android/build/`. This preserves the project's Gradle scripts and Android resources. Godot generates the project assets and .NET runtime files during export.
3. In **Project > Export**, choose the **Android** preset to export an ARM64 debug APK. Choose a local output directory such as `exports/`. Keep signing keys and credentials local.
4. Connect a physical ARM64 Android device with USB debugging enabled. Run `adb install -r <path-to-apk>`, launch **CastrumTactics**, and follow the draft walkthrough with touch.

The template pins NDK `29.0.14206865` for native tooling. The verified debug export used prebuilt native libraries and packaged them without stripping symbols, so it succeeded without that NDK. The current Gradle project has no CMake build. Use the versions in [`config.gradle`](android/build/config.gradle) for this project. The general Godot 4.7 setup guide lists older SDK packages. [Godot 4.7 Android setup](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_android.html#download-the-android-sdk)

See the [game design](Documentation/GameDesign.md) and [art direction](Documentation/ArtDirection.md) for planned gameplay and visual direction.

## Developer documentation

With the .NET 10 SDK, run `dotnet build CastrumTactics.sln` to build all projects and `dotnet test CastrumTactics.sln` to run the domain, local session, and board geometry tests. The tests run without the Godot editor or runtime.

See the [documentation homepage source](Documentation/index.md) for architecture and API reference navigation, and the [documentation contributor guide](Documentation/contributing.md) for local builds, writing conventions, and GitHub Pages setup.
