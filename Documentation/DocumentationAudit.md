# Milestone 9B documentation audit

The starting point was integrated 9A at `ea4d4823f226d0bd2a18e0b3d55d937e728aeb7a` (PR #24, reviewed head `8700d3148151565deac32f12f8ed091eaed20f70`). This audit reconciles descriptions with that implementation. [Architecture](architecture.md) · [Roadmap](DevelopmentRoadmap.md) · [Decision log](DecisionLog.md)

## Coverage

The integrated baseline contains 556 tracked paths. The inventory covered root configuration, workflows, source, tests, documentation, map data, assets and template and archive boundaries. The table records the scope of the semantic pass.

| Area | Evidence and outcome |
| --- | --- |
| README, AGENTS and DocFX | Checked setup and launch flags against `CastrumTactics.csproj`, `project.godot` and `MatchLaunchOptions`. Preserved README anchors and moved the Classic walkthrough and Android export detail to the [platform guide](PlatformVerification.md). Checked SDK levels, ARM64, package ID, rotation and signing guidance against export settings, Gradle config and the Windows installer. DocFX reads the application, Domain and AI. Its publish workflow runs on pushes to main and manual dispatch. |
| Current design and maps | Replaced the local-only documentation introduction with both input modes and the 100-map catalog. Replaced the old GDD sequence and planned-Main wording with current design and roadmap links. Cross-checked draft eligibility, simultaneous resolution, support, territory and scores against Domain and tests. Checked catalog JSON and map validation. Authoring preferences and structural checks supply different evidence from competitive balance. |
| Domain and session | Reviewed public commands, snapshots, resolution, ruleset defaults, preview and publication. Clarified coordinate privacy, round IDs including forced collisions, common draft transitions, post-action support and castle boundaries in territory classification. Domain convenience overloads retain Classic. |
| AI | Reviewed profile values, engine, ranking, probability cutoffs, tactical and strategic evaluation, search accounting, proof and numerical paths. Clarified that Very Hard uses resilient scoring and 500 Main pairs, and Monster uses separate 256/850-pair draft/Main configurations and matrix probabilities when available. The 450 ms scene safeguard is cooperative. Benchmark tournaments default to Knight Support and can configure elapsed limits. |
| Match, scenes, shaders and tests | Checked scene defaults and wiring, observer order and worker retirement, board input geometry and highlight retention, responsive layout, dialog and score banner, and shader comments. Added lifecycle, coordinate and alpha-mask explanations. Checked representative Domain, frozen CPU and presentation tests and documented why separate histories and synchronization matter. |
| Benchmarks, solver and research | Read the research articles, command dispatch and key owners, including scheduling, opponent provenance, replay, persistence, reporting, measurement, tactical and opening studies, and solver progress and bounds. Checked representative manifests and historical map definitions. Corner Watch and Stone Center are both 3 × 3 with 7 and 8 playable cells, respectively. Pocket Meadow is 3 × 3 with 9. The 8E manifest generator identifies Open Meadow (6 × 5), Alder Common (8 × 8), Honeystone Green (10 × 10), Twelve Towers (12 × 12) and ten synthetic layouts. The audit command reproduces their playable-area metadata from the map definitions. Corrected prose metadata and dated 8D.3 plans without changing tables or results. |
| Frozen, generated and third-party material | Inspected frozen project boundaries, the provenance manifest and archive notes, export and template config, and representative asset references. The tracked inventory included UID/import files, binary assets, Android template and generated resources. Review did not extend to every binary asset, generated method, third-party template body, archived result row or individual test assertion. The external project-files directory was outside scope. |

## Public C# API

A Roslyn syntax inventory covered 60 first-party C# files under AI, Domain, Match and Scenes. Each visible public declaration had a summary or inheritdoc. The semantic pass prioritized rules and snapshots, CPU cancellation and time limits, and scene lifecycle. XML edits clarify when the score banner can be updated, what the search limit covers, and which information can be read or reused from a session snapshot.

A command-line rebuild with `-p:NoWarn= -p:WarningsNotAsErrors=CS1591 -p:EmitCompilerGeneratedFiles=true` emitted zero warnings on the pinned toolchain. The inspected Godot-generated script containers and cached names carried generated XML summaries. This probe is specific to the current SDK and source shape. For 9C, trial an application CS1591 build on the existing SDK or a source-only Roslyn check that excludes generated trees if future generators introduce undocumented symbols.

## Verification

The baseline solution and DocFX builds were warning-free and all 369 xUnit tests passed. Feature verification used .NET SDK 10.0.404, Godot 4.7.2 .NET and DocFX 2.81.0 on Linux.

| Check | Result |
| --- | --- |
| `dotnet build CastrumTactics.sln -warnaserror` and `dotnet test CastrumTactics.sln --no-build` | Zero build warnings and 369 passing tests (165 Domain, 73 presentation, 131 AI), including independent domain histories and frozen 8E parity. |
| `dotnet build Benchmarks -c Release -warnaserror` | Zero warnings. Tournament help, round-robin and 8E baseline dry-runs (60 and 320 matches), 30 legacy fixture records, battlefield audit and replay of a saved 9A baseline match passed. The full 8E tournaments and large tablebase runs were outside this smoke check. |
| Godot import and both [headless regressions](https://github.com/ChuckreimuHakunorris/castrum-tactics#developer-documentation) | Both passed. The seeded CPU regression ended 11–12 in 30 Main turns. Import exited successfully with the existing Android settings teardown diagnostic. |
| `dotnet tool restore`, DocFX metadata and site builds with `--warningsAsErrors` | Clean generated outputs from a fresh worktree, zero warnings. Reviewed generated navigation, representative conceptual and API pages, the architecture sketch, research figure, tables, and command examples. |
| Links and references | Checked 27 repository Markdown files (including README, AGENTS and the benchmark archive), 217 local links, 66 Markdown xrefs through DocFX, and 1,072 local links and fragments across 79 generated HTML pages. Verified important official URLs for Godot editor input and Android setup, Android Gradle SDK downloads, GitHub Pages and DocFX (HTTP 200). This external check covered the five selected destinations. |
| Source and archive integrity | Roslyn compared exact C# token kinds and text and preprocessor directives across 33 changed files, including the timing source with instrumentation enabled. Python tokenization and exact shader byte comparison confirmed comment-only edits. Git found no changes to frozen source, archived maps, reference artifacts (including `refactoring-9a.json`), production maps, scenes, Android templates, project settings or workflows. |

## Follow-up

9C can consider PR-time DocFX and link validation, first-party XML enforcement, consistent warnings and analyzers, formatting and automated frozen-integrity checks. Native Windows launch and physical Android install, rotation, sustained play and device measurements remain outstanding for the integrated baseline. The [platform guide](PlatformVerification.md) gives the reproducible steps.
