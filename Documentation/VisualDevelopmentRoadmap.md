# Visual development roadmap

Milestone 10 prepares Castrum Tactics for broader playtesting before the map editor and online multiplayer. Each stage has a separate branch and review. The current [art direction](ArtDirection.md) describes the shipped baseline. See [visual architecture](VisualArchitecture.md), [map format](MapFormat.md), [session architecture](SessionArchitecture.md), and [engineering verification](EngineeringVerification.md) for shared boundaries and checks.

## 10A · Visual architecture foundation

Integrated through PR #27. Introduced optional visual sidecars with stable variants, a resource lookup and documented draw order, one fit geometry, and a resolved-turn contract. Preserve recognizable default art, rules, the production map catalog, and current mouse and touch input. Acceptance requires an end-to-end sidecar fixture, deterministic gameplay comparisons, and regression checks. The [10A verification record](VisualFoundationVerification.md) lists the evidence.

## 10B · Board art and tile rendering

Integrated through PR #28. The owner supplied the three native PNGs in a cloud file archive. The [art verification](BoardArtVerification.md) distinguishes reference pixels, alpha isolation, livery normalization and newly authored terrain and walls.

Fixed terrain roles validate against existing gameplay. Playable grass, sand, snow and bridges have damage art and damage-only marks. Rocks, forest, shrubs, mountains, water, settlements and ruins are inaccessible. Same-faction castles join only in four cardinal directions. All maps receive the new default art and two compatible sidecars demonstrate authored geography. The full composition pass remains 10F.

## 10C · Interaction indicators and UI polish

Uses the integrated 10B rendering and art as the review baseline. Both authoritative Main target sets are visible by default. Shared frames use the active faction on top and left and the opposing faction on bottom and right. Opening starts use gold shields. The static pale cyan frame identifies the provisional square. Pending choices stay private.

The secured-territory tube uses a dynamic denominator of secured plus contested cells. The board has a pale-blue frame outside its logical cells. Persistent preferences control opposing targets, reduced motion and future turn-resolution animations independently. [10C verification](InteractionVerification.md)

## 10C.0 · Native rendering performance baseline

Completed. All seven native Windows and Android runs passed at scenario version 2, including the 20-minute Xperia run. The current renderer performs satisfactorily on the tested hardware. Artwork refinement can proceed. Android turn-resolution efficiency is the principal optimization opportunity. [Native baseline and review](NativeRenderingBaseline.md) · [Benchmark procedures and historical results](RenderingBenchmarks.md)

## 10C.1 · Artwork refinement and terrain autotiling

The owner tested 10B on Windows and accepted its rendering and asset architecture. The artwork needs substantial refinement. The owner and ChatGPT review individual source graphics, and the owner supplies approved originals to Codex. Codex performs the required isolation, alignment, import, faction masking and verification. Review each asset family in-game before proceeding. Ask the owner before redrawing or distorting approved art beyond necessary technical processing. The owner will supply the larger JRPG-inspired battlefield reference and the forest and mountain connection diagrams during the appropriate refinement tasks.

1. 10C.1a implements the knight with tintable red livery and gold lion heraldry.
2. 10C.1b implements the singleton castle with a source-derived mask for the banner and foundation.
3. 10C.1c implements full-tile connected castles assembled offline from façade art, a rampart block, pavement and a faction crystal. Roof tiles add a side rampart at each adjoining friendly façade. The owner approved the horizontal façade endcaps that distinguish opposing fortresses.
4. 10C.1d implements the neutral grave with surrounding dirt and no vegetation.
5. 10C.1e covers playable ground and damage.
6. 10C.1f covers forest and mountain autotiles.
7. 10C.1g covers rivers, lakes and bridges.
8. 10C.1h covers remaining terrain, vegetation, ruins and settlements.

10C.1a–10C.1d are implemented and await owner in-game acceptance. The remaining 10C.1 reviews cover terrain. Start forests and mountains with the 16 cardinal masks. Neighbors should form continuous forests and ranges, with compatible edges, consistent scale, lighting and perspective, and artwork constrained to its assigned footprint. Keep deterministic variants seam-safe. Build reusable adjacency-driven rendering and review assembled boards. Consider diagonal-sensitive corners if assembled configurations need them. Future follow-up includes water connectivity validation, default terrain variation and an investigation of batching efficiency. [Game piece verification](BoardArtVerification.md#10c1a10c1d--approved-game-piece-artwork)

## 10C.2 · Evidence-driven rendering optimization

Repeat the [native baseline](NativeRenderingBaseline.md) after 10C.1. Include uncapped throughput and isolated visual-effect comparisons where practical. Use the reachable damage replay for ordinary play and the extreme fixture for stress comparison. Confirm the actual framebuffer and render-target dimensions before estimating pixel workload.

| Investigation | Priority |
| --- | --- |
| Incremental piece sprite synchronization | Highest |
| Ground damage marker batching | Medium–high |
| Actual framebuffer and render resolution verification | Medium–high |
| Texture atlases and material batching | Medium |
| Managed allocation reduction | Medium |
| Fixed-action-sequence benchmarking | Medium |
| Animated background optimization | Medium–low |
| Frame-rate and battery-saving preferences | Medium–low |
| Reflection-free synthetic fixture construction | Low |
| Texture memory management | Low |
| Mipmap evaluation | Low |

These priorities guide investigation. Select changes according to measured costs and benefits. The frame tags and timing evidence make sprite synchronization a plausible improvement, with causality and savings to be established in isolated comparisons.

### Provisional performance targets

Use these engineering targets for future regression comparisons on the tested i5-7500 and GTX 1060 PC and Xperia 1 V. They guide investigation. Device-wide requirements and release acceptance criteria are outside this baseline.

| Metric | Windows | Android |
| --- | ---: | ---: |
| Normal gameplay FPS | ≥59 | ≥59 |
| Normal gameplay P95 | ≤18 ms | ≤22 ms |
| Normal gameplay P99 | ≤22 ms | ≤30 ms |
| Frame intervals exceeding 33.33 ms | <0.1% | <0.5% |
| Turn-resolution sprite synchronization P95 | ≤2 ms | ≤3 ms |
| Sustained frame-rate degradation | <5% | <5% |
| Persistent unexplained memory growth | None | None |

## 10D · Android zoom and play comfort

Depends on the shared geometry from 10A and the readable controls and indicators from 10C, with the art refinement stage before device zoom. Add pinch-to-zoom and pan on Android only. Keep the minimum at fit, select a useful maximum in playtests, keep cells square and clamp pan so all cells remain reachable. Apply one transform to board content, effects and input mapping. Keep chrome and controls usable and unzoomed.

A pinch or drag must never select or submit. Verify emulated events, multitouch, cancellation, deliberate tap and tap-to-confirm, button submission and rotation. Reset on New Match, retaining relative focus on rotation and providing a return-to-fit action are proposals. Check small and large boards, portrait and landscape, and desktop input regressions. Record physical Android and simulated input evidence separately.

## 10E · Animation and feedback

Depends on the 10A turn record, 10B art, 10C indicators and 10D board transform. Add subtle knight idle motion and feedback for placements, captures, upgrades, collisions, graves and support eliminations. Present the authoritative simultaneous result. Keep the final board and scores correct when effects are skipped, interrupted, reduced or overlap with rapid turns. Gameplay continues independently of decorative effects.

Highlight the opponent's latest resolved action. Decide perspective in local two-human play and how to show upgraded, eliminated, collided or automatically selected squares without implying a surviving piece. Use a restrained reveal and retain the useful marker until superseded or dismissed. Identify events by session and round, and cancel obsolete effects on replacement and restart. Respect the independent turn-resolution animation preference. Use restrained feedback when reduced motion is on and show the authoritative result immediately when resolution animations are off. Check timing and large-board performance on both platforms.

## 10F · Curated battlefield beautification

Depends on the art bank, overlays, zoom and feedback. Compose coherent landscapes reminiscent of classic JRPG overworlds. Use purposeful rivers, lakes, bridges, mountains, vegetation clusters, settlements, sparse ruins, biome transitions and restrained variants. Prefer authored geography or reviewed deterministic tools. Preserve existing layouts and start order by default through visual metadata. Develop a curated set of candidate maps. The owner decides the number and any production additions or retirements.

Decorations preserve visibility of indicators, starts, territory, damage and pieces. A bridge's image does not confer traversal. Verify structural validity, previews, multiple board sizes and both orientations, and provide representative images for review. Validate gameplay and balance for any proposed new layouts. Competitive eligibility remains a separate decision, and historical fixtures stay intact.

## Shared constraints

Domain owns rules and authoritative outcomes. Presentation keeps map visuals deterministic, preserves pending-selection privacy and uses consistent faction colors. Validate readability, Android safe areas, and representative performance. Record sources and rights for art and distinguish headless checks, package inspection and physical-device evidence. Update this roadmap and the relevant documentation and tests at each stage. The map editor, online multiplayer, ranked systems and new CPU research remain deferred pending this phase or an owner decision.
