"""Build the approved 10C.1 game-piece assets and optional review sheets."""

import argparse
import hashlib
from pathlib import Path
from tempfile import TemporaryDirectory

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BOARD = ROOT / "Assets/Sprites/Board"
LANCZOS = Image.Resampling.LANCZOS
SOURCES = {
    "knight_original.png": ((1254, 1254), "bfdc428fa31b422f8b9c9ef0d8a19dd5b35dab58871b78db05c54944b69899bc"),
    "knight_base.png": ((1254, 1254), "0cb86e1ad6b8f0ff40e783d455d68d24e51ffa1558ba0c7b5291ddaac2d1db46"),
    "knight_mask.png": ((1254, 1254), "e114e180fa5cfa9a47600aed7071790cc53bf44effb3a237ae71b951e504d7b5"),
    "castle_asset_sheet.png": ((1312, 1199), "2090ce173f0922dc9b52a4080ceac6170f42dbbad9050a5fb587805aeb090b14"),
    "wall_ew_original.png": ((1254, 1254), "7613e39bff018f852115c96979efe77475e33d05a606c18fd19ba92718fa7b90"),
    "wall_gate_original.png": ((1254, 1254), "f7cd5560c196fbcf284c08534d647d36d659d01277b48469daa2773904c37518"),
    "wall_rampart_block.png": ((313, 313), "0f0293b360ec8e565285822c2a821665080054f1155c8470614aa8384c934500"),
    "wall_floor.png": ((1254, 1254), "0cfd77a67687983ec8a975677d47c85acaedf08481c610d798ab3e223ef4fb43"),
    "wall_crystal_original.png": ((1254, 1254), "22d1e42192af7e06d93d1a58509e2c847f8916e92c8b104341223e2a24fe23ec"),
    "grave.png": ((1254, 1254), "9323113ec280cfbee2f374d8b559b85f093d5d8b74208200d1f60dc9d0a6f847"),
}

NORTH, EAST, SOUTH, WEST = 1, 2, 4, 8  # Scenes/CastleTopology.cs
DIRECTIONS = ((NORTH, 0, -1), (EAST, 1, 0), (SOUTH, 0, 1), (WEST, -1, 0))
FACADE_BASES = {NORTH: NORTH, EAST: EAST, NORTH | EAST: EAST,
                WEST: WEST, NORTH | WEST: WEST, EAST | WEST: EAST | WEST,
                NORTH | EAST | WEST: EAST | WEST}
PAVEMENT_COUNTS = {4: 10, 5: 8, 6: 7, 7: 4, 12: 7, 13: 4, 14: 4, 15: 0}
TILE = 128
BLOCK = 32
ENDCAP_WIDTH = 8
COLORS = ("#f06c68", "#69a4ff", "#59ca87", "#b48af5")


def open_sources(directory):
    images = {}
    for name, (size, digest) in SOURCES.items():
        path = directory / name
        if hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError(f"{path}: SHA-256 differs from the approved source")
        with Image.open(path) as image:
            if image.size != size:
                raise ValueError(f"{path}: expected {size}, found {image.size}")
            images[name] = image.convert("RGBA")
    return images


def isolate(image):
    # The supplied alpha contains faint disconnected pixels outside the intended silhouette.
    alpha = image.getchannel("A").point([0] * 8 + list(range(8, 256)))
    image = image.copy()
    image.putalpha(alpha)
    return image.crop(alpha.getbbox())


def faction_pair(image, regions=None):
    pixels = np.asarray(image).astype(np.float64)
    red, green, blue, alpha = np.moveaxis(pixels, -1, 0)

    # Keep warm gold and brown separate from near-red fabric and gemstone material.
    chroma = np.maximum(red - np.minimum(green, blue), 1)
    hue = (green - blue) / chroma
    coverage = np.clip((red - np.maximum(green, blue) - 8) / 18, 0, 1)
    coverage *= np.clip((0.42 - hue) / 0.16, 0, 1)
    coverage *= alpha > 0
    if regions is not None:
        coverage[~regions] = 0

    # Lift midtones for the multiplicative shader while retaining dark folds and highlights.
    level = 255 * (red / 255) ** 0.68
    pixels[:, :, :3] += (level[:, :, None] - pixels[:, :, :3]) * coverage[:, :, None]
    base = Image.fromarray(np.rint(pixels).astype(np.uint8))
    mask = Image.fromarray(np.rint(255 * coverage).astype(np.uint8))
    return base, mask


def fit_sprite(image, footprint, tinted, regions=None):
    size = (104, 104)
    ratio = footprint / max(image.size)
    scaled = tuple(round(value * ratio) for value in image.size)
    offset = tuple((canvas - extent) // 2 for canvas, extent in zip(size, scaled))
    layers = faction_pair(image, regions) if tinted else (image,)
    result = []
    for layer in layers:
        canvas = Image.new(layer.mode, size)
        canvas.paste(layer.resize(scaled, LANCZOS), offset)
        result.append(canvas)
    if tinted:
        # Lanczos can spread a weak tint beyond the resized alpha silhouette.
        mask = np.asarray(result[1]).copy()
        mask[np.asarray(result[0].getchannel("A")) == 0] = 0
        result[1] = Image.fromarray(mask)
    return tuple(result)


def match_edges(image, horizontal, vertical):
    pixels = np.asarray(image).astype(np.float64)
    for axis, width in ((0, vertical), (1, horizontal)):
        if not width:
            continue
        rows = np.swapaxes(pixels, 0, axis)
        seam = (rows[0] + rows[-1]) / 2
        for i in range(width):
            weight = (1 + np.cos(np.pi * i / width)) / 2
            rows[i] = rows[i] * (1 - weight) + seam * weight
            rows[-1 - i] = rows[-1 - i] * (1 - weight) + seam * weight
    return Image.fromarray(np.rint(pixels).astype(np.uint8))


def finish_facade_edges(production, original, exposed):
    # Widen the original bevel slightly so it remains legible at gameplay scale.
    weights = np.array([1, 1, 1, 0.9, 0.65, 0.35, 0.1, 0])
    result = []
    for layer, source in zip(production, original):
        pixels = np.array(layer)
        for side in exposed:
            if side == WEST:
                bevel = source.crop((0, 0, 5, TILE))
                band = pixels[:, :ENDCAP_WIDTH]
                blend = weights
            else:
                bevel = source.crop((TILE - 5, 0, TILE, TILE))
                band = pixels[:, -ENDCAP_WIDTH:]
                blend = weights[::-1]
            bevel = np.asarray(bevel.resize((ENDCAP_WIDTH, TILE), Image.Resampling.BILINEAR))
            blend = blend.reshape((1, ENDCAP_WIDTH) + (1,) * (band.ndim - 2))
            band[:] = np.rint(band * (1 - blend) + bevel * blend)
        result.append(Image.fromarray(pixels))
    return tuple(result)


def facades(sources):
    original = sources["wall_ew_original.png"].resize((TILE, TILE), LANCZOS)
    master = original.copy()
    # Remove the external bevel on the masonry and red strip so horizontal repeats meet.
    # Leave the four upper block bevels intact, as on the modular rampart.
    masonry = master.crop((3, 32, 125, TILE)).resize((TILE, TILE - 32), LANCZOS)
    master.paste(match_edges(masonry, horizontal=3, vertical=0), (0, 32))

    # Trace the approved arch and jambs in a 500 px coordinate space. The banner,
    # outside courses and foundation come from the same master on both variants.
    gate_source = sources["wall_gate_original.png"].resize((500, 500), LANCZOS)
    stencil = Image.new("L", gate_source.size)
    ImageDraw.Draw(stencil).polygon(
        [(247, 266), (274, 270), (300, 284), (324, 302), (342, 328),
         (352, 356), (355, 430), (352, 434), (146, 434), (142, 429),
         (144, 358), (154, 328), (172, 302), (195, 283), (220, 270)],
        fill=255,
    )
    gate_source.putalpha(stencil)
    door = gate_source.crop((142, 266, 356, 435)).resize((54, 41), LANCZOS)
    allowed = np.broadcast_to((np.arange(TILE) < 72)[:, None] |
                              (np.arange(TILE) >= 111)[:, None], (TILE, TILE))
    base, mask = faction_pair(master, allowed)
    gate = base.copy()
    gate.alpha_composite(door, (37, 70))
    gate_pair = gate, mask
    original_pair = faction_pair(original, allowed)
    return {
        "wall-1": finish_facade_edges(gate_pair, original_pair, (WEST, EAST)),
        "wall-2": finish_facade_edges(gate_pair, original_pair, (WEST,)),
        "wall-8": finish_facade_edges(gate_pair, original_pair, (EAST,)),
        "wall-10": (base, mask),
    }


def rampart_cells(mask):
    return {
        (x, y) for y in range(4) for x in range(4)
        if (y == 0 and not mask & NORTH)
        or (x == 0 and not mask & WEST)
        or (x == 3 and not mask & EAST)
    }


def build(sources):
    floor = match_edges(sources["wall_floor.png"].resize((TILE, TILE), LANCZOS), 5, 5)
    rampart = sources["wall_rampart_block.png"].resize((BLOCK, BLOCK), LANCZOS)
    crystal = isolate(sources["wall_crystal_original.png"])
    crystal_base, crystal_mask = (
        layer.resize((BLOCK, BLOCK), LANCZOS) for layer in faction_pair(crystal)
    )
    center = ((TILE - BLOCK) // 2,) * 2
    floor.alpha_composite(crystal_base, center)
    mask = Image.new("L", (TILE, TILE))
    mask.paste(crystal_mask, center)
    castle = isolate(sources["castle_asset_sheet.png"].crop((380, 0, 925, 530)))
    rows = np.arange(castle.height) / castle.height
    banner_and_foundation = np.broadcast_to(((rows < 0.65) | (rows >= 0.86))[:, None],
                                          (castle.height, castle.width))
    pairs = {
        "knight": fit_sprite(isolate(sources["knight_original.png"]), 100, tinted=True),
        "castle": fit_sprite(castle, 98, tinted=True, regions=banner_and_foundation),
        "grave": fit_sprite(isolate(sources["grave.png"]), 96, tinted=False),
        **facades(sources),
    }
    for topology, count in PAVEMENT_COUNTS.items():
        base = floor.copy()
        cells = rampart_cells(topology)
        if len(cells) != count:
            raise ValueError(f"Unexpected rampart count for topology {topology}")
        for x, y in sorted(cells):
            base.paste(rampart, (x * BLOCK, y * BLOCK))
        pairs[f"wall-{topology}"] = base, mask
    return pairs


def wall_key(topology):
    if topology == 0:
        return "castle"
    return f"wall-{FACADE_BASES.get(topology, topology)}"


def verify(pairs):
    if FACADE_BASES.keys() | PAVEMENT_COUNTS.keys() != set(range(1, 16)):
        raise ValueError("Every nonzero cardinal configuration needs a texture pair")
    for key, layers in pairs.items():
        base = layers[0]
        connected = key.startswith("wall-")
        expected = (TILE, TILE) if connected else (104, 104)
        if base.mode != "RGBA" or base.size != expected:
            raise ValueError(f"{key}: invalid base")
        alpha = np.asarray(base.getchannel("A"))
        if alpha.min() != (255 if connected else 0) or alpha.max() != 255:
            raise ValueError(f"{key}: invalid alpha coverage")
        if len(layers) == 2:
            mask = layers[1]
            if mask.mode != "L" or mask.size != expected or mask.getextrema() != (0, 255):
                raise ValueError(f"{key}: invalid faction mask")
            if np.any((np.asarray(mask) > 0) & (alpha == 0)):
                raise ValueError(f"{key}: tint outside the base silhouette")
    interior = np.asarray(pairs["wall-15"][0])
    if not (np.array_equal(interior[0], interior[-1]) and np.array_equal(interior[:, 0], interior[:, -1])):
        raise ValueError("Pavement edges do not match")
    for standalone, left, right, plain in zip(*(pairs[f"wall-{key}"] for key in (1, 2, 8, 10))):
        if not np.array_equal(np.asarray(plain)[BLOCK:, 0], np.asarray(plain)[BLOCK:, -1]):
            raise ValueError("The middle façade masonry or foundation does not tile")
        if not (np.array_equal(np.asarray(standalone)[:, :ENDCAP_WIDTH], np.asarray(left)[:, :ENDCAP_WIDTH])
                and np.array_equal(np.asarray(standalone)[:, -ENDCAP_WIDTH:], np.asarray(right)[:, -ENDCAP_WIDTH:])):
            raise ValueError("Standalone façade must use both endcaps")
        if not (np.array_equal(np.asarray(left)[:, -ENDCAP_WIDTH:], np.asarray(plain)[:, -ENDCAP_WIDTH:])
                and np.array_equal(np.asarray(right)[:, :ENDCAP_WIDTH], np.asarray(plain)[:, :ENDCAP_WIDTH])):
            raise ValueError("Endpoint façades changed a continuing edge")
        interior = (slice(None), slice(ENDCAP_WIDTH, TILE - ENDCAP_WIDTH))
        gate = np.asarray(standalone)[interior]
        if not (np.array_equal(gate, np.asarray(left)[interior]) and np.array_equal(gate, np.asarray(right)[interior])):
            raise ValueError("Endpoint façades changed the gated interior")
        if any(np.array_equal(np.asarray(standalone)[BLOCK:, side], np.asarray(plain)[BLOCK:, side])
               for side in (slice(ENDCAP_WIDTH), slice(-ENDCAP_WIDTH, None))):
            raise ValueError("A terminal bevel matches a continuing edge")
        changed = np.argwhere(gate != np.asarray(plain)[interior])
        if changed.size and (changed[:, 0].min() < 70 or changed[:, 0].max() > 110
                             or changed[:, 1].min() + ENDCAP_WIDTH < 37
                             or changed[:, 1].max() + ENDCAP_WIDTH > 90):
            raise ValueError("Gate and uninterrupted façade differ outside the doorway")


def write_assets(pairs, directory):
    for key, layers in pairs.items():
        names = ["grave.png"] if key == "grave" else [f"{key}_base.png"]
        # All eight crystal masks are identical. Façade masks include their terminal trim.
        if key in ("knight", "castle", "wall-1", "wall-2", "wall-8", "wall-10", "wall-15"):
            names.append(f"{key}_mask.png")
        for name, layer in zip(names, layers):
            layer.save(directory / name, optimize=True)


def preview_tint(layers, color):
    base = np.asarray(layers[0]).astype(np.float64)
    if len(layers) == 2:
        tint = np.array([int(color[i:i + 2], 16) for i in (1, 3, 5)]) / 255
        weight = np.asarray(layers[1])[:, :, None] / 255
        base[:, :, :3] *= 1 + weight * (tint - 1)
    return Image.fromarray(np.rint(base).astype(np.uint8))


def preview_assembly(pairs, label, layout):
    cells = {(x, y): owner for y, row in enumerate(layout) for x, owner in enumerate(row) if owner != "."}
    topologies = {
        (x, y): sum(bit for bit, dx, dy in DIRECTIONS if cells.get((x + dx, y + dy)) == owner)
        for (x, y), owner in cells.items()
    }
    sheet = Image.new("RGBA", (len(layout[0]) * TILE, len(layout) * TILE + 24), "#657f5a")
    ImageDraw.Draw(sheet).text((8, 5), label, fill="white")
    for (x, y), owner in cells.items():
        artwork = topologies[x, y]
        if artwork & SOUTH:
            # Remove only the roof-to-façade side joins from the artwork lookup.
            for side, dx in ((EAST, 1), (WEST, -1)):
                if artwork & side and not topologies[x + dx, y] & SOUTH:
                    artwork &= ~side
        pair = pairs[wall_key(artwork)]
        sheet.alpha_composite(preview_tint(pair, COLORS[owner == "B"]), (x * TILE, y * TILE + 24))
    return sheet, set(topologies.values())


def review_sheets(pairs, directory):
    directory.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGBA", (6 * 142, 3 * 160), "#657f5a")
    draw = ImageDraw.Draw(sheet)
    for index in range(16):
        x, y = index % 6 * 142, index // 6 * 160
        label = "".join(letter for letter, (bit, _, _) in zip("NESW", DIRECTIONS) if index & bit)
        draw.text((x + 7, y + 4), f"{index}: {label or 'Singleton'}", fill="white")
        sheet.alpha_composite(preview_tint(pairs[wall_key(index)], COLORS[index % 2]),
                              (x + 7, y + 23))
    for index, key in enumerate(("knight", "grave"), start=16):
        x, y = index % 6 * 142, index // 6 * 160
        draw.text((x + 7, y + 4), key.title(), fill="white")
        sheet.alpha_composite(preview_tint(pairs[key], COLORS[0]), (x + 19, y + 35))
    sheet.convert("RGB").save(directory / "connectivity.png")

    sheet = Image.new("RGBA", (6 * 120, 4 * 135), "#657f5a")
    draw = ImageDraw.Draw(sheet)
    for y, key in enumerate(("knight", "castle", "wall-1", "wall-15")):
        base, mask = pairs[key]
        draw.text((8, y * 135 + 3), key, fill="white")
        images = [base, mask.convert("RGBA"), *(preview_tint(pairs[key], color) for color in COLORS)]
        for x, image in enumerate(images):
            sheet.alpha_composite(image.resize((104, 104), LANCZOS), (x * 120 + 8, y * 135 + 23))
    sheet.convert("RGB").save(directory / "faction-colors.png")

    layouts = (
        ("Two façades", ("RR",)), ("Three façades", ("RRR",)),
        ("Five façades", ("RRRRR",)), ("Vertical", ("R", "R", "R")),
        ("2 x 2", ("RR", "RR")), ("3 x 3", ("RRR", "RRR", "RRR")),
        ("Large rectangle", ("BBBBB",) * 4), ("L", ("R..", "R..", "RRR")),
        ("T", ("BBB", ".B.", ".B.")), ("Cross", (".R.", "RRR", ".R.")),
        ("Irregular", ("RR.R.", "RRRRR", ".RR..", "..RR.")),
        ("Opposing factions", ("RRBB", "RRBB")),
    )
    seen = set()
    assemblies = {}
    for number, (label, layout) in enumerate(layouts, 1):
        sheet, topologies = preview_assembly(pairs, label, layout)
        assemblies[label] = sheet
        seen.update(topologies)
        sheet.convert("RGB").save(directory / f"assembly-{number:02}.png")
    if seen != set(range(1, 16)):
        raise ValueError(f"Assembly fixtures missed {set(range(1, 16)) - seen}")

    margin = 12
    facade_width = 6 * TILE
    facade_rows = (
        ("Same-faction wall: continuous masonry and foundation", ("RRRRRR",)),
        ("Adjacent fortresses: red and blue terminate at the center", ("RRRBBB", "RRRBBB")),
    )
    facade_review = Image.new("RGB", (facade_width + 2 * margin, 4 * TILE + 48 + 5 * margin), "#304138")
    draw = ImageDraw.Draw(facade_review)
    labels = ("E: left end", "EW: middle", "W: right end", "N: both ends")
    for column, (key, label) in enumerate(zip((2, 10, 8, 1), labels)):
        x = margin + column * (facade_width - TILE) // 3
        draw.text((x, margin), label, fill="white")
        facade_review.paste(preview_tint(pairs[f"wall-{key}"], COLORS[0]), (x, margin + 16))
    y = TILE + 3 * margin
    for label, layout in facade_rows:
        sheet, _ = preview_assembly(pairs, label, layout)
        facade_review.paste(sheet, (margin, y))
        y += sheet.height + margin
    facade_review.save(directory / "assemblies-facades.png")

    width = 9 * TILE + 4 * margin
    top_height = 3 * TILE + 24
    irregular = Image.new("RGB", (width, top_height + 4 * TILE + 24 + 3 * margin), "#304138")
    for column, label in enumerate(("L", "T", "Cross")):
        irregular.paste(assemblies[label], (margin + column * (3 * TILE + margin), margin))
    irregular.paste(assemblies["Irregular"], (margin, top_height + 2 * margin))
    irregular.paste(assemblies["Opposing factions"], (5 * TILE + 2 * margin, top_height + 2 * margin))
    irregular.save(directory / "assemblies-irregular.png")

    top_height = assemblies["3 x 3"].height
    large = assemblies["Large rectangle"]
    fortress_review = Image.new("RGB", (6 * TILE + 4 * margin, top_height + large.height + 3 * margin), "#304138")
    x = margin
    for label in ("Vertical", "2 x 2", "3 x 3"):
        panel = assemblies[label]
        fortress_review.paste(panel, (x, margin))
        x += panel.width + margin
    fortress_review.paste(large, (margin, top_height + 2 * margin))
    fortress_review.save(directory / "assemblies-fortresses.png")

    row_height = 2 * TILE + 24
    transitions = Image.new("RGB", (width, 2 * row_height + 3 * margin), "#304138")
    for row, (faction, owner) in enumerate((("Red", "R"), ("Blue", "B"))):
        for column, (label, lower_row) in enumerate((
            ("Left facade", ".RR"), ("Right facade", "RR."), ("Both facades", ".R."),
        )):
            layout = (owner * 3, lower_row.replace("R", owner))
            sheet, _ = preview_assembly(pairs, f"{faction}: {label}", layout)
            transitions.paste(sheet, (margin + column * (3 * TILE + margin), margin + row * (row_height + margin)))
    transitions.save(directory / "assemblies-facade-transitions.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sources", type=Path, help="Directory containing all ten approved PNGs")
    parser.add_argument("--check", action="store_true", help="Compare regenerated PNGs byte for byte")
    parser.add_argument("--previews", type=Path, help="Write review sheets to this directory")
    args = parser.parse_args()
    pairs = build(open_sources(args.sources))
    verify(pairs)
    if args.previews:
        review_sheets(pairs, args.previews)
    if args.check:
        with TemporaryDirectory() as temp:
            directory = Path(temp)
            write_assets(pairs, directory)
            expected_walls = {p.name for p in directory.glob("wall-*.png")}
            actual_walls = {p.name for p in BOARD.glob("wall-*.png")}
            if actual_walls != expected_walls:
                raise ValueError(f"Unexpected or missing castle textures: {sorted(actual_walls ^ expected_walls)}")
            for file in sorted(directory.glob("*.png")):
                if file.read_bytes() != (BOARD / file.name).read_bytes():
                    raise ValueError(f"Regenerated {file.name} differs from production")
                settings = (BOARD / f"{file.name}.import").read_text().splitlines()
                if "compress/mode=0" not in settings or "mipmaps/generate=false" not in settings:
                    raise ValueError(f"{file.name}: expected lossless import without mipmaps")
    else:
        write_assets(pairs, BOARD)
    print("Verified 15 castle configurations, footprint, alpha, mask alignment and shared seams.")


if __name__ == "__main__":
    main()
