# Castrum Tactics — Art Direction Reference

- **Version:** 0.2
- **Status:** Implemented UI direction with placeholder board art
- **Engine:** Godot (C#)
- **Target platforms:** PC and Android

## 1. Visual reference

![Selected detailed pixel-art board concept](assets/art-direction-concept-v0.1.png)

*Figure 1 — Selected AI-generated concept. This establishes the desired look, not an exact spritesheet or a verified 32×32 pixel grid. Its red and blue factions are illustrative only; faction colors must be configurable.*

## 2. Art direction

- **Style:** Detailed medieval-fantasy pixel art, retaining the approachable retro character of the original game.
- **Perspective:** Direct top-down board with square, non-isometric cells. Piece illustrations may show stylized front/three-quarter detail as long as placement and readability remain clear.
- **Target tile size:** **32×32 source pixels per board cell**, independently of the on-screen display size. This is the current target, to be validated with a real prototype.
- **Rendering:** Crisp pixel edges, nearest-neighbor filtering, consistent pixel density and integer scaling where practical; avoid unintended antialiasing.
- **Visual language:** Strong silhouettes and outlines; cohesive limited palette; readable highlights/shadows; textured but unobtrusive grass; distinct knights, castles, graves, rocks, and rivers.
- **Priority:** Gameplay readability on small Android screens and variable board dimensions takes precedence over decorative detail.

## 3. Faction-color system — implement early

**Decision:** Do not author separate complete red/blue spritesheets. Use **faction-neutral base artwork** and apply the player's selected color at runtime in Godot.

**Preferred technique:** A shader-driven **recolor mask / indexed faction palette**. Recolor designated faction-owned regions such as a knight's plume, cloth, and shield face or a castle's banner. Keep metal, stone, wood, outlines, and neutral shading unchanged. Preserve light/dark tonal differences within recolored regions.

**Asset conventions:**

- Each faction-owned sprite has a shared neutral base and an accompanying recolor mask (or equivalent explicitly defined palette indices).
- Masks and base textures align exactly, use nearest-neighbor sampling, and have no filtering artifacts.
- A single faction color parameter drives all relevant assets; if needed, derive coordinated highlight and shadow colors from it.
- Neutral objects such as graves, rocks, rivers, and ordinary terrain are not faction-tinted by default.
- Avoid embedding red/blue allegiance in the base sprite, except in concept references.

**Game/data conventions:**

- Store **player identity separately from faction color**. Game rules must not rely on Red/Blue being fixed identities.
- Expose faction-color selection early in the UI, initially with a curated palette; design the rendering path to support arbitrary colors later.
- In multiplayer, synchronize the chosen color as presentation metadata; ownership and turn resolution continue to use stable player IDs.
- Prevent or warn about visually indistinguishable player color choices. Consider accessibility: distinguish factions using optional symbols, outlines, or UI labels as well as color.

The [knight and castle sprites](#7-implemented-faction-sprites) share a Godot `canvas_item` recoloring shader.

## 4. Production asset requirements

- Produce **individual, transparent PNG sprites** or a consistently indexed atlas for units and structures.
- Produce repeatable terrain tiles and test adjacent tile boundaries; rivers/terrain transitions may need multiple variants.
- Keep each sprite aligned to a consistent 32×32 cell coordinate system; use a documented anchor/offset if an illustration extends beyond its cell.
- Separate gameplay overlays (selection, legal moves, ownership, ground health) from decorative artwork so they can change independently.
- Treat generated images as **concepts until manually checked** for pixel-grid accuracy, consistency, transparency, tileability, and in-engine readability.

## 5. Planned production asset validation

1. Create a 32×32 grass tile and evaluate the neutral knight and castle sprites against it.
2. Render the same knight and castle in at least four selectable faction colors via one shader/material approach.
3. Display them on a small board in Godot at typical PC and Android viewing sizes.
4. Check silhouette clarity, color differentiation, pixel sharpness, and consistency before expanding the tileset.

## 6. Decisions still open

- Final faction-color palette and accessibility options.
- Whether production artwork needs indexed faction palettes beyond the prototype's direct tint.
- Tile edge treatment and terrain autotiling strategy.
- Whether 32×32 remains sufficient after the first in-engine readability test.

## 7. Implemented faction sprites

The knight uses `Assets/Sprites/Units/Knight/knight_base.png` and `knight_mask.png` in the same folder. Both PNGs are 756 × 940 pixels and share the same origin and silhouette alignment. The base keeps the original RGBA pixels inside the crop. The common crop removes excess transparent margin and faint stray pixels outside the artwork. The corrected mask follows the base artwork's plume, cape, tunic panels, and shield face. It leaves the armor, spear, white emblem, black outlines, belt, and gold trim untinted.

The castle uses `Assets/Sprites/Units/Castle/castle_base.png` and `castle_mask.png`. Both PNGs are 1234 × 1179 pixels and share the same crop. The castle's banner cloth and front foundation band take the faction tint. The mask preserves the stonework, gate, outlines, gold trim, white heraldic emblem, and bright top edge of the foundation.

`Shaders/faction_recolor.gdshader` samples both textures at the same UV coordinates. In the mask, white applies the full tint, gray blends in part of the tint, and black preserves the base color. Mask alpha scales the strength, so a transparent mask pixel has no effect. The shader multiplies the base RGB by the faction RGB before blending, retaining the artwork's light and dark values. Faction regions should use neutral grayscale for this multiplication. The shader copies the base alpha to the result.

`FactionPalette` maps `PlayerId.PlayerOne` to `#f06c68` and `PlayerId.PlayerTwo` to `#69a4ff`. `FactionPieceArt` shares a base texture and mask for each piece type and reuses one material per owner and piece type. The board centers sprites in their tile rectangles and scales them uniformly. The knight fits within 94% of each tile dimension and the broader castle fits within 98%. Both textures use nearest-neighbor sampling, with repeat disabled for the mask. Fine details can alias at small or fractional display sizes.

To add another recolorable piece, place its neutral transparent base and grayscale mask together under `Assets/Sprites`. Align their pixels and apply any crop to both with the same coordinates. Paint the mask on the base canvas and check its protected details against both faction colors. Import the textures losslessly with mipmaps disabled, create a `FactionPieceArt` for the pair, and use the same tile placement with the piece's snapshot owner. Each new texture pair gets its own per-faction materials and can share the shader.

## 8. Placeholder board art

The ImageGen placeholders use stepped shapes and limited palettes to stay readable at board size. They are temporary pseudo-pixel artwork. The source textures are larger than the planned production pixel grid.

Terrain textures meet at shared tile edges. A translucent dark olive grid draws once over the board at one-third opacity. Shared lines stay at least one rendered pixel wide and blend across pixels at fractional positions. The outer edge stays inside the board, which keeps its outer margin.

| Tile or piece | Asset | Appearance |
| --- | --- | --- |
| Ground health 3 | `Assets/Sprites/Terrain/ground_health_3.png` | Healthy green grass |
| Ground health 2 | `Assets/Sprites/Terrain/ground_health_2.png` | Medium-brown soil with remnants of grass |
| Ground health 1 | `Assets/Sprites/Terrain/ground_health_1.png` | Dark barren soil |
| Ground health 0 | `Assets/Sprites/Terrain/ground_health_0.png` | Purplish-brown soil with a skull and loose bones flanking the grave |
| Inaccessible | `Assets/Sprites/Terrain/rock.png` | Gray boulders |
| Grave | `Assets/Sprites/Objects/Grave/grave.png` | Stone headstone, engraved cross, and brown mound on a transparent background |

Inaccessible terrain always shows rock. Playable health above 3 uses the healthy texture. The grave has no faction material and can sit above ground of any playable health. The renderer fits the entire grave frame to the tile width at its original aspect ratio. The transparent space above the headstone keeps the mound low in the square.

ImageGen generated each terrain tile separately. The five opaque square sources were reduced from 1254 × 1254 to 256 × 256 with nearest-neighbor sampling. The grave uses the supplied 166 × 161 pixel reference frame. A connected color mask removes the surrounding grass, leaving the brown mound and its small attached tufts together with the headstone. The capture cursor was removed using adjacent soil pixels where it crossed the mound. The original canvas placement and remaining artwork pixels were preserved.

To replace the placeholders, keep the filenames and use full-bleed opaque squares for terrain and a near-square RGBA frame for the grave. Keep borders, territory accents, and selection markers out of the artwork. Import losslessly with mipmaps disabled and verify the replacements at several board sizes with nearest-neighbor filtering.

The rendering order is terrain, secured-territory tint and edge, the grid, pieces, legal targets, then the provisional selection. The grid uses `Shaders/tile_grid.gdshader`. `Shaders/selection_indicator.gdshader` measures the distance to the nearest edge in tile UV coordinates. Its uniforms control color (`indicator_color`), border thickness (`border_width`), gradient depth (`fade_depth`), and gradient opacity (`fade_opacity`). The gold legal border uses 4.5% of the tile width and the pale cyan provisional border uses 6.5%. The selection fades extend 20% inward from their solid borders and start at 42% opacity. The tile center remains clear. A provisional selection replaces the legal gradient on that tile. Opening dots remain visible.

## 9. Interface and typography

The interface pairs smooth, rounded controls with the stepped medieval board art. Pale blue panels, navy text, soft shadows, and a blue gradient frame the battlefield. A compact header identifies the phase and round or turn. The selection panel shows instructions or waiting feedback, with faction state on player toggles in Local Multiplayer and passive **You** and **CPU** cards in Versus CPU. Interactive match controls keep a minimum target of 48 × 48 dp on Android. [Android accessibility guidance](https://developer.android.com/guide/topics/ui/accessibility/views/apps-views#touch-targets)

Assign `Themes/castrum_theme.tres` to the root `Control` of a screen so its descendants inherit the styles. Use the default `Button` for secondary actions and `PrimaryButton` for submit, revise, and restart. Use `PlayerButton` for faction cards. Explicit status text and position identify factions independently of the palette. The base buttons provide distinct hover, pressed, focused, disabled, and hover-while-pressed states. Use `Heading`, `SecondaryLabel`, and `RejectionLabel` to distinguish instructions, supporting text, and feedback.

The **Secured Territory** banner uses the themed score surface and bold numerals. One undecorated shield silhouette is tinted with each faction's palette color. Player One always appears on the left and Player Two on the right. The banner sits above the tiles with the exact grid width. Tight layouts may omit the center label and scale the score groups. The banner stays visible on the final board while the selection panel shows the winner or draw.

The compact match panel places actions in one row beneath the board in portrait and stacks them in the landscape sidebar. The active difficulty is on the CPU card, and the difficulty selector lives in the New Match dialog.

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

Turn off **Motion Enabled** on a background instance in the Inspector or set `MotionEnabled` to `false` in C#. The background then uses a static gradient shader with no clock or shape pass. It still fills the viewport and can serve a future reduced-motion or battery-saving preference.

---

**Reference provenance:** The image in `assets/art-direction-concept-v0.1.png` is an AI-generated concept selected during visual exploration. It is **not** a source of gameplay rules or a production-ready asset sheet.
