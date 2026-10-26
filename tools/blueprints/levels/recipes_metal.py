"""Metal: Smelter, Blacksmith, Armourer, Weaponsmith."""
from parts import *
from recipes_homes import recipe, RECIPES


def ore_piles(d, cells, y, code='game:rock-shale'):
    for (x, z) in cells: d.set((x, y, z), code)


def stone_chimney(d, x, z, y0, y1, code=COBBLE, wide=False):
    """A chimney mass: one block wide (or a 2x2 stack) up to y1 with a slab cap."""
    for y in range(y0, y1 + 1):
        d.set((x, y, z), code)
        if wide: d.set((x + 1, y, z), code); d.set((x, y, z + 1), code); d.set((x + 1, y, z + 1), code)
    d.set((x, y1 + 1, z), slab('cobble', 'down'))
    if wide: d.set((x + 1, y1 + 1, z), slab('cobble', 'down')); d.set((x, y1 + 1, z + 1), slab('cobble', 'down')); d.set((x + 1, y1 + 1, z + 1), slab('cobble', 'down'))


def hearth_wall(d, x0, x1, z, y0, y1, code=COBBLE):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1): d.set((x, y, z), code)


# ---------------------------------------------------------------- Smelter
@recipe('smelter')
def smelter(d):
    L = d.level
    if L == 1:
        # A bloomery camp: a stone-ringed firepit for the smelt under an open roof on posts, ore and charcoal
        # baskets, a bellows bench, a hay bed under a wattle screen.
        box = (1, 1, 7, 5)
        yard(d, (0, 0, 12, 10))
        lean_to(d, box, 1, 3, 'north', open_sides=('south', 'east'), level=1, wall=DIRT, floor=DIRT)
        hut_block(d, (6, 1, 4))
        for (dx, dz) in ((-1, 0), (1, 0), (0, -1), (0, 1)): d.set((3 + dx, 1, 3 + dz), COBBLE)
        d.place('game:firepit-cold', (3, 1, 3)); d.set((3, 0, 3), COBBLE)
        d.put('game:stationarybasket-north', [(5, 1, 2), (6, 1, 2)])
        for y in (1, 2): d.set((1, y, 7), fence_code('wattle-sticks', 'ns')) if False else None
        d.bed((2, 0, 8), 'hay', 'east') or d.bed((2, 0, 7), 'hay')
        for z in (7, 8): d.set((1, 1, z), fence_code('wattle-sticks', 'ns')) if False else None
        ore_piles(d, [(9, 2), (10, 2), (9, 3)], 0); d.place('game:groundstorage', (10, 0, 6))
        d.torch((4, 2, 1), 'south')
        grass(d, (0, 0, 12, 10), -1, every=6)
    elif L == 2:
        # A smelting hut: a daub hut for the smelter with a bed and chest, a cobble furnace stack in a roofed
        # bay beside it with a chimney, ore bins, a charcoal pile under a wattle lean-to, a wattle fence.
        box = (1, 1, 7, 7); bay = (7, 1, 13, 7)
        yard(d, (0, 0, 20, 12))
        house(d, box, storeys=1, height=3, axis=0, level=2, no_windows=('east',))
        lean_to(d, bay, 1, 3, 'west', open_sides=('east', 'south'), level=2, wall=COBBLE)
        d.door((4, 1, 7), 'crude', 'north')
        hut_block(d, (6, 1, 6))
        d.bed((2, 1, 2), 'rough', 'south'); d.put_against('game:chest', [(2, 1, 6)]); d.place('game:toolrack-west', (6, 2, 2))
        d.torch((4, 2, 5)); d.torch((4, 2, 8), 'south')
        for y in (1, 2, 3): d.set((10, y, 2), COBBLE); d.set((9, y, 2), COBBLE); d.set((11, y, 2), COBBLE)
        d.place('game:firepit-cold', (10, 1, 3)); stone_chimney(d, 10, 2, 4, 7, COBBLE)
        d.put('game:stationarybasket-north', [(8, 1, 5), (12, 1, 5)]); ore_piles(d, [(8, 6), (12, 6)], 1)
        lean_to(d, (15, 2, 19, 6), 1, 2, 'east', open_sides=('west', 'north', 'south'), level=2, wall=None)
        for p in ((16, 1, 3), (17, 1, 3), (18, 1, 3), (17, 1, 5)): d.place('game:groundstorage', p)
        d.fence((0, 0, 20, 11), 0, 'wattle-sticks', gaps=[(4, 11), (5, 11), (20, 4), (20, 5)])
        d.path([(4, 8), (4, 9), (4, 10), (4, 11), (5, 8), (5, 9), (5, 10), (5, 11)], -1)
        grass(d, (0, 0, 20, 12), -1, every=8)
    elif L == 3:
        # A smelting works: a rammed-earth house for two smelters, a stone furnace hall open to the yard with
        # two furnace stacks and a tall chimney, ore bins in a roofed row, a charcoal store, oak fence.
        home = (2, 1, 8, 9); hall = (11, 1, 21, 9); bins = (2, 12, 12, 16)
        yard(d, (0, 0, 28, 20))
        house(d, home, storeys=1, height=3, axis=1, level=3)
        d.door((5, 1, 9), 'solid-oak', 'north')
        hut_block(d, (4, 1, 8))
        d.bed((3, 1, 3), 'rough', 'south'); d.bed((7, 1, 3), 'rough', 'south'); table_and_chairs(d, [(5, 1, 6)], 'rough'); d.put_against('game:chest', [(7, 1, 8)])
        d.lantern((5, 3, 5)); d.lantern((5, 2, 10), 'south')
        lean_to(d, hall, 1, 4, 'north', open_sides=('south',), level=3, wall=COBBLE)
        hearth_wall(d, 12, 20, 2, 1, 4, COBBLE)
        for x in (14, 18):
            for y in (1, 2, 3): d.set((x - 1, y, 3), COBBLE); d.set((x + 1, y, 3), COBBLE)
            d.place('game:firepit-cold', (x, 1, 3)); stone_chimney(d, x, 2, 5, 10, COBBLE)
        d.put('game:stationarybasket-north', [(12, 1, 7), (20, 1, 7)]); d.place('game:toolrack-west', (20, 2, 5)); d.put('game:barrel', [(16, 1, 7)])
        d.lantern((16, 4, 6), 'south')
        lean_to(d, bins, 1, 3, 'south', open_sides=('north',), level=3, wall=PLANK_X)
        for x in range(3, 12, 2):
            for y in (1, 2): d.set((x, y, 13), fence_code('wattle-sticks', 'ns'))
        ore_piles(d, [(4, 14), (6, 14), (8, 14), (10, 14), (4, 15), (8, 15)], 1)
        for p in ((15, 0, 13), (17, 0, 13), (19, 0, 13), (16, 0, 15), (18, 0, 15)): d.place('game:groundstorage', p)
        d.fence((1, 0, 27, 19), 0, 'woodenfence-oak', gaps=[(5, 19), (6, 19), (27, 10), (27, 11)])
        d.path([(x, z) for x in (5, 6) for z in range(10, 20)] + [(x, 10) for x in range(7, 27)] + [(x, 11) for x in range(7, 27)], -1)
        grass(d, (0, 0, 28, 20), -1, every=10)
    elif L == 4:
        # A foundry: a two-storey cobble-and-plank house for the smelters, a stone furnace hall with three stacks
        # under one great chimney, a roofed ore yard with stone bins, a charcoal barn, a stone-paved yard.
        home = (2, 1, 10, 11); hall = (14, 1, 26, 11); ore = (2, 14, 12, 20); coal = (16, 14, 24, 20)
        yard(d, (0, 0, 32, 24))
        house(d, home, storeys=2, height=3, axis=1, level=4)
        d.door((6, 1, 11), 'solid-oak', 'north')
        hut_block(d, (5, 1, 10))
        for y in (1, 2, 3): d.set((6, y, 2), BRICK)
        d.place('game:firepit-cold', (6, 1, 3)); chimney(d, 6, 2, 4, 9, COBBLE)
        table_and_chairs(d, [(6, 1, 6)], 'rough', double=True); d.put_against('game:chest', [(3, 1, 9), (9, 1, 9)])
        d.stair_run((9, 1, 9), 'north', 3, 'planks')
        for z in (6, 7, 8, 9): d.set((9, 4, z), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (7, 5, 3), (7, 5, 6)], 'wood', 'south'); d.put_against('game:chest', [(3, 5, 9)])
        d.lantern((6, 3, 8)); d.lantern((6, 7, 6)); d.lantern((6, 2, 12), 'south')
        lean_to(d, hall, 1, 5, 'north', open_sides=('south',), level=4, wall=COBBLE)
        hearth_wall(d, 15, 25, 2, 1, 5, BRICK)
        for x in (17, 20, 23):
            for y in (1, 2, 3): d.set((x - 1, y, 3), BRICK); d.set((x + 1, y, 3), BRICK)
            d.place('game:firepit-cold', (x, 1, 3))
        stone_chimney(d, 19, 2, 6, 14, BRICK, wide=True)
        d.put('game:stationarybasket-north', [(15, 1, 8), (25, 1, 8)]); d.place('game:toolrack-west', (25, 2, 6)); d.put('game:barrel', [(20, 1, 9)]); d.place('game:anvil-copper', (16, 1, 6))
        d.lantern((17, 5, 8), 'south'); d.lantern((23, 5, 8), 'south')
        lean_to(d, ore, 1, 3, 'south', open_sides=('north',), level=4, wall=COBBLE)
        for x in range(3, 12, 3):
            for y in (1, 2):
                for z in (15, 16, 17): d.set((x, y, z), COBBLE)
        ore_piles(d, [(4, 16), (5, 16), (7, 16), (8, 16), (10, 16), (11, 16), (4, 18), (7, 18), (10, 18)], 1)
        house(d, coal, storeys=1, height=3, axis=0, level=4, walls=dict(WALLS[4], low=PLANK_Z, gable=PLANK_Z), window_sides=())
        d.door((16, 1, 17), 'solid-oak', 'east')
        for p in ((18, 1, 15), (20, 1, 15), (22, 1, 15), (18, 1, 19), (20, 1, 19), (22, 1, 19), (20, 1, 17)): d.place('game:groundstorage', p)
        d.ground((12, 2, 13, 22), -1, COBBLE); d.ground((2, 12, 30, 13), -1, COBBLE)
        d.fence((1, 0, 31, 23), 0, 'woodenfence-oak', gaps=[(6, 23), (7, 23), (31, 12), (31, 13)])
        d.path([(x, z) for x in (6, 7) for z in range(21, 24)] + [(x, 22) for x in range(8, 16)] + [(x, 22) for x in range(25, 31)], -1)
        grass(d, (0, 0, 32, 24), -1, every=11)
    else:
        # A great foundry: stone-brick smelters' hall with a mud-brick upper floor, a furnace hall of four stacks
        # under two great chimneys, a hip-roofed assay tower, a stone ore yard with bins, a charcoal barn, a
        # cobbled yard inside drystone walls.
        home = (2, 1, 12, 13); tow = (12, 9, 16, 13); hall = (20, 1, 34, 13); ore = (2, 16, 14, 22); coal = (18, 16, 30, 22)
        yard(d, (0, 0, 40, 26))
        house(d, home, storeys=2, height=3, axis=1, level=5, no_windows=('east',))
        stone_tower(d, tow, 1, 12, roof='hip', floors_every=4, ladder=(15, 'north', 12))
        opening(d, [(12, 11)], 1, 2); opening(d, [(12, 11)], 5, 6)
        d.door((7, 1, 13), 'solid-oak', 'north')
        hut_block(d, (6, 1, 12))
        for y in (1, 2, 3): d.set((7, y, 2), POLISHED)
        d.place('game:firepit-cold', (7, 1, 3)); chimney(d, 7, 2, 4, 10, BRICK)
        table_and_chairs(d, [(7, 1, 7)], 'rustic', double=True); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(3, 1, 10)]); d.put_against('game:chest', [(11, 1, 5)])
        d.stair_run((11, 1, 11), 'north', 3, 'brick')
        for z in (8, 9, 10, 11): d.set((11, 4, z), None)
        d.beds(6, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (9, 5, 3), (9, 5, 6), (9, 5, 9)], 'rustic', 'east'); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 12)])
        d.put('w4rd0sfurniture:roughtable-north', [(13, 5, 11)]); d.put('game:crate', [(13, 1, 11), (15, 1, 11)]); d.put('game:bookshelf', [(15, 9, 11)])
        for y in (3, 7): d.lantern((7, y, 9)); d.lantern((14, y, 11))
        d.lantern((14, 11, 11)); d.lantern((7, 2, 14), 'south')
        lean_to(d, hall, 1, 6, 'north', open_sides=('south',), level=5, wall=BRICK)
        hearth_wall(d, 21, 33, 2, 1, 6, BRICK)
        for x in (23, 26, 29, 32):
            for y in (1, 2, 3): d.set((x - 1, y, 3), POLISHED); d.set((x + 1, y, 3), POLISHED)
            d.place('game:firepit-cold', (x, 1, 3))
        stone_chimney(d, 24, 2, 7, 16, BRICK, wide=True); stone_chimney(d, 30, 2, 7, 16, BRICK, wide=True)
        d.floor_inside(hall, 0, POLISHED)
        d.put('game:stationarybasket-north', [(21, 1, 10), (33, 1, 10)]); d.place('game:toolrack-west', (33, 2, 7)); d.put('game:barrel', [(27, 1, 11)]); d.place('game:anvil-copper', (22, 1, 7)); d.place('game:anvil-copper', (32, 1, 7))
        for x in (23, 27, 31): d.lantern((x, 6, 10), 'south')
        lean_to(d, ore, 1, 3, 'south', open_sides=('north',), level=5, wall=BRICK)
        for x in range(3, 14, 3):
            for y in (1, 2):
                for z in (17, 18, 19): d.set((x, y, z), BRICK)
        ore_piles(d, [(4, 18), (5, 18), (7, 18), (8, 18), (10, 18), (11, 18), (13, 18), (4, 20), (7, 20), (10, 20), (13, 20)], 1)
        house(d, coal, storeys=1, height=3, axis=0, level=5, walls=dict(WALLS[5], low=lambda x, y, z, a: BRICK if y == 1 else PLANK_Z, gable=PLANK_Z), window_sides=())
        d.door((18, 1, 19), 'solid-oak', 'east')
        for x in range(20, 30, 2): d.place('game:groundstorage', (x, 1, 17)); d.place('game:groundstorage', (x, 1, 21))
        d.ground((2, 14, 38, 15), -1, COBBLE); d.ground((16, 2, 18, 24), -1, COBBLE)
        d.fence((1, 0, 39, 25), 0, 'drystonefence-shale', gaps=[(7, 25), (8, 25), (39, 14), (39, 15)])
        d.path([(x, z) for x in (7, 8) for z in (23, 24, 25)] + [(x, 24) for x in range(9, 18)] + [(x, 24) for x in range(31, 39)], -1)
        grass(d, (0, 0, 40, 26), -1, every=12)
    return d


# ---------------------------------------------------------------- Blacksmith
@recipe('blacksmith')
def blacksmith(d):
    L = d.level
    if L == 1:
        # A smith's fire: an open shelter on posts over a forge and an anvil on a stone block, a quench barrel,
        # tools on a rack, a hay bed at the back.
        box = (1, 1, 7, 5)
        yard(d, (0, 0, 10, 9))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT, wall=COBBLE)
        hut_block(d, (6, 1, 4))
        d.place('game:forge', (3, 1, 2)); d.place('game:anvil-copper', (4, 1, 3)); d.put('game:barrel', [(6, 1, 2)]); d.place('game:toolrack-west', (2, 2, 3)) if d.free((2, 2, 3)) else None
        d.bed((2, 1, 4), 'hay', 'east') or d.bed((2, 1, 3), 'hay')
        d.torch((4, 2, 1), 'south')
        d.place('game:firepit-cold', (4, 0, 7))
        grass(d, (0, 0, 10, 9), -1, every=6)
    elif L == 2:
        # A smithy: a daub house with an open forge bay in front under its own roof, the forge against a cobble
        # back wall with a chimney, anvil, quench barrel, tool racks, the smith's bed and chest inside.
        box = (1, 1, 9, 7); bay = (1, 7, 9, 11)
        yard(d, (0, 0, 14, 15))
        house(d, box, storeys=1, height=3, axis=0, level=2, no_windows=('south',))
        lean_to(d, bay, 1, 3, 'north', open_sides=('south', 'east', 'west'), level=2, wall=None)
        for x in range(2, 9):
            for y in (1, 2):
                if d.get((x, y, 7)) in (DAUB, DAUB_WATTLE): d.set((x, y, 7), COBBLE)
        d.door((5, 1, 7), 'crude', 'north')
        hut_block(d, (8, 1, 6))
        d.place('game:forge', (3, 1, 8)); d.place('game:anvil-copper', (4, 1, 9)); d.put('game:barrel', [(7, 1, 9)]); d.place('game:toolrack-west', (2, 2, 9)) if d.free((2, 2, 9)) else None
        stone_chimney(d, 3, 7, 3, 7, COBBLE)
        d.bed((2, 1, 2), 'rough', 'south'); d.put_against('game:chest', [(8, 1, 2)]); table_and_chairs(d, [(5, 1, 4)], 'rough')
        d.torch((5, 2, 5)); d.torch((5, 2, 10), 'south')
        d.fence((0, 0, 14, 14), 0, 'wattle-sticks', gaps=[(5, 14), (6, 14)])
        d.path([(x, z) for x in (5, 6) for z in (12, 13, 14)], -1)
        grass(d, (0, 0, 14, 15), -1, every=8)
    elif L == 3:
        # A forge house: a rammed-earth smithy with a stone forge wall and two forges, a tall chimney stack, a
        # cross-wing for the smiths' beds, a coal store, a covered trough, an oak-fenced yard with a stone path.
        shop = (2, 1, 12, 9); wing = (12, 3, 18, 9)
        yard(d, (0, 0, 22, 16))
        house(d, shop, storeys=1, height=4, axis=0, level=3, no_windows=('east', 'north'))
        house(d, wing, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        opening(d, [(12, 6)], 1, 2)
        for x in range(3, 12):
            for y in (1, 2, 3): d.set((x, y, 2), COBBLE)
        for x in range(4, 11):
            for y in (1, 2):
                if d.get((x, y, 9)) in (RAMMED, COBBLE) and (x - 2) % 3 != 0: d.set((x, y, 9), None)
        d.door((7, 1, 9), 'solid-oak', 'north') if False else None
        hut_block(d, (11, 1, 8))
        d.place('game:forge', (4, 1, 3)); d.place('game:forge', (9, 1, 3)); d.place('game:anvil-copper', (5, 1, 5)); d.place('game:anvil-copper', (9, 1, 5))
        d.put('game:barrel', [(7, 1, 4)]); d.place('game:toolrack-west', (3, 2, 5)); d.place('game:toolrack-west', (3, 2, 6)); d.put('game:crate', [(3, 1, 8)])
        stone_chimney(d, 6, 2, 4, 10, COBBLE, wide=True)
        d.beds(3, [(14, 1, 4), (16, 1, 4), (17, 1, 7)], 'rough', 'south'); d.put_against('game:chest', [(13, 1, 8)])
        d.lantern((7, 4, 6)); d.lantern((15, 3, 6))
        lean_to(d, (14, 11, 20, 15), 1, 3, 'south', open_sides=('north',), level=3, wall=PLANK_X)
        for p in ((15, 1, 13), (17, 1, 13), (19, 1, 13), (16, 1, 14)): d.place('game:groundstorage', p)
        d.put('game:barrel', [(4, 0, 11), (6, 0, 11)])
        d.fence((1, 0, 21, 15), 0, 'woodenfence-oak', gaps=[(7, 15), (8, 15)])
        d.path([(x, z) for x in (7, 8) for z in range(10, 16)] + [(x, 10) for x in range(9, 21)], -1)
        grass(d, (0, 0, 22, 16), -1, every=10)
    elif L == 4:
        # A master smithy: two storeys, a cobble forge hall below open to the yard through arches, three forges
        # under a great chimney, the smiths' rooms above, a stone coal store, a water trough, a porch sign.
        hall = (2, 1, 14, 11); coal = (18, 2, 24, 8)
        yard(d, (0, 0, 28, 18))
        house(d, hall, storeys=2, height=3, axis=0, level=4, no_windows=('north', 'south'))
        for x in range(4, 13):
            for y in (1, 2):
                if (x - 2) % 4 != 0: d.set((x, y, 11), None)
        for x in range(3, 14):
            for y in (1, 2, 3): d.set((x, y, 2), BRICK)
        hut_block(d, (13, 1, 10))
        for x in (4, 8, 12): d.place('game:forge', (x, 1, 3)); d.place('game:anvil-copper', (x, 1, 5))
        d.put('game:barrel', [(6, 1, 4), (10, 1, 4)]); d.place('game:toolrack-west', (3, 2, 6)); d.place('game:toolrack-west', (3, 2, 7)); d.place('game:toolrack-west', (13, 2, 6)); d.put('game:crate', [(3, 1, 9)])
        stone_chimney(d, 7, 2, 4, 13, BRICK, wide=True)
        d.stair_run((12, 1, 9), 'west', 3, 'planks') if False else d.stair_run((11, 1, 9), 'west', 3, 'planks')
        for x in (8, 9, 10, 11): d.set((x, 4, 9), None)
        d.beds(4, [(3, 5, 3), (3, 5, 6), (13, 5, 6), (13, 5, 3)], 'wood', 'south') or None
        d.beds(2, [(5, 5, 3), (11, 5, 3)], 'wood', 'south'); table_and_chairs(d, [(8, 5, 6)], 'rough'); d.put_against('game:chest', [(3, 5, 9)])
        d.lantern((8, 3, 7)); d.lantern((8, 7, 6)); d.lantern((4, 3, 12), 'south'); d.lantern((12, 3, 12), 'south'); banner(d, (8, 4, 12), 'south')
        store_shed(d, coal, 1, 3, 'west', COBBLE, 4, door=((18, 1, 5), 'east'))
        for p in ((20, 1, 3), (22, 1, 3), (20, 1, 7), (22, 1, 7), (21, 1, 5)): d.place('game:groundstorage', p)
        d.put('game:barrel', [(5, 0, 13), (7, 0, 13), (9, 0, 13)])
        d.ground((2, 12, 26, 14), -1, COBBLE)
        d.fence((1, 0, 27, 17), 0, 'woodenfence-oak', gaps=[(8, 17), (9, 17), (27, 12), (27, 13)])
        d.path([(x, z) for x in (8, 9) for z in (15, 16, 17)] + [(16, z) for z in range(2, 15)], -1)
        grass(d, (0, 0, 28, 18), -1, every=11)
    else:
        # A great smithy: a stone-brick forge hall with a polished floor, four forges and two great chimneys, a
        # mud-brick upper floor for the smiths, a hip-roofed tower for the master and the pattern store, a stone
        # coal vault, a water trough, a drystone-walled yard with a flag.
        hall = (2, 1, 16, 13); tow = (16, 7, 22, 13); coal = (16, 1, 22, 6)
        yard(d, (0, 0, 30, 20))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('north', 'south', 'east'))
        for x in range(4, 15):
            for y in (1, 2):
                if (x - 2) % 4 != 0: d.set((x, y, 13), None)
        house(d, tow, storeys=1, height=4, axis=1, level=5, no_windows=('west',))
        opening(d, [(16, 10)], 1, 2)
        store_shed(d, coal, 1, 3, 'west', BRICK, 5, door=((16, 1, 3), 'east'))
        for p in ((18, 1, 2), (20, 1, 2), (18, 1, 5), (20, 1, 5)): d.place('game:groundstorage', p)
        for x in range(3, 16):
            for y in (1, 2, 3): d.set((x, y, 2), POLISHED)
        d.floor_inside(hall, 0, POLISHED)
        hut_block(d, (15, 1, 12))
        for x in (4, 7, 11, 14): d.place('game:forge', (x, 1, 3)); d.place('game:anvil-copper', (x, 1, 5))
        d.put('game:barrel', [(5, 1, 4), (12, 1, 4)]); d.place('game:toolrack-west', (3, 2, 7)); d.place('game:toolrack-west', (3, 2, 8)); d.place('game:toolrack-west', (15, 2, 7)); d.place('game:toolrack-west', (15, 2, 8))
        d.put('game:crate', [(3, 1, 10), (3, 1, 11)])
        stone_chimney(d, 5, 2, 4, 15, BRICK, wide=True); stone_chimney(d, 12, 2, 4, 15, BRICK, wide=True)
        d.stair_run((14, 1, 11), 'west', 3, 'brick') if False else d.stair_run((13, 1, 11), 'west', 3, 'brick')
        for x in (10, 11, 12, 13): d.set((x, 4, 11), None)
        d.beds(6, [(3, 5, 3), (3, 5, 6), (3, 5, 9), (15, 5, 3), (15, 5, 6), (15, 5, 9)], 'rustic', 'east'); table_and_chairs(d, [(9, 5, 6)], 'rustic', double=True); d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 11)])
        d.bed((18, 1, 11), 'rustic', 'east'); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(20, 1, 11)]); d.put('game:crate', [(18, 1, 8), (20, 1, 8)]); d.put('game:bookshelf', [(21, 1, 10)])
        for p in ((9, 3, 8), (9, 7, 8), (19, 4, 10)): d.lantern(p)
        d.lantern((4, 3, 14), 'south'); d.lantern((14, 3, 14), 'south'); banner(d, (7, 4, 14), 'south'); banner(d, (11, 4, 14), 'south')
        d.put('game:barrel', [(5, 0, 15), (7, 0, 15), (9, 0, 15), (11, 0, 15)])
        d.ground((2, 14, 28, 16), -1, COBBLE)
        flag(d, (25, 0, 17))
        d.fence((1, 0, 29, 19), 0, 'drystonefence-shale', gaps=[(9, 19), (10, 19), (29, 14), (29, 15)])
        d.path([(x, z) for x in (9, 10) for z in (17, 18, 19)] + [(x, 17) for x in range(11, 29)], -1)
        grass(d, (0, 0, 30, 20), -1, every=12)
    return d


# ---------------------------------------------------------------- Armourer
@recipe('armourer')
def armourer(d):
    L = d.level
    if L == 1:
        # An armourer's bench: a lean-to with a forge, anvil and a leather-working table, racks for the plates,
        # a quench barrel, a hay bed.
        box = (1, 1, 7, 5)
        yard(d, (0, 0, 10, 9))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT, wall=COBBLE)
        hut_block(d, (6, 1, 4))
        d.place('game:forge', (2, 1, 2)); d.place('game:anvil-copper', (3, 1, 3)); d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 2)]); d.put('game:barrel', [(6, 1, 2)])
        d.place('game:toolrack-west', (4, 2, 2)) if d.free((4, 2, 2)) else None
        d.bed((2, 1, 4), 'hay', 'east') or None
        d.torch((4, 2, 1), 'south')
        grass(d, (0, 0, 10, 9), -1, every=6)
    elif L == 2:
        # An armourer's shop: a daub house with the forge in a stone bay at the gable end, a fitting room with a
        # table, racks and a wardrobe for finished mail, the armourer's bed, a wattle yard.
        box = (1, 1, 9, 9)
        yard(d, (0, 0, 14, 13))
        house(d, box, storeys=1, height=3, axis=1, level=2, no_windows=('north',))
        for x in range(2, 9):
            for y in (1, 2):
                if d.get((x, y, 1)) in (DAUB, DAUB_WATTLE): d.set((x, y, 1), COBBLE)
        d.door((5, 1, 9), 'crude', 'north')
        hut_block(d, (8, 1, 8))
        d.place('game:forge', (3, 1, 2)); d.place('game:anvil-copper', (4, 1, 3)); d.put('game:barrel', [(7, 1, 2)]); stone_chimney(d, 3, 1, 3, 7, COBBLE)
        d.put('w4rd0sfurniture:roughtable-north', [(4, 1, 6)]); d.put('w4rd0sfurniture:roughstool-west', [(3, 1, 6)]); d.place('game:toolrack-west', (8, 2, 4)); d.place('game:toolrack-west', (8, 2, 5))
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(2, 1, 7)]) or d.put('game:cabinet-bottom', [(2, 1, 7)])
        d.bed((7, 1, 7), 'rough', 'north') or d.bed((7, 1, 6), 'rough')
        d.torch((5, 2, 5)); d.torch((5, 2, 10), 'south')
        d.fence((0, 0, 14, 12), 0, 'wattle-sticks', gaps=[(5, 12), (6, 12)])
        d.path([(x, z) for x in (5, 6) for z in (10, 11, 12)], -1)
        grass(d, (0, 0, 14, 13), -1, every=8)
    elif L == 3:
        # An armoury workshop: rammed-earth forge hall with two forges and a stone chimney, a cross-wing fitting
        # room with tables, racks and a wardrobe, a plate store, a quench trough outside, oak fence and path.
        shop = (2, 1, 10, 9); wing = (10, 1, 16, 9)
        yard(d, (0, 0, 20, 15))
        house(d, shop, storeys=1, height=4, axis=0, level=3, no_windows=('east', 'north'))
        house(d, wing, storeys=1, height=3, axis=1, level=3, no_windows=('west',))
        opening(d, [(10, 5)], 1, 2)
        for x in range(3, 10):
            for y in (1, 2, 3): d.set((x, y, 2), COBBLE)
        d.door((6, 1, 9), 'solid-oak', 'north')
        hut_block(d, (9, 1, 8))
        d.place('game:forge', (4, 1, 3)); d.place('game:forge', (8, 1, 3)); d.place('game:anvil-copper', (5, 1, 5)); d.place('game:anvil-copper', (8, 1, 5)); d.put('game:barrel', [(6, 1, 4)])
        d.place('game:toolrack-west', (3, 2, 6)); d.place('game:toolrack-west', (3, 2, 7)); stone_chimney(d, 6, 2, 4, 10, COBBLE, wide=True)
        d.put('w4rd0sfurniture:roughtable-north', [(12, 1, 3), (14, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(12, 1, 4), (14, 1, 4)])
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(11, 1, 7)]) or d.put('game:cabinet-bottom', [(11, 1, 7)]); d.put('game:cabinet-bottom', [(15, 1, 7)]); d.bed((15, 1, 5), 'rough', 'south') if False else d.bed((13, 1, 7), 'rough', 'south') or None
        d.lantern((6, 4, 6)); d.lantern((13, 3, 5)); d.lantern((6, 2, 10), 'south')
        d.put('game:barrel', [(4, 0, 11), (6, 0, 11)])
        d.fence((1, 0, 19, 14), 0, 'woodenfence-oak', gaps=[(6, 14), (7, 14)])
        d.path([(x, z) for x in (6, 7) for z in range(10, 15)], -1)
        grass(d, (0, 0, 20, 15), -1, every=10)
    elif L == 4:
        # An armourers' hall: two storeys, a cobble forge hall below with three forges and a great chimney, a
        # plank fitting hall above with tables, racks and wardrobes of finished armour, a stone plate store, a
        # porch with a banner, a cobbled yard.
        hall = (2, 1, 14, 11); store = (18, 2, 24, 8)
        yard(d, (0, 0, 28, 18))
        house(d, hall, storeys=2, height=3, axis=0, level=4, no_windows=('north',))
        for x in range(3, 14):
            for y in (1, 2, 3): d.set((x, y, 2), BRICK)
        d.door((8, 1, 11), 'solid-oak', 'north')
        hut_block(d, (13, 1, 10))
        for x in (4, 8, 12): d.place('game:forge', (x, 1, 3)); d.place('game:anvil-copper', (x, 1, 5))
        d.put('game:barrel', [(6, 1, 4), (10, 1, 4)]); d.place('game:toolrack-west', (3, 2, 7)); d.place('game:toolrack-west', (3, 2, 8)); d.place('game:toolrack-west', (13, 2, 7))
        stone_chimney(d, 7, 2, 4, 13, BRICK, wide=True)
        d.stair_run((11, 1, 9), 'west', 3, 'planks')
        for x in (8, 9, 10, 11): d.set((x, 4, 9), None)
        for x in (3, 6, 9): d.put('w4rd0sfurniture:roughtable-north', [(x, 5, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 5, 4)])
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 7), (3, 5, 9)]) or d.put('game:cabinet-bottom', [(3, 5, 7)]); d.put('game:cabinet-bottom', [(13, 5, 3), (13, 5, 5)])
        d.beds(2, [(7, 5, 8), (11, 5, 8)], 'wood', 'south') or None
        d.lantern((8, 3, 7)); d.lantern((8, 7, 6)); d.lantern((8, 2, 12), 'south'); banner(d, (10, 3, 12), 'south')
        porch(d, (6, 12, 10, 13), 1, 3, 'north')
        store_shed(d, store, 1, 3, 'west', COBBLE, 4, door=((18, 1, 5), 'east'))
        d.put('game:crate', [(20, 1, 3), (22, 1, 3), (20, 1, 7), (22, 1, 7)]); d.put_against('game:chest', [(23, 1, 5)])
        d.put('game:barrel', [(4, 0, 13), (6, 0, 13)])
        d.ground((2, 12, 26, 14), -1, COBBLE)
        d.fence((1, 0, 27, 17), 0, 'woodenfence-oak', gaps=[(8, 17), (9, 17)])
        d.path([(x, z) for x in (8, 9) for z in (15, 16, 17)] + [(16, z) for z in range(2, 15)], -1)
        grass(d, (0, 0, 28, 18), -1, every=11)
    else:
        # An armourers' guild: a stone-brick forge hall with four forges and two chimneys, a mud-brick fitting
        # hall above, a hip-roofed tower holding the pattern room and the master's chamber, a stone plate vault,
        # a flagged yard behind drystone walls, banners and a flag.
        hall = (2, 1, 16, 13); tow = (16, 7, 22, 13); vault = (16, 1, 22, 6)
        yard(d, (0, 0, 30, 20))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('north', 'east'))
        stone_tower(d, tow, 1, 11, roof='flat', floors_every=4, ladder=(21, 'north', 12))
        opening(d, [(16, 10)], 1, 2); opening(d, [(16, 10)], 5, 6)
        store_shed(d, vault, 1, 3, 'west', BRICK, 5, door=((16, 1, 3), 'east'))
        d.put('game:crate', [(18, 1, 2), (20, 1, 2), (18, 1, 5), (20, 1, 5)])
        for x in range(3, 16):
            for y in (1, 2, 3): d.set((x, y, 2), POLISHED)
        d.floor_inside(hall, 0, POLISHED)
        d.door((9, 1, 13), 'solid-oak', 'north')
        hut_block(d, (15, 1, 12))
        for x in (4, 7, 11, 14): d.place('game:forge', (x, 1, 3)); d.place('game:anvil-copper', (x, 1, 5))
        d.put('game:barrel', [(5, 1, 4), (12, 1, 4)]); d.place('game:toolrack-west', (3, 2, 8)); d.place('game:toolrack-west', (3, 2, 9)); d.place('game:toolrack-west', (15, 2, 8))
        stone_chimney(d, 5, 2, 4, 15, BRICK, wide=True); stone_chimney(d, 12, 2, 4, 15, BRICK, wide=True)
        d.stair_run((13, 1, 11), 'west', 3, 'brick')
        for x in (10, 11, 12, 13): d.set((x, 4, 11), None)
        for x in (3, 6, 9, 12): d.put('w4rd0sfurniture:roughtable-north', [(x, 5, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 5, 4)])
        d.put('w4rd0sfurniture:rusticwardrobe-east', [(3, 5, 8), (3, 5, 10)]) or None; d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(15, 5, 4)]); d.beds(3, [(7, 5, 9), (10, 5, 9), (13, 5, 9)], 'rustic', 'south') or d.beds(3, [(7, 5, 9), (10, 5, 9), (13, 5, 9)], 'rustic')
        d.bed((18, 5, 11), 'rustic', 'east'); d.put('game:bookshelf', [(20, 5, 9)]); d.put('w4rd0sfurniture:roughtable-north', [(19, 9, 11)])
        for p in ((9, 3, 8), (9, 7, 8), (19, 3, 11), (19, 7, 11), (19, 11, 11)): d.lantern(p)
        d.lantern((9, 2, 14), 'south'); banner(d, (6, 3, 14), 'south'); banner(d, (12, 3, 14), 'south')
        porch(d, (7, 14, 11, 15), 1, 3, 'north')
        d.put('game:barrel', [(4, 0, 15), (13, 0, 15)])
        d.ground((2, 16, 28, 17), -1, COBBLE); flag(d, (26, 0, 18))
        d.fence((1, 0, 29, 19), 0, 'drystonefence-shale', gaps=[(9, 19), (10, 19)])
        d.path([(x, z) for x in (9, 10) for z in (16, 17, 18, 19)], -1)
        grass(d, (0, 0, 30, 20), -1, every=12)
    return d


# ---------------------------------------------------------------- Weaponsmith
@recipe('weaponsmith')
def weaponsmith(d):
    L = d.level
    if L == 1:
        # A weaponsmith's fire: a lean-to forge with an anvil and grindstone bench, spear racks, a straw target
        # out in the yard for trying blades and bows.
        box = (1, 1, 7, 5)
        yard(d, (0, 0, 12, 12))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT, wall=COBBLE)
        hut_block(d, (6, 1, 4))
        d.place('game:forge', (2, 1, 2)); d.place('game:anvil-copper', (3, 1, 3)); d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 2)]); d.place('game:toolrack-west', (6, 2, 2)) if d.free((6, 2, 2)) else None
        d.bed((2, 1, 4), 'hay', 'east') or None
        d.set((9, 0, 9), HAY); d.set((9, 1, 9), HAY)
        d.torch((4, 2, 1), 'south')
        grass(d, (0, 0, 12, 12), -1, every=6)
    elif L == 2:
        # A weaponsmith's shop: a daub house with a stone forge bay, a grinding bench, racks of spears and
        # blades on the walls, the smith's bed, a fenced trying yard with two straw targets.
        box = (1, 1, 9, 9)
        yard(d, (0, 0, 18, 13))
        house(d, box, storeys=1, height=3, axis=1, level=2, no_windows=('north',))
        for x in range(2, 9):
            for y in (1, 2):
                if d.get((x, y, 1)) in (DAUB, DAUB_WATTLE): d.set((x, y, 1), COBBLE)
        d.door((5, 1, 9), 'crude', 'north')
        hut_block(d, (8, 1, 8))
        d.place('game:forge', (3, 1, 2)); d.place('game:anvil-copper', (4, 1, 3)); d.put('game:barrel', [(7, 1, 2)]); stone_chimney(d, 3, 1, 3, 7, COBBLE)
        d.put('w4rd0sfurniture:roughtable-north', [(5, 1, 6)]); d.put('w4rd0sfurniture:roughstool-west', [(4, 1, 6)])
        for z in (4, 5, 6): d.place('game:toolrack-west', (8, 2, z)); d.place('game:toolrack-west', (2, 2, z)) if False else None
        d.bed((2, 1, 7), 'rough', 'east') or d.bed((3, 1, 7), 'rough')
        d.torch((5, 2, 5)); d.torch((5, 2, 10), 'south')
        d.fence((11, 2, 17, 10), 0, 'wattle-sticks', gaps=[(11, 6)])
        for z in (3, 9): d.set((16, 0, z), HAY); d.set((16, 1, z), HAY)
        d.fence((0, 0, 18, 12), 0, 'wattle-sticks', gaps=[(5, 12), (6, 12)])
        d.path([(x, z) for x in (5, 6) for z in (10, 11, 12)] + [(x, 6) for x in range(7, 11)], -1)
        grass(d, (0, 0, 18, 13), -1, every=8)
    elif L == 3:
        # A weapon works: a rammed-earth forge hall with two forges and a stone chimney, a grinding wing with
        # benches and racks, a store for shafts under a lean-to, a trying yard with targets, oak fence.
        shop = (2, 1, 10, 9); wing = (10, 1, 16, 9)
        yard(d, (0, 0, 26, 16))
        house(d, shop, storeys=1, height=4, axis=0, level=3, no_windows=('east', 'north'))
        house(d, wing, storeys=1, height=3, axis=1, level=3, no_windows=('west',))
        opening(d, [(10, 5)], 1, 2)
        for x in range(3, 10):
            for y in (1, 2, 3): d.set((x, y, 2), COBBLE)
        d.door((6, 1, 9), 'solid-oak', 'north')
        hut_block(d, (9, 1, 8))
        d.place('game:forge', (4, 1, 3)); d.place('game:forge', (8, 1, 3)); d.place('game:anvil-copper', (5, 1, 5)); d.place('game:anvil-copper', (8, 1, 5)); d.put('game:barrel', [(6, 1, 4)])
        stone_chimney(d, 6, 2, 4, 10, COBBLE, wide=True)
        d.put('w4rd0sfurniture:roughtable-north', [(12, 1, 3), (14, 1, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(12, 1, 4), (14, 1, 4)])
        for z in (5, 6, 7): d.place('game:toolrack-west', (15, 2, z))
        d.bed((11, 1, 7), 'rough', 'south') or None; d.put_against('game:chest', [(13, 1, 8)])
        d.lantern((6, 4, 6)); d.lantern((13, 3, 5)); d.lantern((6, 2, 10), 'south')
        lean_to(d, (2, 11, 8, 15), 1, 3, 'south', open_sides=('north',), level=3, wall=PLANK_X)
        woodpile(d, [(3, 13), (4, 13), (5, 13), (6, 13), (7, 13)], 1, 1, 'ns'); d.put('game:crate', [(3, 1, 14)])
        d.fence((18, 2, 25, 14), 0, 'woodenfence-oak', gaps=[(18, 8)])
        for z in (4, 8, 12): d.set((24, 0, z), HAY); d.set((24, 1, z), HAY)
        d.fence((1, 0, 26, 15), 0, 'woodenfence-oak', gaps=[(6, 15), (7, 15)])
        d.path([(x, z) for x in (6, 7) for z in range(10, 16)] + [(x, 10) for x in range(8, 18)] + [(17, z) for z in range(2, 15)], -1)
        grass(d, (0, 0, 26, 16), -1, every=10)
    elif L == 4:
        # A weaponsmiths' hall: two storeys, a cobble forge hall below with three forges and a great chimney,
        # the grinding and hafting hall above with racks along every wall, a stone shaft store, a long trying
        # range with targets, a porch and banner.
        hall = (2, 1, 14, 11); store = (18, 2, 24, 8)
        yard(d, (0, 0, 30, 20))
        house(d, hall, storeys=2, height=3, axis=0, level=4, no_windows=('north',))
        for x in range(3, 14):
            for y in (1, 2, 3): d.set((x, y, 2), BRICK)
        d.door((8, 1, 11), 'solid-oak', 'north')
        hut_block(d, (13, 1, 10))
        for x in (4, 8, 12): d.place('game:forge', (x, 1, 3)); d.place('game:anvil-copper', (x, 1, 5))
        d.put('game:barrel', [(6, 1, 4), (10, 1, 4)]); d.place('game:toolrack-west', (3, 2, 7)); d.place('game:toolrack-west', (3, 2, 8)); d.place('game:toolrack-west', (13, 2, 7))
        stone_chimney(d, 7, 2, 4, 13, BRICK, wide=True)
        d.stair_run((11, 1, 9), 'west', 3, 'planks')
        for x in (8, 9, 10, 11): d.set((x, 4, 9), None)
        for x in (3, 6, 9): d.put('w4rd0sfurniture:roughtable-north', [(x, 5, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 5, 4)])
        for z in (6, 7, 8, 9): d.place('game:toolrack-west', (3, 6, z)); d.place('game:toolrack-west', (13, 6, z))
        d.beds(2, [(7, 5, 8), (11, 5, 8)], 'wood', 'south') or None; d.put_against('game:chest', [(13, 5, 3)])
        d.lantern((8, 3, 7)); d.lantern((8, 7, 6)); d.lantern((8, 2, 12), 'south'); banner(d, (10, 3, 12), 'south')
        porch(d, (6, 12, 10, 13), 1, 3, 'north')
        store_shed(d, store, 1, 3, 'west', COBBLE, 4, door=((18, 1, 5), 'east'))
        woodpile(d, [(20, 3), (21, 3), (22, 3), (20, 7), (21, 7), (22, 7)], 1, 1, 'we'); d.put('game:crate', [(23, 1, 5)])
        d.fence((2, 14, 28, 19), 0, 'woodenfence-oak', gaps=[(2, 16)])
        for x in (26, 27): d.set((x, 0, 16), HAY); d.set((x, 1, 16), HAY); d.set((x, 0, 18), HAY)
        d.ground((2, 12, 28, 13), -1, COBBLE)
        d.fence((1, 0, 29, 19), 0, 'woodenfence-oak', gaps=[(8, 19), (9, 19)]) if False else None
        d.fence((1, 0, 29, 20), 0, 'woodenfence-oak', gaps=[(29, 12), (29, 13)])
        d.path([(x, z) for x in (8, 9) for z in (12, 13)] + [(16, z) for z in range(2, 14)] + [(x, 12) for x in range(10, 29)], -1)
        grass(d, (0, 0, 30, 20), -1, every=11)
    else:
        # A weaponsmiths' guild: a stone-brick forge hall with four forges and two chimneys, a mud-brick hafting
        # hall above lined with racks, a hip-roofed tower for the master and the pattern blades, a stone shaft
        # vault, a flagged yard with a trying range behind drystone walls, banners and flags.
        hall = (2, 1, 16, 13); tow = (16, 7, 22, 13); vault = (16, 1, 22, 6)
        yard(d, (0, 0, 34, 24))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('north', 'east'))
        house(d, tow, storeys=1, height=3, axis=1, level=5, ends='gable', no_windows=('west',))
        opening(d, [(16, 10)], 1, 2)
        store_shed(d, vault, 1, 3, 'west', BRICK, 5, door=((16, 1, 3), 'east'))
        woodpile(d, [(18, 2), (19, 2), (20, 2), (18, 5), (19, 5), (20, 5)], 1, 1, 'we'); d.put('game:crate', [(21, 1, 3)])
        for x in range(3, 16):
            for y in (1, 2, 3): d.set((x, y, 2), POLISHED)
        d.floor_inside(hall, 0, POLISHED)
        d.door((9, 1, 13), 'solid-oak', 'north')
        hut_block(d, (15, 1, 12))
        for x in (4, 7, 11, 14): d.place('game:forge', (x, 1, 3)); d.place('game:anvil-copper', (x, 1, 5))
        d.put('game:barrel', [(5, 1, 4), (12, 1, 4)]); d.place('game:toolrack-west', (3, 2, 8)); d.place('game:toolrack-west', (3, 2, 9)); d.place('game:toolrack-west', (15, 2, 8))
        stone_chimney(d, 5, 2, 4, 15, BRICK, wide=True); stone_chimney(d, 12, 2, 4, 15, BRICK, wide=True)
        d.stair_run((13, 1, 11), 'west', 3, 'brick')
        for x in (10, 11, 12, 13): d.set((x, 4, 11), None)
        for x in (3, 6, 9, 12): d.put('w4rd0sfurniture:roughtable-north', [(x, 5, 3)]); d.put('w4rd0sfurniture:roughstool-west', [(x, 5, 4)])
        for z in (6, 7, 8, 9, 10): d.place('game:toolrack-west', (3, 6, z)); d.place('game:toolrack-west', (15, 6, z))
        d.beds(3, [(6, 5, 9), (9, 5, 9), (12, 5, 9)], 'rustic', 'south') or d.beds(3, [(6, 5, 9), (9, 5, 9), (12, 5, 9)], 'rustic')
        d.bed((18, 1, 11), 'rustic', 'east'); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(20, 1, 11)]); d.put('game:bookshelf', [(21, 1, 9)]); d.place('game:toolrack-west', (21, 2, 10))
        for p in ((9, 3, 8), (9, 7, 8), (19, 3, 10)): d.lantern(p)
        d.lantern((9, 2, 14), 'south'); banner(d, (6, 3, 14), 'south'); banner(d, (12, 3, 14), 'south')
        porch(d, (7, 14, 11, 15), 1, 3, 'north')
        d.fence((2, 17, 32, 23), 0, 'drystonefence-shale', gaps=[(2, 20)])
        for x in (29, 30): d.set((x, 0, 19), HAY); d.set((x, 1, 19), HAY); d.set((x, 0, 21), HAY); d.set((x, 1, 21), HAY)
        d.ground((2, 15, 32, 16), -1, COBBLE); flag(d, (30, 0, 15)); flag(d, (3, 0, 15))
        d.fence((1, 0, 33, 23), 0, 'drystonefence-shale', gaps=[(33, 15), (33, 16)]) if False else None
        d.fence((1, 0, 33, 24), 0, 'drystonefence-shale', gaps=[(33, 15), (33, 16)])
        d.path([(x, z) for x in (9, 10) for z in (15, 16)] + [(x, 16) for x in range(11, 33)] + [(x, 15) for x in range(11, 33)], -1)
        grass(d, (0, 0, 34, 24), -1, every=12)
    return d
