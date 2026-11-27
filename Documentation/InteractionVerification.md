# 10C interaction and presentation verification

Integrated 10B `4fa2fec5b8ceb0873e2a35d5762f4ae716db2d96` (PR #28) is the comparison baseline. The owner tested 10B on Windows and accepted its art integration architecture. The art still needs 10C.1 refinement. [Visual roadmap](VisualDevelopmentRoadmap.md)

## What to review

- Main shows the active and opposing legal sets in their respective faction colors. Shared targets have the active faction on top and left and the opposing faction on bottom and right. A synchronized 2.5-second cycle varies opacity from 80% to 100%. The cyan provisional frame stays static. A shared provisional square retains its outside two-color frame.
- The board and picker share a neutral gold version of the existing territory shield silhouette. The pale-blue battlefield frame and shadow sit outside the rectangular grid. The existing mouse and touch transforms use the grid itself.
- The tube fills from its two faction ends in fixed Player One and Player Two order. The authoritative denominator is the sum of both secured totals and every contested tile. Main's existing legal selection logic only accepts contested targets within reach. Contested cells outside each player's current range still count. The Domain reports graves and owner-free isolated components as neutral. Those cells and inaccessible terrain stay outside the denominator.
- Draft has empty fill. Main and Finished use the same proportions, including a game that finishes as the draft resolves. Numeric scores update on the resolved snapshot. Decorative fills retarget from their displayed level over 0.38 seconds. Shared boundary displacement keeps both sides apart even at a combined fill of 100%. Reduced motion disables slosh and interpolation, keeps legal outlines fully opaque and selects the static ambient background.
- Both modes continue to receive the same authoritative legal sets. A one-sided submission updates status without discarding a provisional selection or restarting the liquid transition. Opponent-only indicators never make a tile selectable.

## Changing preferences during development

The three defaults are shown below. Before starting the app, create or edit `presentation.json` in the Godot project user-data directory. Omitted fields and fields with invalid types use their defaults. An unreadable or malformed file uses the full default set.

```json
{
  "ShowOpponentLegalTargets": true,
  "ReduceMotion": false,
  "PlayTurnResolutionAnimations": true
}
```

- Windows uses `%APPDATA%\Godot\app_userdata\CastrumTactics\presentation.json` with the standard project settings.
- On an Android debug install, use `adb shell run-as com.castrumtactics.prototype ls files` to inspect the app's internal files. With the app closed, send the JSON from a shell with `adb shell 'run-as com.castrumtactics.prototype sh -c "cat > files/presentation.json"' < presentation.json`. Launch the build to apply the file. Use the existing debug deployment instructions for more details. [Platform verification](PlatformVerification.md#android-export)
- In C#, call `PresentationSettings.Application.Update` on the scene thread with the desired immutable preferences. It saves the value and notifies the active scene immediately. The Godot checks inject a pathless store and change settings on a live match. Turn-resolution animation is persisted independently for use in 10E.

## Rendered evidence

- The same resolved Demonstration board shows both player perspectives, cyan selection on a shared target, opponent indicators hidden, and reduced motion. The opening panel shows the gold shield design and empty tube. [Target perspectives](assets/10c/targets.jpg)
- The captured collision reduced claimable area from 28 to 27. The walkthrough ends 8–18 with the two liquids meeting at the authoritative boundary. The section includes the full layout and close crops. [Territory before and after](assets/10c/territory.jpg)
- The screen captures show Twelve Towers (12 × 12) in portrait Draft and landscape Main, plus the live New Match preview. [Large board and picker](assets/10c/layouts.jpg)
- Three consecutive frames from one Main position, about 0.62 s apart, show the faction outlines changing in sync and the two liquid boundaries shifting. [Animation sequence](assets/10c/motion.jpg)

## Rendering observations

Measured on Debian 13.7 x86-64 with Godot 4.7.2 .NET Compatibility and Mesa llvmpipe. The matched captures use the same draft choices on 7 × 5 Demonstration and 12 × 12 Great Divide. A synthetic 12 × 12 board passes 64 pieces and 80 authoritative targets through the production renderer. The active 10B baseline runs the ambient background. Active 10C adds the legal pulse and, where scores are nonzero, the tube shader. Settled runs use static presentation. The table shows median frame intervals in ms after warm-up, using 48-frame samples with two windows for the ordinary maps in the follow-up run. These intervals include the software renderer and host scheduling.

| 900 × 650 scene | Settled 10B → 10C (ms) | Active 10B → 10C (ms) | Draw calls 10B → 10C |
| --- | --- | --- | --- |
| Demonstration | 7.7–8.9 → 9.0–9.8 | 8.2 → 10.2–10.6 | 54 → 55 |
| Great Divide | 7.4–8.2 → 8.7–9.0 | 8.3–8.4 → 9.8–10.5 | 70 → 71 |
| Dense synthetic | 9.9 → 12.5 | 11.4 → 12.8 | 174 → 176 |

At 700 × 940, settled 10C medians were 10.6–11.1 ms on Demonstration and 11.0–11.7 ms on Great Divide. The dense active 95th percentiles were 21.4 ms (10B) and 20.1 ms (10C). The inspected scenes kept the same piece count, with one additional scene node and six resource objects. Godot's static-memory counter grew by about 124–165 KiB. Pulse uses one existing indicator layer with one material, so it neither reevaluates territory nor updates tiles individually. Repeated short samples varied with host scheduling. These observations do not establish native Windows or Android performance. [Matched synthetic boards](assets/10c/stress.jpg)

## Engineering and platform checks

- Local solution and Release benchmark builds completed with warnings treated as errors. All 422 xUnit tests passed (175 Domain, 131 AI, 116 presentation). Both headless Godot presentation checks passed, covering the existing Classic and Knight Support walkthroughs, desktop mouse and simulated touch, the seeded 11–12 CPU result, layout, resize, restart, replacement and live preferences. The rendered playback used the same 10C game scenes on the Compatibility renderer.
- All 32 paired histories (2,571 rounds) match integrated 10B in outcome, recorded history and pair counts. They cover Classic and Knight Support with natural and fixed openings, using eight matches per group from the existing 9A schedules. The archived match replayed with an identical report. Release CLI smoke, all 96 protected archive hashes, C# formatting and DocFX checks passed. Production map, visual sidecar, artwork, Domain, Match, AI and benchmark files match 10B.
- Both ordinary and timing-enabled Android managed builds passed. The Windows x64 ZIP and Android ARM64 debug APK were exported on Linux. The packages contain the four new shaders, their shader includes and the managed game. The APK retains `com.castrumtactics.prototype` with API 24 minimum and target 36. Signature and 16 KiB ZIP alignment passed. The APK uses a disposable cloud signing key. Building with the original signing key is required for an in-place update of an existing installation. Godot emitted its existing Android settings teardown message after successful export.
- Native Windows execution and physical Android install, rotation, touch, sustained frame pacing and preference file behavior remain owner-device checks. [Engineering commands](EngineeringVerification.md#local-commands-and-ci) · [Platform commands](PlatformVerification.md)

## Owner device checklist

1. Open Demonstration in both local modes. Compare active-only, opposing-only and shared frames. Change the local active player and verify that the shared corners flip. Select a shared square and check that the cyan frame clearly identifies it during the pulse. Tapping an opposing-only square should preserve the current choice.
2. Compare draft shields on the board and in New Match. Restart or replace the match and check the empty banner, scores and selection. Play a collision and compare the new grave with the previous territory proportions.
3. Open a 12 × 12 battlefield and rotate between portrait and landscape during a provisional selection. Test inside and outside the corner cells with a mouse or tap. Check the preview and the smallest shields for legibility.
4. Test with opposing indicators hidden and reduced motion on. During Main, check that the background, legal frames and liquid stay still, and that authoritative scores still update. Check animation pacing and frame consistency with reduced motion off. Compare the art separately during the 10C.1 review.
