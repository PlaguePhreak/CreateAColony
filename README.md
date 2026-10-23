# Create a Colony (starter skeleton)

Vintage Story 1.22.x code mod, .NET 10. This is milestone 1 only: found a colony, get a vanilla
land claim, recruit settlers that persist with the save. Compiles cleanly against the 1.22.7 API; not yet tested in-game.

## Build
1. Install the .NET 10 SDK and set `VINTAGE_STORY` to your game folder.
2. Run `dotnet build CreateAColony.csproj -c Release`.
3. The ready-to-install mod is written to `Releases/createacolony_<version>.zip`. Copy that zip into the
   game's `Mods` folder.

## Try it
- Start with a **Supply Camp** (3 firewood over 2 sticks): place it anywhere, no colony needed. Your first camp in a world gives you a Town Hall, a Builder's Hut, a Builder's Wand and a **Colony Ledger**, and right-clicking the camp opens its storage with one set of starting materials. See "The supply camp and the Colony Ledger".
- Or craft a **Builder's Wand** (flint on a stick), then a **Town Hall** (8 planks of any wood around the Builder's Wand; craft a second wand to keep one), or take them from the creative inventory. See "Crafting recipes" for every recipe. Right-click the ground with the wand, pick Town Hall and press Accept: this founds the colony, claims a 250x250 area, and 3 to 4 settlers arrive. Finished huts grow the claim (see "Growing the colony's land").
- `/colony info` inside the claim shows the colony.
- `/colony invite <player>`, then that player types `/colony join`. `/colony leave` leaves the colony you stand in.
- `/colony recruit [name]` (needs controlserver) spawns one more settler, for testing.
- Settlers get hungry and take food from any chest, shelf or crock inside the colony claim. Keep bread, fruit,
  bowls of stew, pots of meals or pies near the Town Hall. With no food they slowly starve. See "Food and meals".
- At night (21:00 to 06:00) each settler claims a free bed inside the claim and sleeps there. Without a bed they
  sleep on the floor by the Town Hall.
- About two in-game hours before a temporal storm, settlers head to their bed and stay put until it passes.
- `/colony citizens` lists every settler with their food level and whether they have a bed.
- Lost settlers (missing for half a day while the Town Hall area is loaded) respawn at the Town Hall. With
  Permadeath off, a killed settler returns after `DownedRecoveryDays`.
- Breaking the Town Hall disbands the colony and removes the claim.
- The mod refuses to start if VS Village is installed.

## Crafting recipes (0.32.0)
Every building needs planks, and planks need a saw, so a colony is a mid game goal (copper age). Huts all use
the same shape: two of the hut's own item in the middle column (top middle and center), the other 7 slots
planks of any wood.

| Block | Recipe |
| --- | --- |
| Builder's Wand | flint on a stick (1x2) |
| Town Hall | 8 planks around a Builder's Wand (the wand is used up) |
| Builder's Hut | 2 rope + 7 planks |
| Farmer's Hut | 2 seeds (any) + 7 planks |
| Forester's Hut | 2 firewood + 7 planks |
| Potter's Hut | 2 clay (any) + 7 planks |
| Cook's Hut | 2 bread (any) + 7 planks |
| Residence | 2 dry grass + 7 planks |
| Guard Tower | 2 spears (any) + 7 planks |
| Tavern | 2 rusty gears + 7 planks |
| Warehouse | 2 chests + 7 planks |
| Prospector's Hut | 2 stones (any rock) + 7 planks |
| Miner's Hut | 2 torches + 7 planks |
| Quarry | 2 rock blocks (any) + 7 planks |
| Supply Camp | 3 firewood over 2 sticks (unchanged) |
| Colony Ledger | 4 paper (any), 2 leather, 2 charcoal: paper, leather, paper / charcoal, paper, charcoal / paper, leather, paper |
| Drop-off | stick over a plank (unchanged) |
| Field Marker | dry grass over 2 sticks (unchanged) |

## The supply camp and the Colony Ledger (0.32.0)
- The **Supply Camp** goes down anywhere, inside a colony or not. The first camp each player places in a world hands them the colony charter: a Town Hall, a Builder's Hut, a Builder's Wand and a Colony Ledger (anything that does not fit drops on the camp). The camp itself starts with one set of materials in its own storage; right-click it to open it like a chest.
- This happens **once per player per world**, saved on the world, so it holds before you have a colony and also for players who join someone else's colony. Breaking the camp and placing it again, or crafting another, only gives an empty camp (still useful as storage). Breaking a camp drops what is inside.
- The daily deliveries are gone, and so is the "Supply bundles left" line in the Town Hall window.
- What the camp gives is in `assets/createacolony/config/supplycamp.json`: `playerItems` go to the player, `items` (up to 16 stacks) go into the camp.
- **Camp buildings**: put a WorldEdit export at `ModConfig/createacolony/blueprints/supplycamp/level1.json` (or `assets/createacolony/config/blueprints/supplycamp/level1.json`). The first camp then puts that building up around itself at once, turned to face you. Put a Supply Camp block in the export where the camp should sit; without one the camp sits at the bottom centre. If any of it would land on land you may not build on, none of it is built.
- The **Colony Ledger** opens your colony's Town Hall window from anywhere when you right-click with it. Quests and the colony's history will live in it later. A replacement is expensive on purpose (4 paper, 2 leather, 2 charcoal); the first one is free.

## Mining: Prospector, Miner and Quarry (0.31.3)
All three are on the new Mining tab of the Builder's Wand and use placeholder blueprints for now.
- **Prospector's Hut**: put a prospecting pick in its storage. The prospector wanders its work area (`ProspectorRadius` 64, changeable on the Work area tab), walking a random `ProspectorMinStep` 25 to `ProspectorMaxStep` 50 blocks from its last reading (or from a good earlier one) and never reading within `ProspectorNearReading` 16 of an existing reading. Each candidate spot is weighted by the ore the nearby readings promise (inverse-distance average of best density plus a quarter of the others; `ProspectorUnknownQuality` 0.15 for unread ground) to the power of `ProspectorPreference` 15, so it searches more near good readings and less near poor ones. It takes a density reading with the game's own prospecting pick code, wearing the pick 3 per reading. Readings appear on the hut's Ore tab and on every colony member's ore map layer (`ProspectorSharesOreMap`). Good finds go in the Town Hall event log.
- **Miner's Hut**: put a pickaxe in its storage. Like MineColonies, the miner digs its own shaft beside the hut: a spiral staircase round a rock pillar (3x3, steps 3 high so settlers can walk it). The first turn is the way in; every turn below it is a level, 8 blocks deeper, with a 2-high tunnel `MinerTunnelLength` 12 out from each side. Each hut level allows `MinerLevelsPerHutLevel` 2 levels. It digs out any ore it can see from where it stands (`MinerReach` 3), but never the floors of the shaft or tunnels. A tunnel that runs into water, lava, a building or someone else's land stops there; a blocked shaft waits.
- **Go to prospected ore** (switch on the Ore tab, on by default): the miner first goes to the best reading in range (`MinerRadius` 64) that is at least `MinerTripMinFactor` 0.267 ("decent"), or any reading of the ore chosen on the Ore tab. It finds that ore's blocks within `MinerOreSearchRadius` 12, digs a staircase down to them from the surface and digs them out, then comes back to its shaft. Ore too hard for its pick waits for a better pickaxe in the hut storage.
- **Quarry**: put a pickaxe in its storage. The quarrier picks a side of the Quarry facing away from the Town Hall and digs a square pit there, clearing up to 4 blocks above the ground first, then one layer at a time, leaving a staircase round the edge. Level 1 is 5x5 and 6 deep (`QuarryRadius` 2, `QuarryDepthPerLevel` 6); each level adds `QuarryRadiusPerLevel` 1 (2 blocks wider) up to `QuarryMaxRadius` 6 (13x13), and 6 more layers.
- **Waypoints for blueprints** (creative inventory only): put a **Mine waypoint** in a Miner's Hut blueprint, on the ground where the middle of the shaft goes, or a **Quarry waypoint** in a Quarry blueprint, on the ground in the middle of the pit. The hut then digs there instead of picking a spot beside itself. The builder never places the waypoint (its spot is cleared), and it leaves out the blueprint's own blocks wherever the miner or quarrier will dig: the shaft's steps (the 3x3 round the waypoint, starting at its north-west corner at the waypoint's height and going down clockwise, with the pillar under the waypoint) and its tunnels, or the whole pit area below the Quarry waypoint (up to 13x13). Everything else the blueprint builds is left alone by the digging. The current level's blueprint is used first, then the other levels.

## Config (`ModConfig/createacolony.json`)
- `Permadeath` (default `true`): settlers who die are gone. Set `false` to have them knocked out and recover after `DownedRecoveryDays`.
- `StartingSettlersMin` / `StartingSettlersMax` (default 3 / 4).
- Hunger: `SaturationMax`, `HungerPerDay`, `EatBelowFraction`, `StarvationDamagePerHour`.
- Sleep hours: `SleepStartHour`, `SleepEndHour`. Storm warning: `StormWarningHours`.

## Layout
- `src/CreateAColonyModSystem.cs` registration, VS Village guard, commands
- `src/ColonyConfig.cs` server config
- `src/Colonies/` Colony data (protobuf) and `ColonyManager` (save/load, claims, members)
- `src/Settlers/` `EntitySettler` and the `colonycitizen` behavior
- `src/Block/` Town Hall block and block entity
- `assets/createacolony/` entity, block and lang JSON

## Known TODOs
- Town Hall and Supply Camp use a plain plank-cube look for now.
- The Town Hall window, the warehouse and courier, and jobs come in milestones 3 to 6.

## Blueprints

Buildings grow from blueprints, which are ordinary WorldEdit exports. Build the structure in a
creative world with its hut block where the hut should stand, select it with WorldEdit, and run
`/we export <name>`. Copy the file from `VintagestoryData/WorldEdit/` to
`VintagestoryData/ModConfig/createacolony/blueprints/<type>/level<N>.json`, where `<type>` is
`builder`, `townhall`, `warehouse`, `forester`, `farmer` or `potter` and `<N>` is 1 to 5. `/colony reloadblueprints` picks up
changes without a restart. Blueprints in ModConfig override the ones shipped in
`assets/createacolony/config/blueprints/`.

Blueprints come in **styles**, picked with the tabs along the top of the Builder's Wand list. The
folders above are the **Testing** style. **Dev's Builds** is the second style, with Aaron's own
buildings (since 0.26.1 the Town Hall and Builder's Hut levels 1 to 4): its blueprints are in
`blueprints/devsbuilds/<type>/level<N>.json`, in the mod or in
`VintagestoryData/ModConfig/createacolony/blueprints/devsbuilds/`. A building keeps the style it was
placed in for every level; a type with no blueprint in a style shows "no blueprint yet" in the list. A
building whose style has no blueprints for its type (a Town Hall placed before 0.26.1, in Testing)
uses the first style that has some, so older colonies keep upgrading.

The placement window's **Level down** and **Level up** buttons flip the preview through every level
of the building, so you can see how big level 4 gets before you place level 1.

Doors in a blueprint are hung the way they were in the export, top half included. The first door
also marks the building's front: when you place the hut, the building is turned so that side faces
you. Soil in a blueprint counts as built on any soil (grassy or bare, rich or poor), and filling a
hole in the ground costs any soil; rock, gravel or sand already there also counts, so builders never
dig out stone to put dirt in its place. Burned-out torches in a blueprint are built as lit ones, and a
torch counts as built whether it is lit or burned out. Items lying on the ground (ground storage) are
left out. Empty space below the hut's floor is dug out only when the blueprint has blocks both under
and over it (a cellar); elsewhere the ground below the floor is left alone.

Since 0.25.0 the Town Hall and the Builder's Hut have four levels each, built by Aaron in game
(now `blueprints/devsbuilds/townhall/level1-4.json` and `blueprints/devsbuilds/builder/level1-4.json`), with cellars, furniture
from W4RD0's Furniture and a flag pole from Heraldry : Banners on the level 4 Town Hall. The 0.18.0
Builder's Hut is kept as `blueprints/builder/level1-old-0.18.json` (the blueprint generator still cuts
its cottages from it), and the old 5 x 4 x 5 sample as `blueprints/builder/level1-old-placeholder.json`
(rename either to `level1.json` in ModConfig to use it).

Since 0.19.0 the Forester's Hut and the Tavern also have thatched cottages for level 1, made from the
Builder's Hut: a 7 x 7 x 9 log cabin for the Forester and an 11 x 7 x 9 wattle-and-daub hall with a fire
pit hearth for the Tavern. Their old samples are kept as `level1-old-placeholder.json` too.

### Making blueprints with the generator

`tools/blueprints/make_blueprints.py` (Python 3 with Pillow) makes those cottages. It cuts the Builder's
Hut into slices across its length and repeats the middle slice to make a longer house, so the roof, frame
and door are copied from blocks that work in game; only walls, windows and furniture change, and only to
blocks in its `VERIFIED` list. It checks each building (one hut block, doors with their top half and
direction, roof data on every roof block) before writing it, and draws a picture and a layer plan of
each in `tools/blueprints/previews/`. New designs go in its `make` function.

### VS Roofing roofs

With the [VS Roofing](https://mods.vintagestory.at/vsroofing) mod installed, a blueprint can have a
VS Roofing roof. Build the roof in game as usual and export it with the rest of the building. A builder
copies each roof block whole, frame, slope, materials and fill included, the way a WorldEdit paste
does, and pays for what the block holds: the frame item, the binding held in the other hand for stick,
bamboo and firewood frames (any of the config list `RoofBindings` will do: flax fibers, cattail tops,
papyrus tops or wild vines), the roofing materials, and any fill block or sod plant. These show up in
the Items needed list and couriers bring them. The roofing materials must be the exact kind in the
blueprint (oak planks stay oak planks, even when any wood is allowed). Without VS Roofing the roof
blocks are skipped, like any block from a mod that is not installed.

The config list `BlueprintBlockDetails` (default `["vsroofing:*"]`) says which blocks are copied with
their saved details. Do not add chests or other containers: whatever they held in the export would
come for free.

### Furniture and banners (W4RD0's Furniture, Heraldry : Banners)

With W4RD0's Furniture (1.5.1) or Heraldry : Banners (2.0.0, with Heraldry) installed, blueprints can hold their
furniture and banner poles. Both mods are optional: without them those blocks are skipped. A builder
places these blocks the way a player does, from their item, so the block sets itself up: a double bed
or a wide table puts down its other half, a shelf its other cells, and the block takes the item's wood
and colours. The builder pays for the furniture item in the wood and cloth colours the blueprint has
("Rustic table (Oak)" in Items needed; "(any wood)" when the colony allows any wood), and for a pole's
banner with the same design, which has to be made at the Heraldry workbench. What furniture held in the
export (a cupboard's contents, a shelf's display) is not copied. The config list `BlueprintPlaceAsItem`
(default `["w4rd0sfurniture:*", "heraldrybanners:*"]`) says which blocks are placed this way; any block
that takes its wood from item attributes (Attribute Rendering Library) is placed this way too.

Settlers sleep in W4RD0's Furniture beds as well as vanilla beds. A double bed sleeps two, each on their
own half, and counts as two free beds for wanderers.

### Ordering buildings and tools

Right-click a hut block to open its window and order a level; the Town Hall's own upgrade is on the
Town Hall's Actions tab. The Builder's Hut window lists every work order and, under Items needed,
what its builder still needs with have/need counts. Builders clear soil and sand with a shovel, wood
with an axe and stone or ore with a pickaxe, of at least the block's mining tier, and wear the tool
down as they work. Put tools in the hut's storage or the warehouse; couriers bring missing ones. Set
`BuildersNeedTools` to false in `ModConfig/createacolony.json` to turn this off.

A builder works on each block from the ground below it, beside it, so roofs and the tops of walls are
built from underneath. It can reach `BuilderReach` (12) blocks; a block further than that from anywhere
the builder can stand shows as unreachable.

### Foresters and farmers

Place a **Forester's Hut** (planks, sticks and firewood) or a **Farmer's Hut** (planks, sticks and
seeds) in the colony and give a settler the Forester or Farmer job in the
Town Hall. Each works from the free hut of its kind nearest the start of the list.

- The forester needs an axe. It fells wild trees within `ForesterRadius` blocks of its hut (never
  placed logs), nearest first, with the game's own axe logic, and plants a tree seed or sapling of
  the same wood on the stump.
- The farmer needs a hoe. A **Field marker** (dry grass on two sticks) marks out the square of
  `FieldRadius` blocks around it. The farmer harvests ripe crops and sows them again, sows bare
  farmland, and tills soil, up to `FieldsPerFarmer` fields each. Right-click a marker with seeds to
  choose its crop; sneak and right-click it with an empty hand to allow any.
- The farmer also pulls weeds on its fields, waters dry crops with a watering can (refilled at the
  nearest water) when its moisture drops below `FarmerWaterBelow`, spreads fertilizer from the hut on
  farmland whose nutrient drops below `FarmerFertilizeBelow`, and picks the crop whose nutrient the
  soil has the most of (crop rotation). It picks ripe berry bushes and fruit trees within
  `FarmerOrchardRadius` blocks of its hut.
- Both carry what they gather to the hut's storage, where couriers pick it up for the warehouse. They
  keep their tool and a stock of seeds (`ForesterSeedReserve`, `FarmerSeedReserve`); seeds over that
  go to the warehouse too. Missing tools and seeds are requested from the warehouse.

### The potter: from the stone age to the copper age

Place a **Potter's Hut** (planks, sticks and clay) and give a settler the **Potter** job. The potter
works with the game's own pottery, by hand:

- It shapes clay on the game's **clay forming block** next to the hut, one layer at a time, following
  the same clay forming recipes a player picks from, using whatever clay fits the recipe (blue, fire or
  red). Each piece takes as much clay as the handbook says.
- It digs a one-block **pit kiln** in soil, gravel or sand near the hut (solid, non-flammable ground
  around it and open sky above, as the game requires), lays up to a full load of raw pieces in it, adds
  the game's pit kiln fuel layer by layer (dry grass, sticks and firewood, read from the game's own pit
  kiln), lights it, and takes the fired pottery out when the game has fired it. Rain that puts the
  kiln out just means it gets lit again.
- The hut window's **Pottery** tab lists every piece the game's clay forming recipes make that fires
  in a pit kiln, with how many the colony has (warehouse, hut, potter and kiln) and how many to keep in
  stock. A new hut keeps 1 crucible and 2 ingot molds (`PotteryDefaultKeep`), what it takes to smelt
  copper; officers change it.
- Put clay and kiln fuel in the hut's storage or the warehouse; the potter asks couriers for what it
  is missing. Fired pottery and dug soil go to the hut storage and on to the warehouse.
- Config: `PotterSecondsPerLayer`, `PotterSecondsPerKilnStep`, `PotterClayReserve`,
  `PotterClayRequest`, `PotterSpotRadius`, `PotteryKeepChoices`, `PotteryDefaultKeep`.

### New settlers: the Tavern and wanderers

- A **Tavern** (planks, sticks and a rusty gear) is placed with the Builder's Wand like any hut. Once
  level 1 is built, a few people looking for work stop by each in-game day (`TavernRecruits`). Its
  **Recruits** tab lists them with what they ask in rusty gears (`RecruitCostMin`/`Max`,
  `RecruitCurrency`); **Hire** takes the gears from your inventory, then from the Tavern's storage, and
  the new settler steps out of the Tavern. Hiring is free in creative mode.
- **Wanderers** now and then turn up near the colony border and walk to the Town Hall
  (`WandererEveryDays`, sooner with a built Tavern: `WandererEveryDaysWithTavern`), one at a time and
  only while there is a free bed (`WanderersNeedFreeBed`). Members get a chat message. An officer
  welcomes them or turns them away on the Town Hall's Citizens tab; one nobody answers moves on after
  `WandererWaitDays`. `/colony wanderer` brings one now (admin, for testing).
- **Permadeath** is a switch on the Town Hall's Settings tab for the colony owner; the config
  `Permadeath` is only the default for new colonies.
- Settlers get names from `config/settlernames.json`.

### Placing buildings with the Builder's Wand

As in MineColonies, the Town Hall, huts and the Warehouse are not placed by hand (that just says to
use the wand). Craft a **Builder's Wand** (flint on a stick), keep the building's block in your
inventory, and right-click the ground with the wand. Pick the building from the list and the outline
of its highest level appears there: blue where free, red where something is in the way that the
builder will clear, yellow where the building's block goes. The window beside it turns the building
and nudges it forward, back, left, right, up or down (as you look). Esc closes the window but leaves
the outline up so you can walk around it; right-clicking with the wand, on a block or in the air,
brings the window back. **Accept** takes one of the building's blocks from your inventory and puts it
down; without one it tells you that you need one. **Change building** goes back to the list.

The list is sorted into tabs down its left side, and it opens on the tab you used last:
**Fundamentals** (Town Hall, Builder's Hut, Tavern), **Agriculture** (Farmer's and Forester's
Huts), **Craftsmanship** (Potter's and Cook's Huts), **Infrastructure** (Warehouse), and
**Education**, **Military**, **Walls** and **Decoration**, which are empty until their buildings
are added. The tabs along the top pick the blueprint style (Testing or Dev's Builds), and the
placement window shows which style the building uses.

Right-clicking an existing hut, the Town Hall or the Warehouse with the wand shows its outline and
lets you turn and move the building (up to 8 blocks up or down, 16 to the sides) until its first
level is ordered; **Build here** orders it and **Not here** gives a hut back.

The preview is a **ghost** of the building: its real blocks, see-through, as the WorldEdit import tool
shows a schematic (roofs, doors and furniture included). Blocks in the way are still marked red and the
building's block yellow. Every turn, move or level change redraws the ghost where the building now stands. The game's own "preview transparency" setting decides how see-through
it is. Set `PlacementPreview` in the mod config to `"outline"` for the old blue outline, or `"both"`
for the two together; if the ghost cannot be made for some reason, the outline is shown instead.

While you place a building, the colony's other buildings close by get **box outlines** so you can set
the new one right beside them: purple for the size they are now (or are being built to), faint purple
for the size they grow to at their top level, and orange for any the new building would overlap. The
placement window names the buildings it overlaps. `NearbyOutlineRange` (default 32 blocks, 0 = off) and
`NearbyOutlineMax` (default 30) in the mod config change how far and how many.

### Work area and guards

Forester's and Farmer's Huts have a **Work area** tab in their window (officers change it):

- **How far out** the worker goes: 16 to 500 blocks (`GatherRadiusChoices`). The area is searched a
  chunk column at a time, nearest first, in a few milliseconds per tick; land no player is near is
  loaded from the save to be looked at, and land never explored is skipped. Long walks go in
  stretches of about 28 blocks, each ending on a free spot with floor under it, trying shorter and
  turned stretches around water, cliffs and out of holes. A forester, farmer or guard
  further than `FarWorkPlayerDistance` blocks from every player stays active and keeps the 3 by 3
  chunk columns around it loaded until it comes back or a player comes near.
- **Escort by guard**: a settler with the new **Guard** job follows the worker and fights monsters
  (`GuardTargets`, by default drifters, locusts, bells, bowtorns, shivers, wolves, hyenas and
  bears) within `GuardThreatRadius` blocks of it. Guards fight with the hardest-hitting weapon they
  carry and fetch one from the warehouse if they have none; spare guards keep watch at the Town Hall.
  The same creatures hunt settlers the way they hunt players (`HostilesAttackSettlers`).
- **Collect berries and fruit** (Farmer's Hut): picks ripe berry bushes and fruit trees within the
  range. Berries further than 32 blocks out wait until the fields have nothing to do.

### Food and meals
- A hungry settler eats one plain food item, one serving of a meal (from a bowl, a cooking pot or a crock,
  in a container or placed in the world) or one slice of a pie. Empty bowls and pots are left behind empty.
  A pot of food left in a fire pit counts once it is cooked; ovens, querns, troughs, barrels and buckets are
  never touched.
- Settlers remember the food groups (fruit, vegetable, protein, grain, dairy) of their last few meals and
  prefer the food group they have had least of, weighed against how far away it is.
- Food mods: Expanded Foods and A Culinary Artillery change the game's own meal nutrition, and settlers use
  that same code, so modded meals count with no hard dependency. Extra nutrients that Expanded Foods items
  carry (`expandedSats`) count too. Food Shelves blocks are ordinary containers, so settlers find food on them.
  A Culinary Artillery's mixing bowl, saucepan and cauldron are skipped by name so settlers never eat what is
  being made in them.

### The cook
- Craft a **Cook's Hut** (2 bread and 7 planks, see "Crafting recipes"), place it with the Builder's Wand and order
  level 1. Its blueprint has a fire pit; any finished fire pit within `CookFirepitRadius` (8) blocks works too.
- Give a settler the Cook job. The cook puts raw food (anything the game cooks in a fire pit into food, such
  as raw meat, fish or dough, from any mod) in the fire pit, adds firewood, lights it and takes the cooked food
  out, using the game's own fire pit. Cooked food stays in the hut's storage, where hungry settlers eat it.
- The Kitchen tab lists every food the game cooks this way, with how much is ready (hut, warehouse, cook and
  fire pit) and how much to keep (`KitchenDefaultKeep`, 8, until an officer changes it). Raw food and firewood
  come from the hut storage; the cook asks couriers for raw food the warehouse has, and for firewood.
- A fire pit holding something the cook does not know how to cook is left alone.
- **Pot meals (0.17.0):** with an empty cooking pot in the hut storage or warehouse, the cook also cooks the
  game's meal recipes (stews, porridge, scrambled eggs and those food mods add) the way a player does: pot in
  the fire pit, its slots filled with ingredients that make the recipe, fire lit. It makes as many servings as
  the ingredients and the pot allow (up to 6), picking different ingredients where it can. The cooked pot goes
  into the hut storage and settlers eat from it a serving at a time; the emptied pot is used again. The
  Kitchen tab lists every meal with servings ready and to keep (`KitchenDefaultMealKeep`, 6). Meals come
  before grilling. Recipes that need water or another liquid are left to players.

### Doors and gates

Settlers walk through doors and fence gates that open by hand. They open one as they reach it and close
it behind them once nobody is standing in it. Doors that need a key or a lever stay shut to them. This
patches the game's pathfinder (with Harmony) only while it plans a path for a settler.

### Hut storage

Every Builder's, Forester's and Farmer's Hut has its own 25-slot storage: sneak and right-click the
hut block, or press Storage in its window. Chests within two blocks of the hut add more room.

### The settler window

Right-click a settler to see it laid out like the player's character window: a Character panel with
the settler's model (drag to turn it) between clothing slots and armor slots that stay empty until
settlers can wear things, a Stats panel (health, satiety, walk speed, status, bed, job and what it is
doing), and an Inventory panel with its bags, held item and pack.

### Importing MineColonies blueprints

Put a MineColonies `.blueprint` file in `VintagestoryData/ModConfig/createacolony/import/` and run
`/colony importmc <file> <type> <level>`, e.g. `/colony importmc builder1 builder 1`. Each
Minecraft block is matched through `assets/createacolony/config/mcimport.json` (copy it to
`ModConfig/createacolony/mcimport.json` to edit); every candidate is checked against the installed
game, and blocks with no match are left empty and listed in chat. Check the license of a style pack
before shipping converted blueprints.

### Roads, ladders and getting unstuck (0.21.0)
- **Pathblocks** (config `PathBlocks`, default stone path, its slabs and stairs, and ladders) are roads. Settlers
  walk `PathSpeedMultiplier` (1.4) times faster on them, and the pathfinder counts a step on one as
  `PathStepCost` (0.65) of a normal step, so settlers take a road when it is up to about 1.5 times longer.
- **Ladders**: the game's pathfinder does not climb. When a settler finds no path to a goal two or more blocks
  up or down, it looks for a ladder within `LadderSearchRadius` (8), walks to it and climbs (`LadderClimbing`).
- **Willpower**: a settler that cannot get where it is going (no path, or no closer for
  `StuckNoProgressSeconds`) builds Willpower, twice as fast when boxed in. At `WillpowerSeconds` (20, give or
  take `WillpowerJitter`) it pops to a safe spot within `WillpowerRadius` (10) towards its goal, preferring
  Pathblocks, with sparks and a crackle. `WillpowerRecklessChance` (5%) of the time it does not look first, and
  guards never look out for monsters. The settler window shows "(stuck, Willpower N%)".
- Commands: `/colony stuck` lists stuck settlers; `/colony pathdebug [on|off]` shows sparks over them (blue to
  red) and logs to the server log. Config `PathDebug` turns it on at start.

### Growing the colony's land (0.22.0)
- The Town Hall claims a `TownHallClaimSize` (250) by 250 square centred on its block (100 before 0.33.0), and
  the square grows `TownHallClaimPerLevel` (50) blocks a side for each Town Hall level above 1, up to
  `TownHallClaimSizeMax` (500). Every hut, once its first
  level is finished, adds a square centred on its hut block; buildings with nothing to build (the Warehouse)
  claim as soon as they are placed. The colony's land is all those squares together, kept as the areas of the
  colony's one vanilla land claim, so hut placement, field markers, beds, food, chests and the /land tools all
  see the grown claim. Each square reaches `ClaimDepthBelow` (32) blocks under and `ClaimHeightAbove` (64)
  over its hut block.
- Square sizes (blocks along a side, config `HutClaimSizes`): Builder 40, Warehouse 40, Tavern 50, Farmer 60,
  Forester 60, Potter 30, Cook 30. Buildings not listed claim `ClaimSizeMin` (30), or `MilitaryClaimSize` (200)
  if they are on the Builder's Wand Military tab. Each hut level above 1 adds `HutClaimPerLevel` (10) blocks a
  side (0.33.0), so a hut near the border pushes it further out as it is upgraded. Every hut size is kept between
  `ClaimSizeMin` and `ClaimSizeMax`.
- A square never covers someone else's land claim: it shrinks two blocks at a time until it fits, down to
  `ClaimSizeMin`, and claims nothing if even that does not fit. Breaking a hut gives its square back.
- Builders build a blueprint's blocks in the colony's land and in unclaimed land around it (a hut only claims
  once built), never in someone else's claim.
- The work range of Forester's and Farmer's Huts (16 to 500 blocks) and guard escorts are unchanged: workers
  may gather in unclaimed land outside the colony, never in another claim.
- The Town Hall's Actions tab shows how much land is claimed. `/colony claims` lists what each building claims;
  `/colony claims show` marks the edge of the colony's land in blue, `/colony claims hide` clears it.

### Guard Tower and the military (0.23.0)
- **Guard Tower** (Military tab of the Builder's Wand; recipe: see "Crafting recipes"). Settlers with the
  Guard job move in by themselves, one per tower level (one while only the tower block stands). Guards without a
  tower still escort workers (Escort by guard) or watch the Town Hall.
- **Tower tasks**: Guard (stay at the tower or the spot set with the Guard Scepter, fighting within
  `GuardTowerRadius`), Patrol (Automatic: hut to hut within `GuardPatrolRange` of the tower; Manual: the scepter's
  points) or Follow (bodyguard to the player who chose it, loose or tight, brought to them past `GuardFollowTeleport`).
- **Night watch** (`GuardNightWatch`): tower guards sleep from `GuardSleepStartHour` to `GuardSleepEndHour` and keep
  watch at night; they stay out in temporal storms (`GuardsFightInStorms`). A guard with a monster close by, or an
  alarm to answer, leaves its meal, bed or shelter.
- **Alarm**: a monster that hurts a settler on the colony's land (plus `GuardAlarmMargin`) calls the nearest free guard.
- **Retreat** (tower setting): below `GuardRetreatHealth` a guard walks back to its tower until `GuardReturnHealth`.
- **Armor** (`GuardsWearArmor`): guards put on the best helmet, body and leg armor from their tower's storage and the
  warehouse. A blow hits head, body or legs and the armor there works as the game's player armor does, wearing down.
  It shows in the settler window's armor slots; it does not show on the settler's model yet.
- **Hut window Main tab** (every hut): name and level with rename, workers with Manage workers / Recall workers,
  build options, courier pickup priority (0 = never) and Request pickup now, a ? guide, Inventory and a list of
  everything stored. Guard Towers add Selection tools, Hostiles (per-tower on/off list), Settings, Minimum stock
  (couriers keep that many in the tower) and Statistics.

### The Town Hall window (0.24.0)

The Town Hall window is laid out like MineColonies', with eight tabs down the left:

- **Actions**: the colony's name and the Town Hall's level on a panel like a hut's Main tab (the pencil
  renames the colony), the colony's numbers, the Town Hall's own upgrade, cancel and turn buttons, how
  much land the colony claims, and a **?** guide.
- **Town map**: the colony from above (0.24.0). The ground is drawn from the land your game has loaded (dark where it
  is not loaded), the colony's land is tinted with a gold edge, and each building's ground is a box (gold Town
  Hall, blue huts, dashed orange while being built). Settlers are green dots (wanderers yellow, knocked out red)
  and you are white. Point at a building for its name and level; **Redraw map** draws the ground again after
  you walked around. Large colonies get one pixel per few blocks (at most 320 pixels a side). Since 0.33.0 the
  mouse wheel (or the **+** and **−** buttons) zooms in up to 10x around the pointer, holding the left button
  and dragging moves the map, and **Fit** shows the whole colony again.
- **Banners**: the colony banner, see Colony banners below.
- **Information**: **Events**, a log of the last 100 things that happened (the colony founded, settlers
  hired, wanderers arriving, welcomed, turned away or leaving, settlers knocked out, recovering or dying,
  buildings placed, finished or taken down), newest first; and **Work orders**, every building waiting for a
  builder in the order builders take them, with Cancel for officers.
- **Citizens**: two pages (0.33.0). **Current Citizens**: every living settler with food, bed and job. Officers
  pick jobs, **Recall** a stuck settler straight to the Town Hall (only while it is in loaded land), and welcome
  or turn away wanderers. **Dead Citizens**: everyone who died for good, with their job, when they died and a
  Buried / Not buried marker. Burial and mourning are coming later, so for now everyone shows Not buried.
- **Statistics**: **Colony** shows settlers against beds, settlers lost for good, buildings, the colony's age,
  and each job's settlers against the places its huts offer; **Work done** adds up every hut's Statistics for
  today, yesterday, the last week, the last 100 days or all time.
- **Permissions**: the players in the colony and their roles (the owner sets roles and removes players).
- **Settings**: Permadeath (the owner's switch), how often wanderers come, and the warehouse.

MineColonies features with no system here yet are marked "Coming later" on their tab: citizen style, alliances, happiness, reordering work orders, custom ranks and
permission logs, auto hiring and housing, and enter/leave messages.

### Colony banners (0.25.1)

The Town Hall's **Banners** tab works with the [Heraldry](https://mods.vintagestory.at/heraldry) and
[Heraldry: Banners](https://mods.vintagestory.at/heraldrybanners) mods (both need Attribute Rendering Library).
Create a Colony does not need them; without them the tab says they are missing.

- Design a banner at the Heraldry Workbench, hold it, and press **Use banner in hand**. The Town Hall
  remembers the design and you keep the banner.
- **On the Town Hall**, **On Guard Towers** and **On every building** copy the colony banner's layers onto every
  banner hanging on a pole (or another display block, never a chest) inside those buildings' blueprint area.
  Each banner keeps its own shape (pennant, swallowtail, ...). Only buildings in loaded land change; chat says
  how many banners changed and how many buildings were skipped.
- **Put the colony banner on each building when a builder finishes it** does the same for each finished level,
  including banner poles the builder put up from the blueprint (0.25.0).
- Officers and the owner can use the tab.

### Families and the Residence (0.27.0, fixed in 0.27.1)

Settlers have families, as MineColonies citizens do (`src/Colonies/ColonyManager.Families.cs`).

- **Family names.** Every settler has a family name from `config/settlersurnames.json`; settlers from
  older saves get one the first time the colony loads. Children take their mother's or father's.
- **Couples.** Once a day, two single adults who have lived in the colony for `FamilyCoupleAfterDays`
  pair up (`FamilyCouplesPerDay`; `FamilyCouplesAnySex` lets two men or two women pair, but only a man and a
  woman have children). Never brothers and sisters, nor a parent and child. They move in together: one
  furniture double bed (W4RD0's Furniture), else beds side by side in the Residence one of them lives in.
- **Children.** Each day a couple has a `FamilyChildChancePerDay` chance of a child, if both are well and
  not hungry, they have fewer than `FamilyMaxChildren`, `FamilyChildCooldownDays` have passed since the last
  one, the colony is below `FamilyMaxColonySize` (0 = no limit) and, with `FamilyChildNeedsFreeBed`, there
  is a free bed. So housing limits growth. A child is a smaller settler (`entities/settlerchild.json`): no
  job, it sleeps near its parents, eats, sheds and plays near home. After `FamilyChildGrowDays` it grows up
  into a normal settler who can be given a job.
- **Residence** (Fundamentals tab of the Builder's Wand; planks, sticks and dry grass). Settlers who sleep
  in a bed inside it are its residents; settlers looking for a bed take one in a Residence first. Its
  window has a **Residents** tab with each household (a couple and their children, or someone on their
  own), jobs, how long children take to grow up and whether a couple can have a child right now. Level 1
  is a placeholder 7x4x7 room with two hay beds.
- **Settler window**: a Family section under Work shows their Residence, partner, parents, children,
  brothers and sisters, and for a couple whether a child may come. The Town Hall's Citizens tab shows
  children with their time to grow up instead of a job.
- `/colony family couple <name> <name>`, `/colony family child <name>` (the couple has a child now, if the
  real limits allow) and `/colony family grow <name>` are admin test commands. Names can be first names.
- `Families: false` in the config stops new couples and children.
