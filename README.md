# Castrum Tactics

Castrum Tactics is a two-player, turn-based strategy game in development about controlling territory on a 2D grid. The engine-independent C# library defines maps and initializes board state. The Godot main scene displays a 7-by-5 demonstration board from that state. The developer verified the earlier startup prototype on Windows and a physical Android device.

## Open and run

1. Install Godot 4.7.2 .NET and a .NET SDK that can build the project's `net8.0` desktop target and `net9.0` Android target. The .NET 10 SDK can build both targets.
2. Import `project.godot` in the Godot Project Manager and press **F5** to run the main scene. The board shows green playable terrain, dark inaccessible terrain, and two gold dots for candidate starting positions. Resize the window to see the board stay centered with square cells.
3. Run `dotnet build CastrumTactics.csproj` from the repository root to check the Godot C# build from the command line.

The project uses C# and Godot's Compatibility renderer. Android export uses Godot's Gradle build template.

## Android export

1. Install the Godot 4.7.2 .NET export templates, JDK 17, Android SDK Platform 36, and Build Tools 36.1.0. Install Android SDK Platform-Tools for device deployment and configure the Java and Android SDK paths in Godot's editor settings. The checked-in template uses compile and target API 36 and minimum API 24. Gradle can install missing SDK packages when their licenses have already been accepted. [Gradle SDK downloads](https://developer.android.com/studio/intro/update#download-with-gradle)
2. On a fresh checkout, open the installed export templates' `android_source.zip` and copy `libs/debug/godot-lib.template_debug.aar` and `libs/release/godot-lib.template_release.aar` into their corresponding folders under `android/build/`. This preserves the project's Gradle scripts and Android resources. Godot generates the project assets and .NET runtime files during export.
3. In **Project > Export**, choose the **Android** preset to export an ARM64 debug APK. Choose a local output directory such as `exports/`. Keep signing keys and credentials local.

The template pins NDK `29.0.14206865` for native tooling. The verified debug export used prebuilt native libraries and packaged them without stripping symbols, so it succeeded without that NDK. The current Gradle project has no CMake build. Use the versions in [`config.gradle`](android/build/config.gradle) for this project. The general Godot 4.7 setup guide lists older SDK packages. [Godot 4.7 Android setup](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_android.html#download-the-android-sdk)

See the [game design](Documentation/GameDesign.md) and [art direction](Documentation/ArtDirection.md) for planned gameplay and visual direction.

## Developer documentation

With the .NET 10 SDK, run `dotnet build CastrumTactics.sln` to build all projects and `dotnet test CastrumTactics.sln` to run the domain tests. The tests run without the Godot editor or runtime.

See the [documentation homepage source](Documentation/index.md) for architecture and API reference navigation, and the [documentation contributor guide](Documentation/contributing.md) for local builds, writing conventions, and GitHub Pages setup.
