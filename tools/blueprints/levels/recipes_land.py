"""The land: Farmer, Forester (small, large), Gatherer, Hunter."""
from parts import *
from recipes_homes import recipe, RECIPES


def field(d, box, y=-1, fence='wattle-sticks', gap=None):
    """A tilled field in rows with a path down the middle and a fence round it."""
    garden(d, box, y)
    x0, z0, x1, z1 = box
    d.fence((x0 - 1, z0 - 1, x1 + 1, z1 + 1), y + 1, fence, gaps=[gap] if gap else [])


# ---------------------------------------------------------------- Farmer
@recipe('farmer')
def farmer(d):
    L = d.level
    if L == 1:
        # A field shelter: a dirt-and-wattle hut at the corner of the first plot, a hay bed, a tool rack, a
        # basket for the seed, the plot fenced with sticks.
        box = (1, 1, 5, 5)
        yard(d, (0, 0, 16, 10))
        house(d, box, storeys=1, height=3, axis=0, level=1)
        d.door((5, 1, 3), 'crude', 'west')
        hut_block(d, (4, 1, 4))
        d.bed((2, 1, 2), 'hay', 'south'); d.place('game:toolrack-west', (2, 2, 4)); d.put('game:stationarybasket-north', [(4, 1, 2)])
        d.torch((3, 2, 2), 'south')
        field(d, (8, 2, 14, 8), gap=(7, 5))
        d.place('game:woodbucket', (6, 0, 6)) if 'game:woodbucket' in CATALOG else None
        grass(d, (0, 0, 16, 10), -1, every=7)
    elif L == 2:
        # A farmhouse: daub cottage with a bed, hearth and table, a wattle byre lean-to with hay for the
        # animals, two fenced fields with a path between, baskets at the door for the harvest.
        box = (1, 1, 7, 9); byre = (7, 3, 11, 7)
        yard(d, (0, 0, 24, 14))
        house(d, box, storeys=1, height=3, axis=1, level=2, no_windows=('east',))
        lean_to(d, byre, 1, 3, 'west', open_sides=('east',), level=2, wall=lambda x, y, z, a: fence_code('wattle-sticks', 'ew' if a else 'ns'))
        d.door((4, 1, 9), 'crude', 'north')
        hut_block(d, (3, 1, 8))
        for y in (1, 2): d.set((4, y, 2), COBBLE)
        d.place('game:firepit-cold', (4, 1, 3)); chimney(d, 4, 2, 3, 7, COBBLE)
        d.bed((2, 1, 5), 'rough', 'south'); table_and_chairs(d, [(4, 1, 6)], 'rough'); d.put_against('game:chest', [(6, 1, 8)])
        d.set((9, 1, 4), HAY); d.set((9, 1, 5), HAY); d.set((9, 2, 4), HAY); d.put('game:trunk-north', [(9, 1, 6)])
        d.put('game:stationarybasket-north', [(5, 0, 10), (6, 0, 10)])
        d.torch((4, 2, 7)); d.torch((4, 2, 10), 'south'); d.torch((9, 2, 6), 'north')
        field(d, (14, 1, 22, 5), gap=(13, 3)); field(d, (14, 9, 22, 13), gap=(13, 11))
        d.path([(x, 7) for x in range(8, 24)] + [(13, 3), (13, 11), (4, 10), (4, 11)], -1)
        grass(d, (0, 0, 24, 14), -1, every=9)
    elif L == 3:
        # A farmstead: rammed-earth house with a cross-wing dairy and store, a timber barn across the yard with a
        # hay loft and wide doors, a trough and well-trodden paths, three fields fenced in oak.
        box = (2, 1, 8, 11); wing = (8, 3, 13, 7); barn = (16, 1, 24, 11)
        yard(d, (0, 0, 40, 20))
        house(d, box, storeys=1, height=3, axis=1, level=3, no_windows=('east',))
        house(d, wing, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        opening(d, [(8, 5)], 1, 2)
        d.door((5, 1, 11), 'solid-oak', 'north')
        hut_block(d, (4, 1, 10))
        for y in (1, 2): d.set((5, y, 2), COBBLE)
        d.place('game:firepit-cold', (5, 1, 3)); chimney(d, 5, 2, 3, 7, COBBLE)
        d.bed((3, 1, 5), 'rough', 'south'); d.bed((7, 1, 5), 'rough', 'south'); table_and_chairs(d, [(5, 1, 8)], 'rough')
        d.put('foodshelves:doubleshelf-normal-east', [(9, 1, 4)]); d.put('game:barrel', [(11, 1, 4)]); d.put('game:crate', [(11, 1, 6)]); d.put('game:stationarybasket-north', [(9, 1, 6)])
        house(d, barn, storeys=1, height=4, axis=1, level=3, walls=dict(WALLS[3], low=PLANK_Z, gable=PLANK_Z), window_sides=())
        d.door((19, 1, 11), 'solid-oak', 'north'); d.door((21, 1, 11), 'solid-oak', 'north')
        for x in range(17, 24):
            for z in range(2, 7): d.set((x, 1, z), HAY) if (x + z) % 2 == 0 else None
        d.floor((17, 2, 23, 6), 4, PLANK_X); d.set((17, 4, 6), None); d.ladder(17, 6, 1, 4, 'north')
        for x in (18, 20, 22): d.set((x, 5, 3), HAY); d.set((x, 5, 4), HAY)
        d.put('game:trunk-north', [(22, 1, 9)]); d.put('game:stationarybasket-north', [(18, 1, 9), (19, 1, 9)])
        d.lantern((5, 3, 7)); d.lantern((20, 4, 8)); d.lantern((5, 2, 12), 'south')
        field(d, (28, 2, 38, 8), fence='woodenfence-oak', gap=(27, 5)); field(d, (28, 12, 38, 18), fence='woodenfence-oak', gap=(27, 15))
        field(d, (3, 14, 12, 18), fence='woodenfence-oak', gap=(7, 13))
        d.path([(x, 10) for x in range(9, 27)] + [(26, z) for z in range(3, 18)] + [(5, 12), (5, 13), (6, 12), (6, 13), (7, 12)], -1)
        grass(d, (0, 0, 40, 20), -1, every=11)
    elif L == 4:
        # A manor farm: two-storey cobble-and-plank farmhouse, a great barn with a threshing floor and lofts
        # either side, a granary on staddle posts, a walled stackyard, four big fields with gates.
        box = (2, 1, 10, 13); barn = (14, 1, 24, 15); gran = (27, 2, 31, 6)
        yard(d, (0, 0, 48, 24))
        house(d, box, storeys=2, height=3, axis=1, level=4)
        d.door((6, 1, 13), 'solid-oak', 'north')
        hut_block(d, (5, 1, 12))
        for y in (1, 2, 3): d.set((6, y, 2), BRICK)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 10, COBBLE)
        d.put('game:clayoven-east', [(3, 1, 3)]); table_and_chairs(d, [(6, 1, 7)], 'rough', double=True); d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 9)])
        d.stair_run((9, 1, 11), 'north', 3, 'planks')
        for z in (8, 9, 10, 11): d.set((9, 4, z), None)
        d.beds(3, [(3, 5, 3), (3, 5, 7), (7, 5, 3)], 'wood', 'south'); d.put_against('game:chest', [(3, 5, 11)])
        d.lantern((6, 3, 9)); d.lantern((6, 7, 7)); d.lantern((6, 2, 14), 'south')
        porch(d, (4, 14, 8, 15), 1, 3, 'north')
        house(d, barn, storeys=1, height=5, axis=1, level=4, walls=dict(WALLS[4], low=PLANK_Z, up=PLANK_Z, gable=PLANK_Z), window_sides=())
        d.door((18, 1, 15), 'solid-oak', 'north'); d.door((20, 1, 15), 'solid-oak', 'north'); d.door((19, 1, 1), 'solid-oak', 'south')
        d.floor((15, 2, 17, 13), 5, PLANK_X); d.floor((21, 2, 23, 13), 5, PLANK_X)
        d.ladder(15, 13, 1, 5, 'north'); d.set((15, 5, 13), None); d.ladder(23, 13, 1, 5, 'north'); d.set((23, 5, 13), None)
        for x in (15, 16, 22, 23):
            for z in range(3, 12, 2): d.set((x, 6, z), HAY)
        for x in (15, 16, 22, 23):
            for z in range(3, 12, 2): d.set((x, 1, z), HAY)
        d.put('game:trunk-north', [(17, 1, 13)]); d.put('game:stationarybasket-north', [(21, 1, 13), (22, 1, 13)])
        d.lantern((19, 4, 5)); d.lantern((19, 4, 11))
        # granary on posts
        for (x, z) in ((27, 2), (31, 2), (27, 6), (31, 6), (29, 4)):
            for y in (0, 1): d.set((x, y, z), LOG)
        d.floor(gran, 2, PLANK_X); d.frame(gran, 3, 5, PLANK_Z, post_every=0); d.gable_roof(gran, 5, 0, ends='jerkin', hip_layers=1, gable=PLANK_Z)
        d.rooms.append((gran, 5)); d.ladder(29, 7, 0, 3, 'north'); d.set((29, 3, 6), None); d.set((29, 2, 6), None)
        d.put('foodshelves:floursack-normal-east', [(28, 3, 3), (30, 3, 3)])
        d.fence((26, 9, 34, 15), 0, 'woodenfence-oak', gaps=[(26, 12)])
        for x in (28, 30, 32):
            for z in (11, 13): d.set((x, 0, z), HAY); d.set((x, 1, z), HAY)
        field(d, (37, 2, 46, 10), fence='woodenfence-oak', gap=(36, 6)); field(d, (37, 14, 46, 22), fence='woodenfence-oak', gap=(36, 18))
        field(d, (3, 17, 12, 22), fence='woodenfence-oak', gap=(7, 16)); field(d, (15, 18, 24, 22), fence='woodenfence-oak', gap=(19, 17))
        d.path([(x, 16) for x in range(5, 36)] + [(35, z) for z in range(3, 22)] + [(6, 15), (7, 15), (19, 16), (19, 17), (6, 14), (7, 14)], -1)
        d.fence((1, 0, 33, 16), 0, 'woodenfence-oak', gaps=[(x, 16) for x in range(4, 10)] + [(33, 8), (33, 9), (18, 16), (19, 16), (20, 16)])
        grass(d, (0, 0, 48, 24), -1, every=12)
    else:
        # A great farm: stone-brick farmhouse with a mud-brick upper storey and a dairy wing, a stone-footed great
        # barn, two granaries, a walled stackyard with hay ricks, a cart shed, six fields behind drystone walls.
        box = (2, 1, 10, 15); wing = (10, 9, 16, 15); barn = (20, 1, 30, 17); shed = (33, 1, 39, 7)
        yard(d, (0, 0, 56, 30))
        house(d, box, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        house(d, wing, storeys=1, height=3, axis=0, level=5, no_windows=('west',))
        opening(d, [(10, 11), (10, 12), (10, 13)], 1, 2)
        d.door((6, 1, 15), 'solid-oak', 'north')
        hut_block(d, (5, 1, 14))
        for y in (1, 2, 3): d.set((6, y, 2), POLISHED)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 11, BRICK)
        d.put('game:clayoven-east', [(3, 1, 3)]); table_and_chairs(d, [(6, 1, 8)], 'rustic', double=True); d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 10)])
        d.put('game:barrel', [(12, 1, 10), (14, 1, 10)]); d.put('foodshelves:coolingcabinet-normal-west', [(15, 1, 12)]) or d.put('game:crate', [(15, 1, 12)]); d.put('game:stationarybasket-north', [(12, 1, 14), (13, 1, 14)])
        d.stair_run((9, 1, 13), 'north', 3, 'brick')
        for z in (10, 11, 12, 13): d.set((9, 4, z), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (7, 5, 3)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 13)])
        d.lantern((6, 3, 11)); d.lantern((6, 7, 8)); d.lantern((13, 3, 12)); d.lantern((6, 2, 16), 'south')
        porch(d, (4, 16, 8, 17), 1, 3, 'north')
        house(d, barn, storeys=1, height=5, axis=1, level=5, walls=dict(WALLS[5], low=lambda x, y, z, a: BRICK if y == 1 else PLANK_Z, gable=PLANK_Z), window_sides=())
        d.door((24, 1, 17), 'solid-oak', 'north'); d.door((26, 1, 17), 'solid-oak', 'north'); d.door((25, 1, 1), 'solid-oak', 'south')
        d.floor((21, 2, 23, 15), 5, PLANK_X); d.floor((27, 2, 29, 15), 5, PLANK_X)
        d.ladder(21, 15, 1, 5, 'north'); d.set((21, 5, 15), None); d.ladder(29, 15, 1, 5, 'north'); d.set((29, 5, 15), None)
        for x in (21, 22, 28, 29):
            for z in range(3, 14, 2): d.set((x, 6, z), HAY); d.set((x, 1, z), HAY)
        d.put('game:trunk-north', [(23, 1, 15)]); d.put('game:stationarybasket-north', [(27, 1, 15), (28, 1, 15)])
        d.lantern((25, 4, 5)); d.lantern((25, 4, 10)); d.lantern((25, 4, 14))
        for gx in (33, 40):
            gran = (gx, 10, gx + 4, 14)
            for (x, z) in ((gx, 10), (gx + 4, 10), (gx, 14), (gx + 4, 14), (gx + 2, 12)):
                for y in (0, 1): d.set((x, y, z), BRICK)
            d.floor(gran, 2, PLANK_X); d.frame(gran, 3, 5, PLANK_Z, post_every=0); d.gable_roof(gran, 5, 0, ends='jerkin', hip_layers=1, gable=PLANK_Z)
            d.rooms.append((gran, 5)); d.ladder(gx + 2, 15, 0, 3, 'north'); d.set((gx + 2, 3, 14), None); d.set((gx + 2, 2, 14), None)
            d.put('foodshelves:floursack-normal-east', [(gx + 1, 3, 11), (gx + 3, 3, 11)])
        lean_to(d, shed, 1, 3, 'north', open_sides=('south',), level=5, wall=PLANK_X)
        d.put('game:crate', [(35, 1, 3), (37, 1, 3)]); d.put('game:trunk-north', [(34, 1, 5)])
        d.fence((33, 18, 45, 26), 0, 'drystonefence-shale', gaps=[(33, 22)])
        for x in (35, 38, 41, 44):
            for z in (20, 24): d.set((x, 0, z), HAY); d.set((x, 1, z), HAY); d.set((x, 2, z), HAY)
        field(d, (44, 2, 54, 10), fence='drystonefence-shale', gap=(43, 6)); field(d, (49, 14, 54, 26), fence='drystonefence-shale', gap=(48, 20))
        field(d, (3, 20, 12, 28), fence='drystonefence-shale', gap=(7, 19)); field(d, (16, 21, 25, 28), fence='drystonefence-shale', gap=(20, 20))
        field(d, (2, 2, 0, 1), fence='drystonefence-shale') if False else None
        d.path([(x, 18) for x in range(5, 48)] + [(47, z) for z in range(3, 27)] + [(32, z) for z in range(3, 18)] + [(6, 17), (7, 17), (25, 18), (25, 19)], -1)
        d.fence((1, 0, 31, 18), 0, 'drystonefence-shale', gaps=[(x, 18) for x in range(4, 10)] + [(31, 8), (31, 9), (24, 18), (25, 18), (26, 18)])
        grass(d, (0, 0, 56, 30), -1, every=13)
    return d


# ---------------------------------------------------------------- Forester, small
@recipe('forester', 'small')
def forester_small(d):
    L = d.level
    if L == 1:
        # A woodcutter's lean-to: three log walls and a shed roof against a stack of felled trunks, a hay bed,
        # an axe on the rack, a firepit ring of stones.
        box = (1, 1, 5, 5)
        yard(d, (0, 0, 11, 9))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT, wall=LOG_X)
        hut_block(d, (4, 1, 4))
        d.bed((2, 1, 2), 'hay', 'east') or d.bed((2, 1, 2), 'hay'); d.place('game:toolrack-west', (4, 2, 2))
        woodpile(d, [(7, 1), (8, 1), (9, 1), (7, 2), (8, 2), (9, 2)], 0, 2, 'we')
        for p in ((2, 0, 7), (4, 0, 7), (3, 0, 6), (3, 0, 8)): d.set(p, COBBLE)
        d.place('game:firepit-cold', (3, 0, 7))
        d.torch((3, 2, 1), 'south')
        d.place('game:stationarybasket-north', (9, 0, 5))
        grass(d, (0, 0, 11, 9), -1, every=6)
    elif L == 2:
        # A log cabin: 7x7 of stacked oak logs with a hipped-top roof, a bunk, a stove-corner hearth, a
        # sapling nursery of baskets in a wattle pen, a log stack under a wattle lean-to.
        box = (1, 1, 7, 7)
        yard(d, (0, 0, 16, 12))
        house(d, box, storeys=1, height=3, axis=0, level=2, walls=dict(WALLS[2], low=lambda x, y, z, a: LOG_X if a else LOG_Z, gable=PLANK_Z), post_every=0)
        d.door((7, 1, 4), 'crude', 'west')
        hut_block(d, (5, 1, 5))
        for y in (1, 2): d.set((1, y, 2), COBBLE)
        d.place('game:firepit-cold', (2, 1, 2)); chimney(d, 1, 2, 3, 6, COBBLE)
        d.bed((2, 1, 5), 'rough', 'north') or d.bed((2, 1, 5), 'rough'); d.put_against('game:chest', [(5, 1, 2)]); d.place('game:toolrack-west', (6, 2, 6))
        d.torch((4, 2, 6), 'north')
        d.fence((10, 1, 15, 5), 0, 'wattle-sticks', gaps=[(10, 3)])
        for x in (11, 13): d.place('game:stationarybasket-north', (x, 0, 2)); d.place('game:stationarybasket-north', (x, 0, 4))
        lean_to(d, (10, 8, 15, 11), 1, 2, 'south', open_sides=('north', 'east', 'west'), level=2, wall=None)
        woodpile(d, [(11, 9), (12, 9), (13, 9), (14, 9), (11, 10), (12, 10), (13, 10), (14, 10)], 0, 1, 'we')
        grass(d, (0, 0, 16, 12), -1, every=8)
    elif L == 3:
        # A forester's lodge: a rammed-earth house with a plank upper gable, a log store wing open on one side, a
        # fenced nursery of saplings in rows, a sawing trestle, lanterns, an oak fence with a gate.
        box = (2, 1, 8, 11); store = (8, 3, 14, 9)
        yard(d, (0, 0, 22, 16))
        house(d, box, storeys=1, height=3, axis=1, level=3, no_windows=('east',))
        lean_to(d, store, 1, 3, 'west', open_sides=('east',), level=3, wall=PLANK_Z)
        d.door((5, 1, 11), 'solid-oak', 'north')
        hut_block(d, (4, 1, 10))
        for y in (1, 2): d.set((5, y, 2), COBBLE)
        d.place('game:firepit-cold', (5, 1, 3)); chimney(d, 5, 2, 3, 7, COBBLE)
        d.bed((3, 1, 5), 'rough', 'south'); d.bed((7, 1, 5), 'rough', 'south'); table_and_chairs(d, [(5, 1, 8)], 'rough')
        d.place('game:toolrack-west', (7, 2, 9)); d.put_against('game:chest', [(3, 1, 9)])
        woodpile(d, [(10, 4), (11, 4), (12, 4), (10, 5), (11, 5), (12, 5)], 0, 2, 'ns'); d.put('game:trunk-west', [(12, 1, 8)])
        d.lantern((5, 3, 7)); d.lantern((11, 3, 6)); d.lantern((5, 2, 12), 'south')
        d.fence((16, 1, 21, 11), 0, 'woodenfence-oak', gaps=[(16, 6)])
        for x in (17, 19):
            for z in (2, 4, 6, 8, 10): d.place('game:stationarybasket-north', (x, 0, z))
        for (x, z) in ((10, 13), (14, 13)):
            for y in (0, 1): d.set((x, y, z), LOG)
        for x in range(10, 15): d.set((x, 2, 13), LOG_X)
        d.fence((1, 0, 22, 15), 0, 'woodenfence-oak', gaps=[(5, 15), (6, 15)])
        d.path([(5, z) for z in range(12, 16)] + [(6, z) for z in range(12, 16)], -1)
        grass(d, (0, 0, 22, 16), -1, every=10)
    elif L == 4:
        # A forester's house: two storeys, cobble below with the workshop and store, planks above for the
        # foresters' beds, a long timber-drying shed on posts across the yard, a big nursery, a porch.
        box = (2, 1, 10, 11); dry = (14, 1, 24, 7)
        yard(d, (0, 0, 28, 20))
        house(d, box, storeys=2, height=3, axis=1, level=4)
        d.door((6, 1, 11), 'solid-oak', 'north')
        hut_block(d, (5, 1, 10))
        for y in (1, 2, 3): d.set((6, y, 2), BRICK)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 9, COBBLE)
        table_and_chairs(d, [(6, 1, 6)], 'rough'); d.place('game:toolrack-west', (9, 2, 4)); d.place('game:toolrack-west', (9, 2, 5))
        d.put_against('game:chest', [(3, 1, 9)]); d.put('game:trunk-north', [(3, 1, 4)])
        d.stair_run((9, 1, 9), 'north', 3, 'planks')
        for z in (6, 7, 8, 9): d.set((9, 4, z), None)
        d.beds(3, [(3, 5, 3), (3, 5, 6), (7, 5, 3)], 'wood', 'south'); d.put_against('game:chest', [(3, 5, 9)])
        d.lantern((6, 3, 8)); d.lantern((6, 7, 6)); d.lantern((6, 2, 12), 'south')
        porch(d, (4, 12, 8, 13), 1, 3, 'north')
        house(d, dry, storeys=1, height=3, axis=0, level=4, walls=dict(WALLS[4], low=None, gable=PLANK_Z), post_every=3, window_sides=())
        for (x, z) in ring(dry):
            for y in (1, 2):
                if d.get((x, y, z)) == COBBLE: d.set((x, y, z), None)
        d.floor_inside(dry, 0, COBBLE)
        woodpile(d, [(x, 3) for x in range(15, 24)], 1, 1, 'we'); woodpile(d, [(x, 5) for x in range(15, 24)], 1, 2, 'we')
        d.lantern((19, 3, 4))
        d.fence((14, 10, 26, 18), 0, 'woodenfence-oak', gaps=[(14, 14)])
        for x in range(15, 26, 2):
            for z in (11, 13, 15, 17): d.place('game:stationarybasket-north', (x, 0, z))
        d.fence((1, 0, 28, 19), 0, 'woodenfence-oak', gaps=[(6, 19), (7, 19), (28, 8), (28, 9)])
        d.path([(x, z) for x in (6, 7) for z in range(14, 20)] + [(x, 9) for x in range(11, 27)] + [(13, z) for z in range(2, 19)], -1)
        grass(d, (0, 0, 28, 20), -1, every=11)
    else:
        # A forestry: stone-brick lodge with a mud-brick upper storey, a stone tower lookout over the woods, a
        # roofed sawpit shed, a long drying barn, a walled nursery of saplings, drystone walls all round.
        box = (2, 1, 10, 13); tow = (10, 9, 14, 13); barn = (18, 1, 30, 7); pit = (18, 11, 24, 17)
        yard(d, (0, 0, 34, 26))
        house(d, box, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        tower(d, tow, 1, 12, BRICK, roof='hip', floors_every=4, ladder=(13, 'north', 12))
        opening(d, [(10, 11)], 1, 2); opening(d, [(10, 11)], 5, 6)
        for y in range(9, 11):
            for (x, z) in ring(tow):
                if (x + z) % 2 == 0 and d.get((x, y, z)) == BRICK: d.set((x, y, z), None)
        d.door((6, 1, 13), 'solid-oak', 'north')
        hut_block(d, (5, 1, 12))
        for y in (1, 2, 3): d.set((6, y, 2), POLISHED)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 10, BRICK)
        table_and_chairs(d, [(6, 1, 7)], 'rustic', double=True); d.place('game:toolrack-west', (9, 2, 4)); d.place('game:toolrack-west', (9, 2, 5))
        d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 10)]); d.put('game:trunk-north', [(3, 1, 4)])
        d.stair_run((9, 1, 11), 'north', 3, 'brick')
        for z in (8, 9, 10, 11): d.set((9, 4, z), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (7, 5, 3)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(7, 5, 11)])
        d.lantern((6, 3, 9)); d.lantern((6, 7, 7)); d.lantern((12, 3, 11)); d.lantern((12, 7, 11)); d.lantern((6, 2, 14), 'south')
        house(d, barn, storeys=1, height=4, axis=0, level=5, walls=dict(WALLS[5], low=lambda x, y, z, a: BRICK if y == 1 else PLANK_Z, gable=PLANK_Z), window_sides=())
        d.door((18, 1, 4), 'solid-oak', 'east'); d.door((30, 1, 4), 'solid-oak', 'west')
        woodpile(d, [(x, 2) for x in range(20, 29)], 1, 2, 'we'); woodpile(d, [(x, 6) for x in range(20, 29)], 1, 2, 'we'); woodpile(d, [(x, 4) for x in range(20, 29)], 1, 1, 'we')
        d.lantern((24, 4, 4))
        lean_to(d, pit, 1, 3, 'north', open_sides=('south', 'east', 'west'), level=5, wall=None)
        d.box_fill((19, 12, 23, 16), -2, -1, None); d.plain_walls((19, 12, 23, 16), -2, -1, BRICK); d.floor((19, 12, 23, 16), -3, BRICK)
        for x in range(19, 24): d.set((x, 1, 14), LOG_X)
        d.fence((27, 10, 33, 24), 0, 'drystonefence-shale', gaps=[(27, 17)])
        for x in (28, 30, 32):
            for z in range(11, 24, 2): d.place('game:stationarybasket-north', (x, 0, z))
        d.fence((1, 0, 34, 25), 0, 'drystonefence-shale', gaps=[(6, 25), (7, 25), (34, 4), (34, 5)])
        d.path([(x, z) for x in (6, 7) for z in range(14, 26)] + [(x, 9) for x in range(8, 34)] + [(16, z) for z in range(2, 25)] + [(26, z) for z in range(10, 25)], -1)
        grass(d, (0, 0, 34, 26), -1, every=12)
    return d


# ---------------------------------------------------------------- Forester, large: the timber camp
@recipe('forester', 'large')
def forester_large(d):
    L = d.level
    if L == 1:
        # A timber camp: two open log shelters facing a fire, a stack of trunks between them, baskets of
        # saplings, nothing closed in yet.
        yard(d, (0, 0, 15, 11))
        lean_to(d, (1, 1, 5, 5), 1, 3, 'west', open_sides=('east', 'south'), level=1, floor=DIRT, wall=LOG_Z)
        lean_to(d, (9, 1, 13, 5), 1, 3, 'east', open_sides=('west', 'south'), level=1, floor=DIRT, wall=LOG_Z)
        hut_block(d, (4, 1, 4))
        d.bed((2, 1, 2), 'hay', 'south'); d.bed((12, 1, 2), 'hay', 'south') or d.bed((11, 1, 2), 'hay')
        d.place('game:toolrack-west', (4, 2, 2)); d.put('game:trunk-north', [(10, 1, 4)])
        woodpile(d, [(6, 2), (7, 2), (8, 2), (6, 3), (7, 3), (8, 3)], 0, 2, 'ns')
        d.place('game:firepit-cold', (7, 0, 8))
        d.place('game:stationarybasket-north', (2, 0, 9)); d.place('game:stationarybasket-north', (12, 0, 9))
        d.torch((2, 2, 1), 'south'); d.torch((12, 2, 1), 'south')
        grass(d, (0, 0, 15, 11), -1, every=6)
    elif L == 2:
        # A logging station: a long open-fronted log shed on posts with the bunk room closed in at one end,
        # trunks stacked down its length, a sawing trestle in the yard, a wattle nursery pen.
        box = (1, 1, 15, 7)
        yard(d, (0, 0, 20, 14))
        house(d, box, storeys=1, height=3, axis=0, level=2, walls=dict(WALLS[2], low=lambda x, y, z, a: (LOG_X if a else LOG_Z) if x <= 5 or z == 1 else None, gable=PLANK_Z), post_every=3, window_sides=())
        for (x, z) in ring(box):
            if z == 7 and x > 5:
                for y in (1, 2):
                    if d.get((x, y, z)) in (DAUB, DAUB_WATTLE): d.set((x, y, z), None)
        for y in (1, 2): d.set((5, y, 3), LOG_Z); d.set((5, y, 5), LOG_Z); d.set((5, y, 4), None)
        d.door((5, 1, 4), 'crude', 'east')
        hut_block(d, (7, 1, 6))
        d.bed((2, 1, 2), 'rough', 'south'); d.bed((4, 1, 2), 'rough', 'south'); d.put_against('game:chest', [(2, 1, 6)])
        woodpile(d, [(x, 2) for x in range(7, 15)], 1, 2, 'we'); woodpile(d, [(x, 4) for x in range(7, 14)], 1, 1, 'we')
        d.place('game:toolrack-west', (14, 2, 6))
        d.torch((3, 2, 4), 'east'); d.torch((10, 2, 2), 'south')
        for (x, z) in ((7, 11), (11, 11)):
            for y in (0, 1): d.set((x, y, z), LOG)
        for x in range(7, 12): d.set((x, 2, 11), LOG_X)
        d.fence((14, 9, 19, 13), 0, 'wattle-sticks', gaps=[(14, 11)])
        for x in (15, 17): d.place('game:stationarybasket-north', (x, 0, 10)); d.place('game:stationarybasket-north', (x, 0, 12))
        d.place('game:firepit-cold', (3, 0, 11))
        grass(d, (0, 0, 20, 14), -1, every=8)
    elif L == 3:
        # A lumber yard: a rammed-earth bunkhouse for four, a long log barn open on the yard side, a covered
        # sawpit, a nursery in rows behind an oak fence, lanterns on the posts.
        bunk = (2, 1, 8, 11); barn = (12, 1, 26, 7); pit = (12, 11, 18, 15)
        yard(d, (0, 0, 32, 20))
        house(d, bunk, storeys=1, height=3, axis=1, level=3)
        d.door((5, 1, 11), 'solid-oak', 'north')
        hut_block(d, (4, 1, 10))
        for y in (1, 2): d.set((5, y, 2), COBBLE)
        d.place('game:firepit-cold', (5, 1, 3)); chimney(d, 5, 2, 3, 7, COBBLE)
        d.beds(4, [(3, 1, 5), (7, 1, 5), (3, 1, 8), (7, 1, 8)], 'rough', 'south'); d.put_against('game:chest', [(7, 1, 10)])
        d.lantern((5, 3, 7)); d.lantern((5, 2, 12), 'south')
        lean_to(d, barn, 1, 4, 'north', open_sides=('south',), level=3, wall=PLANK_X)
        woodpile(d, [(x, 2) for x in range(13, 26)], 1, 3, 'we'); woodpile(d, [(x, 4) for x in range(13, 26)], 1, 2, 'we'); woodpile(d, [(x, 6) for x in range(13, 26)], 1, 1, 'we')
        d.lantern((19, 4, 7), 'south')
        lean_to(d, pit, 1, 3, 'north', open_sides=('south', 'east', 'west'), level=3, wall=None)
        d.box_fill((13, 12, 17, 14), -2, -1, None); d.plain_walls((13, 12, 17, 14), -2, -1, COBBLE); d.floor((13, 12, 17, 14), -3, COBBLE)
        for x in range(13, 18): d.set((x, 1, 13), LOG_X)
        d.fence((21, 10, 31, 18), 0, 'woodenfence-oak', gaps=[(21, 14)])
        for x in (22, 24, 26, 28, 30):
            for z in (11, 13, 15, 17): d.place('game:stationarybasket-north', (x, 0, z))
        d.place('game:toolrack-west', (8, 2, 14)) if False else None
        d.fence((1, 0, 32, 19), 0, 'woodenfence-oak', gaps=[(5, 19), (6, 19), (32, 4), (32, 5)])
        d.path([(x, z) for x in (5, 6) for z in range(12, 20)] + [(x, 9) for x in range(7, 32)] + [(20, z) for z in range(10, 19)], -1)
        grass(d, (0, 0, 32, 20), -1, every=10)
    elif L == 4:
        # A timber works: a two-storey cobble-and-plank lodge for the crew, two long drying barns, a sawpit
        # shed, a stone-floored loading yard, a big fenced nursery, a crane beam over the log pile.
        lodge = (2, 1, 10, 13); b1 = (14, 1, 28, 7); b2 = (14, 11, 28, 17); pit = (32, 2, 38, 8)
        yard(d, (0, 0, 42, 26))
        house(d, lodge, storeys=2, height=3, axis=1, level=4)
        d.door((6, 1, 13), 'solid-oak', 'north')
        hut_block(d, (5, 1, 12))
        for y in (1, 2, 3): d.set((6, y, 2), BRICK)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 10, COBBLE)
        table_and_chairs(d, [(6, 1, 7)], 'rough', double=True); d.place('game:toolrack-west', (9, 2, 4)); d.place('game:toolrack-west', (9, 2, 5)); d.put('game:trunk-north', [(3, 1, 4)])
        d.stair_run((9, 1, 11), 'north', 3, 'planks')
        for z in (8, 9, 10, 11): d.set((9, 4, z), None)
        d.beds(5, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (7, 5, 3), (7, 5, 6)], 'wood', 'south'); d.put_against('game:chest', [(3, 5, 12)])
        d.lantern((6, 3, 9)); d.lantern((6, 7, 7)); d.lantern((6, 2, 14), 'south')
        porch(d, (4, 14, 8, 15), 1, 3, 'north')
        for b in (b1, b2):
            house(d, b, storeys=1, height=3, axis=0, level=4, walls=dict(WALLS[4], low=None, gable=PLANK_Z), post_every=3, window_sides=())
            for (x, z) in ring(b):
                for y in (1, 2):
                    if d.get((x, y, z)) == COBBLE: d.set((x, y, z), None)
            d.floor_inside(b, 0, COBBLE)
            woodpile(d, [(x, b[1] + 1) for x in range(15, 28)], 1, 2, 'we'); woodpile(d, [(x, b[1] + 3) for x in range(15, 28)], 1, 1, 'we'); woodpile(d, [(x, b[1] + 5) for x in range(15, 28)], 1, 2, 'we')
            d.lantern((21, 3, b[1] + 3))
        lean_to(d, pit, 1, 3, 'north', open_sides=('south', 'east', 'west'), level=4, wall=None)
        d.box_fill((33, 3, 37, 7), -2, -1, None); d.plain_walls((33, 3, 37, 7), -2, -1, COBBLE); d.floor((33, 3, 37, 7), -3, COBBLE)
        for x in range(33, 38): d.set((x, 1, 5), LOG_X)
        for y in range(0, 7): d.set((31, y, 12), LOG); d.set((31, y, 18), LOG)
        for z in range(12, 19): d.set((31, 7, z), LOG_Z)
        woodpile(d, [(32, z) for z in range(13, 18)] + [(33, z) for z in range(13, 18)], 0, 2, 'ns')
        d.fence((14, 20, 40, 25), 0, 'woodenfence-oak', gaps=[(14, 22)])
        for x in range(15, 40, 2):
            for z in (21, 23): d.place('game:stationarybasket-north', (x, 0, z))
        d.ground((12, 8, 30, 10), -1, COBBLE)
        d.fence((1, 0, 42, 25), 0, 'woodenfence-oak', gaps=[(6, 25), (7, 25), (42, 9), (42, 10)])
        d.path([(x, z) for x in (6, 7) for z in range(16, 26)] + [(x, 9) for x in range(31, 42)] + [(12, z) for z in range(2, 20)] + [(x, 19) for x in range(12, 41)], -1)
        grass(d, (0, 0, 42, 26), -1, every=12)
    else:
        # A forestry hall: a stone-brick hall for the crew with a mud-brick upper floor and a lookout tower, three
        # long drying barns on stone footings, a covered sawpit, a stone crane base, a walled nursery and a
        # cobbled loading yard inside drystone walls.
        hall = (2, 1, 12, 15); tow = (12, 11, 16, 15); pit = (36, 2, 42, 8)
        yard(d, (0, 0, 46, 30))
        house(d, hall, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        tower(d, tow, 1, 13, BRICK, roof='hip', floors_every=4, ladder=(15, 'north', 14))
        opening(d, [(12, 13)], 1, 2); opening(d, [(12, 13)], 5, 6)
        for y in range(10, 12):
            for (x, z) in ring(tow):
                if (x + z) % 2 == 0 and d.get((x, y, z)) == BRICK: d.set((x, y, z), None)
        d.door((7, 1, 15), 'solid-oak', 'north')
        hut_block(d, (6, 1, 14))
        for y in (1, 2, 3): d.set((7, y, 2), POLISHED)
        d.place('game:firepit-cold', (7, 1, 3)); chimney(d, 7, 2, 4, 11, BRICK)
        table_and_chairs(d, [(7, 1, 7)], 'rustic', double=True); table_and_chairs(d, [(7, 1, 10)], 'rustic', double=True)
        d.place('game:toolrack-west', (11, 2, 4)); d.place('game:toolrack-west', (11, 2, 5)); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 12)])
        d.stair_run((11, 1, 13), 'north', 3, 'brick')
        for z in (10, 11, 12, 13): d.set((11, 4, z), None)
        d.beds(6, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (9, 5, 3), (9, 5, 6), (9, 5, 9)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 13)])
        for y in (3, 7): d.lantern((7, y, 8)); d.lantern((14, y, 13))
        d.lantern((7, 2, 16), 'south'); d.lantern((14, 11, 13))
        for z0 in (1, 11, 21):
            b = (18, z0, 32, z0 + 6)
            house(d, b, storeys=1, height=3, axis=0, level=5, walls=dict(WALLS[5], low=lambda x, y, z, a: BRICK if y == 1 else None, gable=PLANK_Z), post_every=3, window_sides=())
            d.floor_inside(b, 0, COBBLE)
            woodpile(d, [(x, z0 + 1) for x in range(19, 32)], 2, 1, 'we'); woodpile(d, [(x, z0 + 3) for x in range(19, 32)], 2, 1, 'we'); woodpile(d, [(x, z0 + 5) for x in range(19, 32)], 2, 1, 'we')
            d.lantern((25, 3, z0 + 3))
        lean_to(d, pit, 1, 3, 'north', open_sides=('south', 'east', 'west'), level=5, wall=None)
        d.box_fill((37, 3, 41, 7), -2, -1, None); d.plain_walls((37, 3, 41, 7), -2, -1, BRICK); d.floor((37, 3, 41, 7), -3, BRICK)
        for x in range(37, 42): d.set((x, 1, 5), LOG_X)
        for p in ((35, 0, 12), (35, 0, 13), (36, 0, 12), (36, 0, 13)): d.set(p, BRICK)
        for y in range(1, 8): d.set((35, y, 12), LOG)
        for z in range(12, 19): d.set((35, 8, z), LOG_Z)
        woodpile(d, [(36, z) for z in range(14, 19)] + [(37, z) for z in range(14, 19)], 0, 2, 'ns')
        d.fence((36, 21, 45, 29), 0, 'drystonefence-shale', gaps=[(36, 25)])
        for x in range(37, 45, 2):
            for z in (22, 24, 26, 28): d.place('game:stationarybasket-north', (x, 0, z))
        d.ground((17, 8, 33, 10), -1, COBBLE); d.ground((17, 18, 33, 20), -1, COBBLE)
        d.fence((1, 0, 46, 29), 0, 'drystonefence-shale', gaps=[(7, 29), (8, 29), (46, 9), (46, 10)])
        d.path([(x, z) for x in (7, 8) for z in range(17, 30)] + [(x, 9) for x in range(34, 46)] + [(16, z) for z in range(2, 29)] + [(x, 28) for x in range(9, 36)], -1)
        grass(d, (0, 0, 46, 30), -1, every=13)
    return d


# ---------------------------------------------------------------- Gatherer
@recipe('gatherer')
def gatherer(d):
    L = d.level
    if L == 1:
        # A gatherer's bothy: a round-cornered dirt hut with a wattle drying rack outside, baskets, a bed of
        # reeds, a firepit.
        box = (1, 1, 5, 5)
        yard(d, (0, 0, 10, 9))
        house(d, box, storeys=1, height=3, axis=1, level=1, post_every=0)
        d.door((3, 1, 5), 'crude', 'north')
        hut_block(d, (4, 1, 4))
        d.bed((2, 1, 2), 'hay', 'south'); d.put('game:stationarybasket-north', [(4, 1, 2)])
        d.torch((2, 2, 4), 'east')
        for (x, z) in ((7, 2), (7, 6)):
            for y in (0, 1): d.set((x, y, z), LOG)
        for z in range(3, 6): d.set((7, 1, z), fence_code('wattle-sticks', 'ns')); d.set((7, 2, z), fence_code('wattle-sticks', 'ns'))
        d.place('game:stationarybasket-north', (8, 0, 3)); d.place('game:stationarybasket-north', (8, 0, 5))
        d.place('game:firepit-cold', (3, 0, 8))
        grass(d, (0, 0, 10, 9), -1, every=5)
    elif L == 2:
        # A gatherer's cottage: daub house with a bed, hearth and a wall of baskets and shelves, a drying shed
        # open on two sides with wattle racks, a herb patch by the door.
        box = (1, 1, 7, 9); dry = (10, 2, 15, 8)
        yard(d, (0, 0, 17, 13))
        house(d, box, storeys=1, height=3, axis=1, level=2)
        d.door((4, 1, 9), 'crude', 'north')
        hut_block(d, (3, 1, 8))
        for y in (1, 2): d.set((4, y, 2), COBBLE)
        d.place('game:firepit-cold', (4, 1, 3)); chimney(d, 4, 2, 3, 7, COBBLE)
        d.bed((2, 1, 5), 'rough', 'south'); d.put('game:stationarybasket-north', [(6, 1, 3), (6, 1, 4), (6, 1, 5)]); d.put('game:shelf-normal-west', [(2, 1, 7)]) or d.put('foodshelves:doubleshelf-normal-east', [(2, 1, 7)])
        d.torch((4, 2, 6)); d.torch((4, 2, 10), 'south')
        lean_to(d, dry, 1, 3, 'east', open_sides=('west', 'south'), level=2, wall=lambda x, y, z, a: fence_code('wattle-sticks', 'ew' if a else 'ns'))
        for z in (4, 6):
            for x in range(11, 15): d.set((x, 2, z), fence_code('wattle-sticks', 'ew'))
        d.place('game:stationarybasket-north', (11, 1, 7)); d.place('game:stationarybasket-north', (13, 1, 7))
        garden(d, (2, 11, 6, 12), -1, rows=False)
        d.fence((0, 0, 17, 12), 0, 'wattle-sticks', gaps=[(8, 12), (9, 12)])
        d.path([(4, 10), (8, z := 11), (8, 12), (9, 11), (9, 12)] + [(x, 10) for x in range(5, 10)], -1)
        grass(d, (0, 0, 17, 13), -1, every=8)
    elif L == 3:
        # A gatherers' house: rammed earth with a sorting room wing (table, shelves, baskets in rows), a root
        # cellar with a ladder, a long drying shed, a herb garden, oak fence and paths, lanterns.
        box = (2, 1, 8, 11); wing = (8, 3, 14, 9); dry = (17, 1, 23, 11)
        yard(d, (0, 0, 26, 16))
        house(d, box, storeys=1, height=3, axis=1, level=3, no_windows=('east',))
        house(d, wing, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        opening(d, [(8, 6)], 1, 2)
        d.door((5, 1, 11), 'solid-oak', 'north'); d.door((11, 1, 9), 'solid-oak', 'north')
        hut_block(d, (4, 1, 10))
        for y in (1, 2): d.set((5, y, 2), COBBLE)
        d.place('game:firepit-cold', (5, 1, 3)); chimney(d, 5, 2, 3, 7, COBBLE)
        d.bed((3, 1, 5), 'rough', 'south'); d.bed((7, 1, 5), 'rough', 'south'); table_and_chairs(d, [(5, 1, 8)], 'rough')
        d.put('w4rd0sfurniture:roughtable-north', [(10, 1, 5)]); d.put('game:stationarybasket-north', [(9, 1, 8), (10, 1, 8), (12, 1, 8), (13, 1, 8)])
        d.put('foodshelves:doubleshelf-normal-east', [(13, 1, 4)]); d.put('game:shelf-normal-west', [(9, 1, 4)])
        d.box_fill((3, 2, 7, 10), -3, -1, None); d.plain_walls((3, 2, 7, 10), -3, -1, COBBLE); d.floor((3, 2, 7, 10), -4, COBBLE)
        d.set((7, 0, 9), None); d.ladder(7, 9, -3, 0, 'north')
        d.put('game:barrel', [(4, -3, 3), (6, -3, 3)]); d.put('game:stationarybasket-north', [(4, -3, 8), (5, -3, 8)]); d.put('game:crate', [(4, -3, 5)])
        d.lantern((5, 3, 7)); d.lantern((11, 3, 6)); d.lantern((5, 2, 12), 'south'); d.lantern((5, -1, 6))
        lean_to(d, dry, 1, 3, 'east', open_sides=('west',), level=3, wall=PLANK_Z)
        for z in range(3, 10, 2):
            for x in range(18, 23): d.set((x, 2, z), fence_code('wattle-sticks', 'ew'))
        d.put('game:stationarybasket-north', [(18, 1, 10), (20, 1, 10), (22, 1, 10)])
        garden(d, (10, 12, 16, 14), -1, rows=False)
        d.fence((1, 0, 25, 15), 0, 'woodenfence-oak', gaps=[(5, 15), (6, 15)])
        d.path([(5, z) for z in range(12, 16)] + [(6, z) for z in range(12, 16)] + [(x, 11) for x in range(9, 17)] + [(16, z) for z in range(2, 12)], -1)
        grass(d, (0, 0, 26, 16), -1, every=10)
    elif L == 4:
        # A gatherers' lodge: two storeys, cobble below with the sorting hall and store, planks above for four
        # beds, a porch, two drying sheds, a walled herb garden in rows, a stone path through it.
        box = (2, 1, 10, 13); d1 = (14, 1, 20, 9); d2 = (23, 1, 29, 9)
        yard(d, (0, 0, 32, 22))
        house(d, box, storeys=2, height=3, axis=1, level=4)
        d.door((6, 1, 13), 'solid-oak', 'north')
        hut_block(d, (5, 1, 12))
        for y in (1, 2, 3): d.set((6, y, 2), BRICK)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 10, COBBLE)
        d.put('w4rd0sfurniture:roughtabledouble-south', [(5, 1, 6)]) or d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 6)])
        d.put('game:stationarybasket-north', [(3, 1, 9), (3, 1, 10), (9, 1, 9), (9, 1, 10)]); d.put('foodshelves:doubleshelf-normal-east', [(3, 1, 4)]); d.put('game:barrel', [(9, 1, 4)])
        d.stair_run((9, 1, 11), 'north', 3, 'planks')
        for z in (8, 9, 10, 11): d.set((9, 4, z), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (7, 5, 3), (7, 5, 6)], 'wood', 'south'); d.put_against('game:chest', [(3, 5, 11)])
        d.lantern((6, 3, 9)); d.lantern((6, 7, 7)); d.lantern((6, 2, 14), 'south')
        porch(d, (4, 14, 8, 15), 1, 3, 'north')
        for b in (d1, d2):
            lean_to(d, b, 1, 3, 'north', open_sides=('south',), level=4, wall=PLANK_X)
            for z in range(2, 8, 2):
                for x in range(b[0] + 1, b[2]): d.set((x, 2, z), fence_code('wattle-sticks', 'ew'))
            d.put('game:stationarybasket-north', [(b[0] + 1, 1, 8), (b[0] + 3, 1, 8), (b[0] + 5, 1, 8)])
            d.lantern((b[0] + 3, 3, 8), 'south')
        garden(d, (14, 13, 28, 19), -1)
        d.fence((13, 12, 29, 20), 0, 'woodenfence-oak', gaps=[(13, 16)])
        d.fence((1, 0, 32, 21), 0, 'woodenfence-oak', gaps=[(6, 21), (7, 21)])
        d.path([(x, z) for x in (6, 7) for z in range(16, 22)] + [(x, 11) for x in range(8, 31)] + [(12, z) for z in range(2, 21)], -1)
        grass(d, (0, 0, 32, 22), -1, every=11)
    else:
        # A gatherers' guild: a stone-brick hall with the sorting floor and a mud-brick dormitory above, a hip-
        # roofed store tower, three drying sheds, a big walled herb and berry garden, lanterns and paths.
        hall = (2, 1, 12, 15); tow = (12, 9, 18, 15)
        yard(d, (0, 0, 40, 28))
        house(d, hall, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        tower(d, tow, 1, 11, BRICK, roof='hip', floors_every=4, ladder=(17, 'north', 14))
        opening(d, [(12, 11), (12, 12), (12, 13)], 1, 2); opening(d, [(12, 11), (12, 12), (12, 13)], 5, 6)
        for z in (11, 12, 13): d.set((12, 4, z), PLANK_X); d.set((12, 8, z), None); d.set((12, 9, z), None)
        d.door((7, 1, 15), 'solid-oak', 'north')
        hut_block(d, (6, 1, 14))
        for y in (1, 2, 3): d.set((7, y, 2), POLISHED)
        d.place('game:firepit-cold', (7, 1, 3)); chimney(d, 7, 2, 4, 11, BRICK)
        d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(5, 1, 6)]) or d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 6)]); d.put('w4rd0sfurniture:rusticsturdytabledouble-east', [(5, 1, 9)]) or None
        d.put('game:stationarybasket-north', [(3, 1, z) for z in range(4, 13)]); d.put('foodshelves:doubleshelf-normal-east', [(11, 1, 4)]); d.put('game:barrel', [(11, 1, 6), (11, 1, 8)])
        d.stair_run((11, 1, 13), 'north', 3, 'brick')
        for z in (10, 11, 12, 13): d.set((11, 4, z), None)
        d.beds(6, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (9, 5, 3), (9, 5, 6), (9, 5, 9)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 13)])
        d.put('game:crate', [(14, 1, 11), (16, 1, 11), (14, 1, 13)]); d.put('game:stationarybasket-north', [(14, 5, 11), (16, 5, 11), (14, 5, 13), (16, 5, 13)])
        for y in (3, 7): d.lantern((7, y, 8)); d.lantern((15, y, 12))
        d.lantern((7, 2, 16), 'south')
        for x0 in (20, 27, 34):
            b = (x0, 1, x0 + 5, 9)
            lean_to(d, b, 1, 3, 'north', open_sides=('south',), level=5, wall=lambda x, y, z, a: BRICK if y == 1 else PLANK_X)
            for z in range(2, 8, 2):
                for x in range(x0 + 1, x0 + 5): d.set((x, 2, z), fence_code('wattle-sticks', 'ew'))
            d.put('game:stationarybasket-north', [(x0 + 1, 1, 8), (x0 + 3, 1, 8)])
            d.lantern((x0 + 2, 3, 8), 'south')
        garden(d, (20, 14, 38, 25), -1)
        d.fence((19, 13, 39, 26), 0, 'drystonefence-shale', gaps=[(19, 19)])
        d.fence((1, 0, 40, 27), 0, 'drystonefence-shale', gaps=[(7, 27), (8, 27)])
        d.path([(x, z) for x in (7, 8) for z in range(16, 28)] + [(x, 11) for x in range(9, 40)] + [(18, z) for z in range(2, 27)], -1)
        grass(d, (0, 0, 40, 28), -1, every=13)
    return d


# ---------------------------------------------------------------- Hunter
@recipe('hunter')
def hunter(d):
    L = d.level
    if L == 1:
        # A hunter's hide: a low log shelter open to a firepit, a skinning frame of posts and a hook rail,
        # a hay bed, a rack for the spear.
        box = (1, 1, 5, 5)
        yard(d, (0, 0, 11, 9))
        lean_to(d, box, 1, 2, 'north', open_sides=('south',), level=1, floor=DIRT, wall=LOG_X)
        hut_block(d, (4, 1, 4))
        d.bed((2, 1, 2), 'hay', 'east') or d.bed((2, 1, 2), 'hay'); d.place('game:toolrack-west', (4, 1, 2)) if d.free((4, 1, 2)) else None
        for (x, z) in ((8, 2), (8, 6)):
            for y in (0, 1, 2): d.set((x, y, z), LOG)
        for z in (3, 4, 5): d.set((8, 3, z), LOG_Z)
        d.place('aculinaryartillery:meathooks-east', (8, 2, 4)) if 'aculinaryartillery:meathooks-east' in CATALOG else None
        d.place('game:firepit-cold', (3, 0, 7))
        d.torch((3, 2, 1), 'south')
        grass(d, (0, 0, 11, 9), -1, every=5)
    elif L == 2:
        # A hunter's cabin: a log cabin with a bunk, a hearth, hooks for game under the eave, a smoking shed of
        # wattle, a skinning rack, a fenced run for the hounds.
        box = (1, 1, 7, 7); smoke = (10, 1, 13, 4)
        yard(d, (0, 0, 17, 12))
        house(d, box, storeys=1, height=3, axis=0, level=2, walls=dict(WALLS[2], low=lambda x, y, z, a: LOG_X if a else LOG_Z, gable=PLANK_Z), post_every=0)
        d.door((7, 1, 4), 'crude', 'west')
        hut_block(d, (5, 1, 5))
        for y in (1, 2): d.set((1, y, 2), COBBLE)
        d.place('game:firepit-cold', (2, 1, 2)); chimney(d, 1, 2, 3, 6, COBBLE)
        d.bed((2, 1, 5), 'rough', 'north') or d.bed((2, 1, 5), 'rough'); d.put_against('game:chest', [(5, 1, 2)])
        d.place('game:toolrack-west', (6, 2, 6)); d.place('aculinaryartillery:meathooks-north', (4, 2, 1)) if 'aculinaryartillery:meathooks-north' in CATALOG else None
        d.torch((4, 2, 6), 'north')
        d.plain_walls(smoke, 1, 3, lambda x, y, z, a: fence_code('wattle-sticks', 'ew' if a else 'ns')); d.hollow(smoke, 1, 3)
        d.floor(smoke, 4, COBBLE); d.place('game:firepit-cold', (11, 1, 2)); d.set((11, 1, 4), None)
        d.place('aculinaryartillery:meathooks-east', (12, 2, 3)) if 'aculinaryartillery:meathooks-east' in CATALOG else None
        for (x, z) in ((10, 8), (14, 8)):
            for y in (0, 1, 2): d.set((x, y, z), LOG)
        for x in range(10, 15): d.set((x, 3, 8), LOG_X)
        d.fence((1, 9, 7, 11), 0, 'wattle-sticks', gaps=[(4, 9)])
        d.place('game:firepit-cold', (8, 0, 10))
        grass(d, (0, 0, 17, 12), -1, every=7)
    elif L == 3:
        # A hunting lodge: rammed-earth lodge with a trophy hall (hooks, racks) and a bunk wing, a stone
        # smokehouse with a slab roof, a tanning yard with racks, a kennel, oak fence and paths.
        hall = (2, 1, 10, 9); wing = (10, 3, 16, 9); smoke = (19, 2, 23, 6)
        yard(d, (0, 0, 26, 16))
        house(d, hall, storeys=1, height=4, axis=0, level=3, no_windows=('east',))
        house(d, wing, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        opening(d, [(10, 6)], 1, 2)
        d.door((6, 1, 9), 'solid-oak', 'north')
        hut_block(d, (9, 1, 8))
        for y in (1, 2): d.set((2, y, 5), COBBLE)
        d.place('game:firepit-cold', (3, 1, 5)); chimney(d, 2, 5, 3, 8, COBBLE)
        table_and_chairs(d, [(6, 1, 5)], 'rough'); d.place('game:toolrack-west', (9, 2, 3)); d.place('game:toolrack-west', (9, 2, 4))
        for x in (4, 6, 8): d.place('aculinaryartillery:meathooks-north', (x, 3, 2)) if 'aculinaryartillery:meathooks-north' in CATALOG else None
        d.beds(3, [(12, 1, 4), (14, 1, 4), (15, 1, 7)], 'rough', 'south'); d.put_against('game:chest', [(11, 1, 8)])
        d.plain_walls(smoke, 1, 3, COBBLE); d.hollow(smoke, 1, 3); d.floor(smoke, 4, COBBLE); d.rooms.append((smoke, 3))
        d.door((21, 1, 6), 'solid-oak', 'north'); d.place('game:firepit-cold', (21, 1, 3))
        d.place('aculinaryartillery:meathooks-east', (22, 2, 4)) if 'aculinaryartillery:meathooks-east' in CATALOG else None
        d.place('aculinaryartillery:meathooks-east', (20, 2, 4)) if 'aculinaryartillery:meathooks-east' in CATALOG else None
        for (x, z) in ((19, 9), (23, 9), (19, 13), (23, 13)):
            for y in (0, 1, 2): d.set((x, y, z), LOG)
        for x in range(19, 24): d.set((x, 3, 9), LOG_X); d.set((x, 3, 13), LOG_X)
        for z in (10, 11, 12): d.set((19, 2, z), fence_code('wattle-sticks', 'ns')); d.set((23, 2, z), fence_code('wattle-sticks', 'ns'))
        d.fence((3, 11, 9, 14), 0, 'woodenfence-oak', gaps=[(6, 11)])
        d.lantern((6, 3, 6)); d.lantern((13, 3, 6)); d.lantern((6, 2, 10), 'south')
        d.fence((1, 0, 25, 15), 0, 'woodenfence-oak', gaps=[(12, 15), (13, 15)])
        d.path([(x, 10) for x in range(6, 19)] + [(12, z) for z in range(11, 16)] + [(13, z) for z in range(11, 16)], -1)
        grass(d, (0, 0, 26, 16), -1, every=9)
    elif L == 4:
        # A hunters' hall: two storeys, cobble trophy hall below with a great hearth, the hunters' beds above, a
        # stone smokehouse, a tanning shed, a kennel yard and a fenced paddock, lanterns and a banner.
        hall = (2, 1, 12, 13); smoke = (16, 2, 20, 6); tan = (16, 9, 22, 13)
        yard(d, (0, 0, 32, 22))
        house(d, hall, storeys=2, height=3, axis=1, level=4)
        d.door((7, 1, 13), 'solid-oak', 'north')
        hut_block(d, (6, 1, 12))
        for y in (1, 2, 3): d.set((7, y, 2), BRICK); d.set((6, y, 2), BRICK); d.set((8, y, 2), BRICK)
        d.place('game:firepit-cold', (7, 1, 3)); chimney(d, 7, 2, 4, 10, COBBLE)
        table_and_chairs(d, [(7, 1, 7)], 'rough', double=True); bench(d, [(4, 1, 5), (10, 1, 5)])
        for x in (4, 10): d.place('game:toolrack-west', (x, 2, 11)) if False else None
        d.place('game:toolrack-west', (11, 2, 5)); d.place('game:toolrack-west', (11, 2, 6)); d.place('game:toolrack-west', (3, 2, 5))
        for z in (4, 7, 10): d.place('aculinaryartillery:meathooks-east', (11, 3, z)) if 'aculinaryartillery:meathooks-east' in CATALOG else None
        d.put_against('game:chest', [(3, 1, 11)]); d.put('game:barrel', [(10, 1, 11)])
        d.stair_run((11, 1, 11), 'north', 3, 'planks')
        for z in (8, 9, 10, 11): d.set((11, 4, z), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (9, 5, 3), (9, 5, 6)], 'wood', 'south'); d.put_against('game:chest', [(3, 5, 11)])
        d.lantern((7, 3, 9)); d.lantern((7, 7, 7)); d.lantern((7, 2, 14), 'south'); banner(d, (9, 3, 14), 'south')
        porch(d, (5, 14, 9, 15), 1, 3, 'north')
        d.plain_walls(smoke, 1, 3, COBBLE); d.hollow(smoke, 1, 3); d.floor(smoke, 4, COBBLE); d.rooms.append((smoke, 3))
        d.door((18, 1, 6), 'solid-oak', 'north'); d.place('game:firepit-cold', (18, 1, 3))
        for x in (17, 19): d.place('aculinaryartillery:meathooks-east', (x, 2, 4)) if 'aculinaryartillery:meathooks-east' in CATALOG else None
        lean_to(d, tan, 1, 3, 'north', open_sides=('south',), level=4, wall=PLANK_X)
        for x in range(17, 22): d.set((x, 2, 11), fence_code('wattle-sticks', 'ew'))
        d.put('game:barrel', [(17, 1, 12), (21, 1, 12)])
        d.fence((25, 2, 31, 9), 0, 'woodenfence-oak', gaps=[(25, 5)])
        d.fence((16, 16, 31, 21), 0, 'woodenfence-oak', gaps=[(16, 18)])
        d.fence((1, 0, 32, 21), 0, 'woodenfence-oak', gaps=[(7, 21), (8, 21)])
        d.path([(x, z) for x in (7, 8) for z in range(16, 22)] + [(x, 15) for x in range(9, 25)] + [(14, z) for z in range(2, 16)], -1)
        grass(d, (0, 0, 32, 22), -1, every=11)
    else:
        # A hunters' lodge of stone: shale-brick great hall with the trophy wall and hearth, mud-brick upper
        # storey, a square watch tower for spotting game, a stone smokehouse, a tannery range, kennels and a
        # walled paddock, banners at the door.
        hall = (2, 1, 12, 15); tow = (12, 11, 16, 15); smoke = (20, 2, 24, 6); tan = (20, 9, 28, 13)
        yard(d, (0, 0, 36, 26))
        house(d, hall, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        tower(d, tow, 1, 13, BRICK, roof='hip', floors_every=4, ladder=(15, 'north', 14))
        opening(d, [(12, 13)], 1, 2); opening(d, [(12, 13)], 5, 6)
        for y in range(10, 12):
            for (x, z) in ring(tow):
                if (x + z) % 2 == 0 and d.get((x, y, z)) == BRICK: d.set((x, y, z), None)
        d.door((7, 1, 15), 'solid-oak', 'north')
        hut_block(d, (6, 1, 14))
        for y in (1, 2, 3): d.set((7, y, 2), POLISHED); d.set((6, y, 2), POLISHED); d.set((8, y, 2), POLISHED)
        d.place('game:firepit-cold', (7, 1, 3)); chimney(d, 7, 2, 4, 11, BRICK)
        table_and_chairs(d, [(7, 1, 7)], 'rustic', double=True); table_and_chairs(d, [(7, 1, 10)], 'rustic', double=True); bench(d, [(4, 1, 5), (10, 1, 5)])
        d.place('game:toolrack-west', (11, 2, 5)); d.place('game:toolrack-west', (11, 2, 6)); d.place('game:toolrack-west', (3, 2, 5)); d.place('game:toolrack-west', (3, 2, 6))
        for z in (4, 8, 12): d.place('aculinaryartillery:meathooks-east', (11, 3, z)) if 'aculinaryartillery:meathooks-east' in CATALOG else None
        d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 12)]); d.put('game:barrel', [(10, 1, 13)])
        d.stair_run((11, 1, 13), 'north', 3, 'brick')
        for z in (10, 11, 12, 13): d.set((11, 4, z), None)
        d.beds(6, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (9, 5, 3), (9, 5, 6), (9, 5, 9)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 13)])
        for y in (3, 7): d.lantern((7, y, 8)); d.lantern((14, y, 13))
        d.lantern((7, 2, 16), 'south'); banner(d, (5, 3, 16), 'south'); banner(d, (9, 3, 16), 'south')
        d.plain_walls(smoke, 1, 3, BRICK); d.hollow(smoke, 1, 3); d.floor(smoke, 4, BRICK); d.rooms.append((smoke, 3))
        d.door((22, 1, 6), 'solid-oak', 'north'); d.place('game:firepit-cold', (22, 1, 3))
        for x in (21, 23): d.place('aculinaryartillery:meathooks-east', (x, 2, 4)) if 'aculinaryartillery:meathooks-east' in CATALOG else None
        lean_to(d, tan, 1, 3, 'north', open_sides=('south',), level=5, wall=lambda x, y, z, a: BRICK if y == 1 else PLANK_X)
        for x in range(21, 28): d.set((x, 2, 11), fence_code('wattle-sticks', 'ew'))
        d.put('game:barrel', [(21, 1, 12), (24, 1, 12), (27, 1, 12)])
        d.fence((29, 2, 35, 9), 0, 'drystonefence-shale', gaps=[(29, 5)])
        d.fence((18, 17, 35, 25), 0, 'drystonefence-shale', gaps=[(18, 21)])
        d.fence((1, 0, 36, 25), 0, 'drystonefence-shale', gaps=[(7, 25), (8, 25)])
        d.path([(x, z) for x in (7, 8) for z in range(16, 26)] + [(x, 15) for x in range(9, 29)] + [(18, z) for z in range(2, 16)], -1)
        grass(d, (0, 0, 36, 26), -1, every=12)
    return d
