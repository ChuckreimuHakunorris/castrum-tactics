# Milestone 9C engineering verification

The starting point was the integrated 9B commit `006c7e40e65144f5ffe70c642fd354ca9bf28646` from [PR #25](https://github.com/ChuckreimuHakunorris/castrum-tactics/pull/25). Its reviewed head was `2bf5d0bcf5592ec479180af30031b69105327e07`. Milestone 9C adds engineering checks while retaining the existing game, research controls and test suite.

## Toolchain and policy

- Install the .NET SDK specified in `global.json`. It sets 10.0.404 as the minimum patch and accepts later stable patches in the 10.0.4xx feature band. Run `dotnet --version` from the checkout. To change bands, update the pin and repeat the solution, DocFX, Godot and managed Android checks together. CI uses the same file and provisions .NET 8 separately for the Godot job.
- Godot stays at 4.7.2 .NET with checksum verification in CI. The application targets .NET 8 on desktop and .NET 9 on Android. Domain and AI target .NET 8. Tests, Benchmarks and the link checker target .NET 10. Install the .NET 8 runtime separately when launching a desktop build that needs it. DocFX stays at 2.81.0.
- First-party projects use the .NET 10 SDK analyzer **Default** mode. It enforces the SDK's default-enabled diagnostics in warnings-as-errors builds, including reliability checks such as CA2014. It leaves opt-in analyzers and naming and language-style preferences advisory. Frozen projects keep their previous SDK analyzer settings.
- Enable CS1591 for public application, Domain and AI APIs. Compiler XML diagnostics remain active. Generated Godot members compiled without any new suppression. Internal helpers, test and benchmark types are outside the public API documentation gate. Review comments for semantic quality using the [contributor guide](contributing.md).
- Check added and changed tracked first-party C# files with the SDK whitespace formatter in an isolated folder. This covers the game, Domain, AI, tests, active Benchmarks and the documentation checker. The formatter receives no project references, frozen files, ignored generated files or Android template sources. The whole-tree trial exposed extensive existing formatting differences, so the gate applies to touched files to keep review diffs focused.

## Local commands and CI

Run from the repository root with Git, Python 3 and the pinned SDK. Use the Godot .NET executable on your path when running the [presentation commands](https://github.com/ChuckreimuHakunorris/castrum-tactics#developer-documentation).

```sh
dotnet clean CastrumTactics.sln
dotnet restore CastrumTactics.sln
dotnet build CastrumTactics.sln --no-restore -warnaserror
dotnet test CastrumTactics.sln --no-build -m:1
dotnet build Benchmarks -c Release -warnaserror
python3 scripts/verify-format.py --base origin/main
python3 scripts/verify-archive.py --base origin/main
bash scripts/build-docs.sh
```

Stage new C# files before checking formatting so Git includes them. For focused local runs, test `Tests/CastrumTactics.Domain.Tests.csproj` or `Tests/Presentation/CastrumTactics.Presentation.Tests.csproj` directly. The AI project supports standard xUnit name filters, for example `--filter FullyQualifiedName~Frozen8EParityTests`. Use `TournamentTests` or `SmallBoardSolverTests` in that expression to focus on the benchmark or solver suites. The full suite remains the default in CI.

PR verification has four jobs. Build and test runs the warnings-as-errors solution build and all xUnit cases. Godot presentation imports the assets and runs both headless regressions with the existing seed and assertions. Engineering checks whitespace and the historical archive against the PR base, builds Release Benchmarks and runs CLI help, a schedule dry-run, fixtures and a small generated match/replay round trip. Documentation builds fresh DocFX output and checks repository and rendered links with read-only permissions. A separate workflow still publishes to Pages on pushes to `main` and manual runs.

The link tool uses a Markdown parser with GitHub heading identifiers and reads actual HTML identifiers in the generated site. It checks tracked Markdown including README, AGENTS and archive notes, and generated HTML including API pages. It resolves case-sensitive local paths and fragments and leaves symbolic DocFX xrefs to DocFX warnings and the generated links. External URLs and fragments in non-Markdown/non-HTML resources are outside the check. Run the link fixtures with `dotnet run --project scripts/DocumentationLinks -- --self-test`.

## Archive integrity

`Benchmarks/ReferenceIntegrity.json` records SHA-256 over 96 checkout files pinned to the integrated 9B Git blobs. It covers the six frozen C# generations, retired layouts, historical reference datasets and solver results, `refactoring-9a.json` and the 8E study figure. Current maps, project wiring, scripts and documentation remain outside the manifest. The existing LF checkout policy keeps text bytes stable across hosts.

The verifier flags changed, deleted and unexpected protected paths. CI checks that all entries already in the base still exist with their reviewed hashes. When introducing a new protected file or collection with owner approval, add its scope and digest from the reviewed source and review the manifest diff explicitly. Existing entries must retain their digests. Repository review remains necessary because the manifest and verifier are themselves repository files.

## Verification record

Local baseline and feature checks used Debian 13.7 x86-64, .NET SDK 10.0.404, .NET runtimes 10.0.15 and 8.0.28, Godot 4.7.2 .NET and DocFX 2.81.0.

| Check | Result |
| --- | --- |
| Integrated 9B baseline | Warnings-as-errors solution and Release builds, 369 tests (165 Domain, 131 AI, 73 presentation), both Godot regressions and clean DocFX passed. Benchmark help, 60-match dry-run, 30 fixture records and replay of the saved 9A match passed. |
| 9C builds and presentation | Clean restore, warnings-as-errors solution and Release benchmark builds, all 369 xUnit tests (165 Domain, 131 AI, 73 presentation), and both Godot headless checks passed. The seeded Classic CPU match again ended 11–12 after 30 Main turns. |
| 9C benchmark smoke checks | Help and the 60-match dry-run passed. All 30 fixtures match the baseline outside timing fields. The generated legacy round trip passed and the saved 9A replay matched byte for byte. |
| 9C documentation | Clean metadata and site builds reported zero warnings. The link checker passed on 28 Markdown files and 80 generated HTML pages. Browser checks covered navigation, the engineering record, the study figure and representative API pages. |
| Compiler and analyzer negative probes | A disposable public API without XML failed with CS1591. A separate stack allocation inside a loop failed with CA2014. The probes were removed. |
| Formatting and links | Formatted first-party input passed. A misindented first-party probe failed while an intentionally misformatted frozen probe stayed out of the formatter. The link self-test exercised encoded paths, duplicate headings, wrong case, missing targets and missing fragments in Markdown and generated HTML. |
| Archive | All 96 digests match. Altered, missing and unexpected protected files and changes to existing approved entries were rejected in isolation. All six frozen generations match the documented Git revisions, including all 15 files at integrated 8E `b63e7fbbb5450b4f9a5b6bb4047579bc978669c5`. |
| Platform packaging | Windows x64 debug export produced a PE32+ executable, pack and managed sidecars. Both ordinary and instrumented Android managed builds passed. The clean Linux checkout initially failed to invoke `gradlew`. The executable-bit fix preserves the wrapper bytes. The retry produced a signed ARM64 debug APK using a disposable key. Signature and 16 KiB alignment checks passed. The package ID is `com.castrumtactics.prototype`, with minimum API 24 and target API 36. See [platform verification](PlatformVerification.md) for installation and runtime procedures. |

The existing Godot Android editor-settings teardown diagnostic can appear even after a successful headless import or export. The baseline test host also reported a VSTest artifact-directory fallback when the environment variable used by CI was absent. The Linux host had no native Windows runtime or connected Android device. Native Windows launch and physical Android installation, gameplay, rotation, latency and thermal observations remain outstanding.
