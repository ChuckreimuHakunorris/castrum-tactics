"""Build review sheets from production art and the owner's selected reference crop."""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
BANK = ROOT / "Assets/Sprites/Board"
OUT = ROOT / "Documentation/assets/10b"
BG = "#1c2c41"
TEXT = "#e8eff7"
PALETTES = ((240,108,104),(105,164,255),(120,216,138),(207,120,237))
CROPS = {"knight":(228,927,329,1027), "castle":(132,927,230,1027), "grave":(132,240,230,341)}


def asset(name):
    return Image.open(BANK / f"{name}.png").convert("RGBA")


def font(size):
    return ImageFont.truetype("DejaVuSans.ttf",size)


def sheet(width,height,title):
    image = Image.new("RGBA",(width,height),BG)
    draw = ImageDraw.Draw(image)
    draw.text((15,11),title,fill=TEXT,font=font(22))
    return image,draw


def place(dst,img,x,y,size):
    img=img.convert("RGBA").resize((size,size),Image.Resampling.LANCZOS)
    dst.alpha_composite(img,(x,y))


def recolor(name,color):
    base=asset(name+"_base")
    mask=Image.open(BANK/f"{name}_mask.png").convert("L")
    pixels=base.load()
    for y in range(base.height):
        for x in range(base.width):
            alpha=mask.getpixel((x,y))/255
            red,green,blue,a=pixels[x,y]
            channels=[round(v*(1-alpha+alpha*c/255)) for v,c in zip((red,green,blue),color)]
            pixels[x,y]=(*channels,a)
    return base


def source_sheet(source):
    image,d=sheet(970,430,"Reference pixels, isolated alpha, and the production tint")
    for i,(kind,bounds) in enumerate(CROPS.items()):
        y=67+i*116
        d.text((15,y+42),kind.capitalize(),fill=TEXT,font=font(18))
        place(image,source.crop(bounds),129,y,100)
        sprite=asset(kind if kind=="grave" else kind+"_base")
        checker=Image.new("RGBA",(100,100),"#6a7687")
        shades=ImageDraw.Draw(checker)
        for j in range(10):
            for k in range(10):
                if (j+k)%2==0:
                    shades.rectangle((j*10,k*10,j*10+9,k*10+9),fill="#788597")
        place(checker,sprite,0,0,100)
        image.alpha_composite(checker,(250,y))
        if kind!="grave":
            place(image,Image.open(BANK/f"{kind}_mask.png").convert("RGBA"),372,y,100)
        for j,color in enumerate(PALETTES):
            texture=asset("grass-plain" if j%2==0 else "snow-plain")
            place(texture,sprite if kind=="grave" else recolor(kind,color),0,0,128)
            place(image,texture,494+j*117,y,100)
    for x,label in [(129,"Native crop"),(250,"RGBA base"),(372,"Tint mask"),(494,"Default factions"),(728,"Test colors")]:
        d.text((x,47),label,fill=TEXT,font=font(12))
    image.convert("RGB").save(OUT/"reference-pieces.png",optimize=True)


def terrain_sheet():
    files=sorted(f.stem for f in BANK.glob("*.png") if not f.stem.startswith(("wall-","knight","castle","grave"))
                 and not f.stem.endswith(("-1","-2","-3")))
    image,d=sheet(900,68+((len(files)+7)//8)*107,"Terrain library · intact and blocked variants")
    for i,name in enumerate(files):
        x,y=11+(i%8)*112,55+(i//8)*107
        place(image,asset(name),x+10,y,82)
        d.text((x,y+84),name,fill=TEXT,font=font(10))
    image.convert("RGB").save(OUT/"terrain-bank.png",optimize=True)


def damage_sheet():
    image,d=sheet(870,505,"Damage-only cues · zero, one, two and three marks")
    for i,kind in enumerate(("grass-plain","sand-plain","snow-plain","bridge-ew")):
        y=66+i*103
        d.text((12,y+31),kind.split("-")[0].capitalize(),fill=TEXT,font=font(17))
        for damage in range(4):
            x=120+damage*177
            terrain=asset(kind+f"-{damage}" if damage else kind)
            place(image,terrain,x,y,85)
            place(image,terrain,x+87,y,76)
            piece=asset("grave") if i%2==0 else recolor("knight",PALETTES[0])
            place(image,piece,x+89,y+2,72)
            for left,size in ((x,85),(x+87,76)):
                for mark in range(damage):
                    cx,cy=left+size*(.87-.10*mark),y+size*.88
                    d.ellipse((cx-4,cy-4,cx+4,cy+4),fill="#362624")
                    d.ellipse((cx-2,cy-2,cx+2,cy+2),fill="#ffe3a2")
    image.convert("RGB").save(OUT/"ground-damage.png",optimize=True)


def walls_sheet():
    image,d=sheet(1280,489,"Cardinal connection masks · N=1, E=2, S=4, W=8")
    for topology in range(16):
        x=12+topology*79
        d.text((x+23,47),f"{topology:02d}",fill=TEXT,font=font(12))
        for i,color in enumerate(PALETTES):
            y=67+i*102
            place(image,asset("grass-plain"),x,y,70)
            place(image,recolor("castle" if topology==0 else f"wall-{topology}",color),x,y,70)
    for i,label in enumerate(("Player One","Player Two","Test green","Test violet")):
        d.text((14,141+i*102),label,fill=TEXT,font=font(13))
    image.convert("RGB").save(OUT/"castle-masks.png",optimize=True)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference",type=Path)
    args=parser.parse_args()
    OUT.mkdir(parents=True,exist_ok=True)
    source_sheet(Image.open(args.reference))
    terrain_sheet()
    damage_sheet()
    walls_sheet()


if __name__=="__main__":
    main()
