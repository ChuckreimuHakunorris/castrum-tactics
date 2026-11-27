"""Author the smooth 10B terrain library as antialiased, deterministic raster tiles."""
from pathlib import Path
import math
import random
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Sprites/Board"
SIZE = 128
S = 3
INK = "#303742"


def points(seq):
    return [(round(x*S), round(y*S)) for x, y in seq]


def poly(d, seq, color, outline=None):
    if outline is not None:
        line(d, [*seq, seq[0]], outline, 3)
    d.polygon(points(seq), fill=color)


def line(d, seq, color, width=2):
    d.line(points(seq), fill=color, width=round(width*S), joint="curve")


def rect(d, box, color, radius=0, outline=None):
    d.rounded_rectangle(tuple(round(v*S) for v in box), radius=radius*S, fill=color,
                        outline=outline, width=2*S if outline else 1)


def oval(d, x, y, w, h, color, outline=None):
    d.ellipse(tuple(round(a*S) for a in (x,y,x+w,y+h)), fill=color,
              outline=outline, width=2*S if outline else 1)


def blank(color=(0,0,0,0)):
    im = Image.new("RGBA", (SIZE*S, SIZE*S), color)
    return im, ImageDraw.Draw(im)


def ramp(d, top, bottom):
    for y in range(SIZE*S):
        f = y/(SIZE*S-1)
        d.line((0,y,SIZE*S,y), fill=tuple(round(a*(1-f)+b*f) for a,b in zip(top,bottom)))


def tuft(d, x, y, scale=1):
    shape = [(-9,-7),(-1,-3),(-3,-13),(2,-5),(7,-12),(6,-2),(12,-5),(6,2),(0,3)]
    poly(d, [(x+a*scale,y+b*scale) for a,b in shape], "#83aa44")


def accent(d, kind, x, y):
    if kind in ("tufts", "grass-tuft"):
        tuft(d,x,y,.87)
    elif kind == "flowers":
        for a,b in ((-4,0),(7,5),(3,-8)):
            oval(d,x+a-3,y+b-3,7,7,"#fff1d0")
            oval(d,x+a-1,y+b-1,3,3,"#e6b947")
    else:
        oval(d,x-6,y-2,11,6,"#979883")
        oval(d,x-5,y-2,8,3,"#c9c8ac")
        oval(d,x+6,y+4,5,3,"#a9a791")


def rock(d, x, y, w, h):
    shape = [(x,y+h*.77),(x+w*.08,y+h*.32),(x+w*.33,y+h*.07),
             (x+w*.69,y),(x+w*.92,y+h*.26),(x+w,y+h*.72),(x+w*.77,y+h),(x+w*.18,y+h)]
    poly(d,shape,"#62687b",INK)
    poly(d,[shape[1],shape[2],shape[3],shape[4],(x+w*.64,y+h*.47),(x+w*.35,y+h*.57)],"#aeb6c4")
    poly(d,[shape[0],shape[1],(x+w*.35,y+h*.57),(x+w*.62,y+h*.48),shape[-1]],"#858da0")
    poly(d,[(x+w*.53,y+h*.77),(x+w*.7,y+h*.49),shape[4],shape[5],shape[6]],"#555d71")


def peak(d, x, y, w, h, snowy):
    base = [(x,y+h),(x+w*.13,y+h*.67),(x+w*.43,y+h*.04),(x+w*.52,y),
            (x+w*.71,y+h*.29),(x+w,y+h)]
    poly(d,base,"#606783",INK)
    poly(d,[base[0],base[2],base[3],(x+w*.52,y+h*.43),(x+w*.38,y+h*.60),
            (x+w*.33,y+h*.90)],"#a2aabc")
    poly(d,[base[3],base[4],base[5],(x+w*.68,y+h*.68),(x+w*.61,y+h*.48)],"#4d566e")
    if snowy:
        poly(d,[base[2],base[3],base[4],(x+w*.80,y+h*.53),(x+w*.61,y+h*.42),
                (x+w*.54,y+h*.55),(x+w*.48,y+h*.4),(x+w*.30,y+h*.46)],"#e1f1f6")
        poly(d,[base[3],base[4],(x+w*.80,y+h*.53),(x+w*.61,y+h*.42)],"#a7c6d9")


def foliage(d, x, y, k, tree, berries=False):
    def blob(dx,dy,w,h,c,edge=None):
        oval(d,x+dx*k,y+dy*k,w*k,h*k,c,edge)
    blob(-31,13,66,24,"#416335")
    if tree:
        poly(d,[(x-8*k,y+4*k),(x+8*k,y+4*k),(x+8*k,y+27*k),(x+17*k,y+32*k),
                (x-14*k,y+34*k),(x-7*k,y+21*k)],"#875834","#493925")
        line(d,[(x-2*k,y+11*k),(x-2*k,y+26*k)],"#b58447",3*k)
    for dx,dy,w,h in [(-29,-9,37,31),(-12,-26,43,41),(11,-13,31,32),(-10,-6,44,33)]:
        blob(dx,dy,w,h,"#497f30","#2f5430")
        blob(dx+1,dy+1,w*.89,h*.65,"#70a730")
        blob(dx+5,dy+2,w*.51,h*.24,"#99c64c")
    if berries:
        for dx,dy in [(-15,4),(6,-13),(15,10),(28,1),(-4,14)]:
            blob(dx,dy,6,6,"#da5940")
            blob(dx+1,dy,2,2,"#ffe19b")


def house(d,x,y,k):
    def p(seq): return [(x+a*k,y+b*k) for a,b in seq]
    poly(d,p([(-28,0),(27,0),(27,33),(-28,33)]),"#e7c98f","#4e4032")
    poly(d,p([(-33,4),(-11,-24),(22,-24),(32,3),(13,8),(-28,8)]),"#a64e43","#673f38")
    poly(d,p([(-33,4),(-11,-24),(22,-24),(13,-13),(-10,-2)]),"#dc7751")
    line(d,p([(-23,10),(-23,32),(22,32),(22,10)]),"#9c7250",2)
    poly(d,p([(-7,33),(-7,14),(6,14),(6,33)]),"#633e2e")
    rect(d,(x+11*k,y+13*k,x+21*k,y+24*k),"#557a8d",1)


def ruins(d):
    for x,y,w,h in [(16,62,20,46),(91,64,20,41),(37,46,16,22),(51,35,25,18),(76,46,16,21)]:
        rect(d,(x,y,x+w,y+h),"#92948b",4,"#4b554c")
        rect(d,(x+3,y+3,x+w-4,y+7),"#c3c3ad",2)
        line(d,[(x+1,y+h*.62),(x+w-1,y+h*.62)],"#616b61")
        oval(d,x-2,y-1,w*.7,8,"#739c43")
    rock(d,22,99,26,16)
    rock(d,73,99,22,16)


EDGE = {"n":(64,-4),"e":(132,64),"s":(64,132),"w":(-4,64)}
WATER = "#438bad"


def spline(a, b, c):
    return [((1-t)**2*a[0]+2*t*(1-t)*b[0]+t*t*c[0],
             (1-t)**2*a[1]+2*t*(1-t)*b[1]+t*t*c[1]) for t in (i/32 for i in range(33))]


def river(d, variant):
    start=EDGE[variant[0]]
    end=EDGE[variant[-1]] if len(variant)>1 else (64,64)
    path=spline(start,(64,64),end) if len(variant)>1 else [start,end]
    for color,width in [("#59666b",67),("#ad936a",61),("#7dc8c2",55),(WATER,49)]:
        line(d,path,color,width)
        if len(variant)==1: oval(d,64-width/2,64-width/2,width,width,color)
    # Wavelets remain inside the common edge profile.
    for t in (6,16,25):
        if len(path)>2:
            x,y=path[t]
            line(d,[(x-11,y+2),(x-6,y),(x+3,y+1)],"#6cb2c3",2)


def lake(d, variant):
    d.rectangle((0,0,SIZE*S,SIZE*S), fill=WATER)
    for side in variant if variant!="open" else "":
        path={"n":[(-8,4),(64,6),(136,4)],"e":[(124,-8),(121,64),(124,136)],
              "s":[(-8,124),(64,122),(136,124)],"w":[(4,-8),(7,64),(4,136)]}[side]
        for color,width in [("#536c3c",23),("#ad936a",14),("#7dc8c2",7)]:
            offset={"n":(0,4),"e":(-4,0),"s":(0,-4),"w":(4,0)}[side]
            line(d,[(x+offset[0]*(23-width)/5,y+offset[1]*(23-width)/5) for x,y in path],color,width)
    for x,y in [(30,54),(78,37),(63,93)]:
        line(d,[(x,y+2),(x+9,y),(x+24,y+1)],"#70afc3",2)


def bridge(d, variant, damage):
    river(d, "ns" if variant=="ew" else "ew")
    def pt(x,y): return (x,y) if variant=="ew" else (y,x)
    poly(d,[pt(-3,33),pt(131,33),pt(131,96),pt(-3,96)],"#69452c")
    for i,x in enumerate(range(0,128,16)):
        top=("#b68650","#c49b63","#b18a55")[i%3]
        poly(d,[pt(x,40),pt(x+15,40),pt(x+15,88),pt(x,88)],top)
        line(d,[pt(x+3,43),pt(x+3,83)],"#a2784d",2)
    for y in (33,89):
        line(d,[pt(-4,y),pt(132,y)],"#4d3e34",7)
        line(d,[pt(-4,y-2),pt(132,y-2)],"#ad8b64",3)
    for x in (21,106):
        for y in (36,87):
            px,py=pt(x,y)
            oval(d,px-4,py-4,8,8,"#c2c3b5","#645c51")
    for i in range(damage*3):
        x=16+i*11
        line(d,[pt(x,55+i%2*9),pt(x+6,69+i%2*9),pt(x+9,66)],"#836245",3)


PALETTES={"grass":((118,155,56),(99,137,51)),"sand":((220,188,124),(197,156,91)),
          "snow":((225,239,240),(192,215,224))}
KINDS={"grass":["plain","tufts","flowers","pebbles"],"sand":["plain","pebbles"],
       "snow":["plain","pebbles"],"bridge":["ew","ns"],"rock":["plain","cluster"],
       "bush":["plain","berries"],"forest":["single","cluster"],"mountain":["plain","ridge"],
       "snow-mountain":["plain","ridge"],"river":["ew","ns","ne","es","sw","wn","n","e","s","w"],
       "lake":["open","n","e","s","w","ne","es","sw","wn"],"house":["plain"],"town":["plain"],"ruin":["plain"]}


def tile(kind, variant, damage):
    im,d=blank()
    rng=random.Random(177)
    ramp(d,*PALETTES.get(kind,((104,128,70),(77,99,60))))
    if kind in PALETTES:
        if kind=="grass":
            for x,y in [(23,26),(92,48),(45,93)]:
                tuft(d,x,y,.54)
        else:
            for _ in range(12):
                x,y=rng.randint(4,121),rng.randint(4,121)
                oval(d,x,y,5,2,"#d4ab71" if kind=="sand" else "#d6e5ea")
        if variant!="plain":
            for x,y in [(22,89),(90,24)]:
                accent(d,variant,x,y)
        colors={"grass":("#9a7851","#ab8555"),"sand":("#ab8458","#bb905d"),
                "snow":("#a2a9aa","#b3bbbc")}[kind]
        for _ in range(damage*3):
            x,y=rng.randint(14,107),rng.randint(15,111)
            w=rng.randint(9,13)+damage*6
            poly(d,[(x-w,y),(x-w*.6,y-4),(x+w*.4,y-6),(x+w,y+1),(x+2,y+6),(x-w*.5,y+5)],colors[0])
            line(d,[(x-w*.6,y-3),(x+w*.35,y-5)],colors[1],2)
    elif kind=="rock":
        shapes=[(11,31,71,61),(64,52,55,58),(23,75,54,39)] if variant=="plain" else [(34,19,72,72),(9,58,55,49),(72,78,44,37)]
        for shape in shapes: rock(d,*shape)
    elif kind in ("mountain","snow-mountain"):
        peak(d,22,10,93,97,kind=="snow-mountain")
        if variant=="ridge": peak(d,7,46,71,66,kind=="snow-mountain")
        else: rock(d,13,80,52,34)
    elif kind=="forest":
        if variant=="cluster":
            foliage(d,76,53,.83,True)
            foliage(d,47,75,.75,True)
        else:
            foliage(d,63,64,1.24,True)
    elif kind=="bush":
        foliage(d,43,78,.78,False,variant=="berries")
        foliage(d,78,57,.9,False,variant=="berries")
    elif kind=="river": river(d,variant)
    elif kind=="lake": lake(d,variant)
    elif kind=="bridge": bridge(d,variant,damage)
    elif kind=="ruin": ruins(d)
    elif kind=="town":
        house(d,42,43,.77)
        house(d,88,73,.74)
        house(d,49,91,.71)
    elif kind=="house": house(d,65,69,1.35)
    return im


def save(im,name):
    im.resize((SIZE,SIZE),Image.Resampling.LANCZOS).save(OUT/f"{name}.png",optimize=True)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for kind, variants in KINDS.items():
        for variant in variants:
            for damage in range(4 if kind in (*PALETTES,"bridge") else 1):
                suffix=f"-{damage}" if damage else ""
                save(tile(kind,variant,damage),f"{kind}-{variant}{suffix}")
    for prop in ("flowers","pebbles","grass-tuft"):
        im,d=blank()
        accent(d,prop,25,37)
        accent(d,prop,91,90)
        save(im,f"accent-{prop}")


if __name__=="__main__":
    main()
