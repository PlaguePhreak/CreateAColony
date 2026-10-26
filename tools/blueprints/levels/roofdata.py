"""
Shape data for VS Roofing blocks on roofs that are drawn new.

VS Roofing works out a roof block's shape from its neighbours ("Auto" mode; a block is only fixed when
locked with the hammer, and none of Aaron's are). The saved data still has to be there and should be as
right as we can make it, so every new roof block gets the data of the block in Aaron's roofs whose
neighbourhood looks the same, turned to the new block's facing. The lookup is facing-relative: his
3,478 roof blocks are stored with their 5x3x5 neighbourhood rotated so the block faces north, and any
direction names inside the data (corner side, ridge ends) are rotated along with it.
"""
import os, sys, json, collections, pickle
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tree

DIRS = ['north', 'east', 'south', 'west']
DIRBYTES = {d.encode(): d for d in DIRS}
ROT_KEYS = ('CornerInfo', 'RidgeInfo', 'RidgeConnectInfo', 'RidgeRoofInfo', 'RoofInfo', 'Info')
SHAPE_KEYS = ('Type', 'CornerType', 'CornerInfo', 'SideEaveVariant', 'RoofInfo', 'RidgeInfo', 'RidgeConnectInfo', 'RidgeRoofInfo', 'RidgeShapeResolved')


# ---- a small protobuf reader/writer, enough to turn direction names inside the roof data ----
def _varint(b, i):
    n = 0; sh = 0
    while True:
        c = b[i]; i += 1; n |= (c & 0x7f) << sh; sh += 7
        if not c & 0x80: return n, i


def _wvarint(n):
    out = bytearray()
    while True:
        c = n & 0x7f; n >>= 7
        out.append(c | 0x80 if n else c)
        if not n: return bytes(out)


def pb_parse(b):
    """[(field, wiretype, value)]; a length-delimited value is ('dir', name) for a direction name,
    ('msg', [...]) when it reads as a message that writes back to the same bytes, else ('bytes', b)."""
    out = []; i = 0
    while i < len(b):
        key, i = _varint(b, i); f = key >> 3; wt = key & 7
        if wt == 0: v, i = _varint(b, i); out.append((f, 0, v))
        elif wt == 2:
            n, i = _varint(b, i); d = bytes(b[i:i + n]); i += n
            if d in DIRBYTES: out.append((f, 2, ('dir', DIRBYTES[d])))
            else:
                try:
                    m = pb_parse(d)
                    if pb_write(m) == d: out.append((f, 2, ('msg', m))); continue
                except Exception: pass
                out.append((f, 2, ('bytes', d)))
        elif wt == 5: out.append((f, 5, bytes(b[i:i + 4]))); i += 4
        elif wt == 1: out.append((f, 1, bytes(b[i:i + 8]))); i += 8
        else: raise ValueError(f'wire type {wt}')
    return out


def pb_write(m):
    out = bytearray()
    for f, wt, v in m:
        out += _wvarint((f << 3) | wt)
        if wt == 0: out += _wvarint(v)
        elif wt == 2:
            kind, val = v
            d = val.encode() if kind == 'dir' else pb_write(val) if kind == 'msg' else val
            out += _wvarint(len(d)); out += d
        else: out += v
    return bytes(out)


def pb_rotate(m, k):
    out = []
    for f, wt, v in m:
        if wt == 2:
            kind, val = v
            if kind == 'dir': v = ('dir', DIRS[(DIRS.index(val) + k) % 4])
            elif kind == 'msg': v = ('msg', pb_rotate(val, k))
        out.append((f, wt, v))
    return out


def rotate_tree(t, k):
    """The saved data of a roof block turned k quarter turns clockwise (seen from above)."""
    if k % 4 == 0: return dict(t)
    out = {}
    for key, (ty, val) in t.items():
        if ty == 'bytes' and key in ROT_KEYS:
            val = pb_write(pb_rotate(pb_parse(val), k))
        elif key == 'blockCode' and ty == 'str':
            d = val.split('-')[-1]
            if d in DIRS: val = val[:-len(d)] + DIRS[(DIRS.index(d) + k) % 4]
        out[key] = (ty, val)
    return out


# ---- neighbourhoods ----
R = ((-2, -1, 0, 1, 2), (-1, 0, 1), (-2, -1, 0, 1, 2))   # x, y, z offsets looked at
OFFS = [(dx, dy, dz) for dy in R[1] for dz in R[2] for dx in R[0] if (dx, dy, dz) != (0, 0, 0)]
NEAR = [(dx, dy, dz) for (dx, dy, dz) in OFFS if abs(dx) <= 1 and abs(dz) <= 1]
WEIGHT = {o: (4 if o in NEAR else 1) for o in OFFS}


def rot_off(dx, dz, k):
    for _ in range(k): dx, dz = -dz, dx
    return dx, dz


def facing_of(code): return code.split('-')[-1]


def signature(blocks, p, facing):
    """What stands round p, seen with the block turned to face north. Each cell is 'R'+facing letter for a
    roof block, '#' for anything solid, '.' for air (multiblock parts and plants count as air)."""
    k = (-DIRS.index(facing)) % 4
    x, y, z = p; sig = {}
    for (dx, dy, dz) in OFFS:
        c = blocks.get((x + dx, y + dy, z + dz), 'game:air')
        if c.startswith('vsroofing'): v = 'R' + DIRS[(DIRS.index(facing_of(c)) + k) % 4][0]
        elif c == 'game:air' or 'multiblock' in c or 'tallgrass' in c or 'flower' in c or 'fence' in c or 'wattle-sticks' in c: v = '.'
        else: v = '#'
        rx, rz = rot_off(dx, dz, k)
        sig[(rx, dy, rz)] = v
    return sig, k


def sig_key(sig): return tuple(sig[o] for o in OFFS)


# ---- the library of his roof blocks ----
LIB_PATH = os.path.join(HERE, 'roofdata.pkl')


def build_library(db_files):
    import kit2
    lib = collections.defaultdict(collections.Counter)   # (kind, sigkey) -> Counter(tree bytes)
    trees = {}
    for name, path in sorted(db_files.items()):
        b = kit2.Build.load(name)
        for p, c in b.blocks.items():
            if not c.startswith('vsroofing') or p not in b.ents: continue
            kind = c.rsplit('-', 1)[0]                     # vsroofing:roof-filled
            sig, k = signature(b.blocks, p, facing_of(c))
            t = rotate_tree(b.ents[p], k)                  # stored facing north
            t['blockCode'] = ('str', kind + '-north')
            enc = tree.encode(t)
            lib[(kind, sig_key(sig))][enc] += 1
            trees[enc] = t
    return dict(lib), trees


def library():
    if os.path.exists(LIB_PATH):
        return pickle.load(open(LIB_PATH, 'rb'))
    import kit2
    lib, trees = build_library(kit2.DB)
    pickle.dump((lib, trees), open(LIB_PATH, 'wb'))
    return lib, trees


_LIB = None
def _lib():
    global _LIB
    if _LIB is None: _LIB = library()
    return _LIB


_INDEX = None
def _index():
    """Signatures grouped by kind, with the near cells first for quick scoring."""
    global _INDEX
    if _INDEX is None:
        lib, trees = _lib(); _INDEX = collections.defaultdict(list)
        for (kind, key), cnt in lib.items():
            enc = cnt.most_common(1)[0][0]
            _INDEX[kind].append((key, trees[enc], sum(cnt.values())))
    return _INDEX


def best_data(blocks, p, code, want=None):
    """The saved data for a roof block at p: the data of his best-matching block, turned to p's facing.
    want: 'ridge', 'eave' or None, which prefers blocks of that Type. Returns (tree, score, exact)."""
    kind, facing = code.rsplit('-', 1)
    sig, k = signature(blocks, p, facing)
    key = sig_key(sig)
    best = None
    for okey, t, n in _index().get(kind, []):
        score = 0
        for o, a, b in zip(OFFS, key, okey):
            if a != b: score += WEIGHT[o] * (1 if (a == '.' or b == '.') and (a == '#' or b == '#') else 2)
        ty = t.get('Type', ('int', 0))[1]
        if want == 'ridge' and ty != 2: score += 3
        if want == 'eave' and ty != 1: score += 3
        if want is None and ty in (1, 2): score += 1
        if best is None or score < best[0] or (score == best[0] and n > best[2]): best = (score, t, n)
    if best is None: return None, 999, False
    t = rotate_tree(best[1], (-k) % 4)
    t['blockCode'] = ('str', code)
    return t, best[0], best[0] == 0


def attach(build, wants=None):
    """Gives every roof block in the build that has no data (or whose data is marked stale) its best data.
    wants: {pos: 'ridge'|'eave'}. Returns (count, exact matches)."""
    wants = wants or {}
    n = ex = 0
    for p, c in list(build.blocks.items()):
        if not c.startswith('vsroofing'): continue
        if p in build.ents and build.ents[p].get('_new') is None: continue
        t, score, exact = best_data(build.blocks, p, c, wants.get(p))
        if t is None: continue
        build.ents[p] = t; n += 1; ex += exact
    return n, ex


if __name__ == '__main__':
    import kit2
    # round trip of the protobuf reader on every roof block, and the rotation being its own inverse
    bad = 0; total = 0
    for name in sorted(kit2.DB):
        b = kit2.Build.load(name)
        for p, c in b.blocks.items():
            if not c.startswith('vsroofing') or p not in b.ents: continue
            t = b.ents[p]; total += 1
            for key, (ty, val) in t.items():
                if ty == 'bytes' and key in ROT_KEYS and pb_write(pb_parse(val)) != val: bad += 1; print('no round trip', name, p, key)
            back = rotate_tree(rotate_tree(t, 1), 3)
            if tree.encode(back) != tree.encode(t): bad += 1; print('rotation not undone', name, p)
    print(total, 'roof blocks checked,', bad, 'bad')
    # leave-one-building-out: how often does the lookup give exactly his data?
    hits = misses = 0; shape_hits = 0
    for name in sorted(kit2.DB):
        others = {n: p for n, p in kit2.DB.items() if n != name}
        _LIB = build_library(others); _INDEX = None
        b = kit2.Build.load(name)
        for p, c in b.blocks.items():
            if not c.startswith('vsroofing') or p not in b.ents: continue
            t, score, exact = best_data(b.blocks, p, c)
            if t is None: misses += 1; continue
            same = all(t.get(k) == b.ents[p].get(k) for k in SHAPE_KEYS)
            hits += same; misses += not same
            shape_hits += all(t.get(k) == b.ents[p].get(k) for k in ('Type', 'CornerType', 'RidgeShapeResolved'))
        print(f'{name:28s} running: {hits} same, {misses} different, {shape_hits} same type')
