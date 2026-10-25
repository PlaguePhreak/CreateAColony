"""
Draws the Vintage Colonies paintings: every picture is original art made by this script (no outside images).

Each scene is drawn large with simple shapes, shrunk to 32 pixels per block, given a little brush noise and
a limited palette so it reads as a small oil painting in Vintage Story's style. The script also writes the
frame texture, one shape per size and the painting blocktype, so adding a picture is one entry in PAINTINGS
(plus its name in lang/en.json).

    python3 tools/paintings/paintings.py              # writes the textures, shapes and blocktype into assets/
    python3 tools/paintings/paintings.py --sheet out.png   # also writes a preview sheet with frames and names
"""
import argparse
import json
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

PX = 32           # texture pixels per block
S = 8             # drawing scale before shrinking
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
ASSETS = os.path.join(ROOT, "assets", "createacolony")
TEX_DIR = os.path.join(ASSETS, "textures", "block", "painting")
SHAPE_DIR = os.path.join(ASSETS, "shapes", "block", "painting")
BLOCKTYPE = os.path.join(ASSETS, "blocktypes", "painting.json")
SIZES = {(1, 1): "small", (2, 1): "wide", (1, 2): "tall", (2, 2): "large"}
SIDES = [("north", 0), ("east", 270), ("south", 180), ("west", 90)]


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


class Canvas:
    def __init__(self, w, h, seed):
        self.w, self.h = w * S, h * S
        self.img = Image.new("RGB", (self.w, self.h))
        self.d = ImageDraw.Draw(self.img)
        self.rng = random.Random(seed)

    # Coordinates are in texture pixels (0..32 per block); they are scaled up here.
    def p(self, pts):
        return [(x * S, y * S) for x, y in pts]

    def vgrad(self, top, bottom, y0=0, y1=None):
        y1 = self.h / S if y1 is None else y1
        for y in range(int(y0 * S), int(y1 * S)):
            t = (y - y0 * S) / max(1, (y1 - y0) * S - 1)
            self.d.line([(0, y), (self.w, y)], fill=lerp(top, bottom, t))

    def poly(self, pts, fill):
        self.d.polygon(self.p(pts), fill=fill)

    def rect(self, x0, y0, x1, y1, fill):
        self.d.rectangle([x0 * S, y0 * S, x1 * S - 1, y1 * S - 1], fill=fill)

    def ellipse(self, x0, y0, x1, y1, fill):
        self.d.ellipse([x0 * S, y0 * S, x1 * S, y1 * S], fill=fill)

    def circle(self, cx, cy, r, fill):
        self.ellipse(cx - r, cy - r, cx + r, cy + r, fill)

    def line(self, pts, fill, width=1.0):
        self.d.line(self.p(pts), fill=fill, width=max(1, int(width * S)))

    def ridge(self, y_base, amp, rough, fill, x0=0, x1=None, seed_off=0, bottom=None):
        """A hill or mountain line across the picture, filled down to the bottom."""
        x1 = self.w / S if x1 is None else x1
        bottom = self.h / S if bottom is None else bottom
        r = random.Random(self.rng.random() + seed_off)
        phase = [r.random() * 6.28 for _ in range(3)]
        pts = []
        steps = int((x1 - x0) * 2) + 1
        for i in range(steps + 1):
            x = x0 + (x1 - x0) * i / steps
            y = (y_base - amp * (0.5 * math.sin(x / 9 + phase[0]) + 0.3 * math.sin(x / 4.3 + phase[1])
                                 + 0.2 * math.sin(x / 2.1 + phase[2]))
                 + (r.random() - 0.5) * rough)
            pts.append((x, y))
        self.poly(pts + [(x1, bottom), (x0, bottom)], fill)

    def pine(self, x, base, hgt, fill, trunk=(60, 40, 28)):
        w = hgt * 0.38
        self.rect(x - 0.4, base - hgt * 0.15, x + 0.4, base, trunk)
        for k in range(3):
            top = base - hgt + k * hgt * 0.22
            bot = base - hgt * 0.15 - (2 - k) * hgt * 0.18
            ww = w * (0.55 + k * 0.25)
            self.poly([(x, top), (x + ww, bot), (x - ww, bot)], fill)

    def leafy(self, x, base, hgt, fill, dark, trunk=(70, 50, 34)):
        self.rect(x - 0.6, base - hgt * 0.5, x + 0.6, base, trunk)
        r = hgt * 0.32
        cy = base - hgt * 0.62
        self.circle(x, cy, r, dark)
        self.circle(x - r * 0.45, cy + r * 0.15, r * 0.7, fill)
        self.circle(x + r * 0.4, cy - r * 0.2, r * 0.65, fill)
        self.circle(x + r * 0.1, cy - r * 0.55, r * 0.5, lerp(fill, (230, 220, 150), 0.15))

    def house(self, x, base, w, h, wall, roof, roof_h=None, door=True):
        roof_h = roof_h or w * 0.55
        self.rect(x, base - h, x + w, base, wall)
        self.poly([(x - 0.8, base - h), (x + w / 2, base - h - roof_h), (x + w + 0.8, base - h)], roof)
        if door:
            self.rect(x + w * 0.4, base - h * 0.55, x + w * 0.6, base, (58, 40, 28))
        self.rect(x + w * 0.12, base - h * 0.75, x + w * 0.28, base - h * 0.5, (235, 190, 90))

    def stars(self, n, y_max, col=(240, 236, 210)):
        for _ in range(n):
            x = self.rng.random() * self.w / S
            y = self.rng.random() * y_max
            self.rect(x, y, x + 0.6, y + 0.6, col)

    def finish(self, colors=40):
        """Shrink, add brush noise and a canvas weave, and limit the palette."""
        small = self.img.resize((self.w // S, self.h // S), Image.LANCZOS)
        px = small.load()
        rng = random.Random(7)
        for y in range(small.height):
            for x in range(small.width):
                r, g, b = px[x, y]
                n = rng.randint(-7, 7) + (3 if (x + y) % 2 == 0 else -3)
                px[x, y] = (max(0, min(255, r + n)), max(0, min(255, g + n)), max(0, min(255, b + n)))
        q = small.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
        return q.convert("RGB")


# ---- The paintings --------------------------------------------------------------------------------

def berry_bowl(c):
    c.vgrad((52, 38, 30), (30, 22, 18))
    c.ellipse(-6, 22, 38, 40, (88, 58, 36))                        # table
    c.ellipse(6, 16, 26, 24, (40, 26, 20))                         # bowl shadow
    c.poly([(5, 17), (27, 17), (24, 25), (8, 25)], (150, 110, 70))  # bowl
    c.ellipse(5, 15, 27, 19, (120, 84, 52))
    for (x, y, col) in [(9, 15, (150, 30, 40)), (12, 13.5, (60, 40, 110)), (15, 15, (170, 40, 46)),
                        (18, 13, (70, 46, 120)), (21, 15, (140, 26, 36)), (14, 12, (190, 60, 50)),
                        (17, 15.8, (60, 40, 100)), (11, 16, (180, 50, 50)), (23, 16, (90, 50, 130))]:
        c.circle(x, y, 1.7, col)
        c.circle(x - 0.5, y - 0.6, 0.5, lerp(col, (255, 255, 255), 0.5))
    c.circle(26, 26, 2.2, (176, 140, 40))                          # a stray apple
    c.poly([(13, 9), (17, 6), (19, 8), (15, 11)], (70, 110, 50))   # leaf
    return c.finish(28)


def candlelight(c):
    c.vgrad((22, 16, 14), (12, 9, 8))
    for r, col in [(14, (60, 40, 22)), (10, (92, 62, 30)), (6, (140, 98, 46))]:
        c.circle(16, 11, r, col)
    c.rect(13, 13, 19, 27, (226, 212, 176))                         # candle
    c.rect(13, 13, 14.5, 27, (250, 240, 210))
    c.poly([(13, 13), (19, 13), (18, 15), (15, 16)], (240, 226, 190))
    c.line([(16, 13), (16, 11)], (40, 30, 24), 0.4)
    c.poly([(16, 4.5), (18, 9), (16, 11.5), (14, 9)], (250, 190, 70))  # flame
    c.poly([(16, 7), (17, 9.5), (16, 11), (15, 9.5)], (255, 245, 200))
    c.ellipse(7, 25, 25, 30, (96, 80, 66))                          # holder
    c.ellipse(9, 24.5, 23, 28, (130, 112, 92))
    return c.finish(24)


def lone_oak(c):
    c.vgrad((236, 170, 110), (120, 120, 160), 0, 20)
    c.circle(22, 14, 3, (255, 214, 140))
    c.ridge(22, 2, 0.5, (88, 98, 70))
    c.ridge(25, 1.5, 0.3, (60, 74, 46))
    c.rect(15, 13, 17, 26, (46, 34, 26))
    c.line([(16, 16), (11, 12)], (46, 34, 26), 0.9)
    c.line([(16, 15), (21, 11)], (46, 34, 26), 0.9)
    for (x, y, r) in [(16, 9, 6), (10, 11, 4.5), (22, 10, 4.5), (13, 6, 4), (19, 6, 4)]:
        c.circle(x, y, r, (40, 58, 36))
    for (x, y, r) in [(15, 7, 3), (20, 8, 2.5), (11, 9, 2.4)]:
        c.circle(x, y, r, (70, 88, 46))
    return c.finish(26)


def hooded_elder(c):
    c.vgrad((60, 66, 54), (32, 34, 30))
    c.poly([(4, 32), (7, 20), (11, 14), (21, 14), (25, 20), (28, 32)], (92, 60, 40))   # cloak
    c.poly([(9, 18), (11, 8), (16, 4), (21, 8), (23, 18), (19, 21), (13, 21)], (110, 72, 46))  # hood
    c.ellipse(12, 9, 20, 19, (36, 24, 20))                          # hood opening
    c.ellipse(13, 10, 19, 18.5, (196, 156, 122))                    # face
    c.poly([(13, 15.5), (19, 15.5), (18, 21), (16, 23), (14, 21)], (210, 206, 196))  # beard
    c.rect(14, 13, 15.2, 13.8, (40, 30, 26))
    c.rect(16.8, 13, 18, 13.8, (40, 30, 26))
    c.line([(15.5, 15.6), (16.5, 15.6)], (150, 100, 80), 0.4)
    c.line([(16, 24), (16, 31)], (176, 150, 70), 0.5)               # brooch chain
    c.circle(16, 25, 1.2, (200, 170, 70))
    return c.finish(28)


def wolf_moon(c):
    c.vgrad((14, 20, 44), (40, 50, 86), 0, 26)
    c.stars(14, 18)
    c.circle(15, 11, 7.5, (236, 230, 200))
    c.circle(18.5, 8, 1.4, (214, 206, 178))
    c.circle(19, 14, 1.0, (214, 206, 178))
    c.ridge(24, 1.5, 0.3, (24, 30, 42))
    c.poly([(1, 32), (2, 26), (8, 23), (18, 24), (24, 28), (26, 32)], (18, 20, 28))  # rock
    wolf = (10, 12, 20)
    c.poly([(6, 25), (6.5, 19), (8, 15.5), (10, 13), (11.5, 10.5), (12.5, 8), (13.2, 6.5), (14.2, 7.5),
            (14, 9.2), (15.6, 11), (14.6, 12), (13, 11.6), (12.6, 14), (12.2, 18), (13.2, 23), (14.4, 25)], wolf)
    c.poly([(11.6, 9.6), (11.4, 7), (12.6, 8.6)], wolf)                # ear
    c.poly([(6.4, 23), (3, 24.5), (2.4, 23.4), (5.6, 21.6)], wolf)     # tail
    return c.finish(22)


def toadstool(c):
    c.vgrad((40, 54, 36), (22, 30, 22))
    c.ellipse(-4, 22, 36, 40, (50, 70, 38))
    c.rect(13, 14, 19, 27, (226, 218, 196))                         # stem
    c.rect(13, 14, 14.5, 27, (246, 240, 224))
    c.ellipse(6, 7, 26, 18, (170, 34, 30))                          # cap
    c.ellipse(8, 7, 24, 12, (196, 50, 40))
    for (x, y) in [(10, 11), (14, 9), (18, 10), (22, 12), (12, 14), (16, 13), (20, 14.5)]:
        c.circle(x, y, 1, (244, 236, 220))
    c.rect(22, 23, 24, 27, (220, 210, 190))
    c.ellipse(20, 20, 26, 24, (150, 30, 28))
    for x in range(1, 32, 3):
        c.line([(x, 32), (x + 0.6, 26 + (x % 5))], (80, 110, 50), 0.4)
    return c.finish(26)


def harvest_field(c):
    c.vgrad((150, 180, 210), (240, 216, 170), 0, 18)
    c.circle(50, 7, 3.5, (255, 240, 190))
    c.ridge(15, 2, 0.4, (110, 130, 120))
    c.ridge(18, 1.2, 0.3, (90, 118, 70))
    c.vgrad((214, 172, 70), (176, 130, 46), 18, 32)
    for x in range(0, 64, 2):
        h = 2 + (x * 7 % 5)
        c.line([(x, 32), (x + 1, 32 - h)], (232, 196, 96), 0.5)
    for k in range(5):                                               # sheaves
        x = 8 + k * 11
        c.poly([(x - 2, 27), (x, 20), (x + 2, 27)], (196, 150, 56))
        c.line([(x - 1.6, 24), (x + 1.6, 24)], (140, 96, 40), 0.4)
    c.poly([(30, 25), (31, 19), (33, 18), (34, 19), (35, 25)], (110, 60, 50))  # reaper
    c.circle(32.5, 17, 1.4, (200, 160, 120))
    c.ellipse(30.5, 15.3, 34.5, 17.2, (190, 170, 110))
    c.line([(35, 19), (38, 15)], (80, 60, 40), 0.4)
    c.line([(38, 15), (40.5, 17)], (180, 180, 180), 0.4)
    return c.finish(36)


def mountain_lake(c):
    c.vgrad((100, 140, 190), (196, 210, 222), 0, 18)
    c.poly([(0, 18), (12, 6), (18, 10), (28, 3), (40, 13), (48, 7), (64, 18)], (110, 116, 136))
    c.poly([(9, 9), (12, 6), (15, 8), (13, 9.5)], (236, 240, 246))
    c.poly([(24, 6.5), (28, 3), (32, 6.5), (29, 7.5), (27, 6.7)], (236, 240, 246))
    c.poly([(45, 9), (48, 7), (51, 9.5)], (236, 240, 246))
    c.ridge(19, 1.5, 0.3, (54, 76, 58))
    c.vgrad((70, 100, 130), (40, 66, 92), 20, 32)
    for y in range(21, 32, 2):
        c.line([(c.rng.random() * 30, y), (30 + c.rng.random() * 34, y)], (120, 150, 176), 0.3)
    for x in [3, 6, 9, 52, 56, 60]:
        c.pine(x, 23, 7 + x % 3, (34, 54, 40))
    c.poly([(0, 32), (0, 24), (12, 26), (18, 32)], (60, 72, 48))
    return c.finish(36)


def river_village(c):
    c.vgrad((176, 196, 214), (226, 222, 200), 0, 16)
    c.ridge(13, 2, 0.4, (120, 140, 120))
    c.ridge(17, 1.2, 0.4, (96, 124, 72))
    c.poly([(26, 32), (30, 17), (36, 17), (46, 32)], (86, 120, 150))     # river
    c.line([(32, 20), (33, 25), (36, 30)], (150, 180, 200), 0.4)
    c.house(6, 22, 7, 5, (200, 180, 140), (168, 140, 70))
    c.house(15, 21, 6, 4.5, (176, 150, 116), (150, 120, 60))
    c.house(43, 23, 8, 5, (196, 176, 136), (110, 70, 50))
    c.house(54, 21, 6, 4, (186, 166, 130), (160, 130, 66))
    c.rect(20.5, 13, 21.3, 17, (110, 100, 96))                         # chimney smoke
    c.circle(21, 11, 1.2, (210, 210, 210))
    c.circle(22.5, 9, 1.5, (220, 220, 222))
    c.rect(27, 24, 41, 25.2, (110, 80, 54))                            # bridge
    c.leafy(2, 24, 9, (80, 110, 56), (52, 78, 42))
    c.leafy(61, 25, 10, (80, 110, 56), (52, 78, 42))
    c.poly([(0, 32), (0, 25), (26, 25), (28, 32)], (90, 120, 60))
    c.poly([(44, 32), (42, 25), (64, 25), (64, 32)], (90, 120, 60))
    return c.finish(40)


def stormy_sea(c):
    c.vgrad((40, 46, 58), (98, 106, 112), 0, 20)
    for (x, y, r) in [(10, 4, 6), (20, 3, 7), (34, 5, 6), (50, 3, 8), (60, 6, 5)]:
        c.circle(x, y, r, (66, 72, 84))
    c.line([(44, 6), (42, 11), (45, 12), (41, 19)], (240, 236, 190), 0.5)  # lightning
    c.vgrad((40, 70, 80), (20, 40, 50), 18, 32)
    c.poly([(22, 22), (40, 22), (37, 26), (25, 26)], (70, 44, 30))          # ship hull
    c.line([(31, 22), (31, 9)], (60, 40, 28), 0.6)
    c.poly([(31.5, 10), (39, 13), (31.5, 20)], (214, 204, 176))
    c.poly([(30.5, 11), (25, 15), (30.5, 19)], (200, 190, 164))
    for k in range(9):
        x = k * 8 - 2
        c.poly([(x, 30), (x + 4, 24 + (k % 3)), (x + 8, 30)], (34, 62, 72))
        c.line([(x + 2.5, 26.5 + (k % 3)), (x + 4, 24 + (k % 3)), (x + 5.5, 26 + (k % 3))], (200, 220, 220), 0.4)
    return c.finish(32)


def aurora_pines(c):
    c.vgrad((8, 14, 30), (20, 40, 60), 0, 24)
    c.stars(30, 20)
    for k, col in enumerate([(60, 200, 140), (80, 220, 160), (120, 120, 200)]):
        pts = [(x, 6 + k * 2 + 3 * math.sin(x / 7 + k)) for x in range(0, 65, 2)]
        for j in range(len(pts) - 1):
            x0, y0 = pts[j]
            c.line([(x0, y0), (x0, y0 + 5 - k)], lerp(col, (20, 40, 60), 0.45), 1.6)
    c.vgrad((200, 210, 226), (150, 166, 190), 25, 32)                 # snow
    for x in range(1, 64, 4):
        c.pine(x + (x % 3), 27, 9 + (x * 5) % 5, (14, 26, 26))
    return c.finish(32)


def waterfall(c):
    c.vgrad((150, 186, 210), (210, 224, 230), 0, 14)
    c.poly([(0, 14), (0, 64), (12, 64), (11, 10), (6, 6)], (88, 90, 92))
    c.poly([(32, 12), (32, 64), (21, 64), (22, 8), (27, 5)], (100, 100, 100))
    c.rect(11, 9, 22, 50, (190, 220, 236))                              # falling water
    for x in range(12, 22, 2):
        c.line([(x, 10), (x + 0.4, 50)], (236, 246, 250), 0.4)
    c.ellipse(4, 46, 28, 56, (240, 248, 252))                           # spray
    c.vgrad((70, 120, 140), (40, 80, 100), 52, 64)
    c.leafy(4, 22, 10, (70, 110, 60), (44, 74, 44))
    c.leafy(28, 30, 9, (70, 110, 60), (44, 74, 44))
    c.poly([(0, 64), (0, 56), (7, 54), (10, 64)], (60, 80, 50))
    c.poly([(32, 64), (32, 55), (24, 56), (22, 64)], (60, 80, 50))
    return c.finish(32)


def watchtower(c):
    c.vgrad((230, 160, 110), (90, 100, 150), 0, 48)
    c.circle(8, 40, 4, (255, 210, 150))
    c.ridge(50, 2, 0.4, (70, 70, 80))
    c.rect(11, 20, 21, 52, (120, 110, 100))                             # stone tower
    c.rect(11, 20, 13, 52, (150, 138, 124))
    for y in range(22, 52, 4):
        c.line([(11, y), (21, y)], (96, 88, 80), 0.3)
    c.rect(9, 15, 23, 20, (110, 100, 90))
    for x in (9, 13, 17, 21):
        c.rect(x, 13, x + 2, 15, (110, 100, 90))
    c.poly([(9, 13), (16, 4), (23, 13)], (110, 60, 46))                 # roof
    c.line([(16, 4), (16, 1)], (60, 40, 30), 0.4)
    c.poly([(16.3, 1), (20, 2), (16.3, 3)], (170, 40, 40))              # pennant
    c.rect(15, 27, 17, 31, (240, 190, 90))                              # lit window
    c.rect(14, 45, 18, 52, (60, 42, 30))
    c.ridge(56, 1.5, 0.3, (54, 70, 46))
    return c.finish(32)


def birch_grove(c):
    c.vgrad((196, 210, 170), (120, 150, 90), 0, 64)
    for (x, w) in [(3, 2.4), (10, 3), (18, 2.2), (25, 3.2), (30, 2)]:
        c.rect(x, 0, x + w, 64, (228, 226, 214))
        for y in range(2, 64, 5):
            yy = y + (x * 3) % 4
            c.rect(x, yy, x + w * 0.6, yy + 1, (40, 40, 36))
    for _ in range(80):                                                  # leaves
        x, y = c.rng.random() * 32, c.rng.random() * 26
        c.circle(x, y, 1.2, (150 + c.rng.randint(-20, 30), 170 + c.rng.randint(-20, 20), 70))
    c.vgrad((90, 120, 60), (60, 86, 40), 56, 64)
    for x in range(0, 32, 2):
        c.line([(x, 64), (x + 0.5, 58 + x % 4)], (110, 140, 70), 0.4)
    c.circle(14, 59, 1.3, (180, 60, 160))                                # wildflowers
    c.circle(22, 60, 1.1, (230, 220, 90))
    return c.finish(30)


def hill_castle(c):
    c.vgrad((120, 160, 200), (220, 214, 196), 0, 40)
    for (x, y, r) in [(10, 8, 4), (15, 7, 5), (20, 9, 3.5), (46, 12, 4), (51, 11, 5)]:
        c.circle(x, y, r, (240, 240, 236))
    c.ridge(34, 2, 0.4, (130, 150, 150))
    c.poly([(8, 64), (22, 34), (34, 30), (46, 36), (60, 64)], (90, 120, 60))   # hill
    c.rect(22, 18, 44, 34, (170, 160, 146))                               # castle wall
    for x in range(22, 44, 3):
        c.rect(x, 16, x + 1.6, 18, (170, 160, 146))
    c.rect(18, 10, 25, 34, (156, 146, 134))                               # towers
    c.rect(41, 12, 48, 34, (156, 146, 134))
    c.poly([(17, 10), (21.5, 2), (26, 10)], (60, 70, 100))
    c.poly([(40, 12), (44.5, 4), (49, 12)], (60, 70, 100))
    c.rect(29, 10, 36, 20, (164, 154, 140))
    c.poly([(28, 10), (32.5, 4), (37, 10)], (60, 70, 100))
    c.line([(32.5, 4), (32.5, 0.5)], (60, 40, 30), 0.4)
    c.poly([(32.8, 0.5), (37, 1.5), (32.8, 2.5)], (180, 40, 40))
    c.rect(30, 27, 35, 34, (60, 42, 30))
    for (x, y) in [(20.5, 16), (43.5, 18), (31.5, 13)]:
        c.rect(x, y, x + 1.6, y + 2.4, (40, 36, 40))
    c.line([(32.5, 34), (30, 44), (36, 54), (33, 64)], (196, 176, 130), 1.6)  # road
    c.leafy(10, 52, 12, (80, 110, 56), (52, 78, 42))
    c.leafy(55, 56, 13, (80, 110, 56), (52, 78, 42))
    c.leafy(48, 46, 8, (90, 120, 60), (60, 86, 44))
    return c.finish(44)


def winter_hunt(c):
    c.vgrad((176, 186, 196), (214, 218, 222), 0, 30)
    c.ridge(26, 3, 0.4, (150, 160, 174))
    c.vgrad((232, 236, 240), (200, 208, 220), 30, 64)
    c.ridge(34, 2, 0.3, (226, 230, 236))
    c.poly([(0, 64), (0, 38), (20, 42), (34, 64)], (240, 242, 246))
    for (x, b, h) in [(40, 36, 10), (46, 37, 12), (52, 35, 9), (58, 37, 11), (62, 36, 8)]:
        c.pine(x, b, h, (36, 46, 44))
    c.house(42, 46, 8, 4, (100, 72, 50), (120, 90, 70))
    c.poly([(41.2, 42), (46, 38.4), (50.8, 42), (49, 41.4), (46, 39.6), (43, 41.4)], (246, 248, 250))  # snow on the roof                 # snowy hut
    k = 1.5                                                                # hunters, drawn a bit larger
    for (x, y, coat) in [(6, 52, (120, 50, 40)), (13, 54, (70, 64, 110)), (20, 53, (100, 76, 46))]:
        c.poly([(x, y - 1.5 * k), (x + 0.4 * k, y - 6 * k), (x + 2.6 * k, y - 6 * k), (x + 3 * k, y - 1.5 * k)], coat)
        c.rect(x + 0.4 * k, y - 1.5 * k, x + 1.2 * k, y, (40, 30, 26))
        c.rect(x + 1.8 * k, y - 1.5 * k, x + 2.6 * k, y, (40, 30, 26))
        c.circle(x + 1.5 * k, y - 7 * k, 1.1 * k, (210, 168, 136))
        c.ellipse(x + 0.3 * k, y - 8.6 * k, x + 2.7 * k, y - 7.2 * k, (60, 44, 36))
        c.line([(x + 3.3 * k, y - 9.5 * k), (x + 3.5 * k, y)], (90, 70, 50), 0.5)
    c.poly([(26, 51), (29.5, 49.4), (33, 49.4), (34.8, 47.8), (35.8, 49.2), (34.2, 51.6), (27, 51.6)], (130, 100, 70))  # hound
    for x in (27, 33):
        c.rect(x, 51.6, x + 0.8, 54, (130, 100, 70))
    c.vgrad((150, 174, 186), (120, 150, 166), 56, 60)                      # frozen pond
    c.rect(0, 60, 64, 64, (230, 234, 240))
    for (x, y) in [(50, 57.5), (54, 58)]:                                   # skaters
        c.rect(x, y - 3, x + 1, y, (60, 40, 40))
    return c.finish(40)


def realm_map(c):
    c.vgrad((226, 204, 156), (204, 178, 130))
    c.rect(1, 1, 63, 2, (150, 110, 70))
    c.rect(1, 62, 63, 63, (150, 110, 70))
    c.rect(1, 1, 2, 63, (150, 110, 70))
    c.rect(62, 1, 63, 63, (150, 110, 70))
    land = [(8, 14), (18, 8), (30, 10), (40, 6), (52, 12), (56, 24), (50, 34), (54, 46), (44, 56), (30, 54),
            (20, 58), (10, 48), (12, 36), (6, 26)]
    c.poly(land, (196, 170, 112))
    c.line(land + [land[0]], (110, 80, 50), 0.6)
    for (x, y) in [(36, 20), (40, 18), (44, 21)]:                         # mountains
        c.poly([(x - 3, y + 3), (x, y - 2), (x + 3, y + 3)], (130, 104, 72))
    for (x, y) in [(18, 30), (22, 28), (20, 34), (25, 32)]:               # forest
        c.circle(x, y, 1.6, (90, 110, 60))
    c.line([(30, 10), (28, 22), (32, 34), (30, 54)], (90, 120, 150), 0.7)  # river
    c.rect(34, 40, 38, 43, (150, 60, 50))                                  # town
    c.poly([(33.5, 40), (36, 37.5), (38.5, 40)], (150, 60, 50))
    c.line([(36, 43), (40, 48), (46, 50)], (140, 100, 60), 0.4)
    c.circle(10, 52, 4, (160, 120, 80))                                    # compass rose
    c.poly([(10, 46), (11, 52), (10, 58), (9, 52)], (110, 60, 40))
    c.poly([(4, 52), (10, 51), (16, 52), (10, 53)], (110, 60, 40))
    c.poly([(56, 54), (59, 52), (61, 55), (58, 58)], (90, 120, 150))       # sea serpent
    c.line([(52, 58), (55, 55), (57, 58), (60, 56)], (90, 120, 150), 0.6)
    return c.finish(28)


def blank(c):
    """Plain linen, ready for dyes."""
    c.vgrad((226, 218, 196), (212, 202, 178))
    return c.finish(12)


# code, English name, width, height, draw. The blank canvas comes from the crafting recipe; dyes turn it into a picture.
PAINTINGS = [
    ("blank", "Blank Canvas", 1, 1, blank),
    ("berrybowl", "Bowl of Berries", 1, 1, berry_bowl),
    ("candlelight", "Candlelight", 1, 1, candlelight),
    ("loneoak", "Lone Oak", 1, 1, lone_oak),
    ("hoodedelder", "The Hooded Elder", 1, 1, hooded_elder),
    ("wolfmoon", "Wolf Moon", 1, 1, wolf_moon),
    ("toadstool", "Toadstool", 1, 1, toadstool),
    ("harvest", "Harvest Field", 2, 1, harvest_field),
    ("mountainlake", "Mountain Lake", 2, 1, mountain_lake),
    ("rivervillage", "River Village", 2, 1, river_village),
    ("stormysea", "Stormy Sea", 2, 1, stormy_sea),
    ("aurora", "Northern Lights", 2, 1, aurora_pines),
    ("waterfall", "Waterfall", 1, 2, waterfall),
    ("watchtower", "Watchtower at Dusk", 1, 2, watchtower),
    ("birchgrove", "Birch Grove", 1, 2, birch_grove),
    ("hillcastle", "Castle on the Hill", 2, 2, hill_castle),
    ("winterhunt", "Winter Hunt", 2, 2, winter_hunt),
    ("realmmap", "Map of the Realm", 2, 2, realm_map),
]


# The buckets of dye around a blank canvas that make each picture, in the order they go into the grid
# (above, below, left, right, then the corners). Names are the game's dye colours (dyeportion-<colour>).
DYES = {
    "berrybowl": ["red", "purple", "brown"],
    "candlelight": ["yellow", "black"],
    "loneoak": ["green", "orange"],
    "hoodedelder": ["brown", "white"],
    "wolfmoon": ["black", "blue", "white"],
    "toadstool": ["red", "white", "green"],
    "harvest": ["yellow", "orange", "blue"],
    "mountainlake": ["blue", "white", "gray"],
    "rivervillage": ["green", "blue", "brown", "yellow"],
    "stormysea": ["gray", "black", "blue", "white"],
    "aurora": ["green", "black", "blue", "purple"],
    "waterfall": ["blue", "white", "gray", "green"],
    "watchtower": ["orange", "purple", "gray", "yellow"],
    "birchgrove": ["white", "black", "green", "yellow"],
    "hillcastle": ["blue", "white", "gray", "green", "red", "brown"],
    "winterhunt": ["white", "gray", "black", "brown", "red", "blue", "green"],
    "realmmap": ["yellow", "brown", "blue", "red", "green", "black", "white", "orange"],
}


def render_all():
    out = []
    for i, (code, name, w, h, fn) in enumerate(PAINTINGS):
        img = fn(Canvas(w * PX, h * PX, seed=i * 101 + 3))
        out.append((code, name, w, h, img))
    return out


def framed(img, scale):
    """The picture in a simple dark wood frame, enlarged, for the preview sheet."""
    border = 2
    f = Image.new("RGB", (img.width + border * 2, img.height + border * 2), (92, 62, 38))
    ImageDraw.Draw(f).rectangle([0, 0, f.width - 1, f.height - 1], outline=(58, 38, 24))
    ImageDraw.Draw(f).rectangle([1, 1, f.width - 2, f.height - 2], outline=(124, 88, 54))
    f.paste(img, (border, border))
    return f.resize((f.width * scale, f.height * scale), Image.NEAREST)


def sheet(paintings, path):
    scale = 4
    groups = [("Small (1 block)", 1, 1), ("Wide (2 x 1)", 2, 1), ("Tall (1 x 2)", 1, 2), ("Large (2 x 2)", 2, 2)]
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 15)
        head = ImageFont.truetype("DejaVuSans-Bold.ttf", 20)
    except OSError:
        font = head = ImageFont.load_default()
    pad, label = 22, 22
    rows = []
    for title, w, h in groups:
        items = [(n, framed(img, scale), i) for i, (c, n, pw, ph, img) in enumerate(paintings) if (pw, ph) == (w, h) and c != "blank"]
        rows.append((title, items))
    width = max(pad + sum(max(it[1].width, 170) + pad for it in items) for _, items in rows)
    height = sum(30 + max(it[1].height for it in items) + label + pad for _, items in rows) + pad
    out = Image.new("RGB", (width, height), (238, 230, 214))
    d = ImageDraw.Draw(out)
    y = pad
    num = 1
    for title, items in rows:
        d.text((pad, y), title, font=head, fill=(60, 44, 30))
        y += 30
        x = pad
        tallest = max(it[1].height for it in items)
        for name, pic, _ in items:
            out.paste(pic, (x, y))
            d.text((x, y + pic.height + 3), f"{num}. {name}", font=font, fill=(40, 30, 22))
            x += max(pic.width, 170) + pad
            num += 1
        y += tallest + label + pad
    out.save(path)


def frame_texture():
    """Dark oak for the frames: 32 x 32 with a soft grain, no strong direction so it suits every bar."""
    rng = random.Random(11)
    img = Image.new("RGB", (32, 32))
    px = img.load()
    for y in range(32):
        for x in range(32):
            g = math.sin((x + y * 0.3) * 0.9 + math.sin(y * 0.7) * 1.5) * 6
            n = rng.randint(-6, 6)
            px[x, y] = (int(92 + g + n), int(62 + g * 0.7 + n), int(38 + g * 0.4 + n))
    return img


def face_uvs(size, tex):
    """Each face of a box shows the matching size of the texture from its top left corner."""
    dx, dy, dz = size
    rect = {"north": (dx, dy), "south": (dx, dy), "east": (dz, dy), "west": (dz, dy), "up": (dx, dz), "down": (dx, dz)}
    return {f: {"texture": tex, "uv": [0, 0, round(w, 3), round(h, 3)]} for f, (w, h) in rect.items()}


def box(name, frm, to, tex, faces=None):
    size = [to[i] - frm[i] for i in range(3)]
    f = face_uvs(size, tex)
    f.update(faces or {})
    return {"name": name, "from": frm, "to": to, "faces": f}


def shape(w, h):
    """The north version (hanging on a wall to its north, facing south): a canvas in a frame, from the block
    up and to the viewer's right when it is bigger than one block. The game turns it for the other sides."""
    W, H = w * 16, h * 16
    o, bar, depth = 0.5, 1.2, 1.4          # gap to the block edge, frame bar width, how far the frame stands out
    canvas = box("canvas", [o + bar, o + bar, 0.2], [W - o - bar, H - o - bar, 0.9], "#frame",
                 {"south": {"texture": "#canvas", "uv": [0, 0, W, H]}})
    frame = [
        box("frameBottom", [o, o, 0], [W - o, o + bar, depth], "#frame"),
        box("frameTop", [o, H - o - bar, 0], [W - o, H - o, depth], "#frame"),
        box("frameLeft", [o, o + bar, 0], [o + bar, H - o - bar, depth], "#frame"),
        box("frameRight", [W - o - bar, o + bar, 0], [W - o, H - o - bar, depth], "#frame"),
    ]
    return {"textureSizes": {"canvas": [W, H], "frame": [32, 32]}, "textureWidth": 32, "textureHeight": 32,
            "elements": [canvas] + frame}


def blocktype(paintings):
    by_type = lambda make: {f"*-{c}-{side}": make(w, h, angle) for c, _, w, h, _ in paintings for side, angle in SIDES}
    by_art = lambda make: {f"*-{c}-*": make(w, h) for c, _, w, h, _ in paintings}
    attrs = {f"*-{c}-*": dict({"paintingSize": SIZES[(w, h)]}, **({"dyes": DYES[c]} if c in DYES else {}))
             for c, _, w, h, _ in paintings}
    sel = lambda w, h, angle: {"x1": 0.03, "y1": 0.03, "z1": 0, "x2": w - 0.03, "y2": h - 0.03, "z2": 0.09, "rotateY": angle}
    gui = lambda w, h: {"rotation": {"x": -8, "y": 200, "z": 0}, "origin": {"x": w / 2, "y": h / 2, "z": 0.05},
                        "scale": {1: 1.25, 2: 0.78}[max(w, h)]}
    hand = lambda w, h: {"translation": {"x": -0.6, "y": -0.3, "z": -0.5}, "rotation": {"x": 0, "y": 90, "z": 0},
                         "origin": {"x": w / 2, "y": h / 2, "z": 0.05}, "scale": {1: 0.8, 2: 0.5}[max(w, h)]}
    return {
        "code": "painting",
        "class": "BlockPainting",
        "behaviors": [{"name": "HorizontalAttachable", "properties": {"handleDrops": True, "dropBlockFace": "north"}}],
        "variantgroups": [
            {"code": "art", "states": [c for c, *_ in paintings]},
            {"code": "side", "states": [side for side, _ in SIDES]},
        ],
        "attributesByType": attrs,
        "creativeinventory": {"general": ["*-north"], "decorative": ["*-north"], "vintagecolonies": ["*-north"]},
        "drawtype": "json",
        "shapeByType": by_type(lambda w, h, angle: {"base": f"block/painting/{SIZES[(w, h)]}", "rotateY": angle}),
        "textures": {"canvas": {"base": "block/painting/{art}"}, "frame": {"base": "block/painting/frame"}},
        "blockmaterial": "Wood",
        "sidesolid": {"all": False},
        "sideopaque": {"all": False},
        "emitsideao": {"all": False},
        "lightAbsorption": 0,
        "resistance": 0.5,
        "collisionbox": None,
        "selectionboxByType": by_type(sel),
        "guiTransformByType": by_art(gui),
        "tpHandTransformByType": by_art(hand),
        "sounds": {"place": "game:block/planks", "break": "game:block/planks", "hit": "game:block/planks"},
    }


def write_assets(paintings):
    os.makedirs(TEX_DIR, exist_ok=True)
    os.makedirs(SHAPE_DIR, exist_ok=True)
    for code, name, w, h, img in paintings:
        img.save(os.path.join(TEX_DIR, code + ".png"))
    frame_texture().save(os.path.join(TEX_DIR, "frame.png"))
    for (w, h), size in SIZES.items():
        with open(os.path.join(SHAPE_DIR, size + ".json"), "w") as f:
            json.dump(shape(w, h), f, indent="\t")
    with open(BLOCKTYPE, "w") as f:
        f.write("// Made by tools/paintings/paintings.py: edit the list there and run it again rather than editing this file.\n")
        json.dump(blocktype(paintings), f, indent="\t")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet", help="also write a preview sheet to this path")
    ap.add_argument("--no-assets", action="store_true", help="do not write anything into assets/")
    args = ap.parse_args()
    paintings = render_all()
    if not args.no_assets:
        write_assets(paintings)
    if args.sheet:
        sheet(paintings, args.sheet)


if __name__ == "__main__":
    main()
