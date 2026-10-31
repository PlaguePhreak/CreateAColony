# Draws a possible mineshaft from the shipped pieces, growing it the way the miner does (ColonyManager.GrowMineshaft).
# Run: python3 render.py <seed> <out.png>
import sys, mine
from PIL import Image, ImageDraw, ImageFont
seed=int(sys.argv[1]); out=sys.argv[2]
pl=mine.grow(seed,12)
S=13
try:
    F=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",15); FB=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",20); FS=ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",12)
except: F=FB=FS=ImageFont.load_default()
xs=[c for p,o in pl for c in (mine.box(p,o)[0][0],mine.box(p,o)[1][0])]; zs=[c for p,o in pl for c in (mine.box(p,o)[0][2],mine.box(p,o)[1][2])]
x0,x1,z0,z1=min(xs)-1,max(xs)+1,min(zs)-1,max(zs)+1
W=(x1-x0+1)*S; H=(z1-z0+1)*S
ROCK=(92,92,98); PLANK=(196,150,92); WALK=(226,205,160); LOG=(110,72,40); BEAM=(140,95,50); LAMP=(255,214,80); STAIR=(170,120,70); OPEN=(30,30,34); SOIL=(120,96,64)
def col(v):
    if v=='dig': return None
    if 'lantern' in v: return LAMP
    if 'supportbeam' in v: return BEAM
    if 'log' in v: return LOG
    if 'stairs' in v or 'slab' in v: return STAIR
    if 'plank' in v: return PLANK
    if 'fence' in v: return LOG
    return (150,150,150)
levels=[(-15,-1,"Upper level (tunnels off the stair landings)"),(-22,-15,"Lower level (off the bottom landing)")]
def level_of(p,o):
    fl=mine.box(p,o)[0][1]; return 0 if fl>=-15 else 1
pad=40; top=70; gap=40
img=Image.new("RGB",(pad*2+W*2+gap, top+H+70+600),(245,242,235)); d=ImageDraw.Draw(img)
d.text((pad,18),"Miner's mineshaft with the new entrance: %d sections (T and straight)"%(len(pl)-1),fill=(30,30,30),font=FB)
for li,(lo,hi,title) in enumerate(levels):
    ox=pad+li*(W+gap); oy=top
    d.rectangle([ox,oy,ox+W-1,oy+H-1],fill=ROCK)
    d.text((ox,oy-22),title,fill=(30,30,30),font=F)
    n=1
    for idx,(p,o) in enumerate(pl):
        ent=p['name']=='entrance'
        if not ent and level_of(p,o)!=li: continue
        b=mine.box(p,o); fl=b[0][1] if not ent else lo
        cols={}
        for (x,y,z),v in p['cells'].items():
            Y=o[1]+y
            if ent:
                if not (lo<Y<=hi): continue
            else:
                if not (fl<=Y<=fl+3): continue
            key=(o[0]+x,o[2]+z); cols.setdefault(key,[]).append((Y,v))
        for (X,Z),lst in cols.items():
            lst.sort(reverse=True)
            if ent:
                c=None
                for Y,v in lst:
                    if v!='dig': c=col(v); break
                if c is None: c=OPEN
            else:
                walk=[v for Y,v in lst if Y>fl]
                fls=[v for Y,v in lst if Y==fl]
                deco=[col(v) for v in walk if v!='dig' and col(v) in (LAMP,BEAM,LOG)]
                if LAMP in deco: c=LAMP
                elif deco and 'dig' in walk: c=deco[0]
                elif 'dig' in walk: c=PLANK if fls and fls[0]!='dig' and 'plank' in fls[0] else WALK
                elif deco: c=deco[0]
                else: continue
            px=ox+(X-x0)*S; pz=oy+(Z-z0)*S
            d.rectangle([px,pz,px+S-1,pz+S-1],fill=c)
        bx0=ox+(b[0][0]-x0)*S; bz0=oy+(b[0][2]-z0)*S; bx1=ox+(b[1][0]-x0+1)*S-1; bz1=oy+(b[1][2]-z0+1)*S-1
        d.rectangle([bx0,bz0,bx1,bz1],outline=(255,255,255) if not ent else (255,120,60),width=2)
        lab="Entrance" if ent else ("T%d" if p['name']=='tee' else "S%d")%idx
        tw=d.textlength(lab,font=FS)
        d.rectangle([bx0+3,bz0+3,bx0+7+tw,bz0+18],fill=(0,0,0)); d.text((bx0+5,bz0+4),lab,fill=(255,255,255),font=FS)
    d.text((ox+W-14,oy+4),"N",fill=(255,255,255),font=F); d.line([ox+W-9,oy+22,ox+W-9,oy+40],fill=(255,255,255),width=2)
# side view
sy=top+H+60
d.text((pad,sy-24),"Side view (looking north): the stairs wind down the entrance; tunnels leave at each landing",fill=(30,30,30),font=F)
ymin=min(mine.box(p,o)[0][1] for p,o in pl)-1
SS=11
gy0=sy+ (1-ymin)*0
def gy(Y): return sy+(2-Y)*SS
d.rectangle([pad,gy(1),pad+(x1-x0+1)*SS*2,gy(ymin)],fill=ROCK)
d.rectangle([pad,gy(1)-SS*2,pad+(x1-x0+1)*SS*2,gy(1)],fill=(180,215,240))
d.rectangle([pad,gy(0)-SS,pad+(x1-x0+1)*SS*2,gy(0)],fill=(96,140,70))
for idx,(p,o) in enumerate(pl):
    b=mine.box(p,o); ent=p['name']=='entrance'
    c=(255,120,60) if ent else [(226,205,160),(200,180,230)][level_of(p,o)]
    X0=pad+(b[0][0]-x0)*SS*2; X1=pad+(b[1][0]-x0+1)*SS*2
    if ent:
        d.rectangle([X0,gy(b[1][1]),X1,gy(b[0][1]-1)],fill=c,outline=(0,0,0))
    else:
        d.rectangle([X0,gy(b[0][1]+3),X1,gy(b[0][1])],fill=c,outline=(60,60,60))
d.text((pad,gy(ymin)+6),"Entrance (orange)   Upper level tunnels (tan)   Lower level tunnels (lilac)",fill=(60,60,60),font=FS)
# legend
lx=pad+(x1-x0+1)*SS*2+30; ly=sy
for c,t in [(PLANK,"plank floor"),(WALK,"dug-out way"),(BEAM,"support beam"),(LOG,"log post"),(LAMP,"lantern"),(STAIR,"stairs"),(OPEN,"open shaft"),(ROCK,"rock left alone")]:
    d.rectangle([lx,ly,lx+14,ly+14],fill=c,outline=(0,0,0)); d.text((lx+22,ly-1),t,fill=(30,30,30),font=F); ly+=22
img=img.crop((0,0,img.width,max(ly,gy(ymin)+30)+10))
img.save(out); print(img.size)
