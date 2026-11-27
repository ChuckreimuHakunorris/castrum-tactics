# Castrum Tactics

Castrum Tactics is a two-player, turn-based strategy game about controlling territory on a 2D grid. Play against the CPU or share one device with another player, from the Unlimited Draft through simultaneous main-phase turns and final scoring.

## Open and run

1. Install Godot 4.7.2 .NET and the SDK specified in [global.json](global.json). The SDK builds both the `net8.0` desktop and `net9.0` Android targets. Godot desktop runs also use the .NET 8 runtime. [Toolchain and verification](Documentation/EngineeringVerification.md#toolchain-and-policy)
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

To play **Classic** with starting knights, set **Rules** to **Classic** in the main scene Inspector, pass `RuleSet.Classic` in a programmatic match configuration, or run with `--ruleset classic`. The configured ruleset carries forward when you start a new match. Use `--mode cpu` or `--mode local`, `--ruleset knight-support` or `--ruleset classic`, `--map <id>`, and an optional `--seed <int>` for CPU randomness after Godot’s `--` separator.

```sh
godot --path . -- --mode cpu --ruleset knight-support --map demonstration --seed 42
godot --path . -- --mode local --ruleset classic --map open-meadow
```

## Play a local two-human match

Choose **Player One** (red) or **Player Two** (blue) and select a marked square. Click or tap it again or press **Submit selection**. The board updates once both players submit. While waiting, select your revised square and confirm it the same way or use **Revise selection**. Switching players clears the pale provisional outline and refreshes the legal targets.

Gold shields mark shared draft targets. When only one start remains, it automatically becomes an unowned grave and loses one ground health. In Main, each faction’s legal targets have a matching frame. Shared targets use both colors, and a pale cyan frame marks the provisional selection. Secured cells have a red or blue tint and are unavailable to both players. At the end of a match, both scores and the winner or draw stay visible. **Restart Match** starts a fresh opening on the same map with the initial terrain, health, and starting positions.

### Windows and Android walkthrough

Follow the [Classic 18–8 walkthrough and draft retry](Documentation/PlatformVerification.md#windows-and-android-walkthrough) for a repeatable local-match and resize check.

### Android rotation checks

Use the [rotation walkthrough](Documentation/PlatformVerification.md#android-rotation-checks) to test safe areas, selection retention and touch input.

## Android export

See the [Android setup, export and installer guide](Documentation/PlatformVerification.md#android-export) for the Godot .NET templates, SDK packages, ARM64 APK and stable package ID.

## Developer documentation

With the .NET 10 SDK, run the solution build and tests from the repository root.

```sh
dotnet build CastrumTactics.sln -warnaserror
dotnet test CastrumTactics.sln --no-build
dotnet run -c Release --project Benchmarks -- tournament dry-run --config Benchmarks/Examples/round-robin.json
```

After building, import assets and run the two Godot .NET presentation regressions. Use the Godot .NET executable name on your system in place of `godot` if needed.

```sh
godot --headless --path . --import
godot --headless --path . --script Tests/Presentation/DraftLayoutCheck.gd
godot --headless --path . --script Tests/Presentation/CpuMatchCheck.gd -- --seed 42
```

- [Rules](Documentation/GameDesign.md) and [art direction and credits](Documentation/ArtDirection.md)
- [Architecture](Documentation/architecture.md), [CPU configuration](Documentation/CpuArchitecture.md) and [9A ownership changes](Documentation/ArchitectureRefactoring.md)
- [Roadmap and platform status](Documentation/DevelopmentRoadmap.md) and [decision log](Documentation/DecisionLog.md)
- [Research and benchmark guide](Documentation/CpuBenchmarks.md), [8E calibration](Documentation/CpuDifficultyCalibration.md) and [battlefield study](Documentation/BattlefieldBalance.md)
- [Engineering verification and CI](Documentation/EngineeringVerification.md)
- [Documentation home](Documentation/index.md) and [contributor guide and DocFX commands](Documentation/contributing.md)
