"""Minimal NBT reader (gzipped, big-endian)."""
import gzip, struct, io
def read(path):
    data = open(path, 'rb').read()
    try: data = gzip.decompress(data)
    except OSError: pass
    r = io.BytesIO(data)
    t = r.read(1)[0]
    name = _str(r)
    return _payload(r, t)
def _str(r):
    n = struct.unpack('>H', r.read(2))[0]
    return r.read(n).decode('utf8', 'replace')
def _payload(r, t):
    if t == 1: return struct.unpack('>b', r.read(1))[0]
    if t == 2: return struct.unpack('>h', r.read(2))[0]
    if t == 3: return struct.unpack('>i', r.read(4))[0]
    if t == 4: return struct.unpack('>q', r.read(8))[0]
    if t == 5: return struct.unpack('>f', r.read(4))[0]
    if t == 6: return struct.unpack('>d', r.read(8))[0]
    if t == 7:
        n = struct.unpack('>i', r.read(4))[0]; return r.read(n)
    if t == 8: return _str(r)
    if t == 9:
        et = r.read(1)[0]; n = struct.unpack('>i', r.read(4))[0]
        return [_payload(r, et) for _ in range(n)]
    if t == 10:
        d = {}
        while True:
            tt = r.read(1)[0]
            if tt == 0: return d
            k = _str(r); d[k] = _payload(r, tt)
    if t == 11:
        n = struct.unpack('>i', r.read(4))[0]; return list(struct.unpack('>%di' % n, r.read(4 * n)))
    if t == 12:
        n = struct.unpack('>i', r.read(4))[0]; return list(struct.unpack('>%dq' % n, r.read(8 * n)))
    raise ValueError(t)
