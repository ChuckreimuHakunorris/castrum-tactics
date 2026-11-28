# Board art verification

## 10C.1e.1 · Approved grass artwork

Grass variation is implemented and awaits owner in-game acceptance. Sand, snow and the final ground damage art remain future work. The generator verifies the approved source bytes before reading the artwork.

| Approved source | Source pixels | SHA-256 | Production texture |
| --- | --- | --- | --- |
| `grass_base_01.png` | 1254 × 1254 | `b39ccb4246f771151ecd89641c35af4c790b092b6138dd8593047cd848d97a1b` | `grass_base_01.png`, 128 × 128 |
| `grass_base_02.png` | 1254 × 1254 | `351ae89a1c441fab04c5f301fa724446bf558a815912eb6ffaf11e834d94972a` | `grass_base_02.png`, 128 × 128 |
| `grass_base_03.png` | 1254 × 1254 | `df4e16a2fc2e33813562d7cffa01023194786f5408b5f5340bf4ffbc9064f1f3` | `grass_base_03.png`, 128 × 128 |
| `grass_base_04.png` | 1254 × 1254 | `b033b628606cc8279d1588ce17ce4ee1b271cfbc37795e6d43dcc5510dc7f0b3` | `grass_base_04.png`, 128 × 128 |
| `accent_flowers.png` | 447 × 329 | `cabf9b7133e8de75d360a19294b22a619f30b57cfdc0c7af243ac6f3347c9b65` | `grass-accent-flowers.png`, 128 × 91 |
| `accent_pebbles.png` | 358 × 271 | `a6884dcb7d74468aacca99acafc27a61d49e621e31063b76de755211dcbd20c0` | `grass-accent-pebbles.png`, 128 × 94 |

The bases use Lanczos downsampling. The generator averages opposite borders across the four variants and reconciles the corner pixels. It fades each edge’s color correction into its own texture over six texels with a cosine taper. It compares every ordered pair in both axes and confirms that each resized interior remains pixel-identical. For the isolated accents, it crops fully transparent margins, preserves the aspect ratio up to pixel rounding and retains soft alpha. Production imports use lossless compression, linear filtering and no mipmaps.

```powershell
python -m pip install Pillow==10.3.0 numpy==1.26.4
$sources = 'D:\Game Projects\Files\Assets'
python scripts/art/build-grass.py $sources --previews Documentation/assets/10c1e1
# Import in Godot before checking new texture import settings.
python scripts/art/build-grass.py $sources --check
```

The [mixed-seam sheet](assets/10c1e1/grass-assets.png) shows all four bases and both accents at their scale limits with different offsets and mirroring. The [seed comparison](assets/10c1e1/grass-seeds.png) shows a 12 × 10 field at two seeds. The two renders at seed 0 match pixel for pixel. They contain 89 bare, 20 flower and 11 pebble tiles. Seed 29 contains 96 bare, 17 flower and 7 pebble tiles. [Selection and map compatibility](VisualArchitecture.md#deterministic-terrain-variation)

| Native Windows capture | Review coverage |
| --- | --- |
| [Demonstration draft](assets/10c1e1/mixed-draft.png) | Mixed grass, sand and inaccessible terrain. Check the authored flower at A1, tuft at B1 and contrast with the gold draft shields. |
| [Demonstration Main](assets/10c1e1/mixed-main.png) | Landscape play with knights, standalone castles, graves and tinted damage beneath collision sites. |
| [Open Meadow portrait](assets/10c1e1/small-portrait.png) and [Open Meadow landscape](assets/10c1e1/connected-castles.png) | A 6 × 5 grass-heavy map with Knight Support pieces. Full-tile castles cover their underlying terrain. |
| [Twelve Towers portrait draft](assets/10c1e1/large-portrait.png) | A 12 × 12 board at 360 × 640. The decorative pebbles are visibly smaller than the inaccessible rock tiles. |

For Windows playtesting, choose Open Meadow and Twelve Towers in New Match. Use Demonstration for mixed terrain and the [Classic walkthrough](PlatformVerification.md#windows-and-android-walkthrough) for damage. On Android export the standard signed APK and update with the [repository installer](PlatformVerification.md#android-export). Review both orientations, restart the same map, and check that accents and base choices stay put when pieces and damage change.

## 10C.1a–10C.1d · Approved game piece artwork

The knight, singleton castle, connected castles and gravestone are implemented and await owner in-game acceptance. The integrated baseline is `0f0ca87`. The approved high-resolution originals remain in the owner’s external `D:\Game Projects\Files\Assets` directory. All ten files sit directly in that directory.

| Source | Dimensions | SHA-256 |
| --- | --- | --- |
| `knight_original.png` | 1254 × 1254 | `bfdc428fa31b422f8b9c9ef0d8a19dd5b35dab58871b78db05c54944b69899bc` |
| `knight_base.png` | 1254 × 1254 | `0cb86e1ad6b8f0ff40e783d455d68d24e51ffa1558ba0c7b5291ddaac2d1db46` |
| `knight_mask.png` | 1254 × 1254 | `e114e180fa5cfa9a47600aed7071790cc53bf44effb3a237ae71b951e504d7b5` |
| `castle_asset_sheet.png` | 1312 × 1199 | `2090ce173f0922dc9b52a4080ceac6170f42dbbad9050a5fb587805aeb090b14` |
| `wall_ew_original.png` | 1254 × 1254 | `7613e39bff018f852115c96979efe77475e33d05a606c18fd19ba92718fa7b90` |
| `wall_gate_original.png` | 1254 × 1254 | `f7cd5560c196fbcf284c08534d647d36d659d01277b48469daa2773904c37518` |
| `wall_rampart_block.png` | 313 × 313 | `0f0293b360ec8e565285822c2a821665080054f1155c8470614aa8384c934500` |
| `wall_floor.png` | 1254 × 1254 | `0cfd77a67687983ec8a975677d47c85acaedf08481c610d798ab3e223ef4fb43` |
| `wall_crystal_original.png` | 1254 × 1254 | `22d1e42192af7e06d93d1a58509e2c847f8916e92c8b104341223e2a24fe23ec` |
| `grave.png` | 1254 × 1254 | `9323113ec280cfbee2f374d8b559b85f093d5d8b74208200d1f60dc9d0a6f847` |

### Isolation and faction tint

At an alpha threshold of 32, the knight master, neutral reference and mask reference respectively have bounds `(190, 17, 1069, 1178)`, `(186, 16, 1070, 1180)` and `(188, 17, 1066, 1179)`. The silhouette and color variants differ, so the new knight pair comes from the full-color master. The singleton comes from sheet bounds `(380, 0, 925, 530)` around the full-color castle. Crop bounds exclude the right and bottom coordinates. The illustrated neutral and mask variants on the sheet serve as references.

Isolation clears alpha below 8 and crops to the surviving silhouette before sizing. Lanczos resamples base and mask to the same size and offset. The knight, singleton and grave fit within 100, 98 and 96 px respectively on 104 × 104 canvases. The grave retains the entire headstone and dirt silhouette. Neutralization uses red dominance and hue with soft thresholds, plus region limits around the castle entrance. It lifts tintable midtones for the multiplicative shader and preserves source colors outside the mask. The shared façade mask covers fabric and the foundation strip. The gold lion, gold edging, armor, wood and masonry keep their original colors. All active imports remain lossless, linear-sampled and without mipmaps.

### Connected castle assembly

`CastleTopology` uses north, east, south and west bits 1, 2, 4 and 8. The output names use the mask of a representative base, with separate shared tint IDs. The following entries resolve to `wall-<base>_base.png` and `wall-<tint>_mask.png` under `Assets/Sprites/Board`. Mask zero uses the 104 × 104 `castle_base.png` and `castle_mask.png` pair.

| Artwork mask | Base | Tint | Ramparts |
| --- | --- | --- | --- |
| N (1) | 1, gated, both ends finished | 1 | Façade row |
| E (2), NE (3) | 2, gated, left end finished | 2 | Façade row |
| W (8), NW (9) | 8, gated, right end finished | 8 | Façade row |
| EW (10), NEW (11) | 10, uninterrupted | 10 | Façade row |
| S (4) | 4 | 15 | N, E, W · 10 blocks |
| NS (5) | 5 | 15 | E, W · 8 blocks |
| ES (6) | 6 | 15 | N, W · 7 blocks |
| NES (7) | 7 | 15 | W · 4 blocks |
| SW (12) | 12 | 15 | N, E · 7 blocks |
| NSW (13) | 13 | 15 | E · 4 blocks |
| ESW (14) | 14 | 15 | N · 4 blocks |
| NESW (15) | 15 | 15 | Open interior · 0 blocks |

Roofs evaluate their east and west edges independently. A side gets a rampart if there is no friendly castle on that side or the friendly neighbor has no southward castle connection and therefore displays a façade. For artwork lookup, clear that side bit on the roof. The north edge still uses the roof’s northward connection. The existing pre-composed bases already cover every resulting combination, including two side ramparts. They reuse the same crystal mask, so the cache needs no extra textures, layers or draw calls. The renderer re-evaluates the layout on each new board snapshot, including when a diagonal castle changes a neighboring façade into a roof.

The seamless base uses the uninterrupted source for all four upper blocks, masonry, banner and foundation. Processing narrows the lower façade crop to columns `[3, 125)` at 128 px, refits it to the full width, and feathers the horizontal edges over three texels. A traced arch from the gate source at `(142, 266, 356, 435)` in the script’s 500 × 500 working image is resampled to 54 × 41 and overlaid at `(37, 70)`.

The endcaps restore the original artwork’s outer bevels to the exposed sides of the production gate. The outer five source columns expand to an eight-texel blend band, with matching treatment on the tint mask. This preserves the banner, doorway, masonry courses, rampart grid and connected edges. The façade continues only toward friendly castles, so opposing factions meeting along a horizontal wall show two finished edges. The owner approved the endcaps after reviewing the preview sheet.

Pavement’s raw mean absolute edge jumps were 20.25 RGB levels north–south and 13.77 east–west at 128 px, compared with 16.06 and 8.98 between average interior neighbors. The script feathers both sides toward the mean of their border pixels over five texels with a cosine taper. The opposite border rows and columns match exactly after correction. Interior cobblestones retain their pattern. The nonrotated 32 × 32 rampart block fills one cell of the 4 × 4 grid. Corners shared by two exposed edges receive a single block. The 32 × 32 crystal, including its gold frame, is isolated from source alpha, centered at `(48, 48)` and baked into all eight otherwise opaque compositions. Its shared mask affects the gemstone.

### Regenerate and verify

Use Python 3 with Pillow 10.3.0 and NumPy 1.26.4. The path argument is supplied locally. The PNGs under the production board directory are generated separately from the approved originals.

```powershell
python -m pip install Pillow==10.3.0 numpy==1.26.4
$sources = 'D:\Game Projects\Files\Assets'
python scripts/art/build-game-pieces.py $sources
python scripts/art/build-game-pieces.py $sources --check --previews exports/10c1/previews
```

The check validates source checksums, output dimensions and alpha, mask coverage within the sprite silhouette, all 15 cardinal configurations, rampart counts, and matching pavement borders. It also checks that the façade variants preserve their shared continuing edges and that differences are confined to the doorway and terminal bands. It regenerates PNGs in a temporary directory and compares bytes with production. The generated previews cover both default faction colors and representative green and violet tints.

### Review images

| Evidence | Coverage |
| --- | --- |
| [Faction colors](assets/10c1/faction-colors.png) and [connectivity](assets/10c1/connectivity.png) | Base and mask alignment, four color samples, all 15 connected configurations and the singleton. |
| [Horizontal façade endcaps](assets/10c1/assemblies-facades.png) | Individual façade variants, a continuous six-tile wall, and directly adjacent red and blue fortresses at native texel size. |
| [Vertical and rectangular fortresses](assets/10c1/assemblies-fortresses.png) | S–NS–N, 2 × 2, 3 × 3 and 5 × 4. The 3 × 3 upper, middle and lower rows are ES–ESW–SW, NES–NESW–NSW and NE–NEW–NW. |
| [Irregular formations](assets/10c1/assemblies-irregular.png) and [façade transitions](assets/10c1/assemblies-facade-transitions.png) | L, T, cross, irregular cluster and adjacent opposing factions. The focused sheet shows left, right and bilateral ramparts in red and blue at native texel size. |
| [Godot fortifications](assets/10c1/fortifications.png) and [alternate shader colors](assets/10c1/fortifications-alternate.png) | A temporary review fixture applied all 16 castle masks through the production board renderer and checked the bound base and tint resources. Both default shader colors and `#59ca87` / `#b48af5` were rendered. |
| [Landscape live castle join](assets/10c1/connected-castles.png) and [portrait mixed pieces](assets/10c1/knight-support-mixed-portrait.png) | Real Knight Support submissions on Open Meadow. Includes both factions, a connected façade, singleton castles, knights, graves, territory and legal frames. |
| [Classic shared targets](assets/10c1/10c-red-shared.png) and [collision and damage](assets/10c1/10c-after-grave.png) | Landscape Demonstration during Main. |
| [Compact 12 × 12 Draft](assets/10c1/10c-large-portrait.png), [compact Main](assets/10c1/10c1-compact-main.png) and [Banner Plain opening result](assets/10c1/banner-plain-portrait-resolved.png) | Twelve Towers at 360 × 640, with 28 px tiles, and Banner Plain at 720 × 1000. |

The pavement-to-façade junction and the two faction borders are visible in the assembly and Godot fixture images. Adjacent opposing castles join only to their own faction. At shared boundaries, the floor-edge correction trades exact edge color continuity for a small softening in the border cobblestones. Check the junctions and sprite readability on the owner’s phone.

### Rendering workload

Paired Windows development runs used Godot 4.7.2 .NET, OpenGL Compatibility on a GTX 1060 3 GB, a 900 × 650 window and a reported 1465 × 1057 render size with V-Sync at 59.973 Hz. Each case sampled four seconds following two seconds of warm-up. Quick covered Small, Turns and Monster. A second request used the same timing for Castles, Damage and Lifecycle. These are native desktop measurements. The [10C.0 native report](NativeRenderingBaseline.md) remains the broader Windows and physical Android baseline.

Each cell reports baseline → candidate. Draws and objects are frame medians. Memory estimates and resource and node counts are the end samples. Frame columns use milliseconds.

| Case | Draws | Objects | Nodes | Resources | Texture / video MiB | Frame median / p95 / p99 |
| --- | --- | --- | --- | --- | --- | --- |
| Small | 51 → 51 | 234 → 234 | 188 → 188 | 63 → 63 | 6.43 / 12.66 → 6.43 / 12.66 | 16.67 / 16.79 / 16.80 → 16.68 / 16.72 / 16.73 |
| Turns | 67 → 67 | 260 → 260 | 204 → 204 | 70 → 68 | 6.75 / 12.99 → 6.64 / 12.89 | 16.68 / 16.78 / 16.85 → 16.68 / 16.72 / 16.74 |
| Monster | 94 → 94 | 415 → 415 | 214 → 214 | 73 → 71 | 7.00 / 13.23 → 6.89 / 13.13 | 16.68 / 16.76 / 16.79 → 16.67 / 16.72 / 16.75 |
| Castles | 176 → 175 | 570 → 570 | 247 → 247 | 73 → 62 | 6.56 / 12.80 → 6.21 / 12.44 | 16.68 / 16.78 / 16.80 → 16.67 / 17.31 / 18.46 |
| Damage | 237 → 235 | 593 → 593 | 268 → 268 | 91 → 74 | 7.75 / 14.07 → 7.08 / 13.40 | 16.68 / 16.78 / 16.82 → 16.67 / 16.72 / 16.74 |
| Lifecycle | 44 → 44 | 216 → 216 | 188 → 188 | 100 → 83 | 9.00 / 15.22 → 8.33 / 14.56 | 16.67 / 16.76 / 16.80 → 16.68 / 16.79 / 18.25 |

Median sprite synchronization was 0.490 → 0.459 ms for Turns, 0.453 → 0.481 ms for Monster and 0.101 → 0.083 ms for Lifecycle. The Turns baseline recorded a 160 ms maximum sprite-sync event and two frames above 33.33 ms. The candidate maximum sprite-sync event was 3.63 ms and no candidate case had a frame above 33.33 ms. Castles and Lifecycle had higher p99 in the first candidate run. A repeat after the builds and tests finished recorded 16.82 ms and 16.79 ms respectively, close to the original baseline. The short sample, V-Sync and development-host scheduling limit attribution. The stable node and object counts agree with one sprite per occupied cell and the shared artwork reduced texture memory in the denser scenarios.

### Engineering and export checks

- The warnings-as-errors solution build, Windows `ExportRelease` managed build and Release Benchmarks build passed. All 448 xUnit tests passed, including the castle resource mapping test. The benchmark CLI help, schedule dry-run, fixture generation and a saved match replay also passed.
- Both Godot regressions passed. The Classic CPU match again ended 11–12 after 30 Main turns. The local regression covered resizing, touch and mouse input, draft and match restarts, map replacement, territory and legal frames, and marker suppression under castles and graves.
- Regenerated all 17 art PNGs byte for byte and checked lossless imports without mipmaps. The whitespace and DocFX checks passed. The archive verifier confirmed all 96 protected files.
- The Windows x64 debug package launched on the development machine. The Android ARM64 debug APK passed v2 signature and 16 KiB alignment checks, with package ID `com.castrumtactics.prototype`, minimum API 24 and target API 36. Both packages contain all 17 production artwork imports. The Android APK was validated as a package and awaits physical-device testing.

### Owner acceptance

On Windows, check out `feat/m10c1-game-piece-artwork` and run the Godot .NET project. Use the [Classic walkthrough](PlatformVerification.md#windows-and-android-walkthrough), then run Knight Support and upgrade a knight adjacent to a friendly castle. Check both faction colors, the full-tile join, a portrait-oriented window and a compact 12 × 12 board. Restart and start a New Match on another battlefield.

On Android, export a locally signed debug APK and deploy it with [the standard installer and package ID](PlatformVerification.md#android-export). Use the same signing key as the installed copy for an in-place update. Follow the [rotation checks](PlatformVerification.md#android-rotation-checks) and inspect the approved piece art in both orientations. Physical Android play, sustained timing and owner acceptance are pending.

## 10B historical verification

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

To reproduce the 10B artwork and comparison sheets, use Python with Pillow and the three original files in a local directory. Run from the root of a 10B checkout.

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

The damage sheets and captures in this historical section reflect 10B. The later [10C.0 review](RenderingBenchmarks.md#pr-30-review-follow-up-scenario-version-2) shows the updated marker visibility and matched rendering evidence.
