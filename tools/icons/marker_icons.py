"""
Writes the Vintage Colonies map marker icons to assets/createacolony/textures/icons/worldmap/, one SVG per
icon. The game puts every SVG in that folder in the Add waypoint window's icon list, named by its file name.

Pictures come from game-icons.net (CC BY 3.0, see THIRD_PARTY_NOTICES.md), read from the Iconify JSON set
(npm package @iconify-json/game-icons). Letters are drawn from DejaVu Sans Bold. Icons for one thing in
several colours (Rose Quartz, Blue Clay...) get a letter in the corner so they can be told apart in the list.

Usage: python3 tools/icons/marker_icons.py <game-icons icons.json> [font.ttf] [output folder]
"""
import json, os, sys
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.boundsPen import BoundsPen

# name: (waypoint name suggestion, picture or "" for letters only, letters, colour the colony markers use)
# The picture is a game-icons.net name; letters alone make an element icon such as "Cu".
ICONS = {
    # Elements
    "vc-aluminum": ("Aluminum", "", "Al", "#FFFFFF"), "vc-silver": ("Silver", "", "Ag", "#FFFFFF"),
    "vc-gold": ("Gold", "", "Au", "#FFFFFF"), "vc-bismuth": ("Bismuth", "", "Bi", "#FFFFFF"),
    "vc-borax": ("Borax", "", "B", "#FFFFFF"), "vc-chromium": ("Chromium", "", "Cr", "#FFFFFF"),
    "vc-copper": ("Copper", "", "Cu", "#FFFFFF"), "vc-iron": ("Iron", "", "Fe", "#FFFFFF"),
    "vc-fluorite": ("Fluorite", "", "F", "#FFFFFF"), "vc-manganese": ("Manganese", "", "Mn", "#FFFFFF"),
    "vc-meteoriciron": ("Meteoric Iron", "", "Fe*", "#FFFFFF"), "vc-nickel": ("Nickel", "", "Ni", "#FFFFFF"),
    "vc-phosphorus": ("Phosphorus", "", "P", "#FFFFFF"), "vc-platinum": ("Platinum", "", "Pt", "#FFFFFF"),
    "vc-lead": ("Lead", "", "Pb", "#FFFFFF"), "vc-potassium": ("Potassium", "", "K", "#FFFFFF"),
    "vc-rhodium": ("Rhodium", "", "Rh", "#FFFFFF"), "vc-sodium": ("Sodium", "", "Na", "#FFFFFF"),
    "vc-sulfur": ("Sulfur", "", "S", "#FFFFFF"), "vc-tin": ("Tin", "", "Sn", "#FFFFFF"),
    "vc-titanium": ("Titanium", "", "Ti", "#FFFFFF"), "vc-uranium": ("Uranium", "", "U", "#FFFFFF"),
    "vc-zinc": ("Zinc", "", "Zn", "#FFFFFF"),
    # Minerals and soil
    "vc-coal": ("Coal", "coal-pile", "", "#FFFFFF"),
    "vc-blackcoal": ("Black Coal", "coal-pile", "B", "#080504"),
    "vc-browncoal": ("Brown Coal", "coal-pile", "Br", "#5D3D21"),
    "vc-quartz": ("Quartz", "crystal-cluster", "", "#FFFFFF"),
    "vc-rosequartz": ("Rose Quartz", "crystal-cluster", "R", "#F179AF"),
    "vc-amethyst": ("Amethyst", "crystal-cluster", "A", "#8E007E"),
    "vc-smokyquartz": ("Smoky Quartz", "crystal-cluster", "S", "#706D64"),
    "vc-olivine": ("Olivine", "crystal-cluster", "O", "#47B749"),
    "vc-diamond": ("Diamonds", "cut-diamond", "", "#FFFFFF"),
    "vc-emerald": ("Emerald", "cut-diamond", "E", "#03D941"),
    "vc-peridot": ("Peridot", "cut-diamond", "P", "#47B749"),
    "vc-saltpeter": ("Saltpeter", "powder", "", "#FFFFFF"),
    "vc-salt": ("Salt", "crystal-bars", "", "#FFFFFF"),
    "vc-stones": ("Some Stones", "stone-pile", "", "#AFAAA8"),
    "vc-peat": ("Peat", "stone-block", "P", "#885530"),
    "vc-soil": ("Soil", "dig-hole", "", "#885530"),
    "vc-terrapreta": ("Terra Preta", "dig-hole", "T", "#080504"),
    "vc-digsite": ("Digsite", "trowel", "", "#FFFFFF"),
    "vc-blueclay": ("Blue Clay", "amphora", "B", "#28417A"),
    "vc-redclay": ("Red Clay", "amphora", "R", "#ED272A"),
    "vc-fireclay": ("Fire Clay", "amphora", "F", "#706D64"),
    "vc-resin": ("Resin", "drop", "", "#FDBB3A"),
    # Traders: what they sell, with a coin
    "vc-agriculturetrader": ("Agriculture Trader", "wheat", "$", "#FFFFFF"),
    "vc-artisantrader": ("Artisan Trader", "claw-hammer", "$", "#FFFFFF"),
    "vc-buildingtrader": ("Building Materials Trader", "brick-pile", "$", "#FFFFFF"),
    "vc-clothingtrader": ("Clothing Trader", "shirt", "$", "#FFFFFF"),
    "vc-commoditiestrader": ("Commodities Trader", "wooden-crate", "$", "#FFFFFF"),
    "vc-furnituretrader": ("Furniture Trader", "wooden-chair", "$", "#FFFFFF"),
    "vc-luxuriestrader": ("Luxuries Trader", "diamond-ring", "$", "#FFFFFF"),
    "vc-survivalgoodstrader": ("Survival Goods Trader", "backpack", "$", "#FFFFFF"),
    "vc-treasuretrader": ("Treasure Hunter Trader", "treasure-map", "$", "#FFFFFF"),
    "vc-trader": ("Trader", "coins", "", "#FFFFFF"),
    # Animals
    "vc-bear": ("Bears", "polar-bear", "", "#ED272A"), "vc-boar": ("Boars", "boar", "", "#8BC34A"),
    "vc-chicken": ("Chickens", "chicken", "", "#8BC34A"), "vc-goat": ("Goats", "goat", "", "#8BC34A"),
    "vc-horse": ("Horses", "horse-head", "", "#8BC34A"), "vc-spider": ("Spiders", "spider-alt", "", "#ED272A"),
    "vc-wolf": ("Wolves", "direwolf", "", "#ED272A"), "vc-fish": ("Fish", "salmon", "", "#8BC34A"),
    "vc-deer": ("Deer", "deer", "", "#8BC34A"), "vc-pig": ("Pigs", "pig", "", "#8BC34A"),
    # Plants
    "vc-oak": ("Oak", "acorn", "", "#C8772E"), "vc-turnip": ("Turnip", "beet", "", "#C395C4"),
    "vc-bushes": ("Bushes", "berry-bush", "", "#47B749"), "vc-cabbage": ("Cabbage", "cabbage", "", "#94C948"),
    "vc-carrot": ("Carrot", "carrot", "", "#F47832"), "vc-parsnip": ("Parsnip", "carrot", "P", "#FFFFFF"),
    "vc-cherry": ("Cherry Tree", "cherry", "", "#A30A35"), "vc-flowers": ("Flowers", "daisy", "", "#F9D0DC"),
    "vc-flowers2": ("Flowers", "sunflower", "", "#FDBB3A"), "vc-onion": ("Onion", "garlic", "", "#D9D4CE"),
    "vc-pear": ("Pear", "pear", "", "#C3D941"), "vc-pines": ("Pines", "pine-tree", "", "#366E4F"),
    "vc-pumpkin": ("Pumpkin", "pumpkin", "", "#F47832"), "vc-cattail": ("Cattail", "reed", "", "#C8772E"),
    "vc-berries": ("Berries", "raspberry", "", "#ED272A"), "vc-strawberry": ("Berries", "strawberry", "", "#ED272A"),
    "vc-oaktree": ("Some Trees", "oak", "", "#47B749"), "vc-birch": ("Some Trees", "birch-trees", "", "#94C948"),
    "vc-beech": ("Some Trees", "beech", "", "#47B749"), "vc-wheat": ("Wheat", "wheat", "", "#FDBB3A"),
    "vc-hollowtrunk": ("Hollowed Trunk", "trunk-mushroom", "", "#C8772E"),
    "vc-blueberries": ("Blueberries", "grapes", "", "#204EA2"),
    "vc-cranberries": ("Cranberries", "elderberry", "", "#A30A35"),
    "vc-fruittree": ("Fruit Tree", "fruit-tree", "", "#F15A4A"), "vc-apple": ("Apple Tree", "shiny-apple", "", "#ED272A"),
    "vc-mushrooms": ("Mushrooms", "mushroom", "", "#BF9C86"),
    "vc-poisonousmushrooms": ("Poisonous Mushrooms", "mushroom-gills", "!", "#ED272A"),
    "vc-coral": ("Coral", "coral", "", "#F179AF"), "vc-plant": ("Plant", "oak-leaf", "", "#47B749"),
    "vc-sprout": ("Plant", "sprout", "", "#94C948"), "vc-bees": ("Wild Bees", "tree-beehive", "", "#FDBB3A"),
    # Places and things
    "vc-windmill": ("Windmill", "windmill", "", "#FFFFFF"), "vc-anvil": ("Anvil", "anvil", "", "#FFFFFF"),
    "vc-storage": ("Storage", "chest", "", "#C8772E"), "vc-ingots": ("Ingots", "gold-bar", "", "#FDBB3A"),
    "vc-alchemy": ("Alchemy", "round-potion", "", "#92479B"), "vc-farm": ("Farm", "barn", "", "#F15A4A"),
    "vc-camp": ("Camp", "campfire", "", "#F47832"), "vc-ruins": ("Ruins", "castle-ruins", "", "#AFAAA8"),
    "vc-ruins2": ("Ruins", "ancient-ruins", "", "#AFAAA8"), "vc-mountains": ("Mountains", "mountains", "", "#FFFFFF"),
    "vc-rip": ("RIP", "grave-flowers", "", "#C8C8C8"), "vc-tower": ("Tower of Solitude", "stone-tower", "", "#FFFFFF"),
    "vc-boat": ("Boat", "sailboat", "", "#FFFFFF"), "vc-library": ("Library", "open-book", "", "#FFFFFF"),
    "vc-archive": ("Archive", "bookshelf", "", "#FFFFFF"), "vc-food": ("Fooooood", "knife-fork", "", "#FFFFFF"),
    "vc-deadly": ("Something Deadly", "death-skull", "", "#ED272A"),
    "vc-translocator": ("Translocator", "portal", "", "#14A4DD"),
    # Signs
    "vc-down": ("Down", "", "↓", "#FFFFFF"), "vc-up": ("Up", "", "↑", "#FFFFFF"),
    "vc-left": ("Left", "", "←", "#FFFFFF"), "vc-right": ("Right", "", "→", "#FFFFFF"),
    "vc-exclamation": ("!", "", "!", "#FFFFFF"), "vc-question": ("?", "", "?", "#FFFFFF"),
}

SIZE = 512


class Letters:
    def __init__(self, path):
        self.font = TTFont(path)
        self.glyphs = self.font.getGlyphSet()
        self.cmap = self.font.getBestCmap()
        bp = BoundsPen(self.glyphs)
        self.glyphs["H"].draw(bp)
        self.cap_height = bp.bounds[3]

    def path(self, text, box, cap=None):
        """SVG path data for text, scaled and centred to fit box (x, y, w, h); cap fixes the height of a capital."""
        parts, x, bounds = [], 0, [1e9, 1e9, -1e9, -1e9]
        for ch in text:
            name = self.cmap[ord(ch)]
            glyph = self.glyphs[name]
            bp = BoundsPen(self.glyphs)
            glyph.draw(bp)
            if bp.bounds:
                b = bp.bounds
                bounds = [min(bounds[0], x + b[0]), min(bounds[1], b[1]), max(bounds[2], x + b[2]), max(bounds[3], b[3])]
            pen = SVGPathPen(self.glyphs)
            glyph.draw(pen)
            parts.append((x, pen.getCommands()))
            x += glyph.width
        bx, by, bw, bh = box
        w, h = bounds[2] - bounds[0], bounds[3] - bounds[1]
        s = min(bw / w, bh / h)
        if cap:
            # Every element the same letter height, unless it is too wide for the box.
            s = min(bw / w, cap / self.cap_height)
        ox = bx + (bw - w * s) / 2 - bounds[0] * s
        oy = by + (bh - h * s) / 2 + bounds[3] * s
        # Font units point up; the SVG's point down.
        return "".join(f'<path transform="translate({ox + px * s:.2f} {oy:.2f}) scale({s:.5f} {-s:.5f})" d="{d}"/>' for px, d in parts)


def body(icons, letters, picture, text):
    if not picture:
        return letters.path(text, (16, 16, SIZE - 32, SIZE - 32), cap=None if len(text) == 1 and not text.isalpha() else 300)
    inner = icons[picture]["body"].replace('fill="currentColor"', "")
    if not text:
        return inner
    # The picture shrinks to the top left, and the letter goes in the bottom right corner.
    return (f'<g transform="scale(0.74)">{inner}</g>' + letters.path(text, (SIZE - 200, SIZE - 200, 196, 196)))


def main():
    icons = json.load(open(sys.argv[1]))["icons"]
    font = sys.argv[2] if len(sys.argv) > 2 else "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
    out = sys.argv[3] if len(sys.argv) > 3 else os.path.join(os.path.dirname(__file__), "../../assets/createacolony/textures/icons/worldmap")
    os.makedirs(out, exist_ok=True)
    letters = Letters(font)
    missing = [p for _, p, _, _ in ICONS.values() if p and p not in icons]
    if missing:
        sys.exit("Not in the icon set: " + ", ".join(missing))
    for name, (_, picture, text, _) in ICONS.items():
        svg = (f'<?xml version="1.0" encoding="UTF-8"?>\n'
               f'<svg xmlns="http://www.w3.org/2000/svg" width="{SIZE}" height="{SIZE}" viewBox="0 0 {SIZE} {SIZE}">'
               f'<g fill="#ffffff">{body(icons, letters, picture, text)}</g></svg>\n')
        open(os.path.join(out, name + ".svg"), "w").write(svg)
    print(f"{len(ICONS)} icons written to {out}")


if __name__ == "__main__":
    main()
