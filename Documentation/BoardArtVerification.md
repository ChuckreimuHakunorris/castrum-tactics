# 10B board art verification

Milestone 10B starts from integrated 10A commit `3ef9383d9015d7d24c7ca7f9db16e9a631a7c542` (PR #27). The asset, renderer and test changes are in `ff470192dc86619bfae6772a973e9b79a7d614dd`. The later review commit records the evidence. [Art direction](ArtDirection.md) · [Visual architecture](VisualArchitecture.md) · [Roadmap](VisualDevelopmentRoadmap.md)

## Source and extraction

The owner provided three original PNGs in `Files.rar`. The two numbered reference images were compared at 1448 × 1086. The cleanest blue knight, blue singleton castle and neutral gravestone were in the 1476 × 1065 `Crescent Quarry with Blocky Graves.png`. This is the actual archive filename. SHA-256 of the selected original is `51ded5437ab7a11ad63a4ea64c108e2fce912c3ae6b1f36686f478be0902a74c`. Original files remain with the owner.

| Native crop | Bounds (left, top, right, bottom), exclusive right and bottom |
| --- | --- |
| Blue knight | `(228, 927, 329, 1027)` |
| Blue castle | `(132, 927, 230, 1027)` |
| Gravestone | `(132, 240, 230, 341)` |

The base images preserve the reference silhouette, spear, armor, gold details, heraldry, gate and gravel-gray headstone. Hand-traced alpha isolates the native pixels at their original scale. Cleanup removes the green tile and orange ground, including some background-blended edge pixels. The two colored pieces use luminance-matched neutral pixels and a grayscale coverage mask in originally blue regions. Thin source-edge antialiasing remains the main visible compromise. Fully transparent pixels use neutral padding. Faction colors are applied at render time.

The 15 connected castles retain the upright singleton front and add new stone and faction-tinted trim at the appropriate cardinal edges. The terrain bank and decorations were drawn for the game at 128 × 128. Optional waterfall and cattail studies from the reference remain open for 10F. Distribution and reuse rights outside the prototype remain unverified.

To regenerate the artwork and comparison sheets, use Python with Pillow and the three original files in a local directory. Run from the repo root.

```sh
python3 scripts/art/extract-pieces.py "$REFERENCE_DIRECTORY/Crescent Quarry with Blocky Graves.png"
python3 scripts/art/build-terrain.py
python3 scripts/art/build-walls.py
python3 scripts/art/make-sheets.py "$REFERENCE_DIRECTORY/Crescent Quarry with Blocky Graves.png"
```

## Asset sheets

- Native source tiles, alpha isolation, neutral bases, masks and four colors. [Piece extraction sheet](assets/10b/reference-pieces.png)
- Intact playable surfaces, blocked terrain, accents, variant names and river, lake and bridge seam samples. [Terrain bank](assets/10b/terrain-bank.png)
- Grass, sand, snow and both bridge directions at all four health stages, with occupied examples. Healthy land has no marks. Pips count health lost, regardless of which piece occupies or blocks the cell. [Damage matrix](assets/10b/ground-damage.png)
- All 16 masks in both default faction colors plus green and violet. Testing verifies enemy, knight, grave and diagonal neighbors cannot authorize a join. [Castle topology and faction colors](assets/10b/castle-masks.png)

## In-engine captures

The two comparison sheets use the same Classic local opening positions on each revision. Source viewports were 1080 × 720 and 720 × 1000. The representative maps are Demonstration (7 × 5), Wrenfield (8 × 9), and Great Divide (12 × 12). The capture ran with a static background on Godot’s Compatibility renderer. No gameplay map entries changed. [Landscape comparison](assets/10b/board-comparison-landscape.jpg) · [Portrait comparison](assets/10b/board-comparison-portrait.jpg)

The play sheet shows draft dots, a provisional Main target, a resolved Knight Support castle join, and a terrain preview. Demonstration shows the new mountain, forest, ruins and sand families. Great Divide uses existing crossings for the explicit east–west bridge motif within the illustrated north–south river. New Match reads the same terrain and accent assets. [Play and preview](assets/10b/play-and-preview.jpg)

Two synthetic boards render isolated castles, pairs, corners, straight lines, T and cross joins, a 2 × 2 group, mixed factions, diagonal neighbors and dense castle clusters through the production renderer. Damaged ground beneath knights and graves appears in the bottom row. [Fortifications in-engine](assets/10b/fortifications-in-engine.jpg)

## Rendering observations

All six map and orientation combinations preserved their grid bounds and piece sprite counts. The candidate added one damage-layer node and loaded 9–13 more resource objects in the capture process. Godot's static memory counter increased by about 65–85 KiB. This is an engine allocation counter. A 104 × 104 base/mask pair takes at most 84.5 KiB for uncompressed texels at RGBA8. A 128 × 128 pair takes 128 KiB. The assets use lossless import and no mipmaps. The engine shares textures and uses one faction material per owner and shape.

Each synthetic board ran at 900 × 690 on Mesa llvmpipe. Each cell in the timing columns shows the range of medians from two 72-frame windows. Active windows alternated between two provisional targets using mouse input. Timings measure frame intervals, including the software renderer and OS scheduling. The largest observed 95th percentile was 14.5 ms. Godot reported that the display driver could not change V-Sync.

| 12 × 12 fixture | Settled baseline → candidate | Active baseline → candidate | Scene changes |
| --- | --- | --- | --- |
| 34 sprites with representative joins | 5.5–6.5 → 6.0–7.0 ms | 6.1–6.4 → 6.6–7.4 ms | +22 resources, +1 node, +130 KiB static, 27–28 → 63–64 draw calls |
| 64 sprites with dense clusters | 5.8–6.8 → 7.9–9.0 ms | 6.0–6.2 → 7.7 ms | +28 resources, +1 node, +175 KiB static, 31–32 → 89–90 draw calls |

Different wall textures and materials increase draw calls even with shared resources. The dense synthetic layout exposes that cost on this renderer. Android cost remains a device check.

## Verification and exports

- Debian 13.7 x86-64, Godot 4.7.2 .NET, .NET SDK 10.0.404, and DocFX 2.81.0. Warnings-as-errors solution and Release builds, all 400 xUnit tests and both Godot presentation regressions passed. Local checks exercised map switching, restarts, rotation-size layouts, touch/mouse center and edge selection, damage art, and a castle join created through actual match resolution.
- Baseline and candidate matched in all 32 completed histories across Classic and Knight Support, with natural and fixed openings (728 Classic fixed, 718 Classic natural, 496 Knight Support fixed and 629 Knight Support natural rounds). The comparison included deterministic match histories and pair counts. A saved 9A match replayed with an identical report. Archive validation covered all 96 protected entries. All 100 production map files, active and frozen AI source and export presets were unchanged.
- Exported Windows x64 and Android ARM64 debug packages. Both contain the managed game and 110 new or revised image imports. The two packaged Android sidecars match the source bytes and both appear in the Windows pack. The APK retained `com.castrumtactics.prototype`, passed APK Signature Scheme v2 and 16 KiB ZIP alignment checks, and was signed with a disposable cloud debug key. An existing installation signed with a different key requires a build signed with the owner's key for an in-place update. The Android managed build and the optional device-timing build compiled. Godot logged an EditorSettings message while shutting down after the exports completed.
- Windows execution and physical Android installation, rotation, touch responsiveness, sustained rendering and battery behavior still need device verification. The cloud timings use software rendering and describe only these captures.

Use the commands in [Engineering verification](EngineeringVerification.md) and [Platform verification](PlatformVerification.md) to reproduce the builds, platform export, local and CPU presentation checks. The parity runs used `Benchmarks/Examples/9a/natural.json` and `fixed.json` with `tournament run --max-matches 8` per ruleset and opening type at each revision, followed by the existing comparator.

## Owner device checklist

1. Start Demonstration and Great Divide, and check that the New Match preview matches their intact board art. Open another map to check default grass and rock.
2. Compare the knight, singleton castle and grave with the supplied references. Check red and blue trim and the readability of the neutral materials.
3. In the [Classic walkthrough](PlatformVerification.md#windows-and-android-walkthrough), check the ground beneath the captured piece at E1 and the collision grave at C3. Look for marks on damaged land and empty corners on intact ground.
4. In Knight Support, form adjacent friendly castles. Check that orthogonal joins line up and that opposing or diagonal castles stay separate.
5. On a 12 × 12 match, make and revise a provisional selection, switch orientation with the selection active, then submit. Restart and replace the battlefield. Check the board bounds, previews, restored ground, fresh draft targets and consistent tap behavior.

The damage sheets and captures in this article reflect 10B. The later [10C.0 review](RenderingBenchmarks.md#pr-30-review-follow-up-scenario-version-2) shows the updated marker visibility and matched rendering evidence.
