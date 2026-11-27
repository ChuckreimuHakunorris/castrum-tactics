# Castrum Tactics — Art Direction Reference

10B uses smooth medieval fantasy board art with clean outlines, bright readable surfaces and a stylized front or three-quarter piece view. The gameplay grid stays square and non-isometric. The active board asset bank is `Assets/Sprites/Board`. The owner approved the 10B architecture after Windows testing and identified the need for substantial artwork refinement in 10C.1. [Visual roadmap](VisualDevelopmentRoadmap.md)

## 1. Visual reference

All three original PNGs were supplied through the cloud as `Files.rar` and inspected at native resolution. The displayed Crescent Quarry layout was an illustration. Production gameplay maps derive from the existing catalog.

| Original | Native size | Reference motifs |
| --- | --- | --- |
| ChatGPT Image 27 Nov 2026, 00_05_41-1.png | 1448 × 1086 | Winding river, gray peaks and boulders, grass tufts, tree clusters and exposed ground. |
| ChatGPT Image 27 Nov 2026, 00_05_59-2.png | 1448 × 1086 | Mountain ridge, forest, waterfall, mossy arch, cattails, shrubs and flowers. |
| Crescent Quarry with Blocky Graves.png | 1476 × 1065 | Clean source tiles for the singleton castle, knight and neutral headstone. |

The older [pixel-art concept](assets/art-direction-concept-v0.1.png) remains a historical reference.

## 2. Art direction

Keep bold silhouettes, cohesive edges, recognizable territory and strong faction contrast. Use the review and integration workflow agreed for 10C.1 with each new source family. Use the owner's original isolated pieces where clean separation is possible. Independently authored terrain carries the reference palette and shape language into composable tiles. [Art evidence and provenance](BoardArtVerification.md)

## 3. Faction-color system

The default colors remain `#f06c68` and `#69a4ff`. Knights, singleton castles and wall shapes pair a neutral RGBA base with an aligned grayscale mask. Tint applies to fabric, shield faces, castle banners and trim. Stone, armor, spear, gold and white heraldry keep their source colors. The shader reuses base alpha and one material per shape and owner.

## 4. Production asset requirements

- Terrain and wall textures use 128 × 128 canvases. Native piece crops occupy a 104 × 104 padded canvas and keep their source detail. Pieces center on a cell and stay inside it.
- The new terrain and base/mask pairs use linear sampling. Imports use lossless compression with mipmaps disabled.
- Rivers use compatible edge profiles. Walkable bridges cross depicted water at right angles. Authored orientations remain explicit. Props, buildings and ruins stay inside one cell.
- Preserve visible damage, territory, starts and legal indicators by drawing their cues in independent layers.

## 5. Production asset validation

Review the [terrain bank, damage matrix, tint tests and rendered boards](BoardArtVerification.md). Include small Android-size tiles and mixed-faction neighbors when introducing a new asset. Reuse and distribution conditions outside this project remain unresolved. Confirm rights before a public art release.

## 6. Decisions still open

- Future palette accessibility options beyond the current pair and heraldry.
- Additional shore compositions and optional waterfall and cattail assets for 10F.
- Physical-device readability, frame pacing and whether mipmaps would help dense boards.

## 7. Implemented faction sprites

The knight, singleton castle and grave use pixels from the owner's boulder-enclosure PNG. Extraction traced each silhouette, removed background fragments and recovered neutral shading in blue livery. Isolation edges and livery normalization intentionally change some pixels. The neutral grave retains the headstone, base and leaves.

Connected castles compose the same singleton crop over new masonry joins. The same-faction four-cardinal mask covers isolated, end, straight, corner, T and cross configurations. The front gate and crest remain upright. See the [castle tint sheet](BoardArtVerification.md#asset-sheets) and [topology contract](VisualArchitecture.md#layers-and-transforms).

## 8. Ground art and earlier placeholders

Grass, sand, snow and bridges each have four display tiers. The artwork progresses through exposed soil, scuffs, slush or plank wear. One, two or three outlined pale marks indicate damage. Intact and inaccessible cells appear without markers. A grave can sit on partly damaged ground.

The earlier ImageGen artwork under `Assets/Sprites/Terrain`, `Assets/Sprites/Units` and `Assets/Sprites/Objects` remains available as historical material. The old 32 × 32 and nearest-neighbor targets describe that era.

## 9. Interface and typography

The interface pairs smooth, rounded controls with the smooth medieval board art. Pale blue panels, navy text, soft shadows, and a blue gradient frame the battlefield. A compact header identifies the phase and round or turn. The selection panel shows instructions or waiting feedback, with faction state on player toggles in Local Multiplayer and passive **You** and **CPU** cards in Versus CPU. Interactive match controls keep a minimum target of 48 × 48 dp on Android. [Android accessibility guidance](https://developer.android.com/guide/topics/ui/accessibility/views/apps-views#touch-targets)

Assign `Themes/castrum_theme.tres` to the root `Control` of a screen so its descendants inherit the styles. Use the default `Button` for secondary actions and `PrimaryButton` for submit, revise, and restart. Use `PlayerButton` for faction cards. Explicit status text and position identify factions independently of the palette. The base buttons provide distinct hover, pressed, focused, disabled, and hover-while-pressed states. Use `Heading`, `SecondaryLabel`, and `RejectionLabel` to distinguish instructions, supporting text, and feedback.

The **Secured Territory** banner uses the themed score surface and bold numerals. One undecorated shield silhouette is tinted with each faction's palette color. Player One always appears on the left and Player Two on the right. The banner sits above the tiles with the exact grid width. A pale-blue tube carries muted liquid tints from the two faction ends. A thin pale-blue battlefield frame and soft shadow sit outside the square tiles. Tight layouts may omit the center label and scale the score groups. The banner stays visible on the final board while the selection panel shows the winner or draw.

The compact match panel places actions in one row beneath the board in portrait and stacks them in the landscape sidebar. The header shows the active battlefield, dimensions, ruleset, and phase. New Match keeps confirmation controls below the filterable map list and preview. Tall layouts stack those sections and use up to 660 canvas units of height within the safe area. Wide or short layouts place the preview and CPU difficulty beside the list so rows can extend down to the confirmation controls. The dialog uses the shared pale-blue surface, navy title bar and difficulty menu, and a blue scrollbar. The preview draws the shared intact terrain and gold draft shields at a uniform square tile size. Knight Support uses the same castle art for draft placements and the neutral grave art for unsupported knights. The banner and rendered pieces update from the same resolved snapshot.

The theme uses the regular variable face of Nunito at weights 600 and 800. The font is bundled for offline use under the SIL Open Font License 1.1. The binary and license come from the same [Nunito upstream revision](https://github.com/googlefonts/nunito/tree/8c6a9bb9732545b9ed53f29ec5e1ab0ff53c4e6f). The license is stored at `Assets/Fonts/Nunito/OFL.txt` and included by the Windows and Android export presets.

## 10. Blue background

Instance `Scenes/UI/AnimatedBackground.tscn` as the first child of a viewport-filling screen control. Its full-rectangle anchors follow the parent and its mouse filter lets clicks and taps pass through. It uses one Compatibility canvas draw with a gradient and five diffuse lights. The lights vary between circles and rounded diamonds and use smooth distance falloff.

| Shader parameter | Effect |
| --- | --- |
| `deep_blue`, `royal_blue`, `bright_blue` | Set the navy base, royal midtone, and luminous blue. These colors have shared defaults for both materials. |
| `shape_color` | Tints the drifting lights. |
| `animation_speed` | Sets the rate of the drift and base gradient. The default is 1.0. |
| `shape_opacity` | Scales the lights' contribution. The default is 0.21 and overlap is capped at a 0.27 blend. |

Each light has a separate phase and a lifetime of roughly 8–15 seconds at the default speed. It fades in over about the first quarter of the cycle and fades out in the final third. It gets a new origin, drift, and size while transparent. Four lights start near different edges, where their fades remain visible around the board, and the fifth can start anywhere. Whole cycle counts and wrapped seeds join the animation across Godot's default 3,600-second shader clock rollover. The higher-contrast base glow sweeps horizontally and vertically on slower cycles of about 45 and 58 seconds.

Turn off **Motion Enabled** on a background instance in the Inspector or set `MotionEnabled` to `false` in C#. The background then uses a static gradient shader with no clock or shape pass. It still fills the viewport and is selected by the Reduce motion preference. Main-phase frames and the territory tube also use time-independent shaders under this preference.

---

**Reference provenance:** The image in `assets/art-direction-concept-v0.1.png` is an AI-generated concept selected during visual exploration. It is **not** a source of gameplay rules or a production-ready asset sheet.
