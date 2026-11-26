# Visual development roadmap

Milestone 10 prepares Castrum Tactics for broader playtesting before the map editor and online multiplayer. Each stage has a separate branch and review. The current [art direction](ArtDirection.md) describes the shipped baseline. See [visual architecture](VisualArchitecture.md), [map format](MapFormat.md), [session architecture](SessionArchitecture.md), and [engineering verification](EngineeringVerification.md) for shared boundaries and checks.

## 10A · Visual architecture foundation

Integrated through PR #27. Introduced optional visual sidecars with stable variants, a resource lookup and documented draw order, one fit geometry, and a resolved-turn contract. Preserve recognizable default art, rules, the production map catalog, and current mouse and touch input. Acceptance requires an end-to-end sidecar fixture, deterministic gameplay comparisons, and regression checks. The [10A verification record](VisualFoundationVerification.md) lists the evidence.

## 10B · Board art and tile rendering

Implemented and awaiting review. The owner supplied the three native PNGs in a cloud file archive. The [art verification](BoardArtVerification.md) distinguishes reference pixels, alpha isolation, livery normalization and newly authored terrain and walls.

Fixed terrain roles validate against existing gameplay. Playable grass, sand, snow and bridges have damage art and damage-only marks. Rocks, forest, shrubs, mountains, water, settlements and ruins are inaccessible. Same-faction castles join only in four cardinal directions. All maps receive the new default art and two compatible sidecars demonstrate authored geography. The full composition pass remains 10F.

## 10C · Interaction indicators and UI polish

Depends on the visual vocabulary established in 10B. Add opaque, clearly readable faction-colored legal frames with a subtle pulse and a distinct provisional highlight. Show opposing legal targets from authoritative public sets in both modes. Render shared targets with a half-and-half frame and decide how shared draft starts should appear. Pending coordinates stay private.

Add a border and subtle shadow outside logical cells. Add restrained liquid-like fills to the existing score bars using authoritative secured counts. Propose initial playable area as the denominator and confirm it with the owner. Inaccessible squares contribute no territory. Verify clipping, waves, zero/full states and readable numbers, with reduced-motion support. Preserve hit testing, controls, score alignment and legibility in both orientations and input modes.

## 10D · Android zoom and play comfort

Depends on the shared geometry from 10A and the readable controls and indicators from 10C. Add pinch-to-zoom and pan on Android only. Keep the minimum at fit, select a useful maximum in playtests, keep cells square and clamp pan so all cells remain reachable. Apply one transform to board content, effects and input mapping. Keep chrome and controls usable and unzoomed.

A pinch or drag must never select or submit. Verify emulated events, multitouch, cancellation, deliberate tap and tap-to-confirm, button submission and rotation. Reset on New Match, retaining relative focus on rotation and providing a return-to-fit action are proposals. Check small and large boards, portrait and landscape, and desktop input regressions. Record physical Android and simulated input evidence separately.

## 10E · Animation and feedback

Depends on the 10A turn record, 10B art, 10C indicators and 10D board transform. Add subtle knight idle motion and feedback for placements, captures, upgrades, collisions, graves and support eliminations. Present the authoritative simultaneous result. Keep the final board and scores correct when effects are skipped, interrupted, reduced or overlap with rapid turns. Gameplay continues independently of decorative effects.

Highlight the opponent's latest resolved action. Decide perspective in local two-human play and how to show upgraded, eliminated, collided or automatically selected squares without implying a surviving piece. Use a restrained reveal and retain the useful marker until superseded or dismissed. Identify events by session and round, and cancel obsolete effects on replacement and restart. Check readable timing, reduced motion and large-board performance on both platforms.

## 10F · Curated battlefield beautification

Depends on the art bank, overlays, zoom and feedback. Compose coherent landscapes reminiscent of classic JRPG overworlds. Use purposeful rivers, lakes, bridges, mountains, vegetation clusters, settlements, sparse ruins, biome transitions and restrained variants. Prefer authored geography or reviewed deterministic tools. Preserve existing layouts and start order by default through visual metadata. Develop a curated set of candidate maps. The owner decides the number and any production additions or retirements.

Decorations preserve visibility of indicators, starts, territory, damage and pieces. A bridge's image does not confer traversal. Verify structural validity, previews, multiple board sizes and both orientations, and provide representative images for review. Validate gameplay and balance for any proposed new layouts. Competitive eligibility remains a separate decision, and historical fixtures stay intact.

## Shared constraints

Domain owns rules and authoritative outcomes. Presentation keeps map visuals deterministic, preserves pending-selection privacy and uses consistent faction colors. Validate readability, Android safe areas, and representative performance. Record sources and rights for art and distinguish headless checks, package inspection and physical-device evidence. Update this roadmap and the relevant documentation and tests at each stage. The map editor, online multiplayer, ranked systems and new CPU research remain deferred pending this phase or an owner decision.
