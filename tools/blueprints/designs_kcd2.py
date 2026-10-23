"""
The KCD2 Inspired buildings, levels 1 to 5 for every hut: Bohemia around 1400, as in Kingdom Come:
Deliverance II (the villages round Troskowitz and the town of Kuttenberg).

Looks: timber frames (pale cob between dark oak posts, with braces under the wall plates) on stone
sills; steep roofs, thatch on village cottages and oak shingles in town; stone ground storeys with
jettied timber storeys over them; chimneys; wattle fences round the yards and gardens; copper lanterns
from level 3 (torches before); stone towers with a timber fighting storey and a steep roof; log palisades.

Each design function takes a Build and fills it for build.level. Like the 12th Century pack, later
levels keep the walls of the earlier ones where they can and the hut block stays on the same spot, so
an upgrade builds round what is there. The work blocks and the spots that jobs need (the Mine and
Quarry waypoints, the Warehouse chests near the warehouse block, open ground near the Potter's Hut) are
where the 12th Century pack has them.
"""
from kit import (ASHLAR, BARREL, BASKET, BENCH, BOOKSHELF, COB, DEBARKED, DRYSTONE, EARTH, FENCE, FIREPIT,
                 HAY, LOG_NS, LOG_UD, LOG_WE, OVEN, PATH, PLANKS, PLANKS_UD, POLISHED, STONE, STOOL, TABLE,
                 VESSEL, WATTLE, chair, chest, line, perimeter, rect, slab, stairs, torch)
from designs import log_pile, trestle

GROUND_H = 4  # sill, two courses, wall plate
UPPER_H = 3   # two courses and a plate
PANEL = 4     # a post every 4 blocks; the 3 between get a brace either side and the middle a window

FULL = {"planks": PLANKS, "stone": STONE, "thatch": HAY}


def light(b, x, y, z, facing):
    """A torch on the first two levels (a copper lantern is a lot to ask of a new colony), a lantern after."""
    if b.level <= 2:
        assert facing != "down", "a torch cannot hang"
        b.put(x, y, z, torch(facing))
    else:
        b.lantern(x, y, z, facing)


# ---- Walls ------------------------------------------------------------------------------------------

def frame(b, x0, z0, x1, z1, y0, height, plinth=STONE, door=None, windows=True):
    """
    Timber-framed walls round the rectangle: corner posts and a post every PANEL blocks, logs along
    the top, light cob between, a brace under the wall plate either side of each panel, and a window in
    the middle of every other panel. plinth: a sill course of stone at the bottom (None for an upper
    storey). door: x of a door in the south wall, kept clear of windows.
    """
    top = y0 + height - 1
    rows = [y for y in range(y0 + (1 if plinth else 0), top)]
    for x, z in perimeter(x0, z0, x1, z1):
        along_x = z in (z0, z1)
        corner = x in (x0, x1) and z in (z0, z1)
        off = (x - x0) if along_x else (z - z0)
        length = (x1 - x0) if along_x else (z1 - z0)
        post = corner or off % PANEL == 0 or off == length
        for y in range(y0, top + 1):
            if y == y0 and plinth:
                code = plinth
            elif y == top:
                code = LOG_UD if corner else (LOG_WE if along_x else LOG_NS)
            elif post:
                code = LOG_UD
            elif y == rows[-1] and off % PANEL in (1, 3) and len(rows) > 1:
                code = LOG_UD  # the braces
            else:
                code = COB
            b.put(x, y, z, code)
    if windows:
        y = rows[0] if plinth else rows[-1]
        for x, z in panel_middles(x0, z0, x1, z1):
            if z == z1 and door is not None and abs(x - door) <= 1:
                continue
            b.rm(x, y, z)


def panel_middles(x0, z0, x1, z1, every=2):
    """The middle block of every other full panel on each wall."""
    cells = []
    for i, x in enumerate(range(x0 + 2, x1 - 1, PANEL)):
        if i % every == 0 and x + 2 <= x1:
            cells += [(x, z0), (x, z1)]
    if z1 - z0 >= PANEL:
        for i, z in enumerate(range(z0 + 2, z1 - 1, PANEL)):
            if i % every == 0 and z + 2 <= z1:
                cells += [(x0, z), (x1, z)]
    return cells


def stone(b, x0, z0, x1, z1, y0, height, door=None, windows=True):
    """Rubble stone walls with dressed quoins, and small windows with dressed sills."""
    for x, z in perimeter(x0, z0, x1, z1):
        corner = x in (x0, x1) and z in (z0, z1)
        for y in range(y0, y0 + height):
            b.put(x, y, z, ASHLAR if corner else STONE)
    if windows:
        for x, z in panel_middles(x0, z0, x1, z1):
            if z == z1 and door is not None and abs(x - door) <= 1:
                continue
            b.rm(x, y0 + 1 if height == GROUND_H else y0 + 1, z)
            b.put(x, y0, z, ASHLAR)


def logs(b, x0, z0, x1, z1, y0, height, door=None, windows=True):
    """A log house on a stone sill, the corners notched (upright logs)."""
    for x, z in perimeter(x0, z0, x1, z1):
        corner = x in (x0, x1) and z in (z0, z1)
        for y in range(y0, y0 + height):
            b.put(x, y, z, STONE if y == y0 else LOG_UD if corner else (LOG_WE if z in (z0, z1) else LOG_NS))
    if windows:
        for x, z in panel_middles(x0, z0, x1, z1):
            if z == z1 and door is not None and abs(x - door) <= 1:
                continue
            b.rm(x, y0 + 1, z)


# ---- Roofs ------------------------------------------------------------------------------------------

def roof(b, x0, z0, x1, z1, y, ridge="x", material="planks", over=1, gable=COB, pitch=2):
    """
    A steep pitched roof (each step `pitch` blocks up) over the rectangle, its ridge along x or z,
    starting at y and overhanging by `over`. The gables and wall tops under it are filled with `gable`.
    Returns {(x, z): height of the roof there}.
    """
    full = FULL[material]
    tops = {}
    if ridge == "x":
        a0, a1, b0, b1 = x0 - over, x1 + over, z0 - over, z1 + over
    else:
        a0, a1, b0, b1 = z0 - over, z1 + over, x0 - over, x1 + over
    k = 0
    while b0 + k <= b1 - k:
        lo, hi = b0 + k, b1 - k
        base, yy = y + k * pitch, y + k * pitch + pitch - 1
        for a in range(a0, a1 + 1):
            for bb, part in ([(lo, "ridge")] if lo == hi else [(lo, "lo"), (hi, "hi")]):
                x, z = (a, bb) if ridge == "x" else (bb, a)
                for h in range(base, yy):
                    b.put(x, h, z, full)
                if part == "ridge":
                    code = {"thatch": HAY, "stone": STONE}.get(material, LOG_WE if ridge == "x" else LOG_NS)
                elif material == "thatch":
                    code = HAY
                else:
                    rises = ("south" if part == "lo" else "north") if ridge == "x" else ("east" if part == "lo" else "west")
                    code = stairs(material, rises)
                b.put(x, yy, z, code)
                tops[(x, z)] = yy
        k += 1
    if gable:
        for (x, z), yy in tops.items():
            if x0 <= x <= x1 and z0 <= z <= z1 and (x in (x0, x1) or z in (z0, z1)):
                for h in range(y, yy):
                    if (x, h, z) not in b.blocks:
                        b.put(x, h, z, gable)
    return tops


def hip(b, x0, z0, x1, z1, y, material="planks", over=1, pitch=2):
    """A steep roof sloping on all four sides, for towers."""
    full = FULL[material]
    k = 0
    while True:
        ax0, ax1, az0, az1 = x0 - over + k, x1 + over - k, z0 - over + k, z1 + over - k
        if ax0 > ax1 or az0 > az1:
            break
        base, yy = y + k * pitch, y + k * pitch + pitch - 1
        if ax0 == ax1 or az0 == az1:
            b.fill(ax0, base, az0, ax1, yy, az1, full)
            b.fill(ax0, yy + 1, az0, ax1, yy + 1, az1, LOG_UD)  # a finial
            break
        for x, z in perimeter(ax0, az0, ax1, az1):
            for h in range(base, yy):
                b.put(x, h, z, full)
            corner = x in (ax0, ax1) and z in (az0, az1)
            if material == "thatch" or corner:
                code = full
            elif z == az0:
                code = stairs(material, "south")
            elif z == az1:
                code = stairs(material, "north")
            elif x == ax0:
                code = stairs(material, "east")
            else:
                code = stairs(material, "west")
            b.put(x, yy, z, code)
        k += 1


def chimney(b, x, z, y0, tops):
    """A stone chimney stack from y0 up through the roof, two blocks above it."""
    high = max(tops.get((x, z), y0), y0) + 2
    for y in range(y0, high + 1):
        b.put(x, y, z, STONE)


# ---- Houses -----------------------------------------------------------------------------------------

def house(b, x0, z0, x1, z1, storeys=1, ground="frame", jetty=False, material="planks", ridge=None,
          door=None, door_kind="crude", floor=EARTH, windows=True, over=1, pitch=2, chim=None):
    """
    A house over the rectangle (walls included). ground: "frame", "stone" or "log". Upper storeys are
    whitewashed timber, jettied out one block over the south and north walls when `jetty`. door: x of the
    front door in the south wall. chim: (x, z) of a chimney in a wall. Returns the y of the top wall
    plate and the roof heights.
    """
    ridge = ridge or ("x" if x1 - x0 >= z1 - z0 else "z")
    b.floor(x0, z0, x1, z1, -1, floor, edge=STONE)
    {"frame": frame, "stone": stone, "log": logs}[ground](b, x0, z0, x1, z1, 0, GROUND_H, door=door, windows=windows)
    top = GROUND_H - 1
    uz0, uz1 = z0, z1
    for s in range(1, storeys):
        if jetty and s == 1:
            uz0, uz1 = z0 - 1, z1 + 1
            for x in range(x0, x1 + 1):
                b.put(x, top, uz0, LOG_WE), b.put(x, top, uz1, LOG_WE)
        b.fill(x0 + 1, top, uz0 + 1, x1 - 1, top, uz1 - 1, PLANKS)
        frame(b, x0, uz0, x1, uz1, top + 1, UPPER_H, plinth=None, windows=windows)
        top += UPPER_H
    if door is not None:
        b.door(door, 0, z1, door_kind)
        b.path([(door, z1 + 1)])
    gable = STONE if ground == "stone" and storeys == 1 else PLANKS_UD if ground == "log" and storeys == 1 else COB
    tops = roof(b, x0, uz0, x1, uz1, top + 1, ridge=ridge, material=material, over=over, gable=gable, pitch=pitch) if material else {}
    if chim:
        chimney(b, chim[0], chim[1], 0, tops)
    return top, tops


def shed(b, x0, z0, x1, z1, material="thatch", height=3, posts_every=3, pitch=2):
    """An open-sided shed: posts and a steep roof, no walls."""
    for x in range(x0, x1 + 1):
        for z in range(z0, z1 + 1):
            ex, ez = x in (x0, x1), z in (z0, z1)
            if ex and ez or (ez and (x - x0) % posts_every == 0) or (ex and (z - z0) % posts_every == 0):
                b.fill(x, 0, z, x, height - 2, z, LOG_UD)
    for x in range(x0, x1 + 1):
        b.put(x, height - 1, z0, LOG_WE), b.put(x, height - 1, z1, LOG_WE)
    for z in range(z0 + 1, z1):
        b.put(x0, height - 1, z, LOG_NS), b.put(x1, height - 1, z, LOG_NS)
    roof(b, x0, z0, x1, z1, height, material=material, gable=None, pitch=pitch)


def wattle(b, cells, gaps=()):
    """A wattle fence along the cells, with an oak post every third block and at the ends."""
    for i, (x, z) in enumerate(cells):
        if (x, z) in gaps:
            continue
        if i % 3 == 0 or i == len(cells) - 1:
            b.put(x, 0, z, LOG_UD)
        else:
            b.put(x, 0, z, WATTLE.format("ew"))


def wattle_yard(b, x0, z0, x1, z1, gaps=()):
    cells = [c for c in rect(x0, z0, x1, z1)]
    for x, z in cells:
        if (x, z) in gaps:
            continue
        corner = x in (x0, x1) and z in (z0, z1)
        off = (x - x0) if z in (z0, z1) else (z - z0)
        b.put(x, 0, z, LOG_UD if corner or off % 3 == 0 else WATTLE.format("ew"))


def palisade(b, cells, height=3, gaps=()):
    """A wall of upright logs, sharpened (a fence post on each) every other block."""
    for i, (x, z) in enumerate(cells):
        if (x, z) in gaps:
            continue
        b.fill(x, 0, z, x, height - 1, z, LOG_UD)
        if i % 2 == 0:
            b.put(x, height, z, FENCE.format("empty"))


def haystack(b, x, z):
    b.fill(x, 0, z, x + 1, 0, z + 1, HAY)
    b.put(x, 1, z, HAY)


# ---- Town Hall: the village reeve's house that becomes a town hall with a tower ---------------------

def townhall(b):
    L = b.level
    b.hut(3, 0, 5)
    if L == 1:
        house(b, 0, 0, 10, 6, material="thatch", door=5)
    elif L == 2:
        house(b, 0, 0, 14, 6, material="thatch", door=5, chim=(14, 3))
        b.door(12, 0, 6, "crude")
        b.path([(12, 7)])
    else:
        top, tops = house(b, 0, 0, 14, 6, storeys=2, ground="frame" if L == 3 else "stone", jetty=True,
                          door=5, door_kind="oak", floor=EARTH if L == 3 else PLANKS, chim=(14, 3))
        b.staircase(10, 1, "east", 0, 4)
    b.put(7, 0, 3, FIREPIT)
    b.put(1, 0, 1, chest("east"))
    trestle(b, 2, 3, 3)
    b.put(9, 0, 1, BENCH)
    light(b, 1, 2, 4, "east")
    light(b, 13 if L >= 2 else 9, 2, 4, "west")
    if L == 2:
        trestle(b, 9, 3, 3)
        b.put(13, 0, 1, chest("west"))
    if L >= 3:
        b.put(1, 0, 2, BOOKSHELF)
        b.put(13, 0, 5, chest("west"))
        # The council chamber upstairs.
        b.put(2, 4, 2, TABLE), b.put(3, 4, 2, chair("east")), b.put(2, 4, 3, chair("north"))
        b.put(1, 4, 5, chest("east")), b.put(1, 4, 1, BOOKSHELF)
        light(b, 13, 5, 4, "west")
        b.banner(5, 5, 8)
    if L >= 4:
        # An arcade under the jettied front: stone piers holding up the upper storey.
        for x in (0, 3, 8, 11, 14):
            b.fill(x, 0, 7, x, 2, 7, ASHLAR)
        for x in (1, 4, 9, 12):
            b.put(x, 2, 7, stairs("stone", "west", upside_down=True))
        for x in (2, 7, 10, 13):
            b.put(x, 2, 7, stairs("stone", "east", upside_down=True))
        b.floor(0, 7, 14, 7, -1, PATH)
        light(b, 6, 2, 7, "down")
    if L == 5:
        # The town tower at the west end: stone, a timber fighting storey jettied out on every side,
        # and a steep shingle roof.
        stone(b, -6, -1, 0, 5, 0, 12, windows=False)
        b.floor(-6, -1, 0, 5, -1, PLANKS, edge=STONE)
        for y in (3, 7, 11):
            b.fill(-5, y, 0, -1, y, 4, PLANKS)
        b.windows([(-6, 2), (-3, 5), (-3, -1)], 5)
        b.windows([(-6, 3), (-3, -1)], 9)
        for x in range(-7, 2):
            b.put(x, 11, -2, LOG_WE), b.put(x, 11, 6, LOG_WE)
        for z in range(-1, 6):
            b.put(-7, 11, z, LOG_NS), b.put(1, 11, z, LOG_NS)
        b.fill(-6, 11, -1, 0, 11, 5, PLANKS)
        frame(b, -7, -2, 1, 6, 12, UPPER_H, plinth=None)
        hip(b, -7, -2, 1, 6, 15, material="planks")
        b.ladder(-3, 0, 0, 13)
        b.doorway(0, 0, 2)
        light(b, -2, 8, 4, "north")
        light(b, -4, 13, 5, "north")
    return b


# ---- Builder's Hut: a carpenter's house with a timber yard ----------------------------------------

def builder(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 6, 4, material="thatch", door=3)
    elif L == 3:
        house(b, 0, 0, 10, 4, material="planks", door=3)
    else:
        house(b, 0, 0, 10, 4, storeys=2, ground="stone" if L == 5 else "frame", jetty=True, door=3,
              door_kind="oak", floor=PLANKS if L == 5 else EARTH)
        b.staircase(6, 1, "east", 0, 4)
    b.put(5, 0, 1, chest("west"))
    b.put(1, 0, 1, BARREL)
    light(b, 1, 2, 2, "east")
    if L >= 2:
        x0 = 8 if L == 2 else 12
        shed(b, x0, 0, x0 + 4, 4, material="thatch" if L < 4 else "planks")
        log_pile(b, x0 + 1, 1, 3, high=2)
        b.put(x0 + 1, 0, 3, slab("planks")), b.put(x0 + 3, 0, 3, slab("planks"))
    if L >= 3:
        b.put(9, 0, 2, chest("west")), b.put(9, 0, 3, chest("west"))
        b.put(8, 0, 3, TABLE)
    if L >= 4:
        b.bed(2, 4, 1)
        b.put(9, 4, 2, chest("west"))
        light(b, 1, 5, 3, "east")
    if L == 5:
        # A wattle-fenced timber yard with seasoning logs.
        wattle_yard(b, 12, 6, 17, 10, gaps=[(12, 8)])
        log_pile(b, 13, 8, 3, high=1)
        b.path(line((3, 6), (11, 6)) + [(11, 7), (11, 8)])
    return b


# ---- Farmer's Hut: a thatched cottage, a barn, the farmstead round a yard -------------------------

def farmer(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 3:
        house(b, 0, 0, 6, 4, material="thatch", door=3, chim=(0, 2) if L == 3 else None)
    else:
        house(b, 0, 0, 6, 4, storeys=2, ground="stone", material="thatch" if L == 4 else "planks", door=3,
              door_kind="oak", chim=(0, 2))
        b.ladder(5, 2, 0, 3)
    b.put(5, 0, 1, chest("west"))
    b.put(1, 0, 1, BASKET)
    light(b, 2, 2, 1, "south")
    if L >= 2:
        # The barn, thatched, with a wide door.
        bx1 = 14 if L == 2 else 16
        house(b, 8, 0, bx1, 4, material="thatch" if L < 5 else "planks", windows=False)
        for x in (11, 12):
            b.rm(x, 0, 4), b.rm(x, 1, 4)
        b.path([(11, 5), (12, 5)])
        for x in range(9, bx1):
            b.put(x, 0, 1, HAY)
        b.put(bx1 - 1, 1, 1, HAY), b.put(bx1 - 2, 1, 1, HAY)
        b.put(bx1 - 1, 0, 3, chest("west"))
    # The kitchen garden behind a wattle fence.
    wattle_yard(b, 0, 7, 6, 11, gaps=[(3, 7)])
    if L >= 3:
        haystack(b, 18, 6)
    if L >= 4:
        # A granary on stone staddles.
        for x, z in [(9, -6), (13, -6), (9, -2), (13, -2)]:
            b.put(x, 0, z, STONE)
        b.fill(9, 1, -6, 13, 1, -2, PLANKS)
        frame(b, 9, -6, 13, -2, 2, 3, plinth=None, windows=False)
        roof(b, 9, -6, 13, -2, 5, ridge="x", material="thatch" if L == 4 else "planks", gable=COB)
        b.rm(11, 2, -2), b.rm(11, 3, -2)
        b.put(11, 0, -1, stairs("planks", "north"))
        b.put(10, 2, -5, BASKET), b.put(12, 2, -5, BASKET)
    if L == 5:
        # A wattle cattle pen and a second haystack.
        wattle_yard(b, 18, 0, 24, 4, gaps=[(18, 2)])
        haystack(b, 21, 8)
    return b


# ---- Forester's Hut: a log house in the woods ------------------------------------------------------

def forester(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 6, 4, ground="log", material="thatch", door=3)
    elif L == 3:
        house(b, 0, 0, 8, 5, ground="log", material="planks", door=3, chim=(8, 2))
    else:
        house(b, 0, 0, 8, 5, storeys=2, ground="log", material="planks", door=3, door_kind="oak", chim=(8, 2))
        b.staircase(4, 1, "east", 0, 4)
    light(b, 1, 2, 2, "east")
    b.put(1, 0, 1, BARREL)
    if L <= 2:
        b.put(5, 0, 1, chest("west"))
    if L >= 2:
        sx = 9 if L == 2 else 11
        shed(b, sx, 0, sx + 3, 4, material="thatch")
        log_pile(b, sx, 1, 4, high=2)
        log_pile(b, sx, 3, 4, high=1)
    if L >= 3:
        b.put(7, 0, 3, chest("west")), b.put(7, 0, 4, chest("west"))
    if L >= 4:
        b.bed(1, 4, 1)
    if L == 5:
        shed(b, 11, 6, 14, 9, material="planks")
        log_pile(b, 11, 7, 4, high=2)
        wattle_yard(b, 0, 8, 8, 12, gaps=[(4, 8)])
        b.path([(3, 6), (3, 7), (4, 7)])
    return b


# ---- Cook's Hut: a smoky kitchen with a big chimney, later a stone kitchen and bakehouse ----------

def cook(b):
    L = b.level
    b.hut(1, 0, 4)
    if L <= 2:
        house(b, 0, 0, 6, 5, material="thatch", door=3, chim=(0, 2))
    else:
        house(b, 0, 0, 8, 6, ground="stone", material="planks", door=3, door_kind="oak",
              storeys=2 if L == 5 else 1, jetty=False, chim=(0, 2))
        if L == 5:
            b.ladder(2, 1, 0, 3)
    b.put(1, 0, 2, FIREPIT)  # the hearth against the chimney
    b.put(5, 0, 1, VESSEL)
    b.put(1, 0, 3, chest("east"))
    b.put(5, 0, 4, TABLE)
    light(b, 3, 2, 1, "south")
    if L >= 2:
        b.put(5, 0, 2, OVEN)
    if L >= 3:
        b.put(7, 0, 1, VESSEL), b.put(7, 0, 4, BARREL), b.put(7, 0, 5, BASKET)
        b.put(4, 0, 4, STOOL)
    if L >= 4:
        # The larder: a stone room behind the kitchen.
        stone(b, 0, -5, 8, 0, 0, GROUND_H, windows=False)
        b.floor(0, -5, 8, 0, -1, EARTH, edge=STONE)
        roof(b, 0, -5, 8, 0, GROUND_H, ridge="x", material="planks", gable=STONE)
        b.doorway(4, 0, 0)
        for x in (1, 2, 3, 5, 6, 7):
            b.put(x, 0, -4, VESSEL if x % 2 else BARREL)
        b.put(1, 0, -2, BASKET), b.put(7, 0, -2, BASKET)
        light(b, 4, 2, -4, "south")
    if L == 5:
        # The bakehouse with two ovens and its own hearth and chimney.
        _, tops = house(b, 9, 0, 14, 6, ground="stone", material="planks", chim=(14, 3))
        b.doorway(9, 0, 3)
        b.put(13, 0, 1, OVEN), b.put(13, 0, 5, OVEN)
        b.put(13, 0, 3, FIREPIT)
        b.put(10, 0, 1, chest("east"))
        b.door(11, 0, 6, "oak")
        b.path([(11, 7)])
    return b


# ---- Potter's Hut: a workshop with drying racks; the yard stays open for the kiln pits ------------

def potter(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 5, 4, material="thatch", door=3)
    elif L <= 4:
        house(b, 0, 0, 7, 4, material="thatch" if L == 3 else "planks", door=3)
    else:
        house(b, 0, 0, 7, 4, storeys=2, material="planks", door=3, door_kind="oak")
        b.ladder(5, 1, 0, 3)
    b.put(4, 0, 1, chest("west"))
    b.put(1, 0, 1, VESSEL)
    light(b, 1, 2, 2, "east")
    if L >= 2:
        shed(b, 0, -5, 4, -2, material="thatch")
        for x in (1, 2, 3):
            b.put(x, 0, -4, slab("planks"))
        b.put(1, 1, -4, VESSEL), b.put(3, 1, -4, VESSEL)
    if L >= 3:
        b.put(6, 0, 1, VESSEL), b.put(6, 0, 3, BASKET)
        b.put(4, 0, 3, TABLE)
    if L >= 4:
        shed(b, 6, -5, 9, -2, material="planks")
        b.put(7, 0, -4, VESSEL), b.put(8, 0, -4, VESSEL)
    if L == 5:
        b.put(2, 4, 1, chest("east")), b.put(3, 4, 1, VESSEL)
    return b


# ---- Residence: a cottage that becomes a jettied town house round a yard ---------------------------

def residence(b):
    L = b.level
    b.hut(1, 0, 4)
    if L == 1:
        house(b, 0, 0, 6, 5, material="thatch", door=3)
    elif L == 2:
        house(b, 0, 0, 10, 5, material="thatch", door=3, chim=(10, 2))
    elif L == 3:
        house(b, 0, 0, 10, 5, storeys=2, jetty=True, door=3, chim=(10, 2))
        b.staircase(6, 4, "east", 0, 4)
    else:
        house(b, 0, 0, 14, 5, storeys=2, ground="stone", jetty=True, door=3, door_kind="oak", chim=(14, 2))
        b.staircase(6, 4, "east", 0, 4)
    if L <= 2:
        b.bed(1, 0, 1), b.bed(4, 0, 1)
        if L == 2:
            b.bed(7, 0, 1), b.bed(7, 0, 3)
    else:
        b.bed(1, 0, 2, "wood"), b.bed(3, 0, 2, "wood"), b.bed(5, 0, 2, "wood")
        b.bed(1, 4, 2, "wood"), b.bed(3, 4, 2, "wood"), b.bed(5, 4, 2, "wood")
        if L >= 4:
            b.bed(12, 0, 2, "wood"), b.bed(12, 4, 2, "wood")
    b.put(5, 0, 4, chest("west"))
    light(b, 1, 2, 3, "east")
    if L == 2:
        b.put(9, 0, 4, chest("west")), b.put(9, 0, 1, TABLE)
    if L >= 3:
        b.put(9, 0, 3, chest("west"))
        b.put(9, 4, 1, chest("west")), b.put(8, 0, 1, TABLE), b.put(9, 0, 1, chair("east"))
        light(b, 1, 5, 4, "east")
    if L >= 4:
        b.put(13, 0, 4, chest("west")), b.put(13, 4, 4, chest("west"))
    if L == 5:
        # A back wing round a wattle-fenced yard.
        house(b, 0, -8, 6, -1, storeys=2, ridge="z")
        b.door(3, 0, -1, "oak", wall="ns")
        b.rm(3, 0, 0), b.rm(3, 1, 0)
        b.bed(1, 0, -5, "wood"), b.bed(5, 0, -5, "wood"), b.bed(1, 4, -5, "wood"), b.bed(5, 4, -5, "wood")
        b.ladder(3, -7, 0, 3)
        wattle(b, line((7, -8), (14, -8)) + line((14, -7), (14, -1)))
    return b


# ---- Guard Tower: a wooden watchtower, then a stone tower with a timber fighting storey -----------

def fighting_storey(b, x0, z0, x1, z1, y, material="planks"):
    """A timber storey jettied out one block all round on top of a tower at y, with a steep hip roof."""
    for x in range(x0 - 1, x1 + 2):
        b.put(x, y, z0 - 1, LOG_WE), b.put(x, y, z1 + 1, LOG_WE)
    for z in range(z0, z1 + 1):
        b.put(x0 - 1, y, z, LOG_NS), b.put(x1 + 1, y, z, LOG_NS)
    b.fill(x0, y, z0, x1, y, z1, PLANKS)
    frame(b, x0 - 1, z0 - 1, x1 + 1, z1 + 1, y + 1, UPPER_H, plinth=None)
    hip(b, x0 - 1, z0 - 1, x1 + 1, z1 + 1, y + UPPER_H + 1, material=material)
    return y + UPPER_H


def guardtower(b):
    L = b.level
    b.hut(3, 0, 3)
    if L <= 2:
        # A timber tower: closed storeys below, a shingled lookout on top.
        top, _ = house(b, 0, 0, 4, 4, storeys=L, material=None, door=2, windows=False)
        b.fill(1, top, 1, 3, top, 3, PLANKS)
        b.fence_line(rect(0, 0, 4, 4), y=top + 1)
        for x, z in [(0, 0), (4, 0), (0, 4), (4, 4)]:
            b.fill(x, top + 1, z, x, top + 2, z, LOG_UD)
        hip(b, 0, 0, 4, 4, top + 3, material="thatch" if L == 1 else "planks", over=1)
        b.ladder(3, 1, 0, top)
        b.windows([(0, 2), (4, 2), (2, 0)], top - 1)
        b.bed(1, 0, 1)
        b.put(1, 0, 3, chest("east"))
        light(b, 3, 2, 3, "west")
        if L == 2:
            b.bed(1, 4, 2)
        return b
    # Levels 3-5: a stone tower, one storey more each level, the timber fighting storey on top.
    storeys = L - 1
    top, _ = house(b, -1, -1, 5, 5, storeys=1, ground="stone", material=None, door=2, door_kind="oak", floor=PLANKS)
    for s in range(1, storeys):
        b.fill(0, top, 0, 4, top, 4, PLANKS)
        stone(b, -1, -1, 5, 5, top + 1, UPPER_H)
        top += UPPER_H
    fighting_storey(b, -1, -1, 5, 5, top)
    b.ladder(4, 0, 0, top + 1)
    b.bed(0, 0, 4, "wood"), b.bed(1, 0, 4, "wood")
    b.put(4, 0, 3, chest("west")), b.put(4, 0, 4, BARREL)
    for y in [GROUND_H + UPPER_H * i for i in range(storeys - 1)]:
        b.put(0, y, 0, BARREL), b.put(0, y, 4, chest("east"))
        light(b, 2, y + 1, 4, "north")
    light(b, 2, 2, 4, "north")
    b.banner(2, top - 1, 6)
    if L >= 4:
        # Barracks against the east side.
        house(b, 6, -1, 12, 5, material="planks", door=9)
        b.doorway(5, 0, 2)
        b.rm(6, 0, 2), b.rm(6, 1, 2)
        b.bed(7, 0, 0), b.bed(10, 0, 0), b.bed(7, 0, 4), b.bed(10, 0, 4)
        b.put(11, 0, 1, chest("west"))
        b.put(8, 0, 2, TABLE)
    if L == 5:
        # A log palisade round the yard, with a gate.
        cells = rect(-3, 6, 14, 12)
        palisade(b, [c for c in cells if not (c[1] == 6 and -1 <= c[0] <= 12)], gaps=[(5, 12), (6, 12)])
        b.path([(2, 6), (2, 7), (5, 12), (6, 12), (9, 6), (9, 7)])
    return b


# ---- Tavern: a village krčma that becomes a coaching inn ------------------------------------------

def tavern(b):
    L = b.level
    b.hut(1, 0, 5)
    if L == 1:
        house(b, 0, 0, 8, 6, material="thatch", door=4, chim=(8, 3))
    elif L == 2:
        house(b, 0, 0, 12, 6, material="thatch", door=4, chim=(12, 3))
    else:
        house(b, 0, 0, 12, 6, storeys=2, ground="frame" if L == 3 else "stone", jetty=True, door=4,
              door_kind="oak", floor=PLANKS if L == 5 else EARTH, chim=(12, 3))
        b.staircase(8, 1, "east", 0, 4)
    b.put(4, 0, 3, FIREPIT)
    trestle(b, 1, 3, 2)
    b.put(7, 0, 1, BARREL), b.put(7, 0, 2, BARREL)
    light(b, 1, 2, 4, "east")
    if L >= 2:
        trestle(b, 9, 4, 3 if L == 2 else 2)
        if L == 2:
            for x in range(9, 12):
                b.put(x, 0, 1, BARREL)
        b.put(6, 0, 5, chest("west"))
    if L >= 3:
        b.bed(1, 4, 1), b.bed(4, 4, 1), b.bed(1, 4, 4), b.bed(4, 4, 4)
        b.put(7, 4, 5, chest("west"))
        light(b, 11, 5, 2, "west")
        b.banner(6, 5, 8)  # the inn sign over the door
    if L >= 4:
        # The brewhouse.
        house(b, 13, 0, 18, 6, ground="stone", material="planks", windows=True)
        b.doorway(12, 0, 4)
        b.rm(13, 0, 4), b.rm(13, 1, 4)
        b.put(16, 0, 2, FIREPIT)
        for z in (1, 3, 5):
            b.put(17, 0, z, BARREL)
        light(b, 15, 2, 1, "south")
    if L == 5:
        # A wing of rooms and a stable yard behind.
        house(b, 0, -7, 8, -1, storeys=2, ridge="x")
        b.rm(2, 0, 0), b.rm(2, 1, 0)
        b.door(2, 0, -1, "oak")
        b.bed(1, 0, -5), b.bed(5, 0, -5), b.bed(1, 4, -5), b.bed(5, 4, -5)
        b.ladder(7, -6, 0, 3)
        wattle(b, line((9, -7), (18, -7)) + line((18, -6), (18, -1)))
        for x in (11, 14):
            b.put(x, 0, -6, HAY)
    return b


# ---- Warehouse: a merchant's storehouse, every chest within reach of the warehouse block ----------

def warehouse(b):
    """The warehouse block is at the middle; chests count while they are within 5 blocks across and 3 up or down."""
    L = b.level
    b.hut(0, 0, 0)
    half_x = {1: 3, 2: 4, 3: 5, 4: 5, 5: 5}[L]
    half_z = {1: 3, 2: 3, 3: 4, 4: 4, 5: 5}[L]
    x0, x1, z0, z1 = -half_x, half_x, -half_z, half_z
    house(b, x0, z0, x1, z1, ground="stone" if L >= 4 else "frame", material="thatch" if L <= 2 else "planks",
          door=0, door_kind="crude" if L <= 2 else "oak", floor=PLANKS if L >= 3 else EARTH, windows=L < 5,
          storeys=2 if L == 5 else 1, jetty=L == 5)
    if L >= 3:
        b.door(-1, 0, z1, "oak")
        b.path([(-1, z1 + 1)])
    spots = [(x, z0 + 1) for x in range(x0 + 1, x1)]
    spots += [(x0 + 1, z) for z in range(z0 + 2, z1 - 1)] + [(x1 - 1, z) for z in range(z0 + 2, z1 - 1)]
    want = {1: 4, 2: 8, 3: 12, 4: 16, 5: 20}[L]
    if L >= 4:
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
    light(b, x0 + 1, 2, z1 - 2, "east"), light(b, x1 - 1, 2, z1 - 2, "west")
    if L >= 4:
        # A hoist beam over the loft door.
        b.put(0, 4 if L == 4 else 6, z1 + (2 if L == 5 else 1), LOG_NS)
    return b


# ---- Prospector's Hut: a lodge and a lookout -------------------------------------------------------

def prospector(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 5, 4, material="thatch", door=3)
    else:
        house(b, 0, 0, 7, 4, material="planks", door=3, door_kind="oak" if L >= 4 else "crude",
              ground="stone" if L == 5 else "frame")
    b.put(4, 0, 1, chest("west"))
    b.put(1, 0, 1, TABLE), b.put(2, 0, 1, chair("east"))
    light(b, 1, 2, 2, "east")
    if L >= 2:
        b.put(-1, 0, 1, BASKET), b.put(-1, 0, 2, BASKET)
    if L >= 3:
        b.put(6, 0, 1, BOOKSHELF), b.put(6, 0, 3, chest("west"))
    if L >= 4:
        top, _ = house(b, 9, 0, 11, 2, storeys=3, material=None, windows=False, door=10)
        b.fill(10, top, 1, 10, top, 1, PLANKS)
        b.fence_line(rect(9, 0, 11, 2), y=top + 1)
        b.ladder(10, 1, 0, top)
        if L == 5:
            for x, z in [(9, 0), (11, 0), (9, 2), (11, 2)]:
                b.fill(x, top + 1, z, x, top + 2, z, LOG_UD)
            hip(b, 9, 0, 11, 2, top + 3, material="planks", over=1)
    return b


# ---- Miner's Hut: a miner's cottage and a shingled headframe over the shaft -----------------------

MINE = (10, 0, 2)  # the Mine waypoint: the shaft goes down here, 3x3 round it


def miner(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 5, 4, material="thatch", door=3)
    elif L <= 4:
        house(b, 0, 0, 6, 5, material="planks", door=3, ground="frame" if L == 3 else "stone")
    else:
        house(b, 0, 0, 6, 5, storeys=2, ground="stone", material="planks", door=3, door_kind="oak")
        b.ladder(5, 1, 0, 3)
    b.put(4, 0, 1, chest("west"))
    b.put(1, 0, 1, BARREL)
    light(b, 1, 2, 2, "east")
    mx, my, mz = MINE
    b.put(mx, my, mz, "createacolony:waypoint-mine")
    h = 4 if L < 3 else 5
    for x, z in [(mx - 2, mz - 2), (mx + 2, mz - 2), (mx - 2, mz + 2), (mx + 2, mz + 2)]:
        b.fill(x, 0, z, x, h - 1, z, LOG_UD)
    for x in range(mx - 2, mx + 3):
        b.put(x, h - 1, mz - 2, LOG_WE), b.put(x, h - 1, mz + 2, LOG_WE)
    roof(b, mx - 2, mz - 2, mx + 2, mz + 2, h, ridge="x", material="thatch" if L < 3 else "planks", gable=None)
    b.path([(mx - 3, mz), (mx - 4, mz), (6, 3), (7, 3)] if L <= 2 else [(mx - 3, mz), (7, 3)])
    if L >= 2:
        shed(b, mx - 2, mz + 4, mx + 2, mz + 7, material="thatch" if L < 4 else "planks")
        b.put(mx - 1, 0, mz + 6, chest("north")), b.put(mx + 1, 0, mz + 6, chest("north"))
    if L >= 3:
        b.put(5, 0, 4, chest("west"))
        b.put(3, 0, 1, TABLE)
    if L >= 4:
        b.fence_line(line((mx - 1, mz - 3), (mx + 1, mz - 3)))
    if L == 5:
        b.bed(1, 4, 1)
        b.put(4, 4, 4, chest("west"))
    return b


# ---- Quarry: the stonecutters' lodge, with the pit north of it ------------------------------------

QUARRY = (3, 0, -9)  # the Quarry waypoint: the pit (up to 13 by 13) is dug below it


def quarry(b):
    L = b.level
    b.hut(1, 0, 3)
    if L <= 2:
        house(b, 0, 0, 6, 4, material="thatch", door=3)
    elif L <= 4:
        house(b, 0, 0, 8, 4, material="planks", door=3, ground="frame" if L == 3 else "stone")
    else:
        house(b, 0, 0, 8, 4, storeys=2, ground="stone", material="planks", door=3, door_kind="oak")
        b.ladder(7, 1, 0, 3)
    b.put(5, 0, 1, chest("west"))
    b.put(1, 0, 1, BARREL)
    light(b, 1, 2, 2, "east")
    qx, qy, qz = QUARRY
    b.put(qx, qy, qz, "createacolony:waypoint-quarry")
    if L >= 2:
        x0 = 8 if L == 2 else 10
        shed(b, x0, 0, x0 + 4, 4, material="thatch" if L < 4 else "planks")
        b.put(x0 + 2, 0, 2, POLISHED), b.put(x0 + 1, 0, 2, STONE)
    if L >= 3:
        b.put(7, 0, 3, chest("west")), b.put(3, 0, 1, TABLE)
        b.fence_line(line((-3, -2), (9, -2)), family=DRYSTONE)
        b.rm(3, 0, -2)
    if L == 5:
        b.bed(1, 4, 1)
        b.put(5, 4, 2, chest("west"))
    return b


# ---- Supply Camp: a soldiers' camp shelter, put down at once ---------------------------------------

def supplycamp(b):
    b.hut(0, 0, 0)
    b.put(0, -1, 0, PATH)
    # A lean-to of poles thatched with hay, a wattle windbreak, a fire ringed with stones.
    shed(b, -3, -4, 3, -1, material="thatch")
    wattle(b, line((-3, -5), (3, -5)))
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
