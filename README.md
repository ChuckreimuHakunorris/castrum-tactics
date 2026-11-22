# Castrum Tactics

Castrum Tactics is a planned two-player, turn-based strategy game about controlling territory on a 2D grid. The current proof of concept runs a single scene whose C# script updates a label and logs a startup message. Gameplay is still in the design stage. The developer manually verified the proof of concept on Windows and installed and ran the exported APK on a physical Android device.

## Open and run

1. Install Godot 4.7.2 .NET and a .NET SDK that can build the project's `net8.0` desktop target and `net9.0` Android target. The .NET 10 SDK can build both targets.
2. Import `project.godot` in the Godot Project Manager and press **F5** to run the main scene. The label should display `C# is running successfully!`.
3. Run `dotnet build` from the repository root to check the C# build from the command line.

The project uses C# and Godot's Compatibility renderer. Android export uses Godot's Gradle build template.

## Android export

1. Install the Godot 4.7.2 .NET export templates and configure the Java and Android SDK paths in Godot's editor settings. The installed Gradle template specifies JDK 17 as the minimum, Android SDK Platform 36, Build Tools 36.1.0 and NDK 29.0.14206865. Install Android SDK Platform-Tools for device deployment.
2. On a fresh checkout, open the installed export templates' `android_source.zip` and copy `libs/debug/godot-lib.template_debug.aar` and `libs/release/godot-lib.template_release.aar` into their corresponding folders under `android/build/`. This preserves the project's Gradle scripts and Android resources. Godot generates the project assets and .NET runtime files during export.
3. In **Project > Export**, choose the **Android** preset to export an ARM64 debug APK. Choose a local output directory such as `exports/`. Keep signing keys and credentials local.

See the [game design](Documentation/GameDesign.md) and [art direction](Documentation/ArtDirection.md) for planned gameplay and visual direction.
