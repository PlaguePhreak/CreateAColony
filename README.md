# Create a Colony (starter skeleton)

Vintage Story 1.22.x code mod, .NET 10. This is milestone 1 only: found a colony, get a vanilla
land claim, recruit settlers that persist with the save. Compiles cleanly against the 1.22.7 API; not yet tested in-game.

## Build
1. Install the .NET 10 SDK and set `VINTAGE_STORY` to your game folder.
2. Run `dotnet build CreateAColony.csproj -c Release`.
3. The ready-to-install mod is written to `Releases/createacolony_<version>.zip`. Copy that zip into the
   game's `Mods` folder.

## Try it
- Craft a **Town Hall** (5 planks of any wood in a U shape: top row full, middle row left and right) or take it from the creative inventory, and a **Builder's Wand** (flint on a stick). Right-click the ground with the wand, pick Town Hall and press Accept: this founds the colony, claims a 49x49 area, and 3 to 4 settlers arrive.
- `/colony info` inside the claim shows the colony.
- `/colony invite <player>`, then that player types `/colony join`. `/colony leave` leaves the colony you stand in.
- `/colony recruit [name]` (needs controlserver) spawns one more settler, for testing.
- Craft a **Supply Camp** (3 firewood over 2 sticks) and place it inside your colony. Right-click once per in-game day for starter materials, 5 deliveries per colony. Edit `assets/createacolony/config/supplycamp.json` to change the bundle.
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

### Ordering buildings and tools

Right-click a hut block to open its window and order a level; the Town Hall's own upgrade is on the
Town Hall's Overview tab. The Builder's Hut window lists every work order and, under Items needed,
what its builder still needs with have/need counts. Builders clear soil and sand with a shovel, wood
with an axe and stone or ore with a pickaxe, of at least the block's mining tier, and wear the tool
down as they work. Put tools in the hut's storage or the warehouse; couriers bring missing ones. Set
`BuildersNeedTools` to false in `ModConfig/createacolony.json` to turn this off.

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
  welcomes them or turns them away on the Town Hall's Settlers tab; one nobody answers moves on after
  `WandererWaitDays`. `/colony wanderer` brings one now (admin, for testing).
- **Permadeath** is a switch on the Town Hall's Overview tab for the colony owner; the config
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

Right-clicking an existing hut, the Town Hall or the Warehouse with the wand shows its outline and
lets you turn and move the building (up to 8 blocks up or down, 16 to the sides) until its first
level is ordered; **Build here** orders it and **Not here** gives a hut back.

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
- Craft a **Cook's Hut** (stick, firewood, stick over 3 planks), place it with the Builder's Wand and order
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
