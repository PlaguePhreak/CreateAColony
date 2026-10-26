"""New levels for the huts Aaron already has builds for. Each recipe starts from his export."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from helpers import *
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')   # copy out/<type>/ into the devsbuilds pack

RESULTS = {}
def done(type, variant, level, b):
    folder = os.path.join(OUT, type, variant) if variant else os.path.join(OUT, type)
    problems = b.save(os.path.join(folder, f'level{level}.json'), type)
    RESULTS[(type, variant, level)] = (b, problems)
    if problems: print(f'{type} {variant} {level}: PROBLEMS', problems)
    else: print(f'{type} {variant} {level}: ok, {len(b.blocks)} blocks')
    return b

# ---------------------------------------------------------------- Forester, small
def forester_small():
    base = Build.load('forester_small_level1')   # 5x5 open log shelter, hut (2,2,2), floor y=1
    H = base.hut()
    # level 2: a lantern, a rough bed, a wattle windbreak on the back (south) side
    b = base.copy()
    for x in (2, 3, 4):
        for y in (2, 3): b.set((x, y, 5), 'game:wattle-sticks-ew-free')
    bed(b, (4, 2, 3), 'rough', 'north') or bed(b, (3, 2, 4), 'rough')
    lantern(b, (4, 3, 1), 'north') if not solid(b, (4, 3, 1)) else None
    b.place('game:lantern-small-down', (3, 4, 3)) if free(b, (3, 4, 3)) and solid(b, (3, 5, 3)) else lantern(b, (2, 3, 4))
    done('forester', 'small', 2, b)
    # level 3: longer (5x7), wattle below and planks above on three sides, a door on the north side, a trunk
    b = b.copy(); b.stretch(1, 2, 2)                     # z grows by 2 at the regular roof slice
    for z in range(1, 8):
        for x in (1, 5):
            if z in (1, 7): continue
            b.set((x, 2, z), 'game:wattle-sticks-ns-free'); b.set((x, 3, z), 'game:planks-oak-ud')
    for x in (2, 3, 4):
        b.set((x, 2, 7), 'game:wattle-sticks-ew-free'); b.set((x, 3, 7), 'game:planks-oak-ud')
        b.set((x, 3, 1), 'game:planks-oak-ud')
    b.set((2, 2, 1), 'game:wattle-sticks-ew-free'); b.set((4, 2, 1), 'game:wattle-sticks-ew-free')
    b.door((3, 2, 1), 'solid-oak', 'south')
    put(b, 'game:trunk-east', [(4, 2, 5), (2, 2, 5), (4, 2, 6)])
    ground(b, (0, 0, 6, 8), 1); b.place('game:firepit-cold', (3, 2, -1)) if solid(b, (3, 1, -1)) else None
    done('forester', 'small', 3, b)
    # level 4: cobblestone footing, plank walls, taller, a second bed, logs stacked outside
    b = b.copy()
    b.swap_codes({'game:wattle-sticks': 'game:planks-oak-ud'}, (1, 2, 1, 5, 3, 7))
    b.swap(lambda c, p: 'game:planks-oak-ud' if c.startswith('game:planks-oak') and p[1] == 2 else None, (1, 2, 1, 5, 2, 7))
    perimeter(b, (1, 1, 1, 5, 1, 7), 1, 'game:cobblestone-shale')
    b.repeat_layer(3, 1); fix_doors(b, 'game:planks-oak-ud')
    bed(b, (2, 2, 6), 'rough', 'north') or bed(b, (2, 2, 3), 'rough')
    for z in (3, 4, 5): b.set((6, 2, z), 'game:log-placed-oak-ns')
    for z in (3, 4): b.set((6, 3, z), 'game:log-placed-oak-ns')
    ground(b, (0, 0, 7, 8), 1)
    lantern(b, (3, 3, 0)) ; lantern(b, (2, 4, 2))
    done('forester', 'small', 4, b)
    # level 5: stone brick footing, rustic beds, lanterns, a drystone wall round a small yard with a path
    b = b.copy()
    b.swap_codes({'game:cobblestone-shale': 'game:stonebricks-shale'})
    for p, c in list(b.blocks.items()):
        if 'roughbed' in c: b.set(p, None)
    for p, c in list(b.blocks.items()):
        if c.startswith('game:multiblock') and not solid(b, add(p, 'down')): pass
    b.swap(lambda c, p: None, None)
    for p, c in list(b.blocks.items()):
        if c.startswith('game:multiblock'):
            o = kit2.off_of(c); m = (p[0] - o[0], p[1] - o[1], p[2] - o[2])
            if m not in b.blocks: b.set(p, None)
    bed(b, (2, 2, 6), 'rustic', 'east') or bed(b, (3, 2, 6), 'rustic')
    bed(b, (2, 2, 3), 'rustic', 'east') or bed(b, (3, 2, 3), 'rustic')
    ground(b, (-2, -2, 8, 10), 1)
    fence_ring(b, (-2, -2, 8, 10), 2, 'drystonefence-shale', gap=(3, -2))
    path(b, [(3, z) for z in range(-2, 1)], 1)
    put(b, 'game:stationarybasket-north', [(7, 2, 7), (7, 2, 8), (-1, 2, 7)])
    put(b, 'game:stationarybasket-north', [(7, 2, 8), (-1, 2, 8), (-1, 2, 6)])
    done('forester', 'small', 5, b)

# ---------------------------------------------------------------- Forester, large
def forester_large():
    base = Build.load('forester_large_level1')   # log shed x1-8 z6-11, floor y=1, hut (6,2,7)
    b = base.copy()
    lanterns_for_torches(b)
    bed(b, (2, 2, 9), 'rough', 'north') or bed(b, (3, 2, 8), 'rough')
    for x in (5, 6, 7):
        if free(b, (x, 3, 6)): b.set((x, 3, 6), 'game:wattle-sticks-ew-free')
        if free(b, (x, 3, 11)): b.set((x, 3, 11), 'game:wattle-sticks-ew-free')
    for p in ((2, 2, 10), (3, 2, 10), (2, 3, 10)):
        if standable(b, p) or (p[1] == 3 and solid(b, add(p, 'down'))): b.set(p, 'game:log-placed-oak-we')
    done('forester', 'large', 2, b)
    # level 3: longer, wattle and plank walls, a door on the west side, a trunk, a firepit outside
    b = b.copy(); b.stretch(0, 5, 2)   # x grows by 2
    b.swap_codes({'game:drystonefence-shale': 'game:wattle-sticks'}, (1, 2, 6, 10, 2, 11))
    for p, c in list(b.blocks.items()):
        if c.startswith('game:wattle-sticks') and kit2.inside(p, (1, 2, 6, 10, 2, 11)): b.set((p[0], 3, p[2]), 'game:planks-oak-ud')
    # the open west side: wattle either side of a door
    for z in (7, 9, 10):
        b.set((1, 2, z), 'game:wattle-sticks-ns-free'); b.set((1, 3, z), 'game:planks-oak-ud')
    b.set((1, 3, 8), 'game:planks-oak-ud'); b.door((1, 2, 8), 'solid-oak', 'east')
    # the north side gap between the posts at x1 and x4
    for x in (2, 3):
        b.set((x, 2, 6), 'game:wattle-sticks-ew-free'); b.set((x, 3, 6), 'game:planks-oak-ud')
        b.set((x, 2, 11), 'game:wattle-sticks-ew-free'); b.set((x, 3, 11), 'game:planks-oak-ud')
    put(b, 'game:trunk-north', [(8, 2, 10), (9, 2, 10), (8, 2, 7)])
    ground(b, (-2, 5, 12, 13), 1); b.place('game:firepit-cold', (-1, 2, 8)) if solid(b, (-1, 1, 8)) else None
    done('forester', 'large', 3, b)
    # level 4: longer again, cobblestone footing, plank walls, taller, two beds, a log stack outside
    b = b.copy(); b.stretch(0, 5, 2)
    box = bbox(b, 'log-placed-oak-ud', (0, 2, 5, 20, 2, 12))
    b.swap_codes({'game:wattle-sticks': 'game:planks-oak-ud'}, (box[0], 2, box[2], box[3], 3, box[5]))
    perimeter(b, (box[0], 1, box[2], box[3], 1, box[5]), 1, 'game:cobblestone-shale')
    b.repeat_layer(3, 1); fix_doors(b, 'game:planks-oak-ud')
    bed(b, (3, 2, 10), 'rough', 'north') or bed(b, (4, 2, 9), 'rough')
    for z in (7, 8, 9, 10): b.set((box[3] + 1, 2, z), 'game:log-placed-oak-ns'); 
    for z in (8, 9): b.set((box[3] + 1, 3, z), 'game:log-placed-oak-ns')
    ground(b, (-2, 5, box[3] + 3, 13), 1)
    lantern(b, (5, 4, 7)); lantern(b, (8, 4, 10))
    done('forester', 'large', 4, b)
    # level 5: stone brick footing, rustic beds, drystone wall, stone path, baskets for saplings
    b = b.copy()
    b.swap_codes({'game:cobblestone-shale': 'game:stonebricks-shale'})
    for p, c in list(b.blocks.items()):
        if 'roughbed' in c: b.set(p, None)
    for p, c in list(b.blocks.items()):
        if c.startswith('game:multiblock'):
            o = kit2.off_of(c); m = (p[0] - o[0], p[1] - o[1], p[2] - o[2])
            if m not in b.blocks: b.set(p, None)
    bed(b, (2, 2, 10), 'rustic', 'east') or bed(b, (3, 2, 9), 'rustic')
    bed(b, (2, 2, 7), 'rustic', 'east') or bed(b, (4, 2, 7), 'rustic')
    ground(b, (-3, 3, box[3] + 4, 14), 1)
    fence_ring(b, (-3, 3, box[3] + 4, 14), 2, 'drystonefence-shale', gap=(-3, 8))
    path(b, [(x, 8) for x in range(-3, 1)], 1)
    put(b, 'game:stationarybasket-north', [(2, 2, 4), (3, 2, 4), (4, 2, 4)])
    put(b, 'game:stationarybasket-north', [(4, 2, 4), (5, 2, 4), (6, 2, 4)])
    done('forester', 'large', 5, b)

# ---------------------------------------------------------------- Gatherer
def gatherer():
    base = Build.load('gatherer_level1')   # two dirt sheds: west x1-8 z2-8 (hut (4,1,3)), east x12-18 z2-9; floor y=0
    b = base.copy()
    b.swap_codes({'game:packeddirt': 'game:rammed-light-plain', 'game:door-crude': 'game:door-solid-oak'})
    lanterns_for_torches(b)
    for x in (9, 10, 11):
        for z in (2, 8):
            if free(b, (x, 1, z)) and solid(b, (x, 0, z)): b.set((x, 1, z), 'game:wattle-sticks-ew-free')
    bed(b, (14, 1, 7), 'rough', 'north') or bed(b, (13, 1, 6), 'rough') or bed(b, (3, 1, 6), 'rough')
    put(b, 'game:stationarybasket-north', box_cells((2, 1, 7, 7, 1, 7)))
    put(b, 'game:stationarybasket-north', box_cells((13, 1, 3, 17, 1, 3)))
    done('gatherer', '', 2, b)
    # level 3: mud brick, both sheds longer, a table and shelves, a trunk in each shed
    b = b.copy()
    b.swap_codes({'game:rammed-light-plain': 'game:mudbrick-light'})
    b.stretch(0, 4, 2); b.stretch(0, 16, 2)
    put(b, 'w4rd0sfurniture:roughtable-north', box_cells((3, 1, 4, 8, 1, 6)))
    put(b, 'game:trunk-north', box_cells((2, 1, 3, 9, 1, 7)))
    put(b, 'game:trunk-north', box_cells((15, 1, 3, 21, 1, 8)))
    for p in box_cells((15, 2, 3, 21, 2, 8)):
        if free(b, p) and solid(b, add(p, 'west')) and free(b, add(p, 'down')): b.place('game:shelf-normal-west', p); break
    done('gatherer', '', 3, b)
    # level 4: cobblestone, a loft over the east shed, more baskets, a yard with a firepit and an oak fence
    b = b.copy()
    b.swap_codes({'game:mudbrick-light': 'game:cobblestone-shale'})
    east = bbox(b, 'cobblestone-shale', (13, 0, 1, 30, 3, 12))
    b.repeat_layer(1, 2); fix_doors(b, 'game:cobblestone-shale')
    # loft floor two up in the east shed, with a ladder in its north-west corner
    ex0, _, ez0, ex1, _, ez1 = east
    for x in range(ex0 + 1, ex1):
        for z in range(ez0 + 1, ez1):
            if free(b, (x, 3, z)) and (x, z) != (ex0 + 1, ez0 + 1): b.set((x, 3, z), 'game:planks-oak-ud')
    for y in (1, 2, 3): b.set((ex0 + 1, y, ez0 + 1), 'game:ladder-wood-oak-north')
    bed(b, (ex0 + 3, 4, ez0 + 2), 'rough', 'south') or bed(b, (ex0 + 2, 4, ez0 + 3), 'rough')
    for _ in range(4): put(b, 'game:stationarybasket-north', box_cells((2, 1, 3, 30, 1, 8)))
    ground(b, (0, 9, 30, 15), 0)
    fence_ring(b, (1, 10, 24, 15), 1, 'woodenfence-oak', gap=(12, 15))
    b.place('game:firepit-cold', (12, 1, 12)) if standable(b, (12, 1, 12)) else None
    done('gatherer', '', 4, b)
    # level 5: stone bricks below, planks for the loft, rustic beds, drystone wall, stone paths
    b = b.copy()
    b.swap(lambda c, p: 'game:stonebricks-shale' if c == 'game:cobblestone-shale' and p[1] <= 2 else ('game:planks-oak-ud' if c == 'game:cobblestone-shale' and p[1] >= 3 else None))
    b.swap_codes({'game:woodenfence-oak': 'game:drystonefence-shale'})
    for p, c in list(b.blocks.items()):
        if 'roughbed' in c: b.set(p, None)
    for p, c in list(b.blocks.items()):
        if c.startswith('game:multiblock'):
            o = kit2.off_of(c); m = (p[0] - o[0], p[1] - o[1], p[2] - o[2])
            if m not in b.blocks: b.set(p, None)
    bed(b, (ex0 + 2, 4, ez0 + 3), 'rustic', 'east') or bed(b, (ex0 + 2, 4, ez0 + 5), 'rustic')
    bed(b, (ex0 + 2, 4, ez0 + 6), 'rustic', 'east') or bed(b, (ex0 + 2, 4, ez0 + 4), 'rustic')
    doors = [p for p, c in b.blocks.items() if c.startswith('game:door-')]
    for d in doors:
        path(b, [(d[0], z) for z in range(d[2] + 1, d[2] + 5)] + [(d[0] + 1, z) for z in range(d[2] + 1, d[2] + 5)], 0)
    done('gatherer', '', 5, b)

# ---------------------------------------------------------------- Blacksmith
def blacksmith():
    base = Build.load('blacksmith_level1')   # open forge x1-19 z1-7, floor y=0, hut (9,1,5); chimney mass x1-4 z1-4
    b = base.copy()
    b.swap(lambda c, p: 'game:cobblestone-shale' if c == 'game:rammed-light-plain' and p[1] == 0 and p[0] <= 12 else None)
    put(b, 'game:toolrack-west', [(5, 1, 2), (5, 1, 3), (6, 1, 2)])
    put(b, 'game:trunk-north', box_cells((5, 1, 2, 11, 1, 6)))
    put(b, 'game:barrel', box_cells((5, 1, 2, 11, 1, 6)))
    done('blacksmith', '', 2, b)
    # level 3: longer, stone brick chimney, a bed on the store platform, a yard with a drystone fence
    b = b.copy(); b.stretch(0, 7, 2)
    b.swap_codes({'game:cobblestone-shale': 'game:stonebricks-shale'}, (1, 1, 1, 4, 7, 4))
    bed(b, (17, 4, 4), 'rough', 'east') or bed(b, (17, 4, 5), 'rough')
    ground(b, (0, 8, 22, 11), 0)
    fence_ring(b, (1, 8, 21, 11), 1, 'drystonefence-shale', gap=(11, 11))
    b.place('game:firepit-cold', (8, 1, 9)) if standable(b, (8, 1, 9)) else None
    done('blacksmith', '', 3, b)
    # level 4: longer again, stone bricks, a second forge and anvil, two rustic beds, a coal store
    b = b.copy(); b.stretch(0, 7, 2)
    b.swap_codes({'game:cobblestone-shale': 'game:stonebricks-shale'}, (0, 0, 0, 30, 8, 7))
    put(b, 'game:forge', box_cells((5, 1, 2, 12, 1, 3)))
    put(b, 'game:anvil-copper', box_cells((6, 1, 4, 12, 1, 6)))
    for p, c in list(b.blocks.items()):
        if 'roughbed' in c: b.set(p, None)
    for p, c in list(b.blocks.items()):
        if c.startswith('game:multiblock'):
            o = kit2.off_of(c); m = (p[0] - o[0], p[1] - o[1], p[2] - o[2])
            if m not in b.blocks: b.set(p, None)
    bed(b, (19, 4, 4), 'rustic', 'east') or bed(b, (19, 4, 5), 'rustic')
    bed(b, (19, 4, 6), 'rustic', 'east') or bed(b, (20, 4, 5), 'rustic')
    for _ in range(2): put(b, 'game:crate', box_cells((13, 1, 2, 18, 1, 6)))
    done('blacksmith', '', 4, b)
    # level 5: the long smithy, polished floor at the anvils, a third forge, crates, lanterns, stone path
    b = b.copy(); b.stretch(0, 7, 4)
    for p, c in list(b.blocks.items()):
        if c == 'game:stonebricks-shale' and p[1] == 0 and 5 <= p[0] <= 12 and 2 <= p[2] <= 6: b.set(p, 'game:rockpolished-shale')
    put(b, 'game:forge', box_cells((5, 1, 2, 16, 1, 3)))
    put(b, 'game:anvil-copper', box_cells((6, 1, 4, 16, 1, 6)))
    for _ in range(3): put(b, 'game:crate', box_cells((13, 1, 2, 24, 1, 6)))
    for x in (8, 14, 20): lantern(b, (x, 3, 7)) or lantern(b, (x, 2, 7))
    path(b, [(x, 8) for x in range(3, 26)], 0)
    done('blacksmith', '', 5, b)

# ---------------------------------------------------------------- Smelter
def smelter():
    base = Build.load('smelter_level1')   # shed x6-16 z2-10, floor y=3, hut (14,4,13) in the yard; bedroom upstairs y8
    shed = (5, 3, 1, 17, 5, 11)
    b = base.copy()
    b.swap_codes({'game:rammed-light-plain': 'game:cobblestone-shale'}, shed)
    lanterns_for_torches(b)
    for _ in range(3): put(b, 'game:stationarybasket-north', box_cells((7, 4, 12, 16, 4, 12)))
    done('smelter', '', 2, b)
    # level 3: closed in on the back and east sides, stone bricks round the chimney, a plank bedroom wall
    b = b.copy()
    for x in range(7, 16):
        for y in (4, 5):
            if not solid(b, (x, y, 2)) and b.get((x, y, 2)) != 'game:log-placed-oak-ud': b.set((x, y, 2), 'game:cobblestone-shale')
    for z in range(3, 10):
        for y in (4, 5):
            if not solid(b, (16, y, z)): b.set((16, y, z), 'game:cobblestone-shale')
    b.swap_codes({'game:cobblestone-shale': 'game:stonebricks-shale'}, (11, 4, 9, 15, 8, 12))
    done('smelter', '', 3, b)
    # level 4: stone brick walls, a block taller, a second wooden bed, a drystone wall with a gate
    b = b.copy()
    b.swap_codes({'game:cobblestone-shale': 'game:stonebricks-shale'}, shed)
    b.repeat_layer(5, 1); fix_doors(b, 'game:planks-oak-ud')
    bed(b, (7, 9, 5), 'wood', 'east') or bed(b, (8, 9, 8), 'wood') or bed(b, (7, 9, 8), 'wood')
    ground(b, (0, 0, 20, 18), 2)
    fence_ring(b, (1, 0, 19, 17), 3, 'drystonefence-shale', gap=(14, 17))
    done('smelter', '', 4, b)
    # level 5: a roofed ore yard beside the works (your gatherer shed), crates, lanterns, stone path
    b = b.copy()
    shedb = Build.load('gatherer_level1')
    b.paste(shedb, at=(-6, 2, 0), box=(0, 0, 1, 9, 6, 9))
    for p, c in list(b.blocks.items()):
        if c == 'createacolony:hut-gatherer': b.set(p, 'game:cobblestone-shale')
    b.swap_codes({'game:packeddirt': 'game:cobblestone-shale', 'game:door-crude': 'game:door-solid-oak'}, (-6, 2, 0, 4, 6, 10))
    lanterns_for_torches(b)
    ground(b, (-7, 0, 20, 18), 2)
    for _ in range(3): put(b, 'game:crate', box_cells((-4, 3, 3, 1, 3, 7)))
    path(b, [(x, 12) for x in range(2, 17)], 2)
    done('smelter', '', 5, b)

# ---------------------------------------------------------------- Sawmill
def sawmill():
    base = Build.load('sawmill_level2')   # hall x3-17 z2-10, floor y=2 (logs), hut (3,3,4), roof y5-9, cellar y0-1
    b = base.copy()
    b.repeat_layer(4, 2)
    for x in range(4, 9):
        for z in range(4, 8):
            if free(b, (x, 5, z)) and (x, z) != (4, 7): b.set((x, 5, z), 'game:planks-oak-ud')
    for y in (3, 4, 5): b.set((4, y, 7), 'game:ladder-wood-oak-north') if free(b, (4, y, 7)) or y == 5 else None
    bed(b, (6, 6, 5), 'rough', 'south') or bed(b, (7, 6, 4), 'rough')
    shed = Build.load('forester_large_level1')
    b.paste(shed, at=(9, 1, 6), box=(0, 1, 5, 9, 5, 12))
    for p, c in list(b.blocks.items()):
        if c == 'createacolony:hut-forester': b.set(p, 'game:packeddirt')
    put(b, 'w4rd0sfurniture:roughtable-north', box_cells((10, 3, 3, 16, 3, 9)))
    for _ in range(2): put(b, 'game:chest-east', box_cells((4, 1, 3, 16, 1, 9)))
    ground(b, (0, 0, 19, 18), 1)
    done('sawmill', '', 3, b)
    # level 4: two storeys, plank store upstairs with crates, two rustic beds, benches, path, drystone fence
    b = b.copy()
    b.repeat_layer(4, 2)
    for x in range(4, 17):
        for z in range(3, 10):
            if free(b, (x, 5, z)) and (x, z) != (4, 7): b.set((x, 5, z), 'game:planks-oak-ud')
    for y in (3, 4, 5): b.set((4, y, 7), 'game:ladder-wood-oak-north')
    for p, c in list(b.blocks.items()):
        if 'roughbed' in c: b.set(p, None)
    for p, c in list(b.blocks.items()):
        if c.startswith('game:multiblock'):
            o = kit2.off_of(c); m = (p[0] - o[0], p[1] - o[1], p[2] - o[2])
            if m not in b.blocks: b.set(p, None)
    bed(b, (6, 6, 4), 'rustic', 'east') or bed(b, (6, 6, 5), 'rustic')
    bed(b, (6, 6, 8), 'rustic', 'east') or bed(b, (6, 6, 7), 'rustic')
    for _ in range(4): put(b, 'game:crate', box_cells((10, 6, 4, 16, 6, 9)))
    put(b, 'w4rd0sfurniture:roughtable-north', box_cells((10, 3, 3, 16, 3, 9)))
    for y in (6, 7): lantern(b, (9, y, 6)) or lantern(b, (12, y, 6))
    fence_ring(b, (0, 0, 19, 18), 2, 'drystonefence-shale', gap=(0, 5))
    path(b, [(x, 5) for x in range(0, 3)], 1)
    done('sawmill', '', 4, b)
    # level 5: stone bricks and polished shale, your first sawmill hall (longer) added as the log yard hall
    b = b.copy()
    hall = Build.load('sawmill_level1'); hall.stretch(0, 11, 2)
    b.paste(hall, at=(3, -1, 19), box=(0, 0, 0, 20, 10, 9))
    for p, c in list(b.blocks.items()):
        if c == 'createacolony:hut-sawmill' and p != (3, 3, 4): b.set(p, 'game:planks-oak-ud')
    b.swap_codes({'game:cobblestone-shale': 'game:stonebricks-shale'})
    for p, c in list(b.blocks.items()):
        if c == 'game:stonebricks-shale' and p[1] == 2 and (p[0] in (3, 17) or p[2] in (2, 10)): b.set(p, 'game:rockpolished-shale')
    for _ in range(3): put(b, 'game:crate', box_cells((5, 1, 3, 16, 1, 9)))
    ground(b, (0, 0, 22, 29), 1)
    done('sawmill', '', 5, b)


# ---------------------------------------------------------------- Residence (small and medium share the house)
def residence(variant='small'):
    base = Build.load(f'residence_{variant}_level1')
    H = base.hut()                                   # (12,2,20) small, (12,2,29) medium; floor y=1
    house = bbox(base, 'daub')                       # the daub walls give the house box
    hx0, _, hz0, hx1, _, hz1 = house
    hb = (hx0, 1, hz0, hx1, 12, hz1)
    # level 2: the daub repaired, lanterns, a third hay bed, oak fence round the garden
    b = base.copy()
    b.swap_codes({'game:daub-tan-cracked': 'game:daub-tan-normal', 'game:daub-tan-wattle': 'game:daub-tan-normal'})
    b.swap(lambda c, p: 'game:daub-tan-normal' if c.startswith('game:wattle-sticks') else None, hb)
    b.swap_codes({'game:wattle-sticks': 'game:woodenfence-oak'})
    for p in box_cells((hx0 + 1, 3, hz0 + 1, hx1 - 1, 3, hz1 - 1)):
        if lantern(b, p): break
    for p in box_cells((hx0 + 1, 3, hz1 - 6, hx1 - 1, 3, hz1 - 1)):
        if lantern(b, p): break
    for p in box_cells((hx0 + 1, 6, hz0 + 1, hx1 - 1, 6, hz1 - 1)):
        if bed(b, p, 'hay', 'south'): break
    done('residence', variant, 2, b)
    # level 3: planks upstairs, a fourth bed, a second firepit, a small cellar with a ladder, stone path round the house
    b = b.copy()
    b.swap(lambda c, p: 'game:planks-oak-ud' if c.startswith('game:daub') and p[1] >= 5 else None, hb)
    for p in box_cells((hx0 + 1, 6, hz0 + 1, hx1 - 1, 6, hz1 - 1)):
        if bed(b, p, 'rough'): break
    for p in box_cells((hx0 + 1, 2, hz0 + 1, hx1 - 1, 2, hz0 + 6)):
        if standable(b, p) and all(free(b, add(p, d)) for d in ('north', 'south', 'east', 'west')): b.place('game:firepit-cold', p); break
    cellar(b, (hx0 + 1, hz1 - 6, hx1 - 1, hz1 - 1), 3, 'game:cobblestone-shale', 'game:cobblestone-shale', ladder_at=(hx0 + 2, hz1 - 2, 'north'))
    for _ in range(2): put(b, 'game:trunk-east', box_cells((hx0 + 2, -2, hz1 - 5, hx1 - 2, -2, hz1 - 2)))
    ring = [(x, z) for x in range(hx0 - 1, hx1 + 2) for z in range(hz0 - 1, hz1 + 2) if x in (hx0 - 1, hx1 + 1) or z in (hz0 - 1, hz1 + 1)]
    path(b, ring, 0)
    done('residence', variant, 3, b)
    # level 4: cobblestone ground floor, a bench on the porch, a fifth bed, shelves, drystone garden wall
    b = b.copy()
    b.swap(lambda c, p: 'game:cobblestone-shale' if c.startswith('game:daub') and p[1] <= 4 else None, hb)
    b.swap_codes({'game:woodenfence-oak': 'game:drystonefence-shale'})
    put(b, 'w4rd0sfurniture:roughbench-west', box_cells((hx0 + 1, 2, hz0 - 3, hx1 - 1, 2, hz0 - 1)))
    for p in box_cells((hx0 + 1, 6, hz0 + 1, hx1 - 1, 6, hz1 - 1)):
        if bed(b, p, 'rough'): break
    put(b, 'foodshelves:doubleshelf-normal-east', box_cells((hx0 + 1, 2, hz1 - 6, hx1 - 1, 2, hz1 - 1)))
    done('residence', variant, 4, b)
    # level 5: stone bricks below and for the chimney, a longer house, rustic beds, more lanterns
    b = b.copy()
    b.swap(lambda c, p: 'game:stonebricks-shale' if c == 'game:cobblestone-shale' and p[1] >= 2 else None, hb)
    at = b.roof_regular(1)[len(b.roof_regular(1)) // 2]
    b.stretch(1, at, 2)
    H2 = b.hut()
    for p in box_cells((hx0 + 1, 6, hz0 + 1, hx1 - 1, 6, hz1 + 2)):
        if bed(b, p, 'rustic', 'east'): break
    for p in box_cells((hx0 + 1, 6, hz0 + 1, hx1 - 1, 6, hz1 + 2)):
        if bed(b, p, 'rustic', 'east'): break
    for p in box_cells((hx0 + 1, 7, hz0 + 1, hx1 - 1, 7, hz1 + 2)):
        if lantern(b, p): break
    done('residence', variant, 5, b)

# ---------------------------------------------------------------- Cook
def cook():
    base = Build.load('cook_level2')   # walls x3-19 z3-11, floor y=7 (hut (11,7,8)), cellar y0-6, roof from y9
    walls = (3, 7, 3, 19, 12, 11)
    # level 3: cobblestone skirt, a porch over the door, a yard with a firepit, stone brick cellar floor, barrel racks
    b = base.copy()
    perimeter(b, walls, 7, 'game:cobblestone-shale', skip=('door', 'multiblock', 'log-placed', 'createacolony', 'trapdoor'))
    door = next(p for p, c in b.blocks.items() if c.startswith('game:door-') and p[2] == 11)
    res = Build.load('residence_small_level1')
    porch = bbox(res, 'vsroofing', (9, 3, 9, 12, 3, 11))
    b.paste(res, at=(door[0] - 2 - 9, 6, 12 - 9), box=(9, 3, 9, 12, 3, 11))        # the porch roof at y 9
    for x in (door[0] - 2, door[0] + 1):
        for y in (7, 8): b.set((x, y, 14), 'game:log-placed-oak-ud')
    ground(b, (0, 12, 22, 18), 6)
    put(b, 'w4rd0sfurniture:rusticsturdytabledouble-east', [(door[0] - 1, 7, 13), (door[0], 7, 13)])
    fence_ring(b, (2, 12, 20, 17), 7, 'woodenfence-oak', gap=(door[0], 17))
    b.place('game:firepit-cold', (14, 7, 15)) if standable(b, (14, 7, 15)) else None
    b.swap(lambda c, p: 'game:stonebricks-shale' if c == 'game:cobblestone-shale' and p[1] == 0 else None)
    for _ in range(3): put(b, 'foodshelves:barrelrack-normal-north', box_cells((4, 1, 4, 18, 1, 10)))
    done('cook', '', 3, b)
    # level 4: a plank upper storey, four ovens, beds upstairs, a stone path
    b = b.copy()
    b.repeat_layer(8, 4); fix_doors(b, 'game:mudbrick-dark')
    b.clear((2, 13, 12, 9, 13, 14)); b.paste(res, at=(door[0] - 2 - 9, 6, 12 - 9), box=(9, 3, 9, 12, 3, 11))   # the porch roof back down
    b.swap(lambda c, p: 'game:planks-oak-ud' if c == 'game:mudbrick-dark' and 11 <= p[1] <= 12 else None, walls[:1] + (10,) + walls[2:4] + (13,) + walls[5:])
    ladders = [p for p, c in b.blocks.items() if 'ladder' in c and 7 <= p[1] <= 9]
    for x in range(4, 19):
        for z in range(4, 11):
            if free(b, (x, 10, z)) and not any(l[0] == x and l[2] == z for l in ladders): b.set((x, 10, z), 'game:planks-oak-ud')
    for l in ladders:
        for y in range(7, 11): b.set((l[0], y, l[2]), b.blocks[l])
    for _ in range(3):
        for p in box_cells((4, 11, 4, 18, 11, 10)):
            if bed(b, p, 'rough'): break
    put(b, 'game:clayoven-east', box_cells((4, 7, 4, 18, 7, 10)))
    for y in (11, 12): lantern(b, (10, y, 5)) or lantern(b, (6, y, 5))
    path(b, [(door[0], z) for z in range(15, 19)] + [(door[0] + 1, z) for z in range(15, 19)], 6)
    done('cook', '', 4, b)
    # level 5: stone brick ground floor and a dining wing (your first Cook's Hut) joined on the south side
    b = b.copy()
    b.clear((2, 7, 12, 20, 9, 17)); ground(b, (0, 12, 22, 18), 6)
    for p, c in list(b.blocks.items()):
        if c.startswith('game:woodenfence') or c == 'game:firepit-cold': b.set(p, None)
    wing = Build.load('cook_level1')
    b.paste(wing, at=(2, 6, 11), box=(2, 1, 1, 14, 13, 11))
    for p, c in list(b.blocks.items()):
        if c == 'createacolony:hut-cook' and p != (11, 7, 8): b.set(p, 'game:mudbrick-dark')
    for x in (10, 11):
        for y in (7, 8):
            for z in (11, 12): b.set((x, y, z), None)
    b.swap(lambda c, p: 'game:stonebricks-shale' if c in ('game:cobblestone-shale', 'game:mudbrick-dark') and p[1] == 7 and (p[0] in (3, 19) or p[2] in (3, 11)) else None, walls)
    done('cook', '', 5, b)

# ---------------------------------------------------------------- Builder
def builder():
    base = Build.load('builder_level4')   # hut x2-8 z1-7 floor y=5 (hut (5,6,2)), cellar y0-5 cobble, yard x8-10
    b = base.copy()
    b.swap_codes({'game:cobblestone-shale': 'game:stonebricks-shale'}, (0, 0, 0, 8, 9, 11))
    b.repeat_layer(7, 2); fix_doors(b, 'game:planks-oak-ud')
    for x in range(3, 8):
        for z in range(2, 7):
            if free(b, (x, 8, z)) and (x, z) != (3, 6): b.set((x, 8, z), 'game:planks-oak-ud')
    for y in (6, 7, 8): b.set((3, y, 6), 'game:ladder-wood-oak-north')
    bed(b, (5, 9, 3), 'rustic', 'east') or bed(b, (4, 9, 3), 'rustic') or bed(b, (6, 9, 4), 'rustic')
    lantern(b, (5, 10, 5)) or lantern(b, (7, 9, 5))
    shed = Build.load('forester_large_level1')
    b.paste(shed, at=(1, 4, -12), box=(0, 1, 5, 9, 5, 12))
    for p, c in list(b.blocks.items()):
        if c == 'createacolony:hut-forester': b.set(p, 'game:packeddirt')
    ground(b, (0, -8, 11, 0), 4)
    for _ in range(2): put(b, 'game:groundstorage', box_cells((3, 5, -5, 8, 5, -2)))
    for _ in range(2): put(b, 'game:trunk-north', box_cells((4, 1, 2, 7, 1, 9)))
    done('builder', '', 5, b)

# ---------------------------------------------------------------- Town Hall
def townhall():
    base = Build.load('townhall_level4')   # hall x3-19 z2-12, floor y=5, hut (5,6,3), roof y20-34
    b = base.copy()
    b.stretch(0, 10, 2)
    hall = (2, 5, 1, 21, 9, 13)
    b.swap_codes({'game:cobblestone-shale': 'game:stonebricks-shale', 'game:cobblestonestairs-shale': 'game:stonebrickstairs-shale'}, hall)
    cellar(b, (4, 3, 20, 11), 4, 'game:stonebricks-shale', 'game:stonebricks-shale', ladder_at=(19, 10, 'north'))
    for _ in range(3): put(b, 'game:crate', box_cells((5, 1, 4, 19, 1, 10)))
    for _ in range(2): put(b, 'game:groundstorage', box_cells((5, 1, 4, 19, 1, 10)))
    lantern(b, (12, 3, 4)) or lantern(b, (6, 3, 4))
    for z in (5, 8):
        p = put(b, 'w4rd0sfurniture:rusticsturdytabledouble-east', box_cells((14, 6, z, 19, 6, z)))
        if p:
            put(b, 'w4rd0sfurniture:rusticchairdining-east', [add(p, 'west')]); put(b, 'w4rd0sfurniture:rusticchairdining-west', [add(p, 'east', 2)])
    for _ in range(2):
        for p in box_cells((14, 10, 3, 20, 10, 11)):
            if bed(b, p, 'rustic', 'east'): break
    done('townhall', '', 5, b)

def run_all(which=None):
    which = which or ['forester_small', 'forester_large', 'gatherer', 'blacksmith', 'smelter', 'sawmill', 'residence_small', 'residence_medium', 'cook', 'builder', 'townhall']
    for w in which:
        if w.startswith('residence_'): residence(w.split('_')[1])
        else: globals()[w]()

def previews():
    groups = {}
    for (t, v, l), (b, pr) in RESULTS.items(): groups.setdefault((t, v), []).append((l, b))
    for (t, v), items in groups.items():
        items.sort()
        have = {l for l, _ in items}
        base = [(int(f.rsplit('level', 1)[1]), Build.load(f)) for f in sorted(kit2.DB) if f.startswith(t + ('_' + v if v else '') + '_level')]
        base = [(l, b) for l, b in base if l not in have]
        allb = sorted(base + items)
        names = [f'{t} {v} level {l}' + (' (new)' if l in have else ' (yours)') for l, _ in allb]
        sheet([b for _, b in allb], names, os.path.join(OUT, f'preview_{t}{"_" + v if v else ""}.png'))

if __name__ == '__main__':
    run_all(sys.argv[1:]); previews()
