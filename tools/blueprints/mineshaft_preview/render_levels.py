# Draws each level of a mine grown from the entrance, level and section pieces (ColonyManager.MineshaftPieces, GrowMineshaft).
# Run: python3 render_levels.py <seed> <level pieces> <sections> <out.png>
import sys,random
sys.path.insert(0,__import__('os').path.dirname(__import__('os').path.abspath(__file__)))
import levels as sim, mine
from PIL import Image, ImageDraw, ImageFont
OPP={'N':'S','S':'N','E':'W','W':'E'}
def rotp(name,t):
    if name in('entrance','level'): return sim.piece(name)
    p=mine.piece(name,t); return p
def at(o,c): return (o[0]+c[0],o[1]+c[1],o[2]+c[2])
def box(p,o): return tuple(o[i]+p['min'][i] for i in range(3)),tuple(o[i]+p['max'][i] for i in range(3))
def grow(seed,levels,count):
    rnd=random.Random(seed)
    e=sim.piece('entrance'); l=sim.piece('level')
    placed=[(e,(0,0,0))]
    floor=max(o[2] for o in e['open'])
    for k in range(levels):
        placed.append((l,(e['shaft'][0],floor-1-k*l['period'],e['shaft'][1])))
    hub=set(at(o,c) for p,o in placed for c in p['cells'])
    lib={(n,t):mine.piece(n,t) for n in ('tee','straight') for t in range(4)}
    secs=[]
    def lvl(y): return max(1,min(1+levels,round((floor-y)/l['period'])+1))
    stats=[0,0]
    while len(secs)<count:
        sides=[]
        for p,o in placed+secs:
            for f,x,y,z in p['open']:
                out=(o[0]+x+sim.FACES[f][0],o[1]+y,o[2]+z+sim.FACES[f][1])
                if out in hub or any(all(box(q,qo)[0][i]<=out[i]<=box(q,qo)[1][i] for i in range(3)) for q,qo in secs): continue
                sides.append((f,out))
        rnd.shuffle(sides); done=False
        for f,out in sides:
            ch=list(lib.items()); rnd.shuffle(ch)
            for key,n in ch:
                back=[o for o in n['open'] if o[0]==OPP[f]]
                if not back: continue
                b=back[0]; org=(out[0]-b[1],out[1]-b[2],out[2]-b[3]); bx=box(n,org)
                if bx[1][1]>=0: stats[0]+=1; continue
                if any(all(bx[0][i]<=box(q,qo)[1][i] and bx[1][i]>=box(q,qo)[0][i] for i in range(3)) for q,qo in secs) or any(at(org,c) in hub for c in n['cells']): stats[1]+=1; continue
                secs.append((n,org)); done=True; break
            if done: break
        if not done: break
    return placed,secs,lvl,floor,l['period'],stats
if __name__=="__main__":
    placed,secs,lvl,floor,P,stats=grow(int(sys.argv[1]),int(sys.argv[2]),int(sys.argv[3]))
    from collections import Counter
    print("sections",len(secs),"by level",Counter(lvl(o[1]+n['open'][0][2]) for n,o in secs),"rejects",stats)
    # render top view per level
    allp=placed+secs
    xs=[c[0]+o[0] for p,o in allp for c in p['cells']]; zs=[c[2]+o[2] for p,o in allp for c in p['cells']]
    x0,x1,z0,z1=min(xs),max(xs),min(zs),max(zs); S=8
    nl=len(placed)
    W=(x1-x0+1)*S; H=(z1-z0+1)*S
    FT=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',18)
    img=Image.new("RGB",(nl*(W+20)+20,H+60),(245,242,235)); d=ImageDraw.Draw(img)
    for li in range(nl):
        fy=floor-li*P
        ox=20+li*(W+20); oy=40
        d.rectangle([ox,oy,ox+W,oy+H],fill=(92,92,98)); d.text((ox,10),("Level 0 (entrance)" if li==0 else "Level %d"%li),fill=(0,0,0),font=FT)
        for p,o in allp:
            for (x,y,z),v in p['cells'].items():
                wy=o[1]+y
                if wy not in (fy+1,): continue
                X=ox+(o[0]+x-x0)*S; Z=oy+(o[2]+z-z0)*S
                c=(30,30,34) if v=='dig' else (196,150,92) if 'plank' in v else (110,72,40) if ('log' in v or 'fence' in v or 'beam' in v) else (255,214,80) if 'lantern' in v else (60,160,60) if 'ladder' in v else (170,120,70)
                d.rectangle([X,Z,X+S-1,Z+S-1],fill=c)
        for p,o in secs:
            if lvl(o[1]+p['open'][0][2])==li+1:
                b=box(p,o); d.rectangle([ox+(b[0][0]-x0)*S,oy+(b[0][2]-z0)*S,ox+(b[1][0]-x0+1)*S,oy+(b[1][2]-z0+1)*S],outline=(220,60,60))
    img.save(sys.argv[4])
