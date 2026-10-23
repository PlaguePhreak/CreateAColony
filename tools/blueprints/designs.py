"""
The 12th Century Inspired buildings, levels 1 to 5 for every hut.

Each design function takes a Build and fills it for build.level. Later levels keep the walls of the
earlier ones where they can (so an upgrade builds round what is there) and add rooms, storeys, wings and
stone. The hut block stays on the same spot for every level.

Looks: timber frames with wattle and daub on stone sills, thatched roofs on the small and early
buildings, oak shingles later, stone halls and towers at the top levels; open hearths, trestle tables
and benches, plain hay and wooden beds.
"""
from kit import (ASHLAR, BARREL, BASKET, BENCH, BOOKSHELF, COB, DAUB, DEBARKED, DRYSTONE, EARTH, FENCE,
                 FIREPIT, HAY, LOG_NS, LOG_UD, LOG_WE, OVEN, PATH, PLANKS, PLANKS_NS, PLANKS_UD, PLANKS_WE,
                 POLISHED, STONE, STOOL, TABLE, VESSEL, WATTLE, chair, chest, line, rect, slab, stairs, torch)

GROUND_H = 4  # ground storey: sill, two courses, wall plate
UPPER_H = 3   # upper storeys: two courses and a plate (the floor is the plate below)


def house(b, x0, z0, x1, z1, storeys=1, walls="timber", upper="timber", roof="thatch", ridge=None,
          door=None, door_kind="crude", floor=EARTH, infill=DAUB, gable=None, windows=True, over=1):
    """
    A house over the rectangle (walls included): storeys, wall kind ("timber", "stone", "log", "plank"),
    roof ("thatch", "planks", "stone", "flat" for a battlemented top, None for none). door = x of the
    front door in the south wall (z1). Returns the y of the top wall plate.
    """
    ridge = ridge or ("x" if x1 - x0 >= z1 - z0 else "z")
    edge = STONE if walls in ("timber", "log", "plank", "stone") else None
    b.floor(x0, z0, x1, z1, -1, floor, edge=edge)
    if walls == "stone":
        b.stone_walls(x0, z0, x1, z1, 0, GROUND_H)
    elif walls == "log":
        b.log_walls(x0, z0, x1, z1, 0, GROUND_H)
    elif walls == "plank":
        b.timber_walls(x0, z0, x1, z1, 0, GROUND_H, infill=PLANKS_UD)
    else:
        b.timber_walls(x0, z0, x1, z1, 0, GROUND_H, infill=infill)
    if windows:
        b.windows(window_cells(x0, z0, x1, z1, door, stone=walls == "stone"), 2)
    top = GROUND_H - 1
    for s in range(1, storeys):
        b.fill(x0 + 1, top, z0 + 1, x1 - 1, top, z1 - 1, PLANKS)
        if upper == "stone":
            b.stone_walls(x0, z0, x1, z1, top + 1, UPPER_H)
        elif upper == "plank":
            b.timber_walls(x0, z0, x1, z1, top + 1, UPPER_H, infill=PLANKS_UD, plinth=None)
        else:
            b.timber_walls(x0, z0, x1, z1, top + 1, UPPER_H, infill=infill, plinth=None)
        top += UPPER_H
        if windows:
            b.windows(window_cells(x0, z0, x1, z1, None, stone=upper == "stone"), top - 1)
    if door is not None:
        b.door(door, 0, z1, door_kind)
        b.path([(door, z1 + 1)])
    gable = gable or (STONE if (upper if storeys > 1 else walls) == "stone" else PLANKS_UD if walls == "plank" else infill)
    if roof == "flat":
        b.fill(x0 + 1, top, z0 + 1, x1 - 1, top, z1 - 1, PLANKS)
        b.battlements(x0, z0, x1, z1, top + 1)
    elif roof:
        b.gable_roof(x0, z0, x1, z1, top + 1, ridge=ridge, material=roof, over=over, gable=gable)
    return top


def window_cells(x0, z0, x1, z1, door, stone=False):
    """Every other bay of each wall, away from corners and the door; slits in stone walls are rarer."""
    step = 4 if not stone else 3
    cells = []
    for x in range(x0 + 1, x1):
        if (x - x0) % step == 1 + (step == 4) * 0 and abs(x - (door if door is not None else -999)) > 1:
            cells += [(x, z0), (x, z1)]
    for z in range(z0 + 1, z1):
        if (z - z0) % step == 1 + (step == 4) * 0 and z1 - z0 >= 4:
            cells += [(x0, z), (x1, z)]
    # Never in a post: timber posts sit on even offsets from the corner.
    return [(x, z) for x, z in cells if not ((x - x0) % 2 == 0 and z in (z0, z1)) and not ((z - z0) % 2 == 0 and x in (x0, x1))]


def shed(b, x0, z0, x1, z1, roof="thatch", height=3, posts_every=3):
    """An open-sided shed: posts and a roof, no walls."""
    for x in range(x0, x1 + 1):
        for z in range(z0, z1 + 1):
            edge_x, edge_z = x in (x0, x1), z in (z0, z1)
            if edge_x and edge_z or (edge_z and (x - x0) % posts_every == 0) or (edge_x and (z - z0) % posts_every == 0):
                b.fill(x, 0, z, x, height - 1, z, LOG_UD)
    for x in range(x0, x1 + 1):
        b.put(x, height - 1, z0, LOG_WE)
        b.put(x, height - 1, z1, LOG_WE)
    for z in range(z0, z1 + 1):
        b.put(x0, height - 1, z, LOG_NS)
        b.put(x1, height - 1, z, LOG_NS)
    for x, z in [(x0, z0), (x0, z1), (x1, z0), (x1, z1)]:
        b.put(x, height - 1, z, LOG_UD)
    b.gable_roof(x0, z0, x1, z1, height, material=roof, gable=None)


def log_pile(b, x0, z, length, high=2, along="x"):
    for i in range(length):
        for y in range(high):
            if along == "x":
                b.put(x0 + i, y, z, LOG_NS)
            else:
                b.put(x0, y, z + i, LOG_WE)


def trestle(b, x, z, length, y=0):
    """A long table with benches on both sides (W4RD0's Furniture)."""
    for i in range(length):
        b.put(x + i, y, z, TABLE)
        b.put(x + i, y, z - 1, chair("north"))
        b.put(x + i, y, z + 1, chair("south"))


# ---- Town Hall: a timber hall that becomes a Norman stone hall with a keep tower -------------------

def townhall(b):
    L = b.level
    b.hut(3, 0, 5)
    if L == 1:
        house(b, 0, 0, 10, 6, roof="thatch", door=5)
    elif L == 2:
        house(b, 0, 0, 14, 6, roof="thatch", door=5)
        b.door(12, 0, 6, "crude")
        b.path([(12, 7)])
    elif L == 3:
        house(b, 0, 0, 14, 6, storeys=2, roof="planks", door=5, door_kind="oak")
        b.staircase(10, 1, "east", 0, 4)
    else:
        house(b, 0, 0, 14, 6, storeys=2, walls="stone", roof="planks", door=5, door_kind="oak", floor=PLANKS)
        b.staircase(10, 1, "east", 0, 4)
        # A porch in front of the door.
        b.stone_walls(4, 7, 6, 9, 0, 3)
        b.floor(4, 7, 6, 9, -1, PATH)
        b.doorway(5, 0, 9)
        b.rm(5, 0, 7), b.rm(5, 1, 7)
        b.gable_roof(4, 7, 6, 9, 3, ridge="z", material="planks", gable=STONE)
        b.path([(5, 10)])
    # The hall: a hearth, the officers' table and benches, a chest for the records.
    b.put(7, 0, 3, FIREPIT)
    b.put(1, 0, 1, chest("east"))
    if L >= 3:
        b.put(1, 0, 2, BOOKSHELF)
    trestle(b, 2, 3, 3)
    b.put(9, 0, 1, BENCH)
    if L == 2:
        trestle(b, 9, 3, 3)
        b.put(13, 0, 1, chest("west"))
    elif L >= 3:
        b.put(13, 0, 5, chest("west"))
        # The headman's banner over the door.
        b.banner(2, 2, 7)
    b.put(1, 2, 5, torch("east")), b.put(9, 2, 5, torch("west"))
    if L >= 3:
        # The solar upstairs: the headman's chamber.
        b.put(2, 4, 2, TABLE), b.put(3, 4, 2, chair("east"))
        b.put(1, 4, 4, chest("east"))
        b.put(12, 5, 1, torch("south"))
    if L >= 5:
        # A square stone tower at the west end, three storeys over the hall with battlements.
        b.stone_walls(-6, -1, 0, 5, 0, 11)
        b.floor(-6, -1, 0, 5, -1, PLANKS, edge=STONE)
        b.fill(-5, 3, 0, -1, 3, 4, PLANKS)
        b.fill(-5, 7, 0, -1, 7, 4, PLANKS)
        b.fill(-5, 10, 0, -1, 10, 4, PLANKS)
        b.battlements(-6, -1, 0, 5, 11)
        b.ladder(-3, 0, 0, 10)
        b.doorway(0, 0, 2)
        b.windows([(-6, 2), (-3, 5), (-6, 1)], 5)
        b.windows([(-6, 3), (-4, 5), (-2, -1)], 9)
        b.put(-2, 8, 4, torch("north"))
        b.banner(-3, 8, 6)
    return b


# ---- Builder's Hut: a carpenter's house and yard -------------------------------------------------

def builder(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 6, 4, roof="thatch", door=3)
    elif L == 3:
        house(b, 0, 0, 10, 4, roof="thatch", door=3)
    else:
        house(b, 0, 0, 10, 4, storeys=2, roof="planks", door=3, door_kind="oak",
              walls="stone" if L == 5 else "timber", floor=PLANKS if L == 5 else EARTH)
        b.staircase(6, 1, "east", 0, 4)
    b.put(5, 0, 1, chest("west"))
    b.put(1, 0, 1, BARREL)
    b.put(1, 2, 2, torch("east"))
    if L >= 2:
        # The work shed: an open roof over the sawhorses and timber.
        x0 = 8 if L == 2 else 12
        shed(b, x0, 0, x0 + 4, 4, roof="thatch" if L < 4 else "planks")
        log_pile(b, x0 + 1, 1, 3, high=2)
        b.put(x0 + 1, 0, 3, slab("planks")), b.put(x0 + 3, 0, 3, slab("planks"))
    if L >= 3:
        b.put(9, 0, 2, chest("west")), b.put(9, 0, 3, chest("west"))
        b.put(8, 0, 3, TABLE)
    if L >= 4:
        b.put(2, 4, 1, "game:bed-hay-head-east"), b.put(3, 4, 1, "game:bed-hay-feet-east")
        b.put(9, 4, 2, chest("west"))
    if L == 5:
        # A fenced timber yard in front.
        b.fence_line(line((12, 6), (16, 6)) + line((16, 5), (16, 9)) + line((12, 9), (16, 9)))
        log_pile(b, 13, 8, 3, high=1)
        b.path(line((3, 6), (11, 6)))
    return b


# ---- Farmer's Hut: cottage, barn, longhouse, granary ---------------------------------------------

def farmer(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 6, 4, roof="thatch", door=3)
    else:
        # House and barn under one long roof.
        house(b, 0, 0, 14, 5, roof="thatch" if L < 5 else "planks", door=3, storeys=1 if L < 5 else 2)
        b.windows([(7, 0), (7, 5)], 1)
        b.fill(7, 0, 1, 7, 2, 4, DAUB)  # cross wall between house and byre
        b.doorway(7, 0, 2)
        b.rm(10, 0, 5), b.rm(11, 0, 5), b.rm(10, 1, 5), b.rm(11, 1, 5)  # wide barn door
        b.path([(10, 6), (11, 6)])
        for x in range(9, 14):
            b.put(x, 0, 1, HAY)
        b.put(13, 1, 1, HAY), b.put(12, 1, 1, HAY)
        b.put(13, 0, 4, chest("west"))
    b.put(5, 0, 1, chest("west"))
    b.put(1, 0, 1, BASKET)
    b.put(1, 2, 2, torch("east"))
    if L == 2:
        # A small barn beside the cottage.
        house(b, 8, 0, 14, 4, roof="thatch", windows=False)
        b.rm(11, 0, 4), b.rm(11, 1, 4), b.rm(10, 0, 4), b.rm(10, 1, 4)
        for x in range(9, 14):
            b.put(x, 0, 1, HAY)
    # The kitchen garden, fenced.
    gx = 0
    b.fence_line(rect(gx, 7, gx + 6, 11))
    b.rm(gx + 3, 0, 7)
    if L >= 4:
        # A granary on stone staddles, kept off the damp ground.
        for x, z in [(9, -6), (13, -6), (9, -2), (13, -2)]:
            b.put(x, 0, z, STONE)
        b.fill(9, 1, -6, 13, 1, -2, PLANKS)
        b.timber_walls(9, -6, 13, -2, 2, 3, infill=PLANKS_UD, plinth=None)
        b.gable_roof(9, -6, 13, -2, 5, ridge="x", material="thatch" if L == 4 else "planks", gable=PLANKS_UD)
        b.rm(11, 2, -2), b.rm(11, 3, -2)
        b.put(11, 0, -1, stairs("planks", "north"))  # a step up to the granary door
        b.put(10, 2, -5, BASKET), b.put(12, 2, -5, BASKET)
    if L == 5:
        # A dry stone cattle pen beside the byre.
        b.fence_line(rect(16, 0, 22, 6), family=DRYSTONE)
        b.rm(16, 0, 3)
    return b


# ---- Forester's Hut: a log cabin and woodsheds ---------------------------------------------------

def forester(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 6, 4, walls="log", roof="thatch", door=3, gable=PLANKS_UD)
    elif L == 3:
        house(b, 0, 0, 8, 5, walls="log", roof="planks", door=3, gable=PLANKS_UD)
    else:
        house(b, 0, 0, 8, 5, walls="log", upper="plank", storeys=2, roof="planks", door=3, door_kind="oak", gable=PLANKS_UD)
        b.staircase(4, 1, "east", 0, 4)
    b.put(1, 2, 2, torch("east"))
    b.put(1, 0, 1, BARREL)
    if L <= 2:
        b.put(5, 0, 1, chest("west"))
    if L >= 2:
        sx = 9 if L == 2 else 11
        shed(b, sx, 0, sx + 3, 4, roof="thatch")
        log_pile(b, sx, 1, 4, high=2)
        log_pile(b, sx, 3, 4, high=1)
    if L >= 3:
        b.put(7, 0, 3, chest("west")), b.put(7, 0, 4, chest("west"))
    if L >= 4:
        b.put(1, 4, 1, "game:bed-hay-head-east"), b.put(2, 4, 1, "game:bed-hay-feet-east")
    if L == 5:
        # A second woodshed and a fenced sapling nursery.
        shed(b, 11, 6, 14, 9, roof="planks")
        log_pile(b, 11, 7, 4, high=2)
        b.fence_line(rect(0, 8, 8, 12))
        b.rm(4, 0, 8)
        b.path(line((3, 7), (3, 7)))
    return b


# ---- Cook's Hut: an open hearth kitchen that becomes a stone kitchen and bakehouse ---------------

def cook(b):
    L = b.level
    b.hut(1, 0, 4)
    if L <= 2:
        house(b, 0, 0, 6, 5, roof="thatch", door=3)
    else:
        house(b, 0, 0, 8, 6, walls="stone", roof="stone" if L >= 4 else "planks", door=3, door_kind="oak",
              storeys=2 if L == 5 else 1, upper="timber")
        if L == 5:
            b.ladder(2, 1, 0, 3)
    # The hearth in the middle of the room and the cook's things round it.
    b.put(3, 0, 2, FIREPIT)
    b.put(5, 0, 1, VESSEL)
    b.put(1, 0, 1, chest("east"))
    b.put(5, 0, 4, TABLE)
    b.put(1, 2, 2, torch("east"))
    if L >= 2:
        b.put(5, 0, 2, OVEN)
    if L >= 3:
        b.put(7, 0, 1, VESSEL), b.put(7, 0, 4, BARREL), b.put(7, 0, 5, BASKET)
        b.put(3, 0, 4, BENCH)
    if L >= 4:
        # The larder, a stone room behind the kitchen.
        b.stone_walls(0, -5, 8, 0, 0, GROUND_H)
        b.floor(0, -5, 8, 0, -1, EARTH, edge=STONE)
        b.gable_roof(0, -5, 8, 0, GROUND_H, ridge="x", material="stone", gable=STONE)
        b.doorway(4, 0, 0)
        for x in (1, 2, 3, 5, 6, 7):
            b.put(x, 0, -4, VESSEL if x % 2 else BARREL)
        b.put(1, 0, -2, BASKET), b.put(7, 0, -2, BASKET)
        b.put(4, 2, -4, torch("south"))
    if L == 5:
        # The bakehouse: two more ovens and a second hearth.
        house(b, 9, 0, 14, 6, walls="stone", roof="stone", windows=True)
        b.doorway(9, 0, 3)
        b.put(13, 0, 1, OVEN), b.put(13, 0, 3, OVEN)
        b.put(11, 0, 4, FIREPIT)
        b.put(13, 0, 5, chest("west"))
        b.door(11, 0, 6, "oak")
        b.path([(11, 7)])
    return b


# ---- Potter's Hut: a small workshop with drying sheds; the yard stays open for the kiln pits -----

def potter(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 5, 4, roof="thatch", door=3)
    elif L <= 4:
        house(b, 0, 0, 7, 4, roof="thatch" if L == 3 else "planks", door=3, walls="timber")
    else:
        house(b, 0, 0, 7, 4, storeys=2, roof="planks", door=3, door_kind="oak")
        b.ladder(5, 1, 0, 3)
    b.put(4, 0, 1, chest("west"))
    b.put(1, 0, 1, VESSEL)
    b.put(1, 2, 2, torch("east"))
    if L >= 2:
        # A drying shed for pots, open on all sides.
        shed(b, 0, -5, 4, -2, roof="thatch")
        for x in (1, 2, 3):
            b.put(x, 0, -4, slab("planks"))
        b.put(1, 1, -4, VESSEL), b.put(3, 1, -4, VESSEL)
    if L >= 3:
        b.put(6, 0, 1, VESSEL), b.put(6, 0, 3, BASKET)
        b.put(4, 0, 3, TABLE)
    if L >= 4:
        shed(b, 6, -5, 9, -2, roof="planks")
        b.put(7, 0, -4, VESSEL), b.put(8, 0, -4, VESSEL)
    if L == 5:
        b.put(2, 4, 1, chest("east")), b.put(3, 4, 1, VESSEL)
    return b


# ---- Residence: from a cottage to a two-storey house round a yard --------------------------------

def residence(b):
    L = b.level
    b.hut(1, 0, 4)
    beds_kind = "hay" if L <= 2 else "wood"
    if L == 1:
        house(b, 0, 0, 6, 5, roof="thatch", door=3)
    elif L == 2:
        house(b, 0, 0, 10, 5, roof="thatch", door=3)
    elif L == 3:
        house(b, 0, 0, 10, 5, storeys=2, roof="planks", door=3)
        b.staircase(6, 4, "east", 0, 4)
    else:
        house(b, 0, 0, 14, 5, storeys=2, roof="planks", door=3, door_kind="oak")
        b.staircase(6, 4, "east", 0, 4)
    # Beds: two in the cottage, two more in each later part.
    if beds_kind == "hay":
        b.bed(1, 0, 1), b.bed(4, 0, 1)
        if L == 2:
            b.bed(7, 0, 1), b.bed(7, 0, 3)
    else:
        b.bed(1, 0, 2, "wood"), b.bed(3, 0, 2, "wood"), b.bed(5, 0, 2, "wood")
        b.bed(1, 4, 2, "wood"), b.bed(3, 4, 2, "wood"), b.bed(5, 4, 2, "wood")
        if L >= 4:
            b.bed(12, 0, 2, "wood"), b.bed(12, 4, 2, "wood")
    b.put(5, 0, 4, chest("west"))
    b.put(1, 2, 3, torch("east"))
    if L == 2:
        b.put(9, 0, 4, chest("west")), b.put(9, 0, 1, TABLE)
    if L >= 3:
        b.put(9, 0, 3, chest("west"))
        b.put(9, 4, 1, chest("west")), b.put(8, 0, 1, TABLE), b.put(9, 0, 1, chair("east"))
    if L >= 4:
        b.put(13, 0, 4, chest("west")), b.put(13, 4, 4, chest("west"))
    if L == 5:
        # A back wing round a small yard: two more rooms with beds.
        house(b, 0, -8, 6, -1, storeys=2, roof="planks", ridge="z")
        b.door(3, 0, -1, "oak", wall="ns")
        b.rm(3, 0, 0), b.rm(3, 1, 0)
        b.bed(1, 0, -5, "wood"), b.bed(5, 0, -5, "wood"), b.bed(1, 4, -5, "wood"), b.bed(5, 4, -5, "wood")
        b.ladder(3, -7, 0, 3)
        b.fence_line(line((7, -8), (14, -8)) + line((14, -7), (14, -1)))
    return b


# ---- Guard Tower: a timber watchtower that becomes a stone tower with barracks ------------------

def guardtower(b):
    L = b.level
    b.hut(3, 0, 3)
    if L <= 2:
        # A timber tower on a stone sill: closed storeys below, a lookout platform with a railing on top.
        top = house(b, 0, 0, 4, 4, storeys=L, roof=None, door=2, windows=False)
        b.fill(1, top, 1, 3, top, 3, PLANKS)
        b.fence_line(rect(0, 0, 4, 4), y=top + 1)
        for x, z in [(0, 0), (4, 0), (0, 4), (4, 4)]:
            b.fill(x, top + 1, z, x, top + 2, z, LOG_UD)
        b.hip_roof(0, 0, 4, 4, top + 3, material="thatch" if L == 1 else "planks", over=0)
        b.ladder(3, 1, 0, top)
        b.windows([(0, 2), (4, 2), (2, 0)], top - 1)
        b.bed(1, 0, 1)
        b.put(1, 0, 3, chest("east"))
        if L == 2:
            b.bed(1, 4, 2)
        return b
    # Levels 3-5: a stone tower, one storey more each level, battlements on top. The guards sleep on the
    # ground floor and, from level 4, in the barracks beside it, so nobody has to climb to bed.
    storeys = L - 1
    top = house(b, -1, -1, 5, 5, storeys=storeys, walls="stone", upper="stone", roof="flat", door=2,
                door_kind="oak", floor=PLANKS, windows=True)
    b.ladder(4, 0, 0, top)
    b.bed(0, 0, 4, "wood"), b.bed(1, 0, 4, "wood")
    b.put(4, 0, 3, chest("west")), b.put(4, 0, 4, BARREL)
    for y in [GROUND_H + UPPER_H * i for i in range(storeys - 1)]:
        # Upper floors: the armoury and lookouts.
        b.put(0, y, 0, BARREL), b.put(0, y, 4, chest("east"))
        b.put(2, y + 1, 4, torch("north"))
    b.put(2, 2, 4, torch("north"))
    b.banner(0, top - 2, 6)
    if L >= 4:
        # Barracks against the east side: a timber hall with more beds.
        house(b, 6, -1, 12, 5, roof="planks", door=9, windows=True)
        b.doorway(5, 0, 2)
        b.rm(6, 0, 2), b.rm(6, 1, 2)
        b.bed(7, 0, 0), b.bed(10, 0, 0), b.bed(7, 0, 4), b.bed(10, 0, 4)
        b.put(11, 0, 1, chest("west"))
        b.put(8, 0, 2, TABLE)
    if L == 5:
        # A walled yard with a gate, like a small castle bailey, joined to the tower and barracks.
        for x, z in rect(-3, 6, 14, 12):
            if z == 6 and -1 <= x <= 12 or x in (5, 6) and z == 12:
                continue
            b.fill(x, 0, z, x, 2, z, STONE)
            if (x + z) % 2 == 0:
                b.put(x, 3, z, STONE)
        b.path([(2, 6), (2, 7), (5, 12), (6, 12)])
    return b


# ---- Tavern: an alehouse that becomes a coaching inn ---------------------------------------------

def tavern(b):
    L = b.level
    b.hut(1, 0, 5)
    if L == 1:
        house(b, 0, 0, 8, 6, roof="thatch", door=4)
    elif L == 2:
        house(b, 0, 0, 12, 6, roof="thatch", door=4)
    else:
        house(b, 0, 0, 12, 6, storeys=2, roof="planks", door=4, door_kind="oak", floor=PLANKS if L == 5 else EARTH)
        b.staircase(8, 1, "east", 0, 4)
    # The hearth, tables, barrels of ale.
    b.put(4, 0, 3, FIREPIT)
    trestle(b, 1, 3, 2)
    b.put(7, 0, 1, BARREL), b.put(7, 0, 2, BARREL)
    b.put(1, 2, 3, torch("east"))
    if L >= 2:
        trestle(b, 9, 4, 3 if L == 2 else 2)
        if L == 2:
            for x in range(9, 12):
                b.put(x, 0, 1, BARREL)
        b.put(6, 0, 5, chest("west"))
    if L >= 3:
        # Rooms upstairs for travellers.
        b.bed(1, 4, 1), b.bed(4, 4, 1), b.bed(1, 4, 4), b.bed(4, 4, 4)
        b.put(7, 4, 5, chest("west"))
    if L >= 4:
        # The brewhouse: a stone lean-to with a fire and barrels.
        house(b, 13, 0, 18, 6, walls="stone", roof="stone")
        b.doorway(12, 0, 4)
        b.rm(13, 0, 4), b.rm(13, 1, 4)
        b.put(16, 0, 2, FIREPIT)
        for z in (1, 3, 5):
            b.put(17, 0, z, BARREL)
        b.put(15, 2, 1, torch("south"))
    if L == 5:
        # A stable yard and a wing of rooms behind.
        house(b, 0, -7, 8, -1, storeys=2, roof="planks", ridge="x")
        b.rm(2, 0, 0), b.rm(2, 1, 0)
        b.door(2, 0, -1, "oak")
        b.bed(1, 0, -5), b.bed(5, 0, -5), b.bed(1, 4, -5), b.bed(5, 4, -5)
        b.ladder(7, -6, 0, 3)
        b.fence_line(line((9, -7), (18, -7)) + line((18, -6), (18, -1)))
        for x in (11, 14):
            b.put(x, 0, -6, HAY)
    return b


# ---- Warehouse: a tithe barn, with every chest within reach of the warehouse block ----------------

def warehouse(b):
    """The warehouse block is at the middle; chests count while they are within 5 blocks across and 3 up or down."""
    L = b.level
    b.hut(0, 0, 0)
    half_x = {1: 3, 2: 4, 3: 5, 4: 5, 5: 5}[L]
    half_z = {1: 3, 2: 3, 3: 4, 4: 4, 5: 5}[L]
    x0, x1, z0, z1 = -half_x, half_x, -half_z, half_z
    walls = "stone" if L == 5 else "timber"
    house(b, x0, z0, x1, z1, walls=walls, roof="thatch" if L <= 2 else "planks", door=0,
          door_kind="crude" if L <= 2 else "oak", floor=PLANKS if L >= 3 else EARTH, windows=L < 5)
    if L >= 3:
        # A wagon door: two doors side by side.
        b.door(-1, 0, z1, "oak")
        b.path([(-1, z1 + 1)])
    # Chests along the walls, never in the way of the doors.
    spots = [(x, z0 + 1) for x in range(x0 + 1, x1)]
    spots += [(x0 + 1, z) for z in range(z0 + 2, z1 - 1)] + [(x1 - 1, z) for z in range(z0 + 2, z1 - 1)]
    want = {1: 4, 2: 8, 3: 12, 4: 16, 5: 20}[L]
    if L >= 4:
        # A row down the middle too.
        spots += [(x, -1 if L == 4 else -2) for x in range(-3, 4) if x != 0]
    placed = 0
    for x, z in spots:
        if placed >= want:
            break
        if abs(x) <= 5 and abs(z) <= 5 and (x, z) != (0, 0):
            facing = "south" if z == z0 + 1 else "east" if x == x0 + 1 else "west" if x == x1 - 1 else "south"
            b.put(x, 0, z, chest(facing))
            placed += 1
    assert placed == want, f"warehouse level {L}: room for {placed} chests, wanted {want}"
    b.put(x0 + 1, 2, z1 - 1, torch("east")), b.put(x1 - 1, 2, z1 - 1, torch("west"))
    if L == 5:
        # Buttresses on the long walls, as on a stone tithe barn.
        for x in (-3, 3):
            b.put(x, 0, z0 - 1, STONE), b.put(x, 1, z0 - 1, stairs("stone", "south"))
        for z in (-3, 3):
            b.put(x0 - 1, 0, z, STONE), b.put(x0 - 1, 1, z, stairs("stone", "east"))
            b.put(x1 + 1, 0, z, STONE), b.put(x1 + 1, 1, z, stairs("stone", "west"))
        b.windows([(x0, -2), (x1, -2), (x0, 2), (x1, 2)], 2)
    return b


# ---- Prospector's Hut: a lodge with maps, later a lookout ---------------------------------------

def prospector(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 5, 4, roof="thatch", door=3)
    else:
        house(b, 0, 0, 7, 4, roof="planks", door=3, door_kind="oak" if L >= 4 else "crude",
              walls="stone" if L == 5 else "timber")
    b.put(4, 0, 1, chest("west"))
    b.put(1, 0, 1, TABLE), b.put(2, 0, 1, chair("east"))
    b.put(1, 2, 2, torch("east"))
    if L >= 2:
        # Rock samples in baskets outside.
        b.put(-1, 0, 1, BASKET), b.put(-1, 0, 2, BASKET)
    if L >= 3:
        b.put(6, 0, 1, BOOKSHELF), b.put(6, 0, 3, chest("west"))
    if L >= 4:
        # A timber lookout to spot outcrops from.
        top = house(b, 9, 0, 11, 2, storeys=3, roof=None, windows=False, door=10)
        b.fill(10, top, 1, 10, top, 1, PLANKS)
        b.fence_line(rect(9, 0, 11, 2), y=top + 1)
        b.ladder(10, 1, 0, top)
        if L == 5:
            b.hip_roof(9, 0, 11, 2, top + 3, material="planks", over=0)
            for x, z in [(9, 0), (11, 0), (9, 2), (11, 2)]:
                b.fill(x, top + 1, z, x, top + 2, z, LOG_UD)
    return b


# ---- Miner's Hut: the miner's house and the headframe over the shaft -----------------------------

MINE = (10, 0, 2)  # the Mine waypoint: the shaft goes down here, 3x3 round it


def miner(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 5, 4, roof="thatch", door=3)
    elif L <= 4:
        house(b, 0, 0, 6, 5, roof="planks", door=3, walls="timber" if L == 3 else "stone")
    else:
        house(b, 0, 0, 6, 5, storeys=2, walls="stone", roof="planks", door=3, door_kind="oak")
        b.ladder(5, 1, 0, 3)
    b.put(4, 0, 1, chest("west"))
    b.put(1, 0, 1, BARREL)
    b.put(1, 2, 2, torch("east"))
    # The headframe: four posts round the shaft and a roof over it.
    mx, my, mz = MINE
    b.put(mx, my, mz, "createacolony:waypoint-mine")
    h = 4 if L < 3 else 5
    for x, z in [(mx - 2, mz - 2), (mx + 2, mz - 2), (mx - 2, mz + 2), (mx + 2, mz + 2)]:
        b.fill(x, 0, z, x, h - 1, z, LOG_UD)
    b.gable_roof(mx - 2, mz - 2, mx + 2, mz + 2, h, ridge="x", material="thatch" if L < 3 else "planks", gable=None)
    b.path([(mx - 3, mz), (mx - 4, mz), (6, 3), (7, 3)] if L <= 2 else [(mx - 3, mz), (7, 3)])
    if L >= 2:
        # An ore store beside the shaft.
        shed(b, mx - 2, mz + 4, mx + 2, mz + 7, roof="thatch" if L < 4 else "planks")
        b.put(mx - 1, 0, mz + 6, chest("north")), b.put(mx + 1, 0, mz + 6, chest("north"))
    if L >= 3:
        b.put(5, 0, 4, chest("west"))
        b.put(3, 0, 1, TABLE)
    if L >= 4:
        # A railing behind the shaft.
        b.fence_line(line((mx - 1, mz - 3), (mx + 1, mz - 3)))
    if L == 5:
        b.put(1, 4, 1, "game:bed-hay-head-east"), b.put(2, 4, 1, "game:bed-hay-feet-east")
        b.put(4, 4, 4, chest("west"))
    return b


# ---- Quarry: the quarrymen's lodge, with the pit north of it -------------------------------------

QUARRY = (3, 0, -9)  # the Quarry waypoint: the pit (up to 13 by 13) is dug below it


def quarry(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 6, 4, roof="thatch", door=3)
    elif L <= 4:
        house(b, 0, 0, 8, 4, roof="planks", door=3, walls="timber" if L == 3 else "stone")
    else:
        house(b, 0, 0, 8, 4, storeys=2, walls="stone", roof="stone", door=3, door_kind="oak")
        b.ladder(7, 1, 0, 3)
    b.put(5, 0, 1, chest("west"))
    b.put(1, 0, 1, BARREL)
    b.put(1, 2, 2, torch("east"))
    qx, qy, qz = QUARRY
    b.put(qx, qy, qz, "createacolony:waypoint-quarry")
    if L >= 2:
        # The banker: an open shed where stone is dressed.
        x0 = 8 if L == 2 else 10
        shed(b, x0, 0, x0 + 4, 4, roof="thatch" if L < 4 else "planks")
        b.put(x0 + 2, 0, 2, POLISHED), b.put(x0 + 1, 0, 2, STONE)
    if L >= 3:
        b.put(7, 0, 3, chest("west")), b.put(3, 0, 1, TABLE)
        # A dry stone wall along the edge of the pit.
        b.fence_line(line((-3, -2), (9, -2)), family=DRYSTONE)
        b.rm(3, 0, -2)
    if L == 5:
        b.put(1, 4, 1, "game:bed-hay-head-east"), b.put(2, 4, 1, "game:bed-hay-feet-east")
        b.put(5, 4, 2, chest("west"))
    return b


# ---- Supply Camp: put down at once with the camp, so only one level -------------------------------

def supplycamp(b):
    b.hut(0, 0, 0)
    b.put(0, -1, 0, PATH)
    # A thatched shelter, a ring of stones round a fire and two hay beds.
    shed(b, -3, -4, 3, -1, roof="thatch")
    b.bed(-2, 0, -3), b.bed(1, 0, -3)
    b.put(0, 0, 2, FIREPIT)
    for x, z in [(-1, 1), (0, 1), (1, 1), (-1, 3), (0, 3), (1, 3), (-1, 2), (1, 2)]:
        b.put(x, -1, z, PATH)
    b.put(-2, 0, 2, LOG_NS), b.put(2, 0, 2, LOG_NS)
    return b


DESIGNS = {
    "townhall": townhall, "builder": builder, "farmer": farmer, "forester": forester, "cook": cook,
    "potter": potter, "residence": residence, "guardtower": guardtower, "tavern": tavern,
    "warehouse": warehouse, "prospector": prospector, "miner": miner, "quarry": quarry,
}
