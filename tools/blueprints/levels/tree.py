"""Vintage Story TreeAttribute binary decode/encode (enough for roof and door block entities)."""
import struct, base64, io
def rstr(b):
    n=0; sh=0
    while True:
        c=b.read(1)[0]; n|=(c&0x7f)<<sh; sh+=7
        if not c&0x80: break
    return b.read(n).decode('utf8')
def wstr(o,s):
    d=s.encode('utf8'); n=len(d)
    while True:
        c=n&0x7f; n>>=7
        o.write(bytes([c|0x80 if n else c]))
        if not n: break
    o.write(d)
def decode(b):
    t={}
    while True:
        id=b.read(1)
        if not id or id[0]==0: break
        id=id[0]; k=rstr(b)
        if id==1: v=('int',struct.unpack('<i',b.read(4))[0])
        elif id==2: v=('long',struct.unpack('<q',b.read(8))[0])
        elif id==3: v=('double',struct.unpack('<d',b.read(8))[0])
        elif id==4: v=('float',struct.unpack('<f',b.read(4))[0])
        elif id==5: v=('str',rstr(b))
        elif id==6: v=('tree',decode(b))
        elif id==8: n=struct.unpack('<H',b.read(2))[0]; v=('bytes',b.read(n))
        elif id==7:
            isnull=b.read(1)[0]
            if isnull: v=('stack',None)
            else: cls,iid,size=struct.unpack('<iii',b.read(12)); v=('stack',(cls,iid,size,decode(b)))
        elif id==9: v=('bool',b.read(1)[0]!=0)
        elif id==11: n=struct.unpack('<i',b.read(4))[0]; v=('ints',list(struct.unpack('<%di'%n,b.read(4*n))))
        elif id==10: n=struct.unpack('<i',b.read(4))[0]; v=('strs',[rstr(b) for _ in range(n)])
        else: raise ValueError(f'attr id {id} key {k}')
        t[k]=v
    return t
def encode(t,o=None):
    top=o is None
    if top: o=io.BytesIO()
    for k,(ty,v) in t.items():
        id={'int':1,'long':2,'double':3,'float':4,'str':5,'tree':6,'bytes':8,'bool':9,'strs':10,'ints':11,'stack':7}[ty]
        o.write(bytes([id])); wstr(o,k)
        if ty=='int': o.write(struct.pack('<i',v))
        elif ty=='long': o.write(struct.pack('<q',v))
        elif ty=='double': o.write(struct.pack('<d',v))
        elif ty=='float': o.write(struct.pack('<f',v))
        elif ty=='str': wstr(o,v)
        elif ty=='tree': encode(v,o)
        elif ty=='bytes': o.write(struct.pack('<H',len(v))); o.write(v)
        elif ty=='stack':
            if v is None: o.write(b'\x01')
            else: o.write(b'\x00'); o.write(struct.pack('<iii',*v[:3])); encode(v[3],o)
        elif ty=='bool': o.write(bytes([1 if v else 0]))
        elif ty=='ints': o.write(struct.pack('<i',len(v))); o.write(struct.pack('<%di'%len(v),*v))
        elif ty=='strs': o.write(struct.pack('<i',len(v))); [wstr(o,s) for s in v]
    o.write(b'\0')
    if top: return o.getvalue()
def from_a85(s): return decode(io.BytesIO(base64.a85decode(s.encode('latin1'))))
def to_a85(t): return base64.a85encode(encode(t)).decode('latin1')
