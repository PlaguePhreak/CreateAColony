# Vintage Colonies

Vintage Story 1.22.x code mod, .NET 10 (mod id `createacolony`). It brings the ideas of the Minecraft mod
MineColonies to Vintage Story: found a colony with a Town Hall, recruit settlers, and place huts that your
settlers build, staff and level up: builders, farmers, foresters, miners, cooks, couriers, guards, crafters
and more. See [ROADMAP.md](ROADMAP.md) for what is done and what is planned.

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
- Breaking the Town Hall disbands the colony and removes the claim. Only the owner (or a server admin) can, and
  only while sneaking. Officers can break huts; a hut that is built or has a work order also needs sneaking.
  Settlers of a disbanded colony leave with it.
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
| Barracks | 2 bows (any) + 7 planks |
| Tavern | 2 rusty gears + 7 planks |
| Warehouse | 2 chests + 7 planks |
| Prospector's Hut | 2 stones (any rock) + 7 planks |
| Miner's Hut | 2 torches + 7 planks |
| Quarry | 2 rock blocks (any) + 7 planks |
| Smelter's Hut | 2 charcoal + 7 planks |
| Blacksmith's Hut | 2 ingots (any metal) + 7 planks |
| Sawmill | 2 logs (any) + 7 planks |
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
- The **Colony Ledger** opens the quest book when you right-click with it, and your colony's Town Hall window when you sneak and right-click. The colony's history pages will live in it later. A replacement is expensive on purpose (4 paper, 2 leather, 2 charcoal); the first one is free.

## The quest book (0.42.0)
An FTB-Quests-style quest book that doubles as a guide to the mod and to the base game. Nothing is ever locked behind a quest; quests only hand out item rewards.
- **Open it** with the **L** key (changeable in the game's controls), by right-clicking the **Colony Ledger** (sneak and right-click opens the Town Hall window instead), with the **Quests** button that appears at the top of the screen while your inventory is open, or from the **Quests** tab of the Town Hall window.
- **Groups** are the tabs down the left: Story, Ages, The Colony, Home and Food, The Land, Crafting, Travel, Dangers, Lore and Tavern. Each group has **chapters** (the list at the top, with "done / total"), and each chapter is a **map**: drag to move it, click a quest to read it. Lines show which quest leads to which; grey means "do the earlier quest first", white open, blue counting, gold ready to claim, green done. A star marks a chapter's last quest, a square a main story quest, a hexagon an optional one.
- **Story** and **The Colony** are shared by the whole colony (any member can claim; the Town Hall's Quests tab shows them). The **Prologue** and every other group are each player's own. Colony quests stay grey until you have a colony.
- **Most quests complete on their own** from what you carry (hotbar and bags), what is in the colony's storage, its buildings, settlers, jobs, families, the Town Hall's statistics and event log. A chat line tells you when one is ready; open the book and press **Claim**. A few story quests (the chapter 4 choice) must be **Accepted** first; **Drop** gives one up with no cost.
- **Track** up to three quests and they show in the top right of the screen with their progress.
- **Quest files** live in `assets/createacolony/config/quests/*.json` (one per group) and their text in `lang/en.json` as `quest-<code>` / `quest-<code>-desc`, `chapter-<code>`, `questgroup-<code>`. Objective types: `obtain` / `deliver` (carried items, wildcards like `game:ore-*copper*` allowed), `stock` (colony storage), `building` (type and optional `level`), `buildings`, `settlers`, `job`, `couples`, `children`, `members`, `stat` (Town Hall statistics key, `kill:*` allowed), `event` (event log key), `age`, `land`, `camp`, `colony`. `x`/`y` place a quest on the map, `requires` draws the lines, `shape` is round, square, star or hexagon, `icon` an item code, `accept: true` asks for Accept, `secret: true` hides it until it opens.
- **Commands** (admin): `/colony quest list`, `/colony quest complete <code>` (marks it done, no rewards), `/colony quest reset` (forgets your quests and your own colony's), `/colony quest reload` (re-reads the quest files; players re-join to see changes).
- Chapters with no quests yet say so in the book (Maps and waypoints, Riding, The wider world, Temporal storms, Raids, Deep places, and the whole Tavern group). Dialogue windows, ledger journal pages and the accepted-and-ignored downside are not in yet.

## Settler gear, clothes and inventory (0.43.0)
- **Settlers look like players.** They use the player's own model and get a random look from the player character creator (skin, eyes, hair, beard; women never get a beard). The look is kept, so a settler who is brought back looks the same. New settlers, and settlers from older saves once, start in the clothes of the game's **Commoner** class (config `SettlerClothesClass`).
- **The settler window has tabs**: Character & Armour, Stats, Inventory, Traits & Skills. Traits are not in yet; the tab says so above the skills.
- **Take and give items.** Colony members click the settler's slots the same way as their own: left-click takes a stack or puts the one on your cursor (swapping if different), right-click takes half or puts one, shift-click sends it to your inventory. Clothing, armor and bag slots only take what fits there; visitors to the colony can only look.
- **Tool bar**: a line of its own on the Inventory tab with one **weapon** slot, three **tool** slots and an **offhand** slot. Tools and weapons a settler gets go there first, so they never get dropped off with the harvest; workers take their tool from it into their hand and put it back after. A shield in the offhand shows in the left hand.
- **Packed food**: three slots of food carried to eat on the go. A settler eats from it whenever a whole bite fits in its stomach, so it stays nearly full while it keeps working. Once the packed food is gone it works on until it is hungry (40%), walks to the nearest food, eats, and packs up to a stomach's worth for later (config `CarryFood`, `CarryFoodSaturation`). Food that rots is moved to the inventory.
- **Dropping things off**: a settler keeps only what its job needs. Workers drop the rest off at their hut whenever they pass within 8 blocks of it (`DepositNearHut`), as well as when their pack is full or work runs out; guards take theirs to the tower; others take theirs to their hut, or the warehouse if they have none.
- **Picking things up**: settlers pick up items they walk past, and an idle settler walks to items lying within 10 blocks inside the colony (`SettlersPickUpItems`, `PickUpRange`). Items are left alone for 5 seconds after they land (`PickUpDelaySeconds`), so a settler does not grab what you just dropped. Settlers without a job pick things up too: they save them up and take them to the Warehouse in one trip once their pack is full or after about two minutes (`LooseItemsDepositSeconds`); with no Warehouse yet, they keep them.
- **Torches**: at night or in a dark place a settler holds a lit torch in its left hand when it carries one (tool bar offhand or inventory). A settler without one fetches a lit torch from its hut's storage or the warehouse when it has nothing else to do (`CarryTorches`).
- If the player's model cannot be read (a mod that changes the player a lot), settlers keep the older villager model and the log says why.

## Mining: Prospector, Miner and Quarry (0.36.0)
All three are on the new Mining tab of the Builder's Wand and use placeholder blueprints for now.
- **Prospector's Hut**: put a prospecting pick in its storage. The prospector wanders its work area (`ProspectorRadius` 64, changeable on the Work area tab), walking a random `ProspectorMinStep` 25 to `ProspectorMaxStep` 50 blocks from its last reading (or from a good earlier one) and never reading within `ProspectorNearReading` 16 of an existing reading. Each candidate spot is weighted by the ore the nearby readings promise (inverse-distance average of best density plus a quarter of the others; `ProspectorUnknownQuality` 0.15 for unread ground) to the power of `ProspectorPreference` 15, so it searches more near good readings and less near poor ones. It takes a density reading with the game's own prospecting pick code, wearing the pick 3 per reading. Readings appear on the hut's Ore tab, on the Town Hall's **Prospecting** tab (top of its tab list) and on a separate **Colony prospecting** layer of every colony member's world map (`ProspectorSharesOreMap`), kept apart from the player's own prospecting layer. Colony readings can't be deleted from the map. Good finds go in the Town Hall event log.
- **Miner's Hut**: put a pickaxe in its storage. Like MineColonies, the miner digs its own shaft beside the hut: a spiral staircase round a rock pillar (3x3, steps 3 high so settlers can walk it). The first turn is the way in; every turn below it is a level, 8 blocks deeper, with a 2-high tunnel `MinerTunnelLength` 12 out from each side. Each hut level allows `MinerLevelsPerHutLevel` 2 levels. It digs out any ore it can see from where it stands (`MinerReach` 3), but never the floors of the shaft or tunnels. A tunnel that runs into water, lava, a building or someone else's land stops there; a blocked shaft waits.
- **Go to prospected ore** (switch on the Ore tab, on by default): the miner first goes to the best reading in range (`MinerRadius` 64) that is at least `MinerTripMinFactor` 0.267 ("decent"), or any reading of the ore chosen on the Ore tab. It finds that ore's blocks within `MinerOreSearchRadius` 12, digs a staircase down to them from the surface and digs them out, then comes back to its shaft. Ore too hard for its pick waits for a better pickaxe in the hut storage.
- **Quarry**: put a pickaxe in its storage. The quarrier picks a side of the Quarry facing away from the Town Hall and digs a square pit there, clearing up to 4 blocks above the ground first, then one layer at a time, leaving a staircase round the edge. Level 1 is 5x5 and 6 deep (`QuarryRadius` 2, `QuarryDepthPerLevel` 6); each level adds `QuarryRadiusPerLevel` 1 (2 blocks wider) up to `QuarryMaxRadius` 6 (13x13), and 6 more layers.
- **The right tool for each block** (0.38.0): miners and quarriers carry a pickaxe and, when the hut's storage has one, a shovel. For each block they hold whichever carried tool is fastest on its material (the game's own `MiningSpeed`, so modded tools work too): the shovel for soil, sand and gravel, the pickaxe for rock and ore. The right tool digs in the normal time, a slow one (a pickaxe in soil) takes up to twice as long. A block too hard for every carried tool still waits for a better pickaxe.
- **Filling finished mines in** (0.41.0): when the ore at a prospected spot is dug out (or the miner leaves it for New mine or a different ore), every staircase it dug there is filled back in, `MinerFillDepth` 6 blocks down from the surface: all the open space joined to the steps beside the entrance, lowest first, never along the surface and never inside buildings. It uses dirt first, then gravel, sand or stone it carries, then fetches from the hut's storage, then asks couriers for `game:soil-*` (going on with other work meanwhile). Save data: `Building` 60 `SiteEntrances`, 61 `MineSeals`.
- **Node search** (0.44.0): the miner carries a prospecting pick (from the hut's storage, or asked of couriers; it digs on without one). Wherever it starts digging somewhere new (more than half a search's reach from its last search), in the shaft or at prospected ore, it does a node search like the pick's node search mode: every ore block within `MinerNodeSearchRadius` (0 = the world's `propickNodeSearchRadius`, or 8 when the world has none) is noted, and it digs a side tunnel to each (nearest first, from the search spot or the end of the last side tunnel), never digging away a floor of the mine or the shaft's pillar. Ore straight above or below a search spot, or behind water or buildings, is let go. Each search takes `MinerNodeSearchSeconds` and wears the pick by `MinerNodeSearchWear`. Finds are kept in memory only; after a restart the miner searches again.
- **Torches in the mine** (0.40.0): the miner keeps up to `MinerTorchesCarried` 8 torches from the hut's storage (topped up when it is down to 2, and asked of couriers as `game:torch-*` when there are none). After each block it digs, if the light where it stands is below `MinerTorchLight` 8 (0 turns this off), it puts a lit torch on the floor there. Miners and quarriers never dig torches out, and treat them as open space.
- **Waypoints for blueprints** (creative inventory only): put a **Mine waypoint** in a Miner's Hut blueprint, on the ground where the middle of the shaft goes, or a **Quarry waypoint** in a Quarry blueprint, on the ground in the middle of the pit. The hut then digs there instead of picking a spot beside itself. The builder never places the waypoint (its spot is cleared), and it leaves out the blueprint's own blocks wherever the miner or quarrier will dig: the shaft's steps (the 3x3 round the waypoint, starting at its north-west corner at the waypoint's height and going down clockwise, with the pillar under the waypoint) and its tunnels, or the whole pit area below the Quarry waypoint (up to 13x13). Everything else the blueprint builds is left alone by the digging. The current level's blueprint is used first, then the other levels.

## Two prospectors and a mineshaft maze (0.62.0)
- **Prospector's Hut** takes two prospectors. In **Manage workers** each one has a button, **Finds ore** or **Mines ore**; click it to switch. Finding ore works as before (prospecting pick). A prospector on Mine ore needs a pickaxe (and torches help): it goes to the best reading in the hut's work area, or of the ore chosen on the hut's Ore tab, digs a staircase down to the ore, digs it out and fills the staircase back in, like the Miner's Hut's trips. Each prospector on Mine ore works its own reading, and its work trains Mining. The hut window shows each prospector's work and status.
- **Miner's Hut mineshaft from pieces**: the miner builds a mineshaft out of schematic pieces in `blueprints/<pack>/mineshaft/`: `entrance.json` (the main opening, the way down) and the sections `cross.json` (+), `tee.json` (T), `straight.json` and `corner.json` (L). It digs and builds the entrance under the hut's Mine waypoint (or beside the hut), then grows a maze: at an open side with nothing beyond it, it picks a section at random, turned so it joins, and digs and builds that, then the next. Each hut level allows `MinerSectionsPerHutLevel` 4 sections. The pieces' posts, beams, ladders and torches come from the hut's storage (couriers are asked for what is missing). When the mineshaft is finished or waiting for materials, it goes to prospected ore with Go to prospected ore on. Without any entrance piece it digs the old spiral shaft. Simple placeholder pieces ship in `medievaloak/mineshaft/` (made by `tools/blueprints/mineshaft_pieces.py`); yours in `ModConfig/createacolony/blueprints/<pack>/mineshaft/` replace them one by one. Save data: `Building` 48 `Trip`, 49 `Sections`; `CitizenRecord` 60 `WorkRole`, 61 `Trip`.

### Making mineshaft pieces
1. In a creative world, build each piece inside solid rock: dig out the way through and put in your posts, beams, ladders and torches. Natural rock, soil, gravel and sand in the export are left as the miner finds them; every empty spot is dug out; everything else is built.
2. Sections (cross, tee, straight, corner) can be any size; their floor is the bottom layer of the export. Pieces line up by their ways through, not their middles, so a way off-centre still meets the next piece's way. Mine waypoints standing in a doorway mark where the next section joins: they count as open and are dug out (0.88.0).
3. An open side is an edge with a gap (empty spots) in the bottom four layers; the middle of its widest gap is where the next piece joins.
4. The entrance (0.63.0, 0.73.0): built down from the surface. Its ground layer is the highest layer whose edge is mostly filled in (natural soil or rock, or a deck of planks); that layer goes at ground height on the land beside the Miner's Hut, with anything above it (a fence) built on top. Tunnels start at **rows of two or more Mine waypoints** on an edge of the entrance: the section's floor goes level with the landing (the waypoints' layer when they replace the landing's floor, the layer above when they sit under a built floor). Sections never come up through the ground layer. Aaron's 0.73.0 entrance winds down 22 blocks with tunnel starts at four landings and one at the bottom, so its stairs no longer repeat (`MinerEntranceRepeatsPerHutLevel` now 0; config version 4 changes a saved 1 to 0). `tools/blueprints/mineshaft_preview/render.py` draws a possible mine from the shipped pieces.
5. Settlers walk steps one block high, and climb ladders.
6. Select each piece with WorldEdit and `/we export entrance` (or `cross`, `tee`, `straight`, `corner`), copy the files to `VintagestoryData/ModConfig/createacolony/blueprints/medievaloak/mineshaft/` (or your pack's folder) and run `/colony reloadblueprints`.

## Aaron's mineshaft pieces and a relieving Quarry (0.63.0)
- **Mineshaft**: Aaron's surface entrance, T section, straight section and corner (0.88.0) ship as the Medieval oak defaults (`assets/createacolony/config/blueprints/medievaloak/mineshaft/`); the cross is still a placeholder (`tools/blueprints/mineshaft_pieces.py`). The entrance goes on the ground beside the hut (saved as the hut's `MineshaftPos`), not under the hut's Mine waypoint, and goes deeper with each hut level. Sections join by their gaps, so their floors line up; tunnels leave the entrance at its rows of Mine waypoints on every loop of the stairs (0.63.1, from Aaron's connected example: loops 7 blocks apart). Support beams keep their saved beams when built.
- **Quarry**: its main work is now whole rock blocks, the game's relieving (a block broken with all six sides open drops itself). Inside the pit's staircase, rock in a 3D checkerboard is left standing (`IsReliefCell`), the rest is dug as before, and each rock that ends up free on all sides is broken out whole before more digging. A 5x5 pit at level 1 gives about 19 rock blocks; any settler digging rock that stands free gets the whole block too. Stat "Whole rock blocks quarried".

## Storage, doors, trader claims and lag fixes (0.72.0)
- **Hut storage**: every chest, trunk, crate, clay storage vessel and modded storage block inside a hut's
  blueprint counts as that hut's storage. A hut without a blueprint uses storage within 2 blocks of the hut
  block. `HutStorageBlocks` in the config adds block codes the mod does not recognise by itself. The
  Warehouse only looks around itself (and inside its building), which was a big source of lag.
- **Doors**: settlers close a door or gate once they have walked through it (after 8 seconds at most), unless
  someone is standing in the doorway.
- **Trader claims**: a land claim the world made for a trader (no player owner, "trader" in its name) is
  lifted when the colony's land reaches it, so the colony can grow over it. `ClearedWorldgenClaims` lists
  the words to look for.
- **Placement**: only officers and the owner can move, turn, confirm or take back placed huts.
- **Lag**: colonies are saved together every 30 seconds (and whenever the world saves) instead of on every
  change; open windows reuse build progress and materials for a few seconds; the mineshaft checks its
  hut's storage once per scan.

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
- Most hut blocks have simple placeholder models (0.57.0); only the Town Hall and the Farmer's Hut have finished ones.
- The Town Hall window, the warehouse and courier, and jobs come in milestones 3 to 6.

## Blueprints

Buildings grow from blueprints, which are ordinary WorldEdit exports. Build the structure in a
creative world with its hut block where the hut should stand, select it with WorldEdit, and run
`/we export <name>`. Copy the file from `VintagestoryData/WorldEdit/` to
`VintagestoryData/ModConfig/createacolony/blueprints/<pack>/<type>/level<N>.json`, where `<pack>` is
`medievaloak` or `devsbuilds`, `<type>` is the building (`townhall`, `builder`, `farmer`, `forester`,
`cook`, `potter`, `residence`, `guardtower`, `tavern`, `warehouse`, `prospector`, `miner`, `quarry`) and
`<N>` is 1 to 5. `/colony reloadblueprints` picks up changes without a restart. Blueprints in ModConfig
override the ones shipped in `assets/createacolony/config/blueprints/`.

### Asset packs (0.46.0)

Blueprints come in **asset packs**, as in MineColonies. The first time you use the Builder's Wand it
asks which pack you want; every building you place after that is built from that pack. The
**Change pack** tab at the bottom of the wand's list picks another one. A building keeps the pack it was
placed in for every level, also after you change packs. Your choice is saved with the world, per player.

- **Medieval Oak** (`blueprints/medievaloak/`, the default since 0.48.0): MineColonies' Medieval Oak style,
  converted from Aaron's copy of its `.blueprint` files by `tools/blueprints/convert_minecolonies.py`.
  Every building, levels 1 to 5 (the Tavern has 3, as in MineColonies), and the Supply Camp. MineColonies
  has no potter, prospector or quarry, so those use its closest workshops: the Glassblower for the
  Potter's Hut, the Sifter for the Prospector's Hut and the Stonemason for the Quarry. What our jobs need
  and MineColonies does not have is added: a fire pit in the Tavern (in place of an oven in the Cook's Hut
  when it has none), the Mine waypoint at the bottom of the Miner's shaft ladder, the Quarry waypoint 8
  blocks beyond the Stonemason's north wall so the pit misses the building, and a bed and a weapons
  chest in Guard Towers without them. Some buildings are open-sided in MineColonies and have no door, so
  they are placed the way they were drawn. The Warehouse's storage racks become chests, but only those
  within the warehouse block's reach (`WarehouseRadius` 5, `WarehouseHeight` 3) count: 10 to 24 of them.
  Pictures of every level are in `tools/blueprints/previews/medievaloak/`.
  Since 0.66.0 MineColonies' alternative Medieval Oak buildings are part of this pack too, as the
  **Alternative** type (`blueprints/medievaloak/<type>/alt/`) of the Town Hall, Builder, Farmer, Forester
  (Lumberjack), Cook, Residence, Guard Tower, Warehouse and Miner: pick it with Previous/Next type in the
  placement window, as with the Small and Medium houses. Buildings and players that were on the old
  Medieval Oak (Alt) pack move to Medieval Oak, with the Alternative type.
- **Dev's Builds** (`blueprints/devsbuilds/`): Aaron's own buildings: Town Hall 1 to 4, Builder's Hut 1
  to 4, Guard Tower 1 to 5, Cook's Hut 1 and 2 (new ones in 0.60.0) and Residence 1 in two types:
  Medium and Small (0.60.0; the older Standard house was removed in 0.60.1). Other buildings have no blueprint in this pack yet, and the list
  says so.

Since 0.64.0 a pack never borrows another pack's design: when your pack has no design for a building, the
wand places just its block (no preview, no build order), so you can build your own building around it. The
hut works at level 0 like any other.

The old **Testing** style and its sample blueprints were removed in 0.46.0. Buildings that were placed
in it keep working: Town Halls and Builder's Huts (built from Dev's Builds since 0.26.1) move to Dev's
Builds, everything else to the default pack, which builds its next level. The 12th Century Inspired
(0.46.0) and KCD2 Inspired (0.47.0) packs were removed in 0.48.0 the same way: their buildings move to
Medieval Oak, and players who had picked one are asked for a pack again. Since the Warehouse now
has levels too, it still works as soon as it is placed, and keeps its land claim before it is built.

The placement window's **Level down** and **Level up** buttons flip the preview through every level
of the building, so you can see how big level 4 gets before you place level 1.

**Building types (0.60.0).** A building can come in several types at the same level, such as a small
or a medium house. Each type is a folder inside the building's folder
(`blueprints/<pack>/<type>/<variant>/level<N>.json`, for example `devsbuilds/residence/small/level1.json`);
the files right in the building's folder are the Standard type. The building list says how many types a
building has, and the placement window shows the type with **< Previous type** and **Next type >** to
switch between them, the outline changing as you go. The type can be changed until the building is
ordered, and it keeps its type for all its levels (a type with only level 1 stops at level 1). A new
type folder needs no code: its name is shown with a capital (`large` shows as "Large"), or from the lang
entry `variant-<name>` when there is one. Folder names are lower-case letters and numbers only.

Doors in a blueprint are hung the way they were in the export, top half included. The first door
also marks the building's front: when you place the hut, the building is turned so that side faces
you. Soil in a blueprint counts as built on any soil (grassy or bare, rich or poor), and filling a
hole in the ground costs any soil; rock, gravel or sand already there also counts, so builders never
dig out stone to put dirt in its place. Burned-out torches in a blueprint are built as lit ones, and a
torch counts as built whether it is lit or burned out. Items lying on the ground (ground storage) are
left out. Empty space below the hut's floor is dug out only when the blueprint has blocks both under
and over it (a cellar); elsewhere the ground below the floor is left alone.

**Wattle and daub (0.56.0)** is built the way a player builds it. Where a blueprint has a daub block
(`daub-tan-wattle`, `daub-tan-cracked` or `daub-tan-normal`), the builder puts up a wattle fence, puts
raw daub on it for the first coat ("wattle"), then more raw daub for "cracked" and again for "normal",
stopping at the stage the blueprint shows. How much raw daub each coat takes comes from the game's own
blocks (`daubUpgradeAmount`), and the Builder's Hut lists it under Items needed. When the colony allows
any wood or stone, any colour of raw daub will do: the wall takes the colour of the first coat, and later
coats use that colour, as the game requires. Otherwise the builder asks for the blueprint's colour. A
smoother coat than the blueprint's also counts as built, and a wattle fence or half-done daub where the
blueprint has daub is never torn down.
Since 0.56.1 builders also vary it: each daub block gets its own stage (wattle, cracked or smooth),
picked from where the block stands, so a building placed many times looks a little different each time.
The pick never changes for a spot, so finished walls stay finished. Set `RandomDaubStages` to false in
`ModConfig/createacolony.json` to build the blueprint's stages exactly.

### Converting MineColonies blueprints

`tools/blueprints/convert_minecolonies.py <style folder> [--library <out folder>]` (Python 3 with Pillow)
turns a MineColonies style (`.blueprint` files, gzipped NBT) into the Medieval Oak packs. Each Minecraft
block becomes the nearest block from Aaron's own exports (and a few plain variants of them, such as
chests, other fence joins, ladders on other walls) in `tools/blueprints/kit.py`: oak for every wood,
shale for every stone (natural stone as cobblestone, since rock cannot be carried), light mud brick for
plaster and pale bricks, oak plank stairs for Domum Ornamentum shingles, and the infill of its timber
frames as the frame block. Glass, carpets, plants, signs, redstone and other things with nothing close
are left out, and the run lists them per building. Structurize's "keep what is there" placeholders are
left out and its "solid ground" placeholder becomes soil. Beds lie the two ways the kit knows (hay beds
east to west, wooden beds north to south), in the same two blocks.

Every level is checked before it is written: one hut block with something under it and nothing on it,
doors with their top half and saved direction and nothing standing in their way, ladders, torches and
lanterns with something behind or under them, whole beds, and the work blocks the hut needs. Loose
furnishings in a doorway are taken out and planks go behind a ladder with nothing to hang on. It draws
all levels of each building, whole and cut away, in `tools/blueprints/previews/<pack>/`, with a list of
what each level needs. With `--library` it also writes every blueprint in the style, the buildings this
mod has no hut for too, as a plain WorldEdit export for `/we import`.

The in-game `/colony importmc` (below) does the same for one file, matching each block against the
installed game instead, so it can use other woods and rocks.

`tools/blueprints/import_devsbuilds.py <folder>` copies Aaron's exports into Dev's Builds (files named
like `TownHallLevel1.json`, `GuardTowerLevel5.json`, `CooksHutLevel2.json`; a word after the level number
is a building type, so `HouseLevel1Small.json` goes to `residence/small/level1.json`). It checks the same
things, can add a hut block where an export has none (`HUT_SPOTS`), and warns when a level would line up better a few blocks off, which
is how an export with the hut block in the wrong place shows up.

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

### Furniture, banners and food shelves (W4RD0's Furniture, Heraldry : Banners, Food Shelves)

With W4RD0's Furniture (1.5.1), Heraldry : Banners (2.0.0, with Heraldry) or Food Shelves (3.1.1) installed,
blueprints can hold their furniture, banner poles and food shelves. The mods are optional: without them those
blocks are skipped. A builder
places these blocks the way a player does, from their item, so the block sets itself up: a double bed
or a wide table puts down its other half, a shelf its other cells, and the block takes the item's wood
and colours. The builder pays for the furniture item in the wood and cloth colours the blueprint has
("Rustic table (Oak)" in Items needed; "(any wood)" when the colony allows any wood), and for a pole's
banner with the same design, which has to be made at the Heraldry workbench. What furniture held in the
export (a cupboard's contents, a shelf's display) is not copied. Food Shelves blocks work the same way: a
builder pays for a "Bread shelf (Oak)" in the blueprint's wood, and the food that stood on it in the export
is not copied. The config list `BlueprintPlaceAsItem` (default `["w4rd0sfurniture:*", "heraldrybanners:*",
"foodshelves:*"]`, added to older config files that still had the old default) says which blocks are placed
this way; any block that takes its wood from item attributes (Attribute Rendering Library) is placed this way too.

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
- The farmer needs a hoe. A **Field marker** (dry grass on two sticks, a scarecrow two blocks tall that needs
  the block above it free) marks out the square of
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
are added. The last tab, **Change pack**, picks the asset pack (see Asset packs above), and the
placement window shows which pack the building uses.

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


**Placing by hand for making blueprints (0.49.0).** An admin can type `/colony handplace on` (or set
`PlaceHutsByHand` to true in `ModConfig/createacolony.json`) so the Town Hall, every hut and the Warehouse
go down by hand like any block, anywhere, with no wand and no colony. They face you as the wand would. A
block placed this way is only a block for building and exporting blueprints: it founds no colony, adds no
building, and its window does not open. `/colony handplace off` brings the wand back. The setting is saved.
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
  carry (`expandedSats`) count too. Settlers find food on Food Shelves shelves like in any other container.
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
- **Food Shelves (0.51.0):** with the Food Shelves mod, any of its shelves, baskets and cabinets inside the
  Cook's Hut (the building as its blueprint stands) are kitchen shelves. The cook puts what it cooks (grilled
  food, pot meals, pies) on a shelf that takes it before anything goes into the hut storage, and moves dishes
  from the hut storage onto shelves with room. It also takes ingredients and raw food from the shelves (a
  vegetable basket, an egg shelf), and food on the shelves counts on the Kitchen tab. Couriers with nothing
  else to do bring ready food from the warehouse to kitchen shelves that have room for it. Settlers stock a
  shelf the way a player does: only what that shelf takes (bread on a bread shelf), one kind per segment.
- **Stone Bake Oven (0.55.0):** with the Stone Bake Oven mod (1.4.0), the cook bakes in an oven whose baking
  spaces are within `CookFirepitRadius` (8) blocks of the Cook's Hut. It puts dough on the baking spaces (four
  pieces each), makes pies itself (4 dough and 8 of one filling, such as berries or vegetables, on an empty
  baking space), puts firewood in the oven's heat source and lights it, keeps the fire going while something
  bakes, and takes the bread and pies out once baked, before they burn. They go on the kitchen shelves or into
  the hut storage. The Kitchen tab has a row for each bake ("Oven: Bread", "Oven: pie (any filling)") with how
  many to keep (bread `KitchenDefaultKeep`, pies `OvenDefaultPieKeep`, 2). Bakes take a Cooking level from
  `config/cooking.json` "baking": bread 1, pies 3, other mods' bakes 2. While the oven bakes, the cook gets on
  with the fire pit; a hut with an oven and no fire pit only bakes. Dough itself is not made by the cook yet:
  put it in the hut storage or the warehouse, and couriers bring it. The oven's cooking top (for pots) is left
  to players. Builders place the oven's blocks from a blueprint like any block: they pay for the block as it
  comes from crafting, any stone when the colony allows it, and never take its heat source for a fire pit.

### The smelter and the blacksmith (0.50.0)
Both huts have a simple placeholder model and a placeholder level 1 blueprint (an open plank shed with a fire
pit or a forge), ready to be replaced by real buildings. Both are on the wand's Craftsmanship tab, and the
Smithing skill grows with both jobs.
- **Smelter's Hut** (2 charcoal + 7 planks). The smelter uses the game's own fire pit (the nearest within
  `SmelterRadius`, 8), crucible and ingot molds. It sets up to `SmelterMolds` (4) fired ingot molds out beside
  the fire pit, fills a fired crucible with one kind of nugget or metal bit (whole ingots only, up to
  `SmelterIngotsPerBatch`, 4), puts it on the fire with fuel hot enough for the metal (charcoal or coal for
  copper; it asks couriers for `SmelterFuelCode`, charcoal, when the warehouse has none), lets the game melt it,
  and pours the metal into the molds. Cooled ingots come out of the molds into the hut storage. Metal that
  cools in the crucible goes back on the fire. The Smelting tab lists every metal with ingots ready (warehouse,
  hut, smelter, molds and crucible) and how many to keep (`SmelterDefaultKeep`: 4 copper, others 0). No
  alloys (bronze) and no bloomery yet.
- **Blacksmith's Hut** (2 ingots + 7 planks). The blacksmith needs a hammer, the game's forge (in the
  blueprint) and an anvil placed by a player, both within `BlacksmithRadius` (8). It heats the ingots for a
  piece in the forge with charcoal, takes them out once they are hot enough to work, and strikes the anvil
  once per `BlacksmithVoxelsPerStrike` (8) voxels of the piece instead of moving voxels one by one. Cold
  ingots go back in the forge. When the game has a 2-item crafting recipe that puts the piece on a handle (a
  pickaxe head and a stick make a pickaxe) and the blacksmith has the handle, it makes the finished tool. What
  can be made comes from the game's smithing recipes; the anvil's tier must be high enough for the metal.
  The Smithing tab shows everything that can be made from the metals the colony has ingots of, with the
  ingots each takes; nothing is kept until an officer picks something.

### Building types and new Dev's Builds (0.60.0)
- Buildings can have several types per level, picked in the placement window (see "Building types" above).
- Dev's Builds: new Cook's Hut levels 1 and 2 (replacing the old level 1), and Small and Medium houses for
  the Residence level 1. 0.60.1 removes the older Standard house; Dev's Builds houses placed with it use
  the first remaining type (Medium).

### Survival Handbook pages (0.59.0)
- Seven guide pages in the game's Survival Handbook (Guides), titled "Vintage Colonies: 1 - Getting Started" to
  "7 - Tips and Commands": getting started, settlers, building with the wand, the huts, storage and couriers,
  guards, and tips. Files `config/handbook/vintagecolonies-*.json`, text `handbook-vc-*` in the lang file.

### Creative tab (0.58.0)
- Every block and item of the mod is also on its own **Vintage Colonies** tab of the creative inventory
  (`"vintagecolonies"` in each `creativeinventory`; the tab name is `game:tabname-vintagecolonies` in the lang file).

### Hut models (0.57.0)
- Every hut block that was a plain cube now has a small model on a crate, like the new huts: Builder (toolbox and
  hammer), Cook (pot on a fire), Forester (stump and axe), Guard Tower (lookout), Miner (rock, ore and pickaxe),
  Potter (pots), Prospector (rock, pick and map), Quarry (stone blocks), Residence (bed), Tavern (barrel and
  mugs) and Warehouse (crates). The Supply Camp is a crate and a small tent. The Town Hall and Farmer's Hut models
  (by Aaron's friend) are unchanged. Generator: `tools/models/hut_models.py`.
- The Field marker is a scarecrow two blocks tall. Only its post is solid, and it needs the block above it free.

### The Explorer, the Gatherer and colony map markers (0.77.0)
- **Explorer's Hut** (1 stick + 7 planks, wand's Infrastructure tab) and **Gatherer's Hut** (1 fruit + 7 planks,
  Agriculture tab), each with a simple placeholder model and a placeholder level 1 blueprint. Neither job needs
  a tool. Both have the Work area tab (range, escort) and a new **Markers** tab: a switch for each kind of
  marker and a "Remove our markers" button.
- The **explorer** walks out over the land around its hut in `ExplorerStep` (32) block squares, nearest first,
  up to `ExplorerRadius` (300), and looks around `ExplorerLookRadius` (20) blocks at each stop. It marks ore
  lying on the ground or showing at the surface, clay, peat and terra preta, traders (by trader type), animals
  to hunt, danger (wolves, bears, drifters and the like), translocators and mountains. When it has seen
  everything it starts over after `ExplorerRevisitDays` (7). New Exploring skill.
- The **gatherer** forages up to `GathererRadius` (48) blocks out: ripe berries and fruit, mushrooms that are
  safe to eat (going by what eating them does), and loose stones, flints and sticks on unclaimed or colony land,
  not in or next to a building (`GathererPicksUpStones`, 0.84.0). Around wherever it works it marks berry bushes, fruit trees, mushrooms (poisonous
  ones apart), wild crops, cattails, resin and wild bees. With nothing to pick it walks out to look around at
  most every two minutes. New Foraging skill.
- **Colony markers** is a new world map layer (`Map/ColonyMarkerMapLayer.cs`), listed after the game's Owned
  creatures, with Colony prospecting moved there too. Every member sees the markers of their colonies. Since
  0.79.0 the icons are Vintage Colonies' own (see below), with the game's own as a fallback. The layer adds the two
  colours More Map Icons names that the game's colour list leaves out (#03D941 and #885530), so the Add
  waypoint window can make Olivine, Emerald, Brown Coal and Peat too.
- A marker of the same kind is never put within `MarkerSpacing` blocks of another (per group: animals and
  danger 96, ore 32, plants 16...). At most `MaxColonyMarkers` (600) from huts per colony.
- A **grave marker** shows where a settler died ("RIP name") or was knocked out ("name fell here"), where their
  things dropped. Only their latest spot is kept (`MarkSettlerDeaths`).
- Code: `ColonyManager.Markers.cs` (marker kinds, LookAround), `ColonyManager.Exploring.cs`,
  `AiTaskSettlerExplorer.cs`, `AiTaskSettlerGatherer.cs`. Not tested in game.

### Vintage Colonies' own map icons (0.79.0)
- 120 new waypoint icons (`assets/createacolony/textures/icons/worldmap/vc-*.svg`), one for every marker on
  More Map Icons' sheets: elements, ores and minerals, traders, animals, plants, places and arrows. Colour
  variants (Olivine, Emerald, Brown Coal, Peat and so on) are their own icon with a letter on it. They show up
  in the Add waypoint window for every player, with or without More Map Icons, and picking one fills in its
  name (`game:wpSuggestion-vc-*` in the lang file).
- Colony markers use these icons now, so they look the same for everyone.
- More Map Icons has no licence, so none of its art is used. The pictures come from game-icons.net (CC BY 3.0)
  and the letters from DejaVu Sans Bold: see THIRD_PARTY_NOTICES.md. `tools/icons/marker_icons.py` makes them
  again. Not tested in game.
- 0.79.1: the Add waypoint and Edit waypoint windows lay the icons out wider (`Map/WaypointIconGrid.cs`), about
  twice as wide as tall and as wide as the screen allows, so they no longer run off the bottom of the screen.

### A marker for every ore, crop, animal, trader and story place (0.80.0)
- When the game starts, Vintage Colonies makes a list of every ore, wild food, animal and story location there
  is, the game's and other mods' (`Map/MarkerSubjects.cs`), and gives each one a waypoint icon. Ores come from
  ore blocks; food only from what grows wild (crops, berry bushes, fruit trees and plants that can be picked);
  animals one per kind of creature (every bear is "Bear"); all traders share one icon; story locations come
  from every `worldgen/storystructures.json`.
- Things with a picture of their own use it (the worldmap icons, or one of 106 pictures in
  `textures/icons/vcart/`, also made by `tools/icons/marker_icons.py`). Things that share a picture, or have
  none (most other mods' things), get their kind's picture with letters from their name in the corner.
- The icons are added to the Add waypoint window with their names, so players can use them too.
- Explorers and Gatherers mark each thing with its own icon and name: each ore, each wild crop, berry bush and
  fruit tree by what it grows, each kind of animal, and traders. Animals that hunt players are marked as
  danger. Explorers also mark story locations they come near, under Landmarks. Two different things close
  together both get a marker; markers put down before 0.80.0 keep their old icons.
- 0.80.1: food only from wild plants (no gelatin and the like), one icon per kind of animal, one for traders.
- 0.80.2: no icons for tamed animals or helpers (animal bot, player bot, mech helper). The waypoint list keeps
  the game's and other mods' icons first, then ours by group: food, ore, animals, traders, story locations, the
  rest. The colour list has 36 more colours (pale and deep) and is as wide as the icons.
- The server and every player make the same list, so the names of the icons match. Not tested in game.

### The barracks (0.54.0)
- **Barracks** (2 bows + 7 planks), with a simple placeholder model and a placeholder level 1 blueprint (an open
  plank shed). It is on the wand's Military tab. It is a second kind of guard post and works like a Guard
  Tower: the same window tabs (Guard Scepter, Hostiles, Settings, Minimum stock, Statistics), tasks, night
  watch and armor. It has `ColonyManager.BarracksGuardsPerLevel` (2) guard places per level instead of one.
  Guards on the Guard task fight up to `BarracksRadius` (32) blocks from their spot instead of
  `GuardTowerRadius` (20), and the automatic patrol visits every hut within `BarracksPatrolRange` (96) instead of
  `GuardPatrolRange` (48). New guards fill Guard Towers with room first, then Barracks.

### The sawmill (0.52.0)
- **Sawmill** (2 logs + 7 planks), with a simple placeholder model and a placeholder level 1 blueprint (an open
  plank shed). It is on the wand's Craftsmanship tab. A settler with the Sawyer job needs a saw and works at the
  hut block itself, one crafting recipe every `SawmillSecondsPerCraft` (5) seconds; the saw wears as it would
  for a player. What it can make comes from the game's crafting recipes: every recipe whose ingredients are all
  wood (`SawmillWoodParts`: logs, planks, firewood, sticks, support beams and the like) and whose tool, if any,
  is a saw (`SawmillToolParts`). Recipes with more than `SawmillMaxInputs` (16) pieces of wood are left to
  players. The Sawmill tab lists what the colony has the wood for and how many to keep; a new Sawmill keeps 16
  planks of each wood (`SawmillDefaultKeep`). The sawyer fetches wood from the hut storage, or asks couriers
  for up to `SawmillBatch` (4) recipes' worth from the warehouse. What it makes goes to the warehouse. The new
  Carpentry skill grows with the work and makes it faster.

### Doors and gates

Settlers walk through doors and fence gates that open by hand. They open one as they reach it and close
it behind them once nobody is standing in it. Doors that need a key or a lever stay shut to them. This
patches the game's pathfinder (with Harmony) only while it plans a path for a settler.

### Hut storage

Every Builder's, Forester's and Farmer's Hut has its own 25-slot storage: sneak and right-click the
hut block, or press Storage in its window. Chests within two blocks of the hut add more room.

### The settler window

Right-click a settler to open its window, laid out like the player's character window with four tabs
along the top: **Character & Armour** (the settler's model, drag to turn it, between its clothing
slots, and its armor row), **Stats** (health, satiety, walk speed, status, bed, job, what it is doing,
family), **Inventory** (tool bar, bags, held item, pack, packed food) and **Traits & Skills**. See
"Settler gear, clothes and inventory (0.43.0)" for what you can do in it.

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

### Farmers soak the soil (0.98.0)
- A farmer with a watering can now waters farmland until it is at 100% moisture, not just until it shows as
  moist. It waters crops whose soil is below `FarmerWaterBelow` (now 0.5, was 0.3).

### Corpses for players and settlers (0.94.0)
Built in from [Player Corpse](https://mods.vintagestory.at/playercorpse) by DArkHekRoMaNT (MIT licence, see
`THIRD_PARTY_NOTICES.md`), brought up to 1.22 and without its CommonLib dependency.
- When a player dies, their things go into a corpse where they fell, instead of spilling on the ground. Hold
  right-click on it to take everything back. Only the owner can do this at first; after 240 in-game hours anyone can.
  A death waypoint is added to the map and goes when the corpse is collected.
- When a settler dies, their pack, bags, tools, armour and clothes go into "Name's corpse". A knocked-out settler
  leaves "Name's things" (their clothes stay on). Any member of their colony can collect it. The RIP marker on the
  map sits on the corpse. The "fell here" marker of a knocked-out settler goes once their things are collected.
- The Corpse Compass (rusty gears, bones and a temporal gear) points at your nearest corpse, or a settler's corpse
  from your colony.
- `/returnthings list <player>` and `/returnthings get <player> <give to> [id]` give back what a player lost in one
  of their last 10 deaths (needs the gamemode privilege).
- Config: `ModConfig/createacolony-corpses.json`. `PlayerCorpses` and `SettlerCorpses` turn each part off. With
  corpses off, things drop on the ground as before. If the Player Corpse mod is installed too, it makes the player
  corpses and this mod only makes settler ones.
- Code: `src/Corpses/`; entity `createacolony:corpse`, item `createacolony:corpsecompass`. No new save fields.

### What killed a settler; gatherer marks resin and beehives (0.93.0)
- The Town Hall's Dead Citizens page has a "Killed by" column: the creature (Wolf), the player or settler, or what
  happened (Starved, A fall, Drowned, Fire). Settlers who died before 0.93.0 show "unknown". Save data:
  `CitizenRecord` 63 `DeathCause`; `CitizenView` 31.
- The gatherer also marks resin on tree trunks and wild beehives in the trees (it looks up to 16 blocks above
  the ground). Wild crops never grow back, so once the gatherer picks the last wild crop near a crop marker,
  that marker comes off the map. Mushroom markers stay, as mushrooms regrow.

### Gatherer: ore bits and wild crops; clearer map markers (0.92.0)
- The gatherer picks up ore bits lying on the ground (native copper in claystone and the like). Each spot is
  marked "Underground copper deposit" (or the ore's own name) on the Colony markers map, and the prospectors
  take a reading there before walking anywhere else, so the miners dig it if the reading is good. One spot per
  ore within `OreHintSpacing` (16) blocks. Save data: `Colony` 91 `OreHints`.
- Wild crops (not on farmland) are picked only once fully grown, never near buildings.
- Colony markers on the world map are solid black with a light rim and a little bigger, so they are easy to see. `MapMarkerColor` ("#000000") sets the colour; "" gives each kind its own colour again. The settlers layer is now called "Colony settlers".

### Allowed to collect (0.91.0)
- A new **Allowed to collect** tab on the Forester's, Gatherer's, Farmer's, Miner's and Prospector's Huts and
  the Quarry. It lists everything that worker can take, each with a switch. Everything is on at first.
  Officers can turn things off one by one, or use All on / All off. The search box finds a row by name.
- **Forester**: each kind of tree. It leaves trees that are turned off standing.
- **Gatherer**: each berry and fruit, each edible mushroom, and loose stones, flint and sticks.
- **Farmer**: each berry and fruit (crops are still chosen on each Field marker).
- **Miner, Prospector (Mine ore) and Quarry**: each ore, each rock and soil, gravel, sand, clay and peat. They
  never go after ore that is turned off. Blocks in the way still get dug, but what they drop is left on the ground.
  Choosing an ore on the Ore tab turns it back on.
- Building field 160 (CollectOff), ColonyAction 160 (SetCollect), ColonyManager.Collecting.cs. Not tested in game.

### Builder's list: one line per item, daub from wattle, any farmland (0.97.0)
- Aaron's Residence asked for the same item more than once. With "any wood or stone" on, a roof's planks and logs
  were still asked for in the exact wood; now they are any wood too, so they join the other planks and logs.
- Wattle and daub was asked for as the finished daub blocks (one line per stage). Daub is now known by its code
  as well as its class, and raw daub is looked for again if it wasn't found the first time, so the list asks
  for wattle fences and raw daub and the builder makes the daub on the spot.
- Farmland counts however wet or fertile it is. Players can't pick farmland up in survival, so the builder makes
  it: soil goes down first (any soil, placed as the kind brought), then the builder tills it with a hoe, as a
  player does. The Builder's Hut asks for a hoe when it needs one, and keeps hoes like its other tools. Soil counts as any soil (farmland too), and
  the list says "Soil (any kind)". Not tested in game.

### The Mineshaft Entrance's preview (0.96.0)
- Aaron saw no building preview when placing the Mineshaft Entrance. The mine entrance piece (entrance.json) had
  its roofs but not their saved details, so they had nothing to draw. Its 61 roof details now come from Aaron's
  SurfaceMineshaftExample (same blocks, 11 layers up and 2 rows over), so the roofs show and get built properly.
- The ghost preview leaves out a roof block with no saved details, so one can't keep the rest from showing.
  Not tested in game.

### The Mineshaft Entrance's building (0.95.0)
- Aaron saw the Mineshaft Entrance had no building. Its building is now the pack's mine entrance piece: the
  placement outline shows it, and Build (or the first linked miner) sets the mine there. Linked miners dig and
  build it; builders never do, and Remove hut gives the block straight back and leaves the mine.
- Where the hut block sits comes from Aaron's surface example: entrance.json now has a
  createacolony:hut-mineshaftentrance block at (7, 12, 7), on the ground layer beside the shaft. A pack whose
  entrance has none puts the block just outside the entrance, on the side the player stood.
- The miners no longer treat the entrance building's box as off limits, and never dig the hut block. Not tested in game.

### Remove hut and the Mineshaft Entrance (0.90.0)
- **Remove hut** (Main tab, build options, officers): press it twice. The hut's workers lose their job at once,
  a builder takes the building down from the top, and the hut block goes back to the player who pressed it (into
  the builder's hut when they are away). Only the blueprint's blocks come down: natural ground, things players
  added and chests with items in them stay. A hut with nothing built goes straight back. Stop removing (or Cancel
  in the work orders lists) stops it. The Town Hall and Warehouse can't be removed, nor the only Builder's Hut.
- **Derelict mines**: a removed Miner's Hut's mineshaft is never taken down. It is left derelict (kept on the
  colony's DerelictMines list) until a Miner's Hut without a mine within 32 blocks, or a Mineshaft Entrance
  within 16 blocks of its entrance, takes it on.
- **Mineshaft Entrance** (Mining tab of the wand, no design, block only): holds a mine so several Miner's Huts
  work it. Put down near a Miner's Hut's mine it takes it over and links that hut; near a derelict mine it takes
  that on. Its Main tab lists the Miner's Huts with their miner and Link / Unlink; Levels and Settings change
  the shared mine for every linked hut. A new Miner's Hut within 32 blocks joins it by itself. A linked hut's
  own old mine is left derelict. The deepest level comes from the highest linked hut. Guards, Required resources,
  the trip to prospected ore and the materials stay per hut. Removing the entrance leaves its mine derelict.
- Building fields 56-59 and 62 (MineEntranceId, Removing, RemovedBy, MineRotation, MineStyle), Colony 90
  (DerelictMines). Not tested in game.

### Old previews no longer pile up (0.89.0)
- Placing a building after another showed the earlier buildings' ghosts inside the new one (the Town Hall and
  Builder's Hut inside a Guard Tower). Each new placement now gets a preview space of its own, and the game
  only draws the latest one, so old ghosts are never drawn again.

### Corner section (0.88.0)
- Aaron's corner (an L turn) ships as blueprints/medievaloak/mineshaft/corner.json and is picked at random with the
  others. The block of Mine waypoints at its far end marks the doorway where the next section joins; checked
  against his corner + T and corner + straight examples, both join at the same spots.
- Its empty quadrant was filled with shale so the miner leaves that rock alone.

### Mine levels (0.87.0)
- Aaron's new pieces: a new entrance (the surface, level 0) and a level piece (blueprints/medievaloak/mineshaft/level.json).
  Level pieces stack under the entrance's shaft, each just under the floor of the one above, lined up as in his
  combined example. Each level opens the floor above where its top layer is open and carries its ladder up.
- Tunnels start at each patch of Mine waypoints in a room's floor (away from the piece's edges), facing away from
  the middle. Sections (tee, straight, cross) branch off them at random on every level, so every level has the
  same middle and different branches. Sections keep off the rooms' dug and built spots, not their whole box.
- A level 0 Miner's Hut has level 1, each upgrade adds a level (`MinerMineLevelsPerHutLevel`), and a hut at its top
  level goes down as far as the world allows. Max depth still caps it. A style without level.json works as before.
- Preview: tools/blueprints/mineshaft_preview/render_levels.py.

### Far workers keep their hut loaded (0.86.1)
- A worker plans every trip from its hut, so when no player was near the hut it found no hut storage and stopped.
  Now the hut's chunk column stays loaded while it has a worker and no player is near.

### Bug fixes (0.86.0)
- Settlers lie along their bed like a player, using the player's lying pose, and stay put so a bedmate does not
  push them askew. Vanilla beds use the game's own sleeping spot; furniture beds use the mod's seat and facing.
- Guards (escorts too) go to bed at night unless a monster is within `GuardWakeRadius` (8) blocks on their level,
  or a settler is attacked. Monsters further off, or in caves below, no longer keep them up all night.
- The Residence hut block is storage, with Storage and Minimum stock like the other huts.
- Settlers resend their animations to players every few seconds, so ones coming back into view are not stiff.

### Gatherer fix (0.84.0)
- The gatherer only picked up loose stones on unclaimed land, but the Town Hall claims everything around the hut,
  so it found nothing and kept looking around. Now it also picks them up on the colony's own land, except in or
  within two blocks of a building. The Gatherer's Hut window shows how many things are ready to pick.

### Settlers move like players (0.83.0)
- A settler holding a torch or lantern holds it up the way a player does (`HoldLightsLikePlayers`).
- A settler with nothing to do now and then looks around, scratches its head, coughs or stretches, like an idle
  player. Late in the day it yawns (`IdleAnimations`).
- Settlers sit down to eat, as a player sits (`SitToEat`). On a taken break they often sit for a while too
  (`SitWhenIdle`). They pick a free chair, stool or bench nearby if there is one, also from other mods, else they
  sit on the floor. Eating sitting down takes a little longer.
- Eating uses the player's hand-to-mouth animation.

### Colony militia, settlers on the world map, tab order (0.85.0)
- Town Hall tabs: Prospecting moved down to just above Settings, which stays last. New **Colony militia** tab
  after Citizens.
- **Colony militia** lists every grown settler with their job, Guard or Civilian, and the weapon they would
  fight with (the one in hand, else the best one they carry), guards first. Two switches for officers:
  **Self Defence** (settlers with a weapon fight a creature that hurts them; this is 0.76.0's fighting back,
  now per colony, default from `FightBackWhenAttacked`) and **Support allies** (when a settler is hurt by a
  creature, other armed non-guard settlers within `SupportAllyRadius` (16) blocks come and fight it too; default
  from `SupportAllies`, on). Helpers that can no longer fight go back to work; they never run unless hit.
- **Colony settlers** world map layer (`Map/ColonySettlerMapLayer.cs`), next to Colony prospecting and Colony
  markers: every settler of the player's colonies as a dot (green, yellow wanderers, red knocked out), sent
  by the server every 3 seconds so far-away settlers show too. Pointing at one shows its name, job and colony.
- The Town map also names the settler under the pointer.

### Town map land drawn black (0.81.0)
- The Town map showed loaded land as solid black: its textures were made without a size, so nothing was
  uploaded. They now get their size (256 by 256) up front, and get smaller copies for zooming far out.

### The Town map shows all loaded land (0.78.0)
- The server draws every chunk near a colony that is loaded, whether a player or a settler loaded it (far
  workers keep their land loaded), in the world map's own colours and shading (`Map/ColonyLandMap.cs`). Each
  chunk is drawn again when it changes, or every 5 minutes while loaded, a dozen chunks every quarter second,
  those nearest the Town Hall first. The pictures are kept in the save, so land stays on the map after it
  unloads, like the world map. Land over `TownMapRange` (1024) blocks from the colony's land is left out.
- The Town map asks the server for new chunks every 2 seconds while it is open (its own network channel,
  `createacolony-landmap`, so it does not touch the main one) and keeps them as textures of 8 by 8 chunks.
- Zoom goes from the whole area down to 16 pixels a block, and dragging can move the map anywhere in the area.
  **Redraw map** is gone (the map keeps itself up to date); **Find me** moves the map to you.

The Town Hall window is laid out like MineColonies', with eight tabs down the left:

- **Actions**: the colony's name and the Town Hall's level on a panel like a hut's Main tab (the pencil
  renames the colony), the colony's numbers, the Town Hall's own upgrade, cancel and turn buttons, how
  much land the colony claims, and a **?** guide.
- **Town map**: the colony from above (0.24.0). Since 0.78.0 it looks like the world map and shows all land
  within `TownMapRange` (1024) blocks of the colony's land that you, another player or a settler has loaded,
  dark where nobody has been. The colony's land is tinted with a gold edge, and each building's ground is a box
  (gold Town Hall, blue huts, dashed orange while being built). Settlers are green dots (wanderers yellow,
  knocked out red) and you are white. Point at a building for its name and level. The mouse wheel (or **+** and
  **−**) zooms around the pointer, from the whole area down to 16 pixels a block; drag with the left button to
  move; **Fit** shows the whole colony again and **Find me** moves the map to you.
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

### Settlers without a job: home upkeep and helping out (0.53.0)

Settlers with no job no longer just stand about (`src/Colonies/ColonyManager.Zones.cs`,
`src/Settlers/AiTaskSettlerChores.cs`).

- **Zones.** Every building's zone is the ground its blueprint covers, one block wider all round. A
  settler's **home** is the building whose zone holds its bed (a Residence first, else the smallest zone).
- **Home upkeep.** A settler without a job looks after the farmland inside its home, like a farmer: it
  harvests ripe crops and sows again, pulls weeds, waters dry crops, sows bare farmland, and tills soil
  wherever the home's blueprint has farmland. Seeds (`JoblessSeedTake` at a time), a hoe and a watering can
  come from a chest in the home, else from the warehouse. When the plot needs nothing more, the harvest
  and what it took go into a chest in the home; with no chest, it takes them to the warehouse. Farmland that
  is part of a farmer's Field marker square is left to the farmer.
- **Helping out.** With nothing to do at home, it helps the couriers: deliveries to huts and builders, emptying
  drop-off chests and clearing huts into the warehouse.
- `JoblessTendHome` and `JoblessHelpHauling` in the config switch either off. More home duties (repairs,
  tidying) can be added to `AiTaskSettlerChores.Plan`.

### Skills (0.37.0)

Settlers learn by doing, Project Zomboid style (`src/Colonies/ColonyManager.Skills.cs`, skills listed in
`config/skills.json`). Each skill goes from level 0 to 10; level n costs `SkillXpPerLevel` x n XP on top
of the level below (75, 150, 225... 4,125 in all).

- **Job skills**: Building, Farming, Woodcutting, Pottery, Cooking, Hauling, Fighting, Prospecting,
  Mining (miner and quarrier), Smithing (blacksmith and smelter) and Carpentry (sawyer). XP comes from the
  work itself (a block built, a tree felled, a hit landed, a stack delivered); each usable level makes the
  job's work 4% faster (Fighting: 2% faster swings and 3% more damage a level).
- **Strength and Fitness** grow for everyone: Strength from felling trees, mining, fighting and full courier
  loads (+3% guard damage a level); Fitness from walking, one XP every 50 blocks (+1.5% walking speed a
  level, +1 max health every two levels).
- **The building limits what counts.** A settler's *usable* level is the lower of their real level and the
  cap of the hut they work at: `SkillCapByBuildingLevel` is 4, 6, 8, 10 for hut levels 1 to 4. Strength and
  Fitness go by the Residence they sleep in. Skill is no use without the tools: a Fighting 10 guard at a
  level 1 Guard Tower fights like a level 4 until the tower is upgraded. XP keeps growing past the cap.
- **Starting skills**: recruits, wanderers and starting settlers roll 0 to 2 in every skill and one
  background skill at 2 to 4. Tavern offers show their two best skills before you pay. Children start at
  nothing and get a third of their parents' average when they grow up. Settlers from older saves roll
  theirs once when the colony loads.
- **Windows**: a Skills panel in the settler window (level, a bar to the next level, "10 (using 4)" in
  orange when the building limits it, hover for XP and what to upgrade); the hut's Manage workers list
  shows each candidate's level in that hut's skill and sorts the best first.
- Level 5 and level 10 go in the Town Hall event log. `/colony skills <name> [skill] [level]` shows or sets
  skills (admin). `Skills: false` turns the whole thing off; `SkillXpMultiplier` speeds it up or down.

### RimWorld-style skills and happiness (0.61.0)

Design doc: https://claude.ai/artifact/D5hsBzzJ8fd3uJ5D6WpEFj. This replaces parts of the 0.37.0 section above.

- **Levels 0 to 20** with medieval names (Unschooled, Novice ... Journeyman ... Master ... Legend of the realm,
  lang `skill-rank-<n>`). Levels 0 to 10 cost what they did, so old saves keep every level; above
  `SkillSteepFrom` (10) steps grow `SkillSteepFactor` (2) times as fast: level 20 is 19,875 XP.
- **Hut caps** `SkillCapByBuildingLevel` 4, 8, 12, 16, 20 for hut levels 1 to 5. A building at the top level its
  blueprint style has always allows all 20 (Dev's Builds huts with fewer levels).
- **Passions** (`CitizenRecord.Passions`, 1 = Interested "+", 2 = Burning "++", marks in lang `passion-mark-*`):
  XP x1 / x2 / x3 (`SkillPassionXp`) and +8 / +14 happiness for 4 hours after each piece of that work. 0 to 3
  rolled per settler (`SkillPassionCountWeights`, a quarter Burning), likelier on their best skill. A job trait
  gives its skill a flame (two if it already had one) instead of the old 1.5x XP. Children take each parent's
  passion half the time when they grow up. Old saves roll passions once.
- **Incapable**: traits with an `incapable-<skill>` effect (Pacifist: fighting; Bad Back: woodcutting and
  mining; Ham-fisted: cooking and pottery) block those jobs, XP and usable level. Hut lists grey them out.
- **Decay**: levels above 10 lose `SkillDecayPerDay` XP every in-game day (5 at 11 up to 270 at 20), never below
  level 10. Good Memory halves it. **Daily soft cap**: `SkillDailyCap` (600) full XP per skill per day, then
  `SkillOverCapFactor` (0.2).
- **Learning rate**: XP = work x passion x trait `learning` (Fast Learner 1.5, Slow Learner 0.6) x mood x
  `SkillXpMultiplier`, then the daily cap. Fractions round by chance.
- **Milestone perks** (`perks` in skills.json, one value per `SkillPerkLevels` 5/10/15/20): Building tool wear,
  Farming/Woodcutting/Mining/Pottery extra yield (in `StoreHarvest`), Cooking extra portion (in
  `EmptyFirepit`), Hauling and Strength pack slots, Fighting swing speed, Prospecting work speed, Fitness hunger.
- **Happiness** (`ColonyManager.Happiness.cs`): 0 to 100, drifting `HappinessPerHour` (2) towards 50 plus all
  thoughts. Lasting thoughts (`CitizenRecord.Thoughts`): passion work, cooked meal, skill up, became a parent,
  knocked out, a death (family -20). Standing ones: hungry/starving, varied diet, bed in a Residence (+1 per
  Residence level) or no bed, partner, Town Hall level, no job, Sanguine/Melancholic (trait `mood`). Joyful 85+
  works 10% faster and learns 20% faster; Happy 70+ 5%/10%; Unhappy below 45 works 5% slower; Miserable below 30
  15% slower, learns 25% slower. At `HappinessBreakAt` (15) a settler stops working for 2 to 4 hours
  (`IsBrokenDown`, checked by every work task), then gets a +20 relief thought for a day.
- **Windows**: Skills panel shows passion marks, "v" when decaying, "Incapable"; hover has the level name, passion,
  today's XP against the cap and the next perk. Stats tab has a happiness bar with every thought on hover. Town
  Hall Overview shows the average happiness. `/colony mood <name> [0-100]` shows or sets happiness (admin).
- Config version 3 upgrades files still at the old skill defaults (max 10, caps 4/6/8/10, effect clamp 2).

### Traits, the population cap and meal levels (0.45.0)

**Traits** are what a settler is born with (`src/Colonies/ColonyManager.Traits.cs`, listed in
`config/colonytraits.json`). Every settler has 2 to 10 of them, always at least one good (points above 0) and
one bad (points below 0); quirks have 0 points. Each trait has a level from 0 to 20: a good trait's effect
grows 5% a level, a bad trait's penalty shrinks 4% a level, so a higher level is always better for the
settler. Traits gain 1 XP every in-game day (3 while in use) and level L to L+1 costs (L+1) x 3 XP.

The file is `colonytraits.json`, not `traits.json`: the game reads every mod's `config/traits.json` as player
character traits, and 0.45.0's file of that name stopped every world from loading (fixed in 0.45.1).

- **General**: Hard Worker / Lazy (work speed), Tough / Frail (max health), Light Eater / Glutton
  (hunger), Fleet-footed / Sluggish (walking), Fast Healer / Slow Healer (time knocked out, healing),
  Careful / Clumsy (tool wear), Iron Stomach / Picky Eater (how much they mind eating the same food),
  Patient / Reckless (Willpower), Heavy Sleeper, Early Riser, Night Owl (sleep hours), Charming, Fertile,
  Loner (families), Cowardly (retreats early, runs from monsters).
- **Job traits** only work in their job and a settler has at most one: Master Mason (builder, some blocks
  cost nothing), Green Thumb, Lumberjack, Steady Hands, Good Cook (cooks a level above their skill),
  Pack Mule (bigger pack), Prospector's Eye, Hard Rock, Sharp-eyed (guard range), Brave (retreats late).
  A job trait also starts its skill two levels higher and makes it grow half again as fast.
- **Earned**: a guard with 25 kills becomes a Veteran for life (+1 damage, swings faster). Anyone knocked
  out three times in ten days is Shaken for ten quiet days (slower, cowardly; Brave is suppressed).
- **Children** inherit: a trait both parents have passes on 75% of the time at the average of their
  levels, one parent's 35% of the time at half its level, plus one new random trait. Clashing traits
  (Tough and Frail) go to the shared or higher one. Children can end up with more than 10.
- **Tavern** offers show each recruit's traits; the price adds one gear per trait point and one more per
  10 levels of good traits. The settler window's Traits & Skills tab lists traits (green good, orange bad,
  grey when idle) with a hover for what each does at its level; the Town Hall list shows them on hover.
  The hut's Manage workers list marks a matching job trait with a star and sorts those first.
- Effects stack with skills inside the same 50% to 200% cap. `/colony traits <name> [add|remove|level
  <trait> <n>]` for testing (admin). `Traits: false` turns them off; `TraitsMin/Max`, `TraitLevelMax`,
  `TraitXpPerLevel`, `TraitVeteranKills`, `TraitShakenCount/Days`, the inherit chances and
  `TraitGearsPerPoint` are in the config. Settlers from older saves roll theirs once.

**Population cap** (`ColonyManager.Population.cs`): `PopulationBase` (20) plus `PopulationPerTownHallLevel`
(5) for each level the Town Hall has been built to; a freshly placed Town Hall is level 0, so 20, and 25
at level 1. When full: no hiring at the Tavern, visitors cannot be welcomed, wanderers stop coming and
couples have no children. The Town Hall shows "Settlers 18 / 20" (orange when full) and the Tavern says
how much room is left. `PopulationBase: 0` removes the limit.

**Meal levels** (`ColonyManager.Cooking.cs`, `config/cooking.json`): every pot recipe takes a Cooking
level: porridge 2, scrambled eggs and soup 3, vegetable stew 4, meaty stew and jam 6. Recipes not listed
(other mods') go by their number of ingredient types: 1 to 2 types level 2, 3 types level 4, 4 or more
level 6; `cooking.json` already names the A Culinary Artillery and Expanded Foods meals for when the cook
learns their stations. The cook makes only the meals at or under their *usable* Cooking level (so the
Cook's Hut level caps the menu: level 1 allows up to 4) plus one with Good Cook. Grilling in the fire pit
never needs a level. The Kitchen tab shows each meal's level and greys out what the cook cannot make yet
("Needs Cooking 6 (cook has 4)"). A cook from an older save gets at least Cooking 4, so stew keeps coming.
