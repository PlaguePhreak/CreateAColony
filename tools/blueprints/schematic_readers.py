"""
Readers for Minecraft schematic files: WorldEdit .schem (Sponge format 2 and 3) and Litematica .litematic.
Each gives the blocks the same way convert_minecolonies.read does: {(x, y, z): (name, properties, None)},
x east, y up, z south, the smallest corner at 0, 0, 0, air left out.
"""
import nbt

AIR = ("minecraft:air", "minecraft:cave_air", "minecraft:void_air", "minecraft:structure_void")


def parse_state(s):
    """'minecraft:oak_stairs[facing=north,half=bottom]' -> ('minecraft:oak_stairs', {'facing': 'north', ...})"""
    if "[" not in s:
        return s, {}
    name, rest = s.split("[", 1)
    props = dict(kv.split("=", 1) for kv in rest.rstrip("]").split(",") if "=" in kv)
    return name, props


def varints(data):
    out, value, shift = [], 0, 0
    for byte in data:
        byte &= 0xFF
        value |= (byte & 0x7F) << shift
        if byte & 0x80:
            shift += 7
        else:
            out.append(value)
            value, shift = 0, 0
    return out


def read_sponge(path):
    d = nbt.read(path)
    d = d.get("Schematic", d)  # format 3 wraps everything in "Schematic"
    w, h, l = d["Width"], d["Height"], d["Length"]
    if "Blocks" in d:  # format 3
        palette, data = d["Blocks"]["Palette"], d["Blocks"]["Data"]
    else:  # format 2
        palette, data = d["Palette"], d["BlockData"]
    states = {i: parse_state(s) for s, i in palette.items()}
    blocks = {}
    for i, v in enumerate(varints(data)):
        name, props = states[v]
        if name in AIR:
            continue
        x, z, y = i % w, (i // w) % l, i // (w * l)
        blocks[(x, y, z)] = (name, props, None)
    return blocks


def read_litematic(path):
    """Every region of the file, placed where Litematica puts it relative to the others."""
    d = nbt.read(path)
    regions = []
    for r in d["Regions"].values():
        pos, size = r["Position"], r["Size"]
        sx, sy, sz = abs(size["x"]), abs(size["y"]), abs(size["z"])
        # A negative size means the region runs the other way from its position.
        corner = tuple(pos[k] + (size[k] + 1 if size[k] < 0 else 0) for k in "xyz")
        palette = [(e["Name"], e.get("Properties", {})) for e in r["BlockStatePalette"]]
        bits = max(2, (len(palette) - 1).bit_length())
        longs = [v & 0xFFFFFFFFFFFFFFFF for v in r["BlockStates"]]
        mask = (1 << bits) - 1
        regions.append((corner, sx, sy, sz, palette, bits, longs, mask))
    mins = [min(c[0][k] for c in regions) for k in range(3)]
    blocks = {}
    for corner, sx, sy, sz, palette, bits, longs, mask in regions:
        ox, oy, oz = (corner[k] - mins[k] for k in range(3))
        # Entries are packed tightly and may run over from one long into the next.
        n = sx * sy * sz
        for i in range(n):
            start = i * bits
            li, off = start >> 6, start & 63
            v = longs[li] >> off
            if off + bits > 64:
                v |= longs[li + 1] << (64 - off)
            name, props = palette[v & mask]
            if name in AIR:
                continue
            x, z, y = i % sx, (i // sx) % sz, i // (sx * sz)
            blocks[(ox + x, oy + y, oz + z)] = (name, props, None)
    return blocks


def read_any(path):
    head = nbt.read(path)
    if "Regions" in head:
        return read_litematic(path)
    return read_sponge(path)
