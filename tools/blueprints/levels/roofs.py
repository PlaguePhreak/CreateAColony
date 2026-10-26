"""Roof modules: whole roofs copied from reference exports (every piece with its saved shape data), tiled
along the ridge where the pieces repeat, turned, and given the level's material by data."""
import copy, sys
sys.path.insert(0, __import__('os').path.dirname(__file__))
import kit2
from roofdata import rotate_tree

MATERIAL_SOURCE = {'drygrass': 'townhall_level1', 'thatch': 'residence_small_level1', 'wood': 'cook_level1', 'shingle': 'smelter_level1'}
MATERIAL_KEYS = ('Materials', 'Frame', 'Offhand', 'Tool')
BY_LEVEL = {1: 'drygrass', 2: 'thatch', 3: 'wood', 4: 'shingle', 5: 'shingle'}
_templates = {}


def material_template(name):
    """The material fields of one piece from the building that uses that material."""
    if name not in _templates:
        b = kit2.Build.load(MATERIAL_SOURCE[name])
        for p, c in b.blocks.items():
            e = b.ents.get(p)
            if c.startswith('vsroofing') and isinstance(e, dict) and e.get('Materials') is not None:
                _templates[name] = {k: copy.deepcopy(e[k]) for k in MATERIAL_KEYS if k in e}
                break
    return _templates[name]


def shape_only(e):
    return {k: v for k, v in e.items() if k not in MATERIAL_KEYS}


def rot_facing(code, k):
    order = ['north', 'east', 'south', 'west']
    f = code.rsplit('-', 1)[1]
    if f not in order: return code
    return code[:-len(f)] + order[(order.index(f) + k) % 4]


class RoofModule:
    """blocks: (x, y, z) -> code; ents: same keys -> entity tree. Origin at the module's min x, y, z."""
    def __init__(self, blocks, ents):
        xs = [p[0] for p in blocks]; ys = [p[1] for p in blocks]; zs = [p[2] for p in blocks]
        x0, y0, z0 = min(xs), min(ys), min(zs)
        self.blocks = {(x - x0, y - y0, z - z0): c for (x, y, z), c in blocks.items()}
        self.ents = {(x - x0, y - y0, z - z0): e for (x, y, z), e in ents.items()}
        self.size = (max(xs) - x0 + 1, max(ys) - y0 + 1, max(zs) - z0 + 1)

    @staticmethod
    def from_build(name, box=None):
        b = kit2.Build.load(name)
        blocks = {}; ents = {}
        for p, c in b.blocks.items():
            if not c.startswith('vsroofing'): continue
            if box and not (box[0] <= p[0] <= box[3] and box[1] <= p[1] <= box[4] and box[2] <= p[2] <= box[5]): continue
            blocks[p] = c; ents[p] = b.ents.get(p, {})
        return RoofModule(blocks, ents)

    def slice(self, axis, i):
        """The pieces in column i along axis (0 = x, 1 = z): (coords without that axis) -> (code, shape)."""
        out = {}
        for p, c in self.blocks.items():
            if p[axis * 2] == i:
                key = (p[1], p[2]) if axis == 0 else (p[0], p[1])
                out[key] = (c, shape_only(self.ents.get(p, {})))
        return out

    def repeat_range(self, axis):
        """The longest run of identical consecutive slices along axis: (first, last) inclusive."""
        n = self.size[axis * 2]
        slices = [self.slice(axis, i) for i in range(n)]
        best = (0, 0); start = 0
        for i in range(1, n + 1):
            if i == n or slices[i] != slices[start]:
                if i - 1 - start > best[1] - best[0]: best = (start, i - 1)
                start = i
        return best

    def tiled(self, axis, length, caps=(True, True)):
        """The module stretched (or cut) along axis to `length` blocks by repeating its middle slice.
        caps: keep the end pieces at the low / high end; a False end runs the middle slice to the edge,
        for a roof that butts against a taller wall."""
        a, b = self.repeat_range(axis)
        n = self.size[axis * 2]
        if b - a < 0: raise ValueError('no repeating slice')
        head = a if caps[0] else 0; tail = (n - 1 - b) if caps[1] else 0
        mid = length - head - tail
        if mid < 1: raise ValueError(f'too short: needs at least {head + tail + 1}')
        blocks = {}; ents = {}
        def put(i_src, i_dst):
            for p, c in self.blocks.items():
                if p[axis * 2] != i_src: continue
                q = list(p); q[axis * 2] = i_dst; q = tuple(q)
                blocks[q] = c; ents[q] = copy.deepcopy(self.ents.get(p, {}))
        for i in range(head): put(i, i)
        for j in range(mid): put(a, head + j)
        for i in range(tail): put(b + 1 + i, head + mid + i)
        return RoofModule(blocks, ents)

    def rotated(self, k):
        """Turned k quarter turns clockwise seen from above (x, z) -> (-z, x)."""
        k %= 4
        if k == 0: return RoofModule(dict(self.blocks), copy.deepcopy(self.ents))
        blocks = {}; ents = {}
        for (x, y, z), c in self.blocks.items():
            q = (x, y, z)
            for _ in range(k): q = (-q[2], q[1], q[0])
            blocks[q] = rot_facing(c, k)
            e = self.ents.get((x, y, z))
            if e is not None: ents[q] = rotate_tree(copy.deepcopy(e), k)
        return RoofModule(blocks, ents)

    def stamp(self, d, origin, material, keep_walls=True):
        """Write the module into design d with its min corner at origin, every piece given `material`.
        keep_walls: never overwrite a wall or other solid block already there (a roof butting a wall)."""
        t = material_template(material)
        ox, oy, oz = origin
        for p, c in self.blocks.items():
            q = (p[0] + ox, p[1] + oy, p[2] + oz)
            if keep_walls and d.get(q) != 'game:air' and not d.get(q).startswith('vsroofing'): continue
            e = copy.deepcopy(self.ents.get(p, {}))
            for k in MATERIAL_KEYS: e.pop(k, None)
            e.update(copy.deepcopy(t))
            e['blockCode'] = ('str', c)
            d.set(q, c, e)
            if not hasattr(d, 'roof_mat'): d.roof_mat = {}
            d.roof_mat[q] = material
        return self
