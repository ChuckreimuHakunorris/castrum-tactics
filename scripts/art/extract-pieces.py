"""Isolate the selected native-pixel reference crops and split blue livery into a mask."""
import argparse
import colorsys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Sprites/Board"

# Bounds are source pixel coordinates. Contours below are relative to each crop.
PIECES = {
    "castle": ((132, 927, 230, 1027), [
        [(1,9),(5,6),(12,5),(14,3),(21,3),(23,5),(29,6),(34,10),(35,18),(34,23),
         (38,23),(40,20),(46,20),(47,17),(56,17),(57,20),(62,21),(63,23),
         (63,11),(66,7),(74,5),(75,3),(84,3),(86,5),(92,6),(96,10),(97,18),(94,23),
         (92,27),(92,64),(94,71),(95,82),(96,85),(96,93),(0,93),(0,85),(3,82),
         (3,71),(5,64),(6,28),(3,24),(1,20)]]),
    "knight": ((228, 927, 329, 1027), [
        [(14,28),(21,9),(24,8),(27,10),(35,30),(33,34),(27,38),(27,72),(24,91),
         (19,92),(15,89),(15,65),(12,63),(12,58),(17,54),(17,38),(13,33)],
        [(17,87),(14,81),(15,74),(20,68),(28,60),(36,53),(34,49),(34,40),(35,34),
         (40,28),(46,25),(43,21),(43,16),(46,10),(51,6),(59,3),(68,3),(76,5),
         (82,9),(85,16),(86,21),(85,25),(89,24),(90,27),(87,29),(83,31),(78,30),
         (74,27),(72,24),(69,23),(66,24),(64,27),(67,30),(68,36),(68,49),(75,50),
         (78,53),(85,54),(92,57),(93,63),(91,73),(88,80),(82,86),(73,91),(65,90),
         (58,89),(53,90),(48,88),(46,84),(43,89),(37,91),(31,91),(26,89)]]),
    "grave": ((132, 240, 230, 341), [
        [(7,77),(5,74),(5,72),(12,71),(13,65),(15,61),(18,61),(22,65),(22,29),
         (25,26),(35,16),(64,16),(69,20),(78,29),(79,62),(81,64),(84,62),
         (87,64),(88,73),(93,73),(94,76),(91,81),(87,84),(82,85),(83,88),
         (21,88),(16,86),(12,84),(9,82)]]),
}


def isolate(source, kind):
    bounds, shapes = PIECES[kind]
    crop = source.crop(bounds).convert("RGBA")
    factor = 8
    silhouette = Image.new("L", (crop.width * factor, crop.height * factor))
    draw = ImageDraw.Draw(silhouette)
    for shape in shapes:
        draw.polygon([(x * factor, y * factor) for x, y in shape], fill=255)
    alpha = silhouette.resize(crop.size, Image.Resampling.LANCZOS)
    mask = Image.new("L", crop.size)
    pixels = crop.load()
    coverage = alpha.load()
    tint = mask.load()
    for y in range(crop.height):
        for x in range(crop.width):
            r, g, b, _ = pixels[x, y]
            a = coverage[x,y]
            if kind != "grave":
                # The playable background is green. Remove it at the traced boundary while preserving dark outlines.
                if g > r * 1.17 and g > b * 1.21 and g > 52:
                    a = 0
                hue, saturation, light = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
                if a and .53 <= hue <= .71 and saturation > .30 and light > .15:
                    # Neutral shading retains the source's luminance variation without multiplying one hue by another.
                    level = min(255, round(max(r,g,b) * 1.07))
                    r = g = b = level
                    tint[x, y] = round(255 * min(1, (saturation - .30) / .18))
            pixels[x, y] = (r, g, b, a)
    # Keep a common 100 px tile footprint without upsampling the native artwork.
    aligned = Image.new("RGBA", (104,104))
    aligned_mask = Image.new("L", aligned.size)
    offset = ((104-crop.width)//2,(104-crop.height)//2)
    aligned.paste(crop,offset)
    aligned_mask.paste(mask,offset)
    return aligned, aligned_mask


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference", type=Path, help="Crescent Quarry with Blocky Graves.png supplied by the owner")
    args = parser.parse_args()
    source = Image.open(args.reference)
    if source.size != (1476,1065):
        raise ValueError("Expected the 1476 × 1065 native reference crop coordinates")
    for kind in PIECES:
        base, mask = isolate(source, kind)
        if kind == "grave":
            base.save(OUT / "grave.png", optimize=True)
        else:
            base.save(OUT / f"{kind}_base.png", optimize=True)
            mask.save(OUT / f"{kind}_mask.png", optimize=True)


if __name__ == "__main__":
    main()
