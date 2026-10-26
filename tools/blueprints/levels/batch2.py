"""Huts with no Dev's Build yet: each takes the whole level ladder of the nearest building of Aaron's,
with its hut block swapped, plus the few things that hut needs (a field, a pit marker, chests)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import batch1
from helpers import *
from batch1 import done, RESULTS, OUT

def series(type, variant=''):
    """His levels and the new ones, in order."""
    files = sorted(f for f in kit2.DB if f.startswith(type + ('_' + variant if variant else '') + '_level'))
    out = {int(f.rsplit('level', 1)[1]): Build.load(f) for f in files}
    for (t, v, l), (b, pr) in RESULTS.items():
        if t == type and v == variant: out[l] = b
    return [out[i].copy() for i in range(1, 6)]

def make(type, base, variant='', extra=None):
    for i, b in enumerate(series(base, variant), 1):
        set_hut(b, type)
        if extra: extra(b, i)
        done(type, '', i, b)

def farmer_extra(b, level):
    """A field beside the hut, bigger each level."""
    x0, y0, z0, x1, y1, z1 = b.bounds(); fy = b.hut()[1] - 1
    size = 3 + level * 2
    ground(b, (x1 + 2, z0, x1 + 1 + size, z0 + size - 1), fy)
    for x in range(x1 + 2, x1 + 2 + size):
        for z in range(z0, z0 + size):
            if b.get((x, fy, z)) == 'game:soil-medium-normal' and free(b, (x, fy + 1, z)): b.place('game:farmland-moist-medium', (x, fy, z))
    fence_ring(b, (x1 + 1, z0 - 1, x1 + 2 + size, z0 + size), fy + 1, 'wattle-sticks' if level < 4 else 'woodenfence-oak')

def quarry_extra(b, level):
    x0, y0, z0, x1, y1, z1 = b.bounds(); h = b.hut()
    b.set((h[0], h[1], z1 + 10), 'createacolony:waypoint-quarry')

def miner_extra(b, level):
    x0, y0, z0, x1, y1, z1 = b.bounds(); h = b.hut()
    b.set((x1 + 4, h[1], h[2]), 'createacolony:waypoint-mine')

def chests_extra(b, level):
    fy = b.hut()[1]
    x0, y0, z0, x1, y1, z1 = b.bounds()
    for _ in range(2 + level): put(b, 'game:chest-south', box_cells((x0, fy, z0, x1, fy, z1)))

if __name__ == '__main__':
    batch1.run_all()
    make('farmer', 'builder', extra=farmer_extra)
    make('craftsman', 'builder')
    make('miner', 'builder', extra=miner_extra)
    make('potter', 'blacksmith')
    make('armourer', 'blacksmith')
    make('weaponsmith', 'blacksmith')
    make('quarry', 'smelter', extra=quarry_extra)
    make('prospector', 'forester', 'small')
    make('explorer', 'forester', 'small')
    make('hunter', 'forester', 'large')
    make('jeweller', 'forester', 'large')
    make('tailor', 'gatherer')
    make('tavern', 'cook')
    make('healer', 'residence', 'small')
    make('church', 'townhall')
    make('warehouse', 'sawmill', extra=chests_extra)
    make('mint', 'sawmill')
    make('barracks', 'guardtower')
    bad = {k: pr for k, (b, pr) in RESULTS.items() if pr}
    print(len(RESULTS), 'files,', len(bad), 'with problems', bad)
    batch1.previews()
