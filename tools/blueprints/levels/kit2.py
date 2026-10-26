"""
Makes new Dev's Builds levels out of Aaron's own exports.

A Build is the blocks of one export (code per spot, plus the saved details of roof blocks, doors and
furniture). Every new level starts from one of his files and is changed by the operations below: swap
materials, stretch along a wall, dig the cellar deeper, paste part of another export, put in furniture
copied from his files. Roof blocks are never made up: they are only ever copied, with their saved
shape data, from a spot in his roofs whose neighbours look the same.
"""
import json, os, re, sys, collections, copy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tree

# His exports as they sit in the Dev's Builds pack: 'builder_level4', 'residence_small_level1', ...
ASSETS = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'assets', 'createacolony', 'config', 'blueprints', 'devsbuilds'))
HIS = {'townhall': 4, 'builder': 4, 'guardtower': 5, 'cook': 2, 'residence/small': 1, 'residence/medium': 1, 'smelter': 1,
       'blacksmith': 1, 'sawmill': 2, 'gatherer': 1, 'forester/small': 1, 'forester/large': 1}
DB_FILES = {}
for _t, _n in HIS.items():
    for _l in range(1, _n + 1):
        DB_FILES[_t.replace('/', '_') + f'_level{_l}'] = os.path.join(ASSETS, _t, f'level{_l}.json')
DB = DB_FILES


def pos_of(i): return i & 1023, (i >> 20) & 1023, (i >> 10) & 1023
def index_of(x, y, z): return (y << 20) | (z << 10) | x


def norm(t):
    t = dict(t)
    for k in ('posx', 'posy', 'posz'): t.pop(k, None)
    return t


# ---- the world's block and item numbers, shared by all his exports (same world) ----
BLOCK_IDS = {}   # code -> id
ITEM_CODES = {}  # id -> code
GAME_VERSION = '1.22.7'
for _f in sorted(DB):
    _bp = json.load(open(DB[_f]))
    for k, v in _bp['BlockCodes'].items(): BLOCK_IDS.setdefault(v, int(k))
    for k, v in _bp['ItemCodes'].items(): ITEM_CODES[int(k)] = v
_next_id = [max(BLOCK_IDS.values()) + 1]


def block_id(code):
    if code not in BLOCK_IDS:
        BLOCK_IDS[code] = _next_id[0]; _next_id[0] += 1
    return BLOCK_IDS[code]


def off_of(code):
    m = re.search(r'monolithic-(n?p?\d)-(n?p?\d)-(n?p?\d)', code)
    return tuple(int(s.replace('p', '').replace('n', '-')) for s in m.groups())


class Build:
    def __init__(self):
        self.blocks = {}   # (x,y,z) -> code
        self.ents = {}     # (x,y,z) -> tree (without pos)
        self.name = ''

    @staticmethod
    def load(name):
        b = Build(); b.name = name
        bp = json.load(open(DB[name]))
        codes = bp['BlockCodes']
        for i, k in zip(bp['Indices'], bp['BlockIds']):
            c = codes[str(k)]
            if c == 'game:air': continue
            b.blocks[pos_of(i)] = c
        for k, v in bp['BlockEntities'].items():
            p = pos_of(int(k))
            if p in b.blocks: b.ents[p] = norm(tree.from_a85(v))
        return b

    def copy(self):
        b = Build(); b.name = self.name
        b.blocks = dict(self.blocks); b.ents = copy.deepcopy(self.ents)
        return b

    # ---- looking ----
    def get(self, p): return self.blocks.get(tuple(p), 'game:air')
    def hut(self):
        return next(p for p, c in self.blocks.items() if c.startswith('createacolony:') and not c.startswith('createacolony:waypoint'))
    def bounds(self):
        xs = [p[0] for p in self.blocks]; ys = [p[1] for p in self.blocks]; zs = [p[2] for p in self.blocks]
        return min(xs), min(ys), min(zs), max(xs), max(ys), max(zs)
    def find(self, part):
        return sorted(p for p, c in self.blocks.items() if part in c)
    def count(self, part): return len(self.find(part))

    # ---- changing one spot ----
    def set(self, p, code, ent=None):
        p = tuple(p)
        if code in (None, 'game:air'):
            self.blocks.pop(p, None); self.ents.pop(p, None); return
        self.blocks[p] = code
        if ent is not None: self.ents[p] = copy.deepcopy(ent)
        else: self.ents.pop(p, None)
    def remove(self, p): self.set(p, None)

    def place(self, code, p, parts=True):
        """A block with its saved details and multiblock parts, as in his exports (see CATALOG)."""
        p = tuple(p)
        ent, part_list = CATALOG.get(code, (None, []))
        self.set(p, code, ent)
        if parts:
            for off, pcode in part_list:
                self.set((p[0] + off[0], p[1] + off[1], p[2] + off[2]), pcode)

    def door(self, p, kind, facing):
        """A single door (kind 'crude' or 'solid-oak') in a wall that runs along x (facing 'north'/'south')
        or along z ('east'/'west'), with its top half."""
        code = 'game:door-' + kind
        ent = DOORS[facing]
        self.set(p, code, ent)
        self.set((p[0], p[1] + 1, p[2]), 'game:multiblock-monolithic-0-p1-0')

    # ---- changing areas ----
    def fill(self, box, code, ent=None):
        x0, y0, z0, x1, y1, z1 = box
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1): self.set((x, y, z), code, ent)
    def clear(self, box): self.fill(box, None)
    def walls(self, box, code):
        """The four sides of a box (not top or bottom)."""
        x0, y0, z0, x1, y1, z1 = box
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    if x in (x0, x1) or z in (z0, z1): self.set((x, y, z), code)
    def posts(self, box, code='game:log-placed-oak-ud'):
        x0, y0, z0, x1, y1, z1 = box
        for x in (x0, x1):
            for z in (z0, z1):
                for y in range(y0, y1 + 1): self.set((x, y, z), code)

    def swap(self, fn, box=None):
        """fn(code, pos) -> new code, or None to keep. Saved details are kept when the block keeps its kind."""
        for p, c in list(self.blocks.items()):
            if box and not inside(p, box): continue
            n = fn(c, p)
            if n and n != c:
                keep = self.ents.get(p) if same_kind(c, n) else None
                if keep is None and n in CATALOG: keep = CATALOG[n][0]
                self.set(p, n, keep)

    def swap_codes(self, mapping, box=None):
        """mapping: old code (or code start) -> new code; a start keeps the rest of the old code
        ('game:torch-basic-lit' -> 'game:lantern-small' turns torch-basic-lit-east into lantern-small-east)."""
        items = sorted(mapping.items(), key=lambda kv: -len(kv[0]))
        def fn(c, p):
            for old, new in items:
                if c == old: return new
                if c.startswith(old + '-'): return new + c[len(old):]
            return None
        self.swap(fn, box)

    def translate(self, d):
        dx, dy, dz = d
        self.blocks = {(x + dx, y + dy, z + dz): c for (x, y, z), c in self.blocks.items()}
        self.ents = {(x + dx, y + dy, z + dz): e for (x, y, z), e in self.ents.items()}

    def paste(self, other, at=(0, 0, 0), box=None, skip=('game:soil',), over=True):
        """Copies other's blocks (those inside box, in other's coordinates) shifted by `at`."""
        dx, dy, dz = at
        for p, c in other.blocks.items():
            if box and not inside(p, box): continue
            if c.startswith(skip): continue
            q = (p[0] + dx, p[1] + dy, p[2] + dz)
            if not over and q in self.blocks: continue
            self.set(q, c, other.ents.get(p))

    def stretch(self, axis, at, n):
        """Makes the build n longer along axis (0 = x, 1 = z) by repeating the slice at `at` n times;
        everything beyond moves along. The roof slice there must match the next one (see roof_regular).
        A multiblock part follows its main block."""
        assert n > 0
        a = 0 if axis == 0 else 2
        def moved(p):
            q = list(p)
            if p[a] > at: q[a] += n
            return tuple(q)
        nb, ne = {}, {}
        for p, c in self.blocks.items():
            if c.startswith('game:multiblock'):
                o = off_of(c); m = (p[0] - o[0], p[1] - o[1], p[2] - o[2]); mm = moved(m)
                q = (mm[0] + o[0], mm[1] + o[1], mm[2] + o[2])
            else:
                q = moved(p)
            nb[q] = c
            if p in self.ents: ne[q] = self.ents[p]
            if p[a] == at and not no_repeat(c):
                for k in range(1, n + 1):
                    q = list(p); q[a] = at + k
                    nb[tuple(q)] = c
                    if p in self.ents: ne[tuple(q)] = copy.deepcopy(self.ents[p])
        self.blocks, self.ents = nb, ne

    def shrink(self, axis, a, b):
        """Removes the slices a..b along axis; everything beyond moves back."""
        ax = 0 if axis == 0 else 2
        n = b - a + 1
        nb, ne = {}, {}
        for p, c in self.blocks.items():
            if a <= p[ax] <= b: continue
            q = list(p)
            if p[ax] > b: q[ax] -= n
            nb[tuple(q)] = c
            if p in self.ents: ne[tuple(q)] = self.ents[p]
        self.blocks, self.ents = nb, ne

    def roof_regular(self, axis):
        """Where the roof repeats: the list of positions `at` where slice at and at+1 have the same roof shape."""
        ax = 0 if axis == 0 else 2
        roof = {p: (c, roof_shape(self.ents.get(p, {}))) for p, c in self.blocks.items() if c.startswith('vsroofing')}
        if not roof: return []
        def sl(i): return {(p[1], p[2 - ax]): v for p, v in roof.items() if p[ax] == i}
        lo = min(p[ax] for p in roof); hi = max(p[ax] for p in roof)
        return [i for i in range(lo, hi) if sl(i) == sl(i + 1)]

    def raise_floor(self, from_y, n):
        """Lifts everything at y >= from_y by n, and repeats the layer from_y-1.. wait: repeats layers
        from_y-? no: callers fill the gap themselves."""
        nb, ne = {}, {}
        for p, c in self.blocks.items():
            q = (p[0], p[1] + n, p[2]) if p[1] >= from_y else p
            nb[q] = c
            if p in self.ents: ne[q] = self.ents[p]
        self.blocks, self.ents = nb, ne

    def repeat_layer(self, y, n):
        """Makes the build n taller by repeating layer y (walls, posts, ladders), lifting all above it."""
        self.raise_floor(y + 1, n)
        for p, c in list(self.blocks.items()):
            if p[1] == y and not no_repeat(c) and c not in CATALOG:
                for k in range(1, n + 1):
                    self.set((p[0], p[1] + k, p[2]), c, self.ents.get(p))

    def ground(self, box, y, code='game:soil-medium-normal'):
        """Natural ground over the plot where nothing else is."""
        x0, z0, x1, z1 = box
        for x in range(x0, x1 + 1):
            for z in range(z0, z1 + 1):
                if (x, y, z) not in self.blocks: self.set((x, y, z), code)

    # ---- out ----
    def save(self, path, type):
        x0, y0, z0, x1, y1, z1 = self.bounds()
        b = self.copy(); b.translate((-x0, -y0, -z0))
        codes = {}; ids = []; idx = []
        for p in sorted(b.blocks, key=lambda p: (p[1], p[2], p[0])):
            c = b.blocks[p]; i = block_id(c); codes[str(i)] = c
            ids.append(i); idx.append(index_of(*p))
        ents = {}
        for p, t in b.ents.items():
            t = dict(t)
            full = {'posx': ('int', p[0]), 'posy': ('int', p[1]), 'posz': ('int', p[2])}
            full.update(t)
            ents[str(index_of(*p))] = tree.to_a85(full)
        bp = {'GameVersion': GAME_VERSION, 'SizeX': x1 - x0 + 1, 'SizeY': y1 - y0 + 1, 'SizeZ': z1 - z0 + 1,
              'BlockCodes': codes, 'ItemCodes': {str(k): v for k, v in sorted(ITEM_CODES.items())},
              'Indices': idx, 'BlockIds': ids, 'DecorIndices': [], 'DecorIds': [], 'BlockEntities': ents,
              'Entities': [], 'ReplaceMode': 2, 'EntranceRotation': -1,
              'OriginalPos': {'X': 0, 'Z': 0, 'InternalY': 0}}
        problems = b.check(type)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        json.dump(bp, open(path, 'w'), separators=(',', ':'))
        return problems

    def check(self, type):
        """The import script's checks."""
        hut_code = hut_code_of(type)
        at = self.blocks; problems = []
        huts = [p for p, c in at.items() if c == hut_code]
        others = sorted({c for c in at.values() if c.startswith('createacolony:') and c != hut_code and not c.startswith('createacolony:waypoint')})
        if len(huts) != 1: problems.append(f'needs exactly one {hut_code}, has {len(huts)}')
        if others: problems.append(f'other colony blocks in it: {others}')
        for (x, y, z), c in at.items():
            if c.startswith('game:door-') and 'trapdoor' not in c:
                if (x, y, z) not in self.ents: problems.append(f'door at {(x, y, z)} has no saved direction')
                if not at.get((x, y + 1, z), '').startswith('game:multiblock-'): problems.append(f'door at {(x, y, z)} has no top half')
            if c.startswith('vsroofing:') and (x, y, z) not in self.ents: problems.append(f'roof block at {(x, y, z)} has no saved roof data')
            if c.startswith('game:multiblock-'):
                o = off_of(c); m = (x - o[0], y - o[1], z - o[2])
                if m not in at or at[m].startswith('game:multiblock-'): problems.append(f'multiblock part at {(x, y, z)} has no main block')
        if len(huts) == 1:
            hx, hy, hz = huts[0]
            if (hx, hy - 1, hz) not in at: problems.append('nothing under the hut block')
            above = at.get((hx, hy + 1, hz), '')
            if above and not any(k in above for k in ('lantern', 'torch')): problems.append(f'the hut block is covered by {above}')
        # furniture and fittings must keep the saved details his exports had
        for p, c in at.items():
            if c in CATALOG and CATALOG[c][0] is not None and p not in self.ents: problems.append(f'{c} at {p} has no saved details')
        return problems


def hut_code_of(type):
    return {'townhall': 'createacolony:townhall', 'warehouse': 'createacolony:warehouse'}.get(type, f'createacolony:hut-{type}')


def no_repeat(code):
    """Blocks a stretched slice must not copy: the hut block, doors, furniture with parts and the parts."""
    if code.startswith('createacolony:') or code.startswith('game:multiblock') or (code.startswith('game:door-') and 'trapdoor' not in code): return True
    return code in CATALOG and bool(CATALOG[code][1])


def inside(p, box):
    x0, y0, z0, x1, y1, z1 = box
    return x0 <= p[0] <= x1 and y0 <= p[1] <= y1 and z0 <= p[2] <= z1


def same_kind(a, b):
    """Two codes that take the same saved details: the same block in another material or another facing."""
    ka = re.sub(r'-(oak|crude|solid-oak|shale|slate|light|dark|tan)', '', a)
    kb = re.sub(r'-(oak|crude|solid-oak|shale|slate|light|dark|tan)', '', b)
    if a.startswith('game:door') and b.startswith('game:door'): return True
    if 'torch' in a and 'lantern' in b: return False
    return ka.split('-')[0] == kb.split('-')[0]


ROOF_SHAPE = ('Type', 'SideEaveVariant', 'RoofInfo', 'CornerInfo', 'CornerType', 'RidgeInfo', 'RidgeConnectInfo', 'RidgeRoofInfo', 'RidgeShapeResolved')
def roof_shape(t): return tuple((k, t[k][1] if k in t else None) for k in ROOF_SHAPE)


# ---- the catalogue of his fittings: code -> (saved details, [(offset, part code)]) ----
def _build_catalog():
    cat = {}; doors = {}
    for f in sorted(DB):
        b = Build.load(f)
        parts = collections.defaultdict(set)
        for p, c in b.blocks.items():
            if 'multiblock' in c:
                o = off_of(c); m = (p[0] - o[0], p[1] - o[1], p[2] - o[2])
                if m in b.blocks and 'multiblock' not in b.blocks[m]: parts[m].add((o, c))
        for p, c in b.blocks.items():
            if c.startswith('vsroofing'): continue
            if c.startswith('game:door-') and 'trapdoor' not in c:
                t = b.ents.get(p)
                if t and 'leftDoorPosX' not in t and 'rightDoorPosX' not in t:
                    # which wall is it in: blocks beside it along x mean the wall runs along x
                    along_x = any(b.get((p[0] + d, p[1], p[2])) not in ('game:air',) and 'door' not in b.get((p[0] + d, p[1], p[2])) for d in (-1, 1))
                    along_z = any(b.get((p[0], p[1], p[2] + d)) not in ('game:air',) and 'door' not in b.get((p[0], p[1], p[2] + d)) for d in (-1, 1))
                    rot = round(t['rotateYRad'][1], 2)
                    doors.setdefault(rot, collections.Counter())[('x' if along_x else '') + ('z' if along_z else '')] += 1
            if p in b.ents or p in parts:
                if c not in cat or (cat[c][0] is None and p in b.ents):
                    cat[c] = (b.ents.get(p), sorted(parts.get(p, ())))
    return cat, doors

CATALOG, _DOOR_ROTS = _build_catalog()

# A door's saved turn for the wall it is in. From his exports: rotateYRad 0 and 3.14 sit in walls that run
# along x (the door faces north or south), 1.57 and -1.57 in walls along z.
def _door_ent(rot):
    for c, t in [(c, t) for c, (t, _) in CATALOG.items() if c.startswith('game:door-') and t]:
        pass
    return {'rotateYRad': ('float', rot), 'opened': ('bool', False), 'invertHandles': ('bool', False)}
DOORS = {'south': _door_ent(0.0), 'north': _door_ent(-3.1415927410125732), 'east': _door_ent(1.5707963705062866), 'west': _door_ent(-1.5707963705062866)}

if __name__ == '__main__':
    for rot, c in sorted(_DOOR_ROTS.items()): print('door rotateYRad', rot, dict(c))
    for c, (t, parts) in sorted(CATALOG.items()):
        print(c, 'details' if t else '-', parts)
