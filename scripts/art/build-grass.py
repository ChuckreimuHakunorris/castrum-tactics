"""Prepare the approved grass artwork and deterministic visual review sheets."""

import argparse
import hashlib
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps

BOARD = Path(__file__).resolve().parents[2] / "Assets/Sprites/Board"
SIZE = 128
FEATHER = 6
LANCZOS = Image.Resampling.LANCZOS
SOURCES = {
    "grass_base_01.png": ((1254, 1254), "b39ccb4246f771151ecd89641c35af4c790b092b6138dd8593047cd848d97a1b"),
    "grass_base_02.png": ((1254, 1254), "351ae89a1c441fab04c5f301fa724446bf558a815912eb6ffaf11e834d94972a"),
    "grass_base_03.png": ((1254, 1254), "df4e16a2fc2e33813562d7cffa01023194786f5408b5f5340bf4ffbc9064f1f3"),
    "grass_base_04.png": ((1254, 1254), "b033b628606cc8279d1588ce17ce4ee1b271cfbc37795e6d43dcc5510dc7f0b3"),
    "accent_flowers.png": ((447, 329), "cabf9b7133e8de75d360a19294b22a619f30b57cfdc0c7af243ac6f3347c9b65"),
    "accent_pebbles.png": ((358, 271), "a6884dcb7d74468aacca99acafc27a61d49e621e31063b76de755211dcbd20c0"),
}
ACCENTS = (("flowers", 0.48, 91), ("pebbles", 0.38, 94))


def read_sources(directory):
    images = {}
    for name, (size, checksum) in SOURCES.items():
        path = directory / name
        if hashlib.sha256(path.read_bytes()).hexdigest() != checksum:
            raise ValueError(f"{name}: differs from the approved source")
        with Image.open(path) as image:
            if image.size != size:
                raise ValueError(f"{name}: expected {size}, got {image.size}")
            images[name] = image.convert("RGBA")
    return images


def build(sources):
    names = [name for name in sources if name.startswith("grass_base")]
    resized = {name: np.asarray(sources[name].convert("RGB").resize((SIZE, SIZE), LANCZOS)) for name in names}
    shared = np.mean(list(resized.values()), axis=0)
    edge = (1 + np.cos(np.pi * np.arange(FEATHER) / (FEATHER - 1))) / 2
    horizontal = (shared[0] + shared[-1]) / 2
    vertical = (shared[:, 0] + shared[:, -1]) / 2
    horizontal[0] = horizontal[-1] = vertical[0] = vertical[-1] = (horizontal[0] + horizontal[-1]) / 2
    assets = {}
    for name, original in resized.items():
        pixels = original.astype(float)
        # Fade only the color difference. Keep each tile's texture detail near the joint.
        for axis, seam in enumerate((horizontal, vertical)):
            rows = np.swapaxes(pixels, 0, axis)
            rows[:FEATHER] += edge[:, None, None] * (seam - rows[0])
            rows[-FEATHER:] += edge[::-1, None, None] * (seam - rows[-1])
        pixels = np.rint(pixels).clip(0, 255).astype(np.uint8)
        if not np.array_equal(pixels[FEATHER:-FEATHER, FEATHER:-FEATHER], original[FEATHER:-FEATHER, FEATHER:-FEATHER]):
            raise ValueError(f"{name}: edge processing changed its interior")
        assets[name] = Image.fromarray(pixels)

    for first in names:
        for second in names:
            a, b = np.asarray(assets[first]), np.asarray(assets[second])
            if not (np.array_equal(a[:, -1], b[:, 0]) and np.array_equal(a[-1], b[0])):
                raise ValueError(f"Incompatible seams or corners for {first} and {second}")

    for kind, _, height in ACCENTS:
        image = sources[f"accent_{kind}.png"]
        trimmed = image.crop(image.getchannel("A").getbbox())
        if round(SIZE * trimmed.height / trimmed.width) != height:
            raise ValueError(f"{kind}: update the documented production aspect ratio")
        sprite = trimmed.resize((SIZE, height), LANCZOS)
        if sprite.getchannel("A").getextrema() != (0, 255):
            raise ValueError(f"{kind}: expected transparent background and opaque detail")
        assets[f"grass-accent-{kind}.png"] = sprite
    return assets


def samples(map_id, salt, x, y):
    # Reference implementation of TerrainVisualRandom for the review sheets.
    def fnv(data, h=2166136261):
        for byte in data:
            h = (h ^ byte) * 16777619 & 0xffffffff
        return h

    def append(h, value):
        return fnv((value & 0xffffffff).to_bytes(4, "little"), h)

    def channel(tile, number):
        value = append(tile, number)
        value = (value ^ (value >> 16)) * 0x85ebca6b & 0xffffffff
        value = (value ^ (value >> 13)) * 0xc2b2ae35 & 0xffffffff
        return value ^ (value >> 16)

    tile = append(append(append(append(fnv(map_id.encode("utf-8")), salt), x), y), fnv(b"grass"))
    return tuple(channel(tile, number) for number in range(1, 7))


def with_accent(assets, kind, scale, center, mirror):
    width = next(value for name, value, _ in ACCENTS if name == kind)
    accent = assets[f"grass-accent-{kind}.png"]
    extent = round(SIZE * width * scale), round(accent.height * width * scale)
    accent = accent.resize(extent, LANCZOS)
    if mirror:
        accent = ImageOps.mirror(accent)
    origin = tuple(max(0, min(SIZE - size, round(c * SIZE - size / 2))) for c, size in zip(center, extent))
    tile = assets["grass_base_01.png"].convert("RGBA")
    tile.alpha_composite(accent, origin)
    return tile.convert("RGB")


def preview(assets, directory):
    directory.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGB", (4 * 144 + 12, 4 * 155 + 12), "#304138")
    labels = ("Base 01", "Base 02", "Base 03", "Base 04", "Flowers 65%", "Flowers 100%",
              "Pebbles 65%", "Pebbles 100%")
    rendered = [assets[f"grass_base_{i:02}.png"] for i in range(1, 5)]
    for kind, _, _ in ACCENTS:
        rendered.extend((with_accent(assets, kind, 0.65, (0.30, 0.70), True),
                         with_accent(assets, kind, 1.00, (0.70, 0.33), False)))
    draw = ImageDraw.Draw(sheet)
    for i, (label, tile) in enumerate(zip(labels, rendered)):
        x, y = 12 + i % 4 * 144, 12 + i // 4 * 155
        draw.text((x, y), label, fill="white")
        sheet.paste(tile, (x, y + 17))
    draw.text((12, 322), "Mixed seams and four-tile intersections", fill="white")
    for y, row in enumerate(((0, 1, 3, 2), (2, 3, 1, 0))):
        for x, tile in enumerate(row):
            sheet.paste(rendered[tile], (12 + x * SIZE, 339 + y * SIZE))
    sheet.save(directory / "grass-assets.png")

    fields = []
    for salt in (0, 29, 0):
        field = Image.new("RGB", (12 * SIZE, 10 * SIZE))
        counts = [0, 0, 0]
        for y in range(10):
            for x in range(12):
                accent, base, scale, dx, dy, mirror = samples("twelve-towers", salt, x, y)
                choice = accent * 100 >> 32
                counts[0 if choice < 75 else 1 if choice < 90 else 2] += 1
                if choice < 75:
                    tile = rendered[base * 4 >> 32]
                else:
                    unit_scale, unit_x, unit_y = ((value >> 8) / 16777216 for value in (scale, dx, dy))
                    tile = with_accent(assets, "flowers" if choice < 90 else "pebbles", 0.65 + 0.35 * unit_scale,
                                       (0.3 + 0.4 * unit_x, 0.3 + 0.4 * unit_y), mirror < (1 << 31))
                field.paste(tile, (x * SIZE, y * SIZE))
        fields.append(field)
        print(f"Preview seed {salt}: {counts} (none, flowers, pebbles)")
    if fields[0].tobytes() != fields[2].tobytes() or fields[0].tobytes() == fields[1].tobytes():
        raise ValueError("Visual repeatability or variation check failed")
    sheet = Image.new("RGB", (12 * 32 * 3 + 48, 10 * 32 + 40), "#304138")
    draw = ImageDraw.Draw(sheet)
    for i, field in enumerate(fields):
        x = 12 + i * (12 * 32 + 12)
        draw.text((x, 8), ("Seed 0", "Seed 29", "Seed 0 repeated · pixel-identical")[i], fill="white")
        sheet.paste(field.resize((12 * 32, 10 * 32), LANCZOS), (x, 24))
    sheet.save(directory / "grass-seeds.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sources", type=Path)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--previews", type=Path)
    args = parser.parse_args()
    assets = build(read_sources(args.sources))
    if args.previews:
        preview(assets, args.previews)
    for name, image in assets.items():
        path = BOARD / name
        if args.check:
            with Image.open(path) as existing:
                if (existing.mode, existing.size, existing.tobytes()) != (image.mode, image.size, image.tobytes()):
                    raise ValueError(f"{name}: production pixels differ from regeneration")
            settings = path.with_suffix(".png.import").read_text().splitlines()
            if "compress/mode=0" not in settings or "mipmaps/generate=false" not in settings:
                raise ValueError(f"{name}: expected lossless textures without mipmaps")
        else:
            image.save(path, optimize=True)
    print("Verified all grass seams and intersections, preserved interiors and checked accent alpha.")


if __name__ == "__main__":
    main()
