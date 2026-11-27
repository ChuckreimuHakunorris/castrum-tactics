# Development roadmap

## Completed

| Work | Outcome |
| --- | --- |
| Project and domain foundation | Godot .NET and Compatibility renderer, engine-independent C# rules and xUnit, configurable boards and terrain, Unlimited Draft, simultaneous Main and final scoring. [Architecture decisions](architecture.md#milestone-4-decisions) |
| Local match and battlefield selection | Shared-device play, board input and presentation, responsive layout, and the 100-map production catalog. [Session architecture](SessionArchitecture.md) · [Map catalog](MapFormat.md) |
| 8A–8B | Engine-independent CPU proposals and asynchronous Versus CPU integration. Startup defaults to Normal on a randomly selected battlefield. [8A decisions](architecture.md#milestone-8a-decisions) · [8B decisions](architecture.md#milestone-8b-decisions) |
| 8B.1–8B.2 | Knight Support starting castles and whole-board support resolution, followed by ruleset selection within the local application and diagnostics. [Knight Support decisions](architecture.md#milestone-8b1-decisions) · [Ruleset decisions](architecture.md#milestone-8b2-decisions) |
| 8C | Simultaneous tactical search for Knight Support, with preserved Classic static play. [8C decisions](architecture.md#milestone-8c-decisions) |
| 8D.1–8D.4 | Reproducible tournaments and exact small-board solver, then search optimization, strategic evaluation and Monster's advanced draft policy. [Tournament guide](CpuTournaments.md) · [Solver](SmallBoardSolver.md) · [Search optimization](CpuSearchOptimization.md) · [Strategic evaluation](CpuStrategicEvaluation.md) · [Opening strategy](CpuOpeningStrategy.md) |
| 8E | Initial empirical ladder calibration and battlefield studies. The selected configuration is the stopping point for initial CPU development. [Calibration](CpuDifficultyCalibration.md) · [Battlefield balance](BattlefieldBalance.md) |
| 9A | Behavior-preserving ownership changes across Domain, AI, Godot and Benchmarks, checked against frozen 8E and independent domain histories. [9A refactoring](ArchitectureRefactoring.md) |

| 9B | Reconciled documentation and comments with post-9A code and verified the historical evidence. [9B audit](DocumentationAudit.md) |

| 9C | Integrated compiler, formatting, documentation, archive and platform verification checks. [Engineering verification](EngineeringVerification.md) |

| 10A | Integrated visual sidecars, board layers, fit geometry and resolved-turn records. [Visual foundation](VisualFoundationVerification.md) |
| 10B | Integrated board art, terrain registry, damage cues and cardinal fortifications through PR #28. The owner accepted the architecture and requested a separate artwork refinement pass. [Board art verification](BoardArtVerification.md) |
| 10C | Integrated interaction indicators, territory and presentation preferences through PR #29. [Interaction verification](InteractionVerification.md) |
| 10C.0 | Integrated the native rendering benchmark through PR #30 and collected all seven Windows and Android baseline runs. Rendering is satisfactory on tested hardware. Android turn-resolution efficiency is the main optimization opportunity. [Native baseline](NativeRenderingBaseline.md) |

## Current

**10C.1a** is next and covers knight artwork refinement. The remaining 10C.1 art reviews follow, then 10C.2 evidence-driven rendering optimization, 10D Android zoom and panning, 10E animation and feedback, and 10F curated battlefield beautification. [Visual roadmap](VisualDevelopmentRoadmap.md)

## Planned

- A basic map editor and online multiplayer remain planned. The map data format for user-created maps, backend and hosting, remote authority and restart policy, competitive eligibility, accounts and ranked features are decisions still to be made. [Open design decisions](GameDesign.md#12-open-decisions-and-questions)

## Deferred research

- Human playtesting and further Easy/Normal calibration. Fixed openings can reverse their ordering in selected schedules.
- Compact tactical weaknesses, including Monster on retired Pocket Meadow, and broader candidate coverage and evaluation on large boards. Explicit multi-front planning is a hypothesis to investigate. The size study found stronger-policy separation on some intermediate maps and mixed results elsewhere.
- Mobile latency, allocation, thermal and battery measurements. Catalog structural flags warrant competitive review before any proposed changes to maps or starts.
- Explore optional initial ground health in a later map-format and editor design review. [Deferred map idea](MapFormat.md#deferred-initial-ground-health)

[CPU calibration and limitations](CpuDifficultyCalibration.md) · [Battlefield study](BattlefieldBalance.md)

## Platform verification

9A verified Windows x64 and signed Android ARM64 package contents. The [9C verification record](EngineeringVerification.md#verification-record) tracks that milestone’s packaging and managed build results. 10C.0 verified native rendering on Windows and physical Android, including both phone orientations and a sustained portrait run. Full manual rotation and gameplay walkthroughs remain separate checks. The 8E report records earlier instrumentation and subjective observations on their own revisions. [Native baseline](NativeRenderingBaseline.md) · [Platform walkthroughs](PlatformVerification.md) · [8E device evidence](CpuDifficultyCalibration.md)
