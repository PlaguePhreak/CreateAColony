# Grows a mineshaft from the shipped pieces the way the miner does (BlueprintLibrary.LoadMinePiece, MinePiece.Deeper, ColonyManager.GrowMineshaft).
import json, random, sys
from PIL import Image, ImageDraw, ImageFont
import os
D=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..','..','assets','createacolony','config','blueprints','medievaloak','mineshaft')+'/'
NAT=("rock-","soil-","gravel-","sand-","ore-")
def nat(c): return c.startswith("game:") and c[5:].startswith(NAT)
FACES={'N':(0,-1),'S':(0,1),'W':(-1,0),'E':(1,0)}
OPP={'N':'S','S':'N','E':'W','W':'E'}
def raw(name):
    d=json.load(open(D+name+'.json')); c={int(k):v for k,v in d["BlockCodes"].items()}
    B={(i&1023,(i>>20)&1023,(i>>10)&1023):c[b] for i,b in zip(d["Indices"],d["BlockIds"])}
    return B,(d["SizeX"],d["SizeY"],d["SizeZ"])
def rot(B,size,turns):
    sx,sy,sz=size
    for _ in range(turns):
        B={(sz-1-z,y,x):v for (x,y,z),v in B.items()}; sx,sz=sz,sx
    return B,(sx,sy,sz)
def piece(name,turns=0):
    B,(sx,sy,sz)=rot(*raw(name),turns)
    ent=name=="entrance"
    ground=sy-1
    if ent:
        for y in range(sy-1,-1,-1):
            e=n=0
            for x in range(sx):
                for z in range(sz):
                    if x in(0,sx-1) or z in(0,sz-1): e+=1; n+=(x,y,z) in B and nat(B[(x,y,z)])
            if n*2>=e: ground=y;break
    ax,ay,az=(sx//2,ground if ent else 0,sz//2)
    cells={}
    for x in range(sx):
        for y in range(sy):
            for z in range(sz):
                v=B.get((x,y,z))
                if v and ("waypoint" in v or nat(v)): continue
                cells[(x-ax,y-ay,z-az)]= 'dig' if v is None else v
    def edge(face,i):
        return (sx-1 if face=='E' else 0 if face=='W' else i, sz-1 if face=='S' else 0 if face=='N' else i)
    opens=[]
    def add(face,run,y):
        m=(run[0]+run[1])//2; x,z=edge(face,m); opens.append((face,x-ax,y-ay,z-az))
    for face in 'NESW':
        L=sz if face in 'EW' else sx
        if ent:
            for y in range(ground+1):
                st=-1
                for i in range(L+1):
                    x,z=edge(face,min(i,L-1)); mk=i<L and "waypoint" in B.get((x,y,z),"")
                    if mk and st<0: st=i
                    if mk or st<0: continue
                    if i-st>=2: add(face,(st,i-1),y)
                    st=-1
        else:
            best=None
            for y in range(min(4,sy)):
                st=-1
                for i in range(L+1):
                    x,z=edge(face,min(i,L-1)); em=i<L and (x,y,z) not in B
                    if em and st<0: st=i
                    if em or st<0: continue
                    if best is None or i-1-st>best[1]-best[0]: best=(st,i-1)
                    st=-1
            if best: add(face,best,0)
    return dict(name=name,cells=cells,open=opens,min=(-ax,-ay,-az),max=(sx-1-ax,sy-1-ay,sz-1-az),period=ground if ent else 0)
def deeper(p,k):
    cells=dict(p['cells']); band={c:v for c,v in p['cells'].items() if c[1]<=0}
    op=list(p['open'])
    for j in range(1,k+1):
        for (x,y,z),v in band.items(): cells[(x,y-j*p['period'],z)]=v
        op+= [(f,x,y-j*p['period'],z) for f,x,y,z in p['open']]
    q=dict(p); q.update(cells=cells,open=op,min=(p['min'][0],p['min'][1]-k*p['period'],p['min'][2])); return q
def box(p,o): return tuple(o[i]+p['min'][i] for i in range(3)),tuple(o[i]+p['max'][i] for i in range(3))
def inside(c,b): return all(b[0][i]<=c[i]<=b[1][i] for i in range(3))
def ov(a,b): return all(a[0][i]<=b[1][i] and a[1][i]>=b[0][i] for i in range(3))
def grow(seed,count,repeats=1):
    rnd=random.Random(seed)
    ent=deeper(piece('entrance'),repeats)
    placed=[(ent,(0,0,0))]
    lib={(n,t):piece(n,t) for n in ('tee','straight') for t in range(4)}
    while len(placed)-1<count:
        sides=[]
        for p,o in placed:
            for f,x,y,z in p['open']:
                out=(o[0]+x+FACES[f][0],o[1]+y,o[2]+z+FACES[f][1])
                if not any(inside(out,box(q,qo)) for q,qo in placed): sides.append((f,out))
        rnd.shuffle(sides); done=False
        for f,out in sides:
            ch=list(lib.items()); rnd.shuffle(ch)
            for key,n in ch:
                back=[o for o in n['open'] if o[0]==OPP[f]]
                if not back: continue
                b=back[0]; org=(out[0]-b[1],out[1]-b[2],out[2]-b[3]); bx=box(n,org)
                if bx[1][1]>=0: continue  # stays under the ground layer
                if any(ov(bx,box(q,qo)) for q,qo in placed): continue
                placed.append((n,org)); done=True; break
            if done: break
        if not done: break
    return placed
if __name__=="__main__":
    pl=grow(int(sys.argv[1]) if len(sys.argv)>1 else 1,12)
    for p,o in pl: print(p['name'],o,box(p,o))
