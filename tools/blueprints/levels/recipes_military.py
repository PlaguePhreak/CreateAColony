"""Military: Guard Tower, Barracks."""
from parts import *
from recipes_homes import recipe, RECIPES


def deck_rail(d, box, y, kind, corners=LOG):
    """A railing round the edge of a deck at y, posts on the corners."""
    x0, z0, x1, z1 = box
    for (x, z) in ring(box):
        if (x, z) in ((x0, z0), (x1, z0), (x0, z1), (x1, z1)): d.set((x, y, z), corners)
        else: d.set((x, y, z), fence_code(kind, 'ew' if z in (z0, z1) else 'ns'))


def training_post(d, x, z, y=0):
    for yy in range(y, y + 3): d.set((x, yy, z), LOG)
    d.set((x, y + 3, z), HAY)


# ---------------------------------------------------------------- Guard Tower
@recipe('guardtower')
def guardtower(d):
    L = d.level
    if L == 1:
        # A watch post: a log-post platform with a wattle parapet, a ladder, a torch, the guard's hay bed in
        # a wattle screen between the legs and a weapons chest.
        box = (2, 2, 6, 6)
        yard(d, (0, 0, 8, 8))
        for (x, z) in ((2, 2), (6, 2), (2, 6), (6, 6)):
            for y in range(0, 6): d.set((x, y, z), LOG)
        d.floor(box, 6, PLANK_X)
        deck_rail(d, box, 7, 'wattle-sticks')
        d.ladder(4, 2, 0, 6, 'north'); d.set((4, 6, 2), None); d.set((4, 7, 2), None)
        d.torch((6, 8, 6)); d.torch((2, 8, 2))
        d.floor(box, 0, DIRT)
        for z in (3, 4, 5): d.set((2, 1, z), fence_code('wattle-sticks', 'ns')); d.set((2, 2, z), fence_code('wattle-sticks', 'ns'))
        for x in (3, 4, 5): d.set((x, 1, 2), fence_code('wattle-sticks', 'ew')); d.set((x, 2, 2), fence_code('wattle-sticks', 'ew'))
        hut_block(d, (5, 1, 5))
        d.bed((3, 1, 3), 'hay', 'east') or d.bed((3, 1, 4), 'hay')
        d.put_against('game:chest', [(5, 1, 3)])
        grass(d, (0, 0, 8, 8), -1, every=6)
    elif L == 2:
        # A daub watchtower: a square timber-framed tower with daub panels on a cobble sill, two floors on a
        # ladder, a fenced open deck under a hip roof, slit windows, the guards' two beds and chest below.
        box = (2, 2, 8, 8)
        yard(d, (0, 0, 10, 10))
        w = wall_fn(2)
        tower(d, box, 1, 9, lambda x, y, z, a: COBBLE if y == 1 else (w(x, y, z, a) if y <= 6 else None), roof='hip', post_every=3, posts=True, floors_every=3, ladder=(5, 'north', 2))
        d.floor(box, 0, COBBLE)
        for (x, z) in ring(box):
            if d.free((x, 7, z)): d.set((x, 7, z), fence_code('woodenfence-oak', 'ew' if z in (2, 8) else 'ns'))
        for (x, z) in ((5, 8), (2, 5), (8, 5)): d.set((x, 5, z), None)
        d.door((5, 1, 8), 'crude', 'north')
        hut_block(d, (7, 1, 7))
        d.beds(2, [(3, 1, 3), (7, 1, 3)], 'rough', 'south'); d.put_against('game:chest', [(3, 1, 7)])
        d.torch((5, 2, 5)); d.torch((5, 5, 5)); d.torch((5, 2, 9), 'south')
        d.fence((1, 0, 9, 9), 0, 'wattle-sticks', gaps=[(5, 9)])
        grass(d, (0, 0, 10, 10), -1, every=8)
    elif L == 3:
        # A rammed-earth keep: a stout square tower with a cobble footing, three floors, an open battlemented
        # top under a torch, a lean-to guard room with three beds, a weapons chest, oak fence and a path.
        box = (2, 2, 8, 8); room = (8, 3, 14, 7)
        yard(d, (0, 0, 16, 12))
        w = wall_fn(3)
        tower(d, box, 1, 10, lambda x, y, z, a: COBBLE if y <= 2 else w(x, y, z, a), roof='flat', post_every=3, posts=True, floors_every=3, ladder=(5, 'north', 2))
        d.floor(box, 0, COBBLE)
        for y in (2, 5, 8):
            for (x, z) in ((5, 8), (2, 5), (8, 5)): d.window((x, y, z), 'hole')
        d.door((5, 1, 8), 'solid-oak', 'north')
        hut_block(d, (7, 1, 7))
        d.put_against('game:chest', [(3, 1, 7), (3, 1, 3)]); d.place('game:toolrack-west', (7, 2, 3)); d.place('game:toolrack-west', (7, 5, 3))
        lean_to(d, room, 1, 3, 'west', level=3, floor=PLANK_X)
        opening(d, [(8, 5)], 1, 2)
        d.beds(3, [(10, 1, 4), (12, 1, 4), (13, 1, 6)], 'rough', 'south')
        d.lantern((5, 3, 5)); d.lantern((5, 6, 5)); d.lantern((5, 9, 5)); d.torch((2, 13, 2)); d.torch((8, 13, 8)); d.lantern((11, 3, 5)); d.lantern((5, 2, 9), 'south')
        d.fence((1, 0, 15, 11), 0, 'woodenfence-oak', gaps=[(5, 11)])
        d.path([(5, z) for z in (9, 10, 11)], -1)
        grass(d, (0, 0, 16, 12), -1, every=10)
    elif L == 4:
        # A cobble tower with a hoarding: a tall cobble tower with a timber hoarding jettied out round the top
        # floor under a hip roof, arrow slits, four floors, a barrack hall of plank on cobble for five guards,
        # a flag, a cobbled court and oak fence.
        box = (3, 3, 9, 9); hall = (11, 1, 21, 9)
        yard(d, (0, 0, 24, 14))
        tower(d, box, 1, 12, lambda x, y, z, a: COBBLE if y <= 9 else None, roof=None, post_every=0, posts=False, floors_every=3, ladder=(6, 'north', 3))
        hb = (2, 2, 10, 10)
        d.floor(hb, 10, PLANK_X)
        d.frame(hb, 11, 13, lambda x, y, z, a: PLANK_Z if a else PLANK_X, post_every=4, beam=True)
        for (x, z) in ((6, 2), (6, 10), (2, 6), (10, 6)): d.set((x, 12, z), None)
        d.gable_roof(hb, 13, 0, ends='hip')
        d.rooms.append((hb, 13))
        for y in (2, 5, 8):
            for (x, z) in ((6, 9), (3, 6), (9, 6)): d.window((x, y, z), 'hole')
        d.floor(box, 0, COBBLE)
        d.door((6, 1, 9), 'solid-oak', 'north')
        hut_block(d, (8, 1, 8))
        d.put_against('game:chest', [(4, 1, 8), (4, 1, 4)]); d.place('game:toolrack-west', (8, 2, 4)); d.place('game:toolrack-west', (8, 5, 4)); d.put('game:crate', [(4, 4, 4)])
        house(d, hall, storeys=1, height=3, axis=0, level=4, no_windows=('west',))
        opening(d, [(9, 6), (11, 6)], 1, 2); d.set((10, 0, 6), COBBLE)
        d.beds(5, [(13, 1, 2), (16, 1, 2), (19, 1, 2), (13, 1, 8), (19, 1, 8)], 'wood', 'south') or d.beds(5, [(13, 1, 2), (16, 1, 2), (19, 1, 2), (13, 1, 8), (19, 1, 8)], 'wood')
        table_and_chairs(d, [(16, 1, 5)], 'rough'); d.put_against('game:chest', [(20, 1, 5)])
        flag(d, (6, 14, 6)) if False else None
        for y in (3, 6, 9): d.lantern((6, y, 6))
        d.lantern((6, 12, 6)); d.lantern((16, 3, 5)); d.lantern((6, 2, 10), 'south'); banner(d, (8, 3, 10), 'south')
        flag(d, (22, 0, 12))
        d.ground((2, 11, 22, 12), -1, COBBLE)
        d.fence((1, 0, 23, 13), 0, 'woodenfence-oak', gaps=[(6, 13), (23, 11)])
        d.path([(6, 13)], -1)
        grass(d, (0, 0, 24, 14), -1, every=10)
    else:
        # A stone keep: a masonry tower with a polished footing, slit windows, a barred gallery and a
        # battlement with a beacon, a corner turret, a stone-brick barrack range for eight guards, a gate
        # tower in a drystone bailey wall, flags and banners.
        box = (3, 3, 11, 11); hall = (13, 1, 27, 9); turret = (9, 9, 13, 13)
        yard(d, (0, 0, 32, 18))
        stone_tower(d, box, 1, 16, roof='flat', floors_every=4, ladder=(7, 'north', 3), bars=True)
        d.floor_inside(box, 0, POLISHED)
        d.place('game:firepit-cold', (7, 17, 7))
        stone_tower(d, turret, 1, 19, roof='hip', floors_every=0, top_layers=2)
        for y in range(1, 17): d.set((11, y, 11), BRICK) if y > 2 else d.set((11, y, 11), POLISHED)
        d.door((7, 1, 11), 'solid-oak', 'north')
        hut_block(d, (9, 1, 5))
        d.put_against('game:chest', [(4, 1, 4), (4, 1, 6), (4, 1, 8)]); d.place('game:toolrack-west', (10, 2, 4)); d.place('game:toolrack-west', (10, 2, 5)); d.put('game:crate', [(4, 5, 4), (4, 9, 4)]); d.put('game:bookshelf', [(10, 5, 10)])
        house(d, hall, storeys=1, height=4, axis=0, level=5, no_windows=('west',))
        opening(d, [(11, 6), (13, 6)], 1, 2); d.set((12, 0, 6), POLISHED)
        d.beds(8, [(15, 1, 2), (18, 1, 2), (21, 1, 2), (24, 1, 2), (15, 1, 8), (18, 1, 8), (21, 1, 8), (24, 1, 8)], 'rustic', 'south') or d.beds(8, [(15, 1, 2), (18, 1, 2), (21, 1, 2), (24, 1, 2), (15, 1, 8), (18, 1, 8), (21, 1, 8), (24, 1, 8)], 'rustic')
        table_and_chairs(d, [(20, 1, 5)], 'rustic', double=True); d.put('w4rd0sfurniture:rusticwardrobe-east', [(26, 1, 5)])
        # the gate tower in the bailey wall
        gt = (5, 14, 9, 18)
        d.plain_walls(gt, 1, 5, BRICK); d.floor((6, 15, 8, 17), 5, PLANK_X)
        for (x, z) in ring(gt):
            if (x + z) % 2 == 0: d.set((x, 6, z), BRICK)
        for y in (1, 2, 3): d.set((7, y, 14), None); d.set((7, y, 18), None)
        d.gate((7, 1, 18), 'north', wide=False)
        d.ladder(5, 16, 1, 5, 'west') if False else d.ladder(8, 16, 1, 5, 'east'); d.set((8, 5, 16), None)
        for y in range(1, 3):
            for (x, z) in ring((1, 1, 30, 17)):
                if not d.get((x, y, z)) or d.get((x, y, z)) == 'game:air': d.set((x, y, z), 'game:drystonefence-shale' if False else POLISHED if y == 1 else BRICK)
        for (x, z) in ((1, 1), (30, 1), (30, 17), (1, 17)): d.set((x, 3, z), BRICK)
        for z in (14, 15, 16, 17, 18): d.set((1, 1, z), POLISHED); d.set((1, 2, z), BRICK)
        d.ground((2, 12, 29, 13), -1, COBBLE); flag(d, (28, 0, 15)); flag(d, (3, 0, 12))
        for y in (3, 7, 11, 15): d.lantern((7, y, 7))
        d.lantern((20, 3, 5)); d.lantern((7, 2, 12), 'south'); banner(d, (5, 3, 12), 'south'); banner(d, (9, 3, 12), 'south'); d.torch((5, 7, 14)); d.torch((9, 7, 18))
        d.path([(7, z) for z in (12, 13)], -1)
        grass(d, (0, 0, 32, 18), -1, every=12)
    return d


# ---------------------------------------------------------------- Barracks
@recipe('barracks')
def barracks(d):
    L = d.level
    if L == 1:
        # A guard camp: a long wattle shelter with four hay beds and a weapons chest, a training post, a fire.
        box = (1, 1, 11, 5)
        yard(d, (0, 0, 14, 10))
        lean_to(d, box, 1, 3, 'north', open_sides=('south',), level=1, floor=DIRT)
        hut_block(d, (10, 1, 4))
        d.beds(4, [(2, 1, 2), (4, 1, 2), (6, 1, 2), (8, 1, 2)], 'hay', 'south') or d.beds(4, [(2, 1, 2), (4, 1, 2), (6, 1, 2), (8, 1, 2)], 'hay')
        d.put_against('game:chest', [(10, 1, 2)])
        training_post(d, 12, 8); d.place('game:firepit-cold', (5, 0, 8))
        d.torch((6, 2, 1), 'south')
        grass(d, (0, 0, 14, 10), -1, every=6)
    elif L == 2:
        # A guard house: a daub hall with six rough beds down the sides and a hearth at the end, a weapons
        # chest and rack, a wattle-fenced drill yard with two training posts.
        box = (2, 1, 14, 7)
        yard(d, (0, 0, 22, 12))
        house(d, box, storeys=1, height=3, axis=0, level=2)
        d.door((8, 1, 7), 'crude', 'north')
        hut_block(d, (13, 1, 6))
        d.beds(6, [(3, 1, 2), (6, 1, 2), (9, 1, 2), (3, 1, 6), (6, 1, 6), (9, 1, 6)], 'rough', 'south') or d.beds(6, [(3, 1, 2), (6, 1, 2), (9, 1, 2), (3, 1, 6), (6, 1, 6), (9, 1, 6)], 'rough')
        for y in (1, 2): d.set((14, y, 4), COBBLE)
        chimney(d, 14, 4, 3, 6, COBBLE); d.place('game:firepit-cold', (13, 1, 4))
        d.put_against('game:chest', [(12, 1, 2)]); d.place('game:toolrack-west', (12, 2, 6)) if d.free((12, 2, 6)) else None
        training_post(d, 17, 3); training_post(d, 20, 8)
        d.torch((8, 2, 8), 'south'); d.torch((8, 2, 4))
        d.fence((1, 0, 21, 11), 0, 'wattle-sticks', gaps=[(8, 11), (9, 11)])
        grass(d, (0, 0, 22, 12), -1, every=8)
    elif L == 3:
        # A garrison: a rammed-earth barrack hall with eight beds, a mess cross-wing with a hearth and tables,
        # an armoury store of cobble, a drill yard of packed earth with posts, oak fence and path.
        hall = (2, 1, 14, 9); mess = (14, 3, 22, 9); arm = (16, 1, 22, 2)
        yard(d, (0, 0, 30, 16))
        house(d, hall, storeys=1, height=4, axis=0, level=3, no_windows=('east',))
        d.door((8, 1, 9), 'solid-oak', 'north')
        hut_block(d, (13, 1, 8))
        d.beds(8, [(3, 1, 2), (6, 1, 2), (9, 1, 2), (12, 1, 2), (3, 1, 8), (6, 1, 8), (9, 1, 8), (12, 1, 8)], 'rough', 'south') or d.beds(8, [(3, 1, 2), (6, 1, 2), (9, 1, 2), (12, 1, 2), (3, 1, 8), (6, 1, 8), (9, 1, 8), (12, 1, 8)], 'rough')
        d.put_against('game:chest', [(3, 1, 5), (13, 1, 5)])
        house(d, mess, storeys=1, height=3, axis=0, level=3, no_windows=('west',))
        opening(d, [(14, 6)], 1, 2)
        for y in (1, 2): d.set((22, y, 6), COBBLE)
        chimney(d, 22, 6, 3, 7, COBBLE); d.place('game:firepit-cold', (21, 1, 6))
        table_and_chairs(d, [(17, 1, 5)], 'rough'); d.put('game:barrel', [(15, 1, 4)])
        store_shed(d, (24, 3, 29, 9), 1, 3, 'west', COBBLE, 3, door=((24, 1, 6), 'east'))
        d.put_against('game:chest', [(26, 1, 4), (28, 1, 4)]); d.place('game:toolrack-west', (28, 2, 8)); d.put('game:crate', [(26, 1, 8)])
        d.ground((3, 11, 27, 14), -1, DIRT)
        training_post(d, 6, 12); training_post(d, 12, 13); training_post(d, 18, 12); training_post(d, 24, 13)
        d.lantern((8, 3, 5)); d.lantern((18, 3, 6)); d.lantern((8, 3, 10), 'south')
        d.fence((1, 0, 29, 15), 0, 'woodenfence-oak', gaps=[(8, 15), (9, 15)])
        d.path([(x, z) for x in (8, 9) for z in (10, 15)], -1)
        grass(d, (0, 0, 30, 16), -1, every=10)
    elif L == 4:
        # A fort house: a two-storey cobble-and-plank barracks for ten with the mess below and the bunk floor
        # above, an armoury tower of cobble, a flagged parade ground, posts, banners and oak fence.
        hall = (2, 1, 16, 9); tb = (16, 3, 22, 9)
        yard(d, (0, 0, 32, 18))
        house(d, hall, storeys=2, height=3, axis=0, level=4, no_windows=('east',))
        d.door((9, 1, 9), 'solid-oak', 'north')
        hut_block(d, (15, 1, 8))
        for y in (1, 2, 3): d.set((3, y, 2), BRICK); d.set((4, y, 2), BRICK)
        chimney(d, 3, 2, 4, 10, BRICK); d.place('game:firepit-cold', (4, 1, 3))
        table_and_chairs(d, [(8, 1, 5)], 'rough'); table_and_chairs(d, [(12, 1, 5)], 'rough'); d.put('game:barrel', [(6, 1, 2), (15, 1, 2)]); d.put_against('game:chest', [(3, 1, 7)])
        d.stair_run((14, 1, 7), 'west', 3, 'planks')
        for x in (11, 12, 13, 14): d.set((x, 4, 7), None)
        d.beds(10, [(3, 5, 2), (6, 5, 2), (9, 5, 2), (12, 5, 2), (15, 5, 2), (3, 5, 8), (6, 5, 8), (9, 5, 8), (3, 5, 5), (15, 5, 5)], 'wood', 'south') or d.beds(10, [(3, 5, 2), (6, 5, 2), (9, 5, 2), (12, 5, 2), (15, 5, 2), (3, 5, 8), (6, 5, 8), (9, 5, 8), (3, 5, 5), (15, 5, 5)], 'wood')
        tower(d, tb, 1, 10, lambda x, y, z, a: COBBLE if y <= 7 else PLANK_Z if a else PLANK_X, roof='hip', post_every=3, posts=True, floors_every=3, ladder=(21, 'north', 3))
        opening(d, [(16, 6)], 1, 2)
        d.put_against('game:chest', [(18, 1, 8), (20, 1, 8)]); d.place('game:toolrack-west', (21, 2, 5)); d.put('game:crate', [(18, 4, 8), (18, 7, 8)]); d.place('game:toolrack-west', (21, 5, 5))
        d.ground((2, 11, 30, 15), -1, COBBLE)
        for x in (6, 12, 18, 24): training_post(d, x, 14)
        flag(d, (28, 0, 12))
        d.lantern((9, 3, 5)); d.lantern((9, 7, 5)); d.lantern((19, 3, 6)); d.lantern((19, 6, 6)); d.lantern((9, 2, 10), 'south'); banner(d, (6, 3, 10), 'south'); banner(d, (12, 3, 10), 'south')
        d.fence((1, 0, 31, 17), 0, 'woodenfence-oak', gaps=[(9, 17), (10, 17)])
        d.path([(x, z) for x in (9, 10) for z in (10, 16, 17)], -1)
        grass(d, (0, 0, 32, 18), -1, every=11)
    else:
        # A citadel barracks: a stone-brick barrack block for twelve round a mess hall with a great hearth, a
        # mud-brick upper floor, a gatehouse into a drystone-walled parade ground with posts and a flag, an
        # armoury vault, two corner turrets, banners and lanterns.
        hall = (2, 1, 18, 11); vault = (20, 2, 26, 10); gate = (10, 14, 14, 18)
        yard(d, (0, 0, 32, 20))
        house(d, hall, storeys=2, height=3, axis=0, level=5, no_windows=('east',))
        d.door((10, 1, 11), 'solid-oak', 'north')
        d.floor_inside(hall, 0, POLISHED)
        hut_block(d, (17, 1, 10))
        for x in range(3, 8):
            for y in (1, 2, 3): d.set((x, y, 2), POLISHED)
        chimney(d, 5, 2, 4, 12, BRICK); d.place('game:firepit-cold', (5, 1, 3))
        table_and_chairs(d, [(9, 1, 5)], 'rustic', double=True); table_and_chairs(d, [(14, 1, 5)], 'rustic', double=True); d.put('game:barrel', [(10, 1, 2), (12, 1, 2)]); d.put('w4rd0sfurniture:rusticcabinetlarge-oak-east', [(17, 1, 3)])
        d.stair_run((16, 1, 9), 'west', 3, 'brick')
        for x in (13, 14, 15, 16): d.set((x, 4, 9), None)
        cells = [(x, 5, 2) for x in (3, 6, 9, 12, 15)] + [(x, 5, 9) for x in (3, 6, 9, 12)] + [(3, 5, 5), (17, 5, 5), (17, 5, 8)]
        d.beds(12, cells, 'rustic', 'south') or d.beds(12, cells, 'rustic')
        store_shed(d, vault, 1, 3, 'west', BRICK, 5, door=((20, 1, 6), 'east'))
        d.put_against('game:chest', [(22, 1, 3), (24, 1, 3), (22, 1, 9), (24, 1, 9)]); d.place('game:toolrack-west', (25, 2, 6)); d.put('game:crate', [(22, 1, 6)])
        # the parade ground inside a wall with turrets, entered by the gatehouse
        d.ground((2, 12, 30, 18), -1, COBBLE)
        for (x, z) in ring((1, 12, 31, 19)):
            if d.free((x, 1, z)) and d.free((x, 2, z)) and not (10 <= x <= 14 and z == 19): d.set((x, 1, z), POLISHED); d.set((x, 2, z), BRICK)
        for (tx, tz) in ((1, 19), (31, 19)):
            for y in range(1, 6): d.set((tx, y, tz), BRICK)
            d.set((tx, 6, tz), slab('cobble', 'down')); d.torch((tx, 7, tz)) if False else d.lantern((tx, 6, tz))
        d.plain_walls(gate, 1, 5, BRICK); d.floor((11, 15, 13, 17), 5, PLANK_X)
        for (x, z) in ring(gate):
            if (x + z) % 2 == 0: d.set((x, 6, z), BRICK)
        for y in (1, 2, 3): d.set((12, y, 14), None); d.set((12, y, 18), None)
        d.gate((12, 1, 18), 'north')
        d.ladder(13, 16, 1, 5, 'east'); d.set((13, 5, 16), None)
        for x in (5, 20, 26): training_post(d, x, 15)
        flag(d, (28, 0, 13)); flag(d, (4, 0, 13))
        for p in ((10, 3, 6), (10, 7, 6), (23, 3, 6)): d.lantern(p)
        d.lantern((10, 2, 12), 'south'); banner(d, (7, 3, 12), 'south'); banner(d, (13, 3, 12), 'south'); d.torch((10, 7, 14)); d.torch((14, 7, 18))
        d.path([(12, z) for z in (12, 13, 19)], -1)
        grass(d, (0, 0, 32, 20), -1, every=12)
    return d
