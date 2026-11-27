"""Compose same-perspective masonry connectors around the current singleton castle."""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Sprites/Board"
CASTLE = OUT
SIZE = 128
SCALE = 3


def draw_join(base, mask, bounds, vertical):
    d, m = ImageDraw.Draw(base), ImageDraw.Draw(mask)

    def rect(draw, box, ink):
        draw.rectangle(tuple(v*SCALE for v in box), fill=ink)

    x0,y0,x1,y1 = bounds
    rect(d,bounds,"#30313e")
    if vertical:
        rect(d,(x0+3,y0,x1-3,y1),"#787d91")
        rect(d,(x0+5,y0,x0+10,y1),"#b9bdc9")
        rect(d,(x1-11,y0,x1-5,y1),"#adb1c0")
        rect(d,(x0+12,y0,x1-12,y1),"#888892")
        rect(m,(x0+12,y0,x1-12,y1),235)
        for y in range(y0+8,y1,16):
            rect(d,(x0+3,y,x1-3,y+1),"#575b6d")
    else:
        rect(d,(x0,y0+3,x1,y1-3),"#8b8fa1")
        rect(d,(x0,y0+3,x1,y0+7),"#bbc0ce")
        rect(d,(x0,y1-11,x1,y1-4),"#afb0bc")
        rect(m,(x0,y1-11,x1,y1-4),255)
        for x in range(8,128,11):
            if x0<x<x1:
                rect(d,(x,y0+8,x+1,y1-13),"#616579")
                rect(d,(x+2,y0+8,x+3,y0+14),"#a5a9ba")
        rect(d,(x0,y0+16,x1,y0+17),"#66687b")


def main():
    original=Image.open(CASTLE/"castle_base.png").convert("RGBA")
    original_mask=Image.open(CASTLE/"castle_mask.png").convert("L")
    box=(94*SCALE,98*SCALE)
    factor=min(box[0]/original.width,box[1]/original.height)
    size=(round(original.width*factor),round(original.height*factor))
    original=original.resize(size,Image.Resampling.LANCZOS)
    original_mask=original_mask.resize(size,Image.Resampling.LANCZOS)
    anchor=((SIZE*SCALE-size[0])//2,17*SCALE)
    for topology in range(1,16):
        base=Image.new("RGBA",(SIZE*SCALE,SIZE*SCALE))
        mask=Image.new("L",base.size)
        for bit,bounds in enumerate(((48,0,81,71),(64,71,128,105),(48,66,81,128),(0,71,64,105))):
            if topology & (1<<bit):
                draw_join(base,mask,bounds,bit%2==0)
        base.alpha_composite(original,anchor)
        # Where battlements leave gaps, keep the extension and its tint channel aligned.
        mask.paste(original_mask,anchor,original.getchannel("A"))
        base.resize((SIZE,SIZE),Image.Resampling.LANCZOS).save(OUT/f"wall-{topology}_base.png",optimize=True)
        mask.resize((SIZE,SIZE),Image.Resampling.LANCZOS).save(OUT/f"wall-{topology}_mask.png",optimize=True)


if __name__=="__main__":
    main()
