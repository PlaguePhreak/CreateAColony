# Loads the entrance and level pieces the way BlueprintLibrary.LoadMinePiece does since 0.87.0 (shaft, tunnel starts off the rooms, level period).
import json,sys

import os
D=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..','..','assets','createacolony','config','blueprints','medievaloak','mineshaft')+'/'
NAT=("rock-","soil-","gravel-","sand-","ore-")
def nat(c): return c.startswith("game:") and c[5:].startswith(NAT)
FACES={'N':(0,-1),'S':(0,1),'W':(-1,0),'E':(1,0)}
def raw(name):
    d=json.load(open(D+name+'.json')); c={int(k):v for k,v in d["BlockCodes"].items()}
    return {(i&1023,(i>>20)&1023,(i>>10)&1023):c[b] for i,b in zip(d["Indices"],d["BlockIds"])},(d["SizeX"],d["SizeY"],d["SizeZ"])
def wp(v): return v is not None and 'waypoint' in v
def middle_opening(B,sx,sz,y):
    empty=lambda x,z: B.get((x,y,z)) is None
    best=None;bd=1e9
    for x in range(sx):
        for z in range(sz):
            d=(x-sx//2)**2+(z-sz//2)**2
            if d<bd and empty(x,z): best,bd=(x,z),d
    if not best: return None
    P={best};st=[best]
    while st:
        a=st.pop()
        for n in [(a[0]+1,a[1]),(a[0]-1,a[1]),(a[0],a[1]+1),(a[0],a[1]-1)]:
            if 0<=n[0]<sx and 0<=n[1]<sz and n not in P and empty(*n): P.add(n);st.append(n)
    return ((min(p[0] for p in P)+max(p[0] for p in P))//2,(min(p[1] for p in P)+max(p[1] for p in P))//2)
def piece(name):
    B,(sx,sy,sz)=raw(name); ent=name=='entrance'; lev=name=='level'
    ground=sy-1
    if ent:
        for y in range(sy-1,-1,-1):
            e=n=0
            for x in range(sx):
                for z in range(sz):
                    if x in(0,sx-1) or z in(0,sz-1): e+=1; n+=(x,y,z) in B and not wp(B[(x,y,z)])
            if n*2>=e: ground=y;break
    ax,ay,az=(sx//2,ground if ent else 0,sz//2)
    hole=middle_opening(B,sx,sz,ground) if ent else middle_opening(B,sx,sz,sy-1) if lev else None
    if lev: ax,ay,az=hole[0],sy-1,hole[1]
    cells={}
    for (x,y,z) in [(x,y,z) for x in range(sx) for y in range(sy) for z in range(sz)]:
        v=B.get((x,y,z))
        if wp(v): cells[(x-ax,y-ay,z-az)]='floor'; continue
        if v and nat(v): continue
        cells[(x-ax,y-ay,z-az)]= 'dig' if v is None else v
    if lev:
        for x in range(sx):
            for z in range(sz):
                v=B.get((x,sy-1,z))
                if v is None: cells[(x-ax,sy-ay,z-az)]='dig'
                elif 'ladder' in v: cells[(x-ax,sy-ay,z-az)]=v
    opens=[]
    mx,mz=(hole if hole else (ax,az))
    seen=set()
    for y in range(ground if ent else sy):
        for x in range(1,sx-1):
            for z in range(1,sz-1):
                if (x,y,z) in seen or not wp(B.get((x,y,z))): continue
                P=[];st=[(x,z)];seen.add((x,y,z))
                while st:
                    a=st.pop();P.append(a)
                    for n in [(a[0]+1,a[1]),(a[0]-1,a[1]),(a[0],a[1]+1),(a[0],a[1]-1)]:
                        if (n[0],y,n[1]) not in seen and wp(B.get((n[0],y,n[1]))): seen.add((n[0],y,n[1]));st.append(n)
                if len(P)<2 or any(p[0] in (0,sx-1) or p[1] in (0,sz-1) for p in P): continue
                x1=min(p[0] for p in P);x2=max(p[0] for p in P);z1=min(p[1] for p in P);z2=max(p[1] for p in P)
                cx=(x1+x2)/2-mx; cz=(z1+z2)/2-mz
                f=('W' if cx<0 else 'E') if abs(cx)>=abs(cz) else ('N' if cz<0 else 'S')
                ox=x1 if f=='W' else x2 if f=='E' else (x1+x2)//2
                oz=z1 if f=='N' else z2 if f=='S' else (z1+z2)//2
                under=all((lambda v: v is not None and not nat(v) and not wp(v))(B.get((p[0],y+1,p[1]))) for p in P)
                opens.append((f,ox-ax,(y+1 if under else y)-ay,oz-az))
    period= ground if ent else (-max(o[2] for o in opens)+1 if lev and opens else 0)
    shaft=(hole[0]-ax,hole[1]-az) if ent else (0,0)
    return dict(name=name,cells=cells,open=opens,period=period,shaft=shaft,min=(-ax,-ay,-az),max=(sx-1-ax,(sy if lev else sy-1)-ay,sz-1-az))
if __name__=="__main__":
    e=piece('entrance'); l=piece('level')
    print("entrance opens",e['open'],"shaft",e['shaft'],"period",e['period'])
    print("level opens",l['open'],"period",l['period'])
    floor=max(o[2] for o in e['open'])
    org=(e['shaft'][0],floor-1,e['shaft'][1])
    print("level1 origin rel entrance anchor",org)
