# Create a Colony (starter skeleton)

Vintage Story 1.22.x code mod, .NET 10. This is milestone 1 only: found a colony, get a vanilla
land claim, recruit settlers that persist with the save. Compiles cleanly against the 1.22.7 API; not yet tested in-game.

## Build
1. Install the .NET 10 SDK and set `VINTAGE_STORY` to your game folder.
2. Run `dotnet build CreateAColony.csproj -c Release`.
3. The ready-to-install mod is written to `Releases/createacolony_<version>.zip`. Copy that zip into the
   game's `Mods` folder.

## Try it
- Craft a **Town Hall** (5 planks of any wood in a U shape: top row full, middle row left and right) or take it from the creative inventory, then place it: founds the colony, claims a 49x49 area, and 3 to 4 settlers arrive.
- `/colony info` inside the claim shows the colony.
- `/colony invite <player>`, then that player types `/colony join`. `/colony leave` leaves the colony you stand in.
- `/colony recruit [name]` (needs controlserver) spawns one more settler, for testing.
- Craft a **Supply Camp** (3 firewood over 2 sticks) and place it inside your colony. Right-click once per in-game day for starter materials, 5 deliveries per colony. Edit `assets/createacolony/config/supplycamp.json` to change the bundle.
- Settlers get hungry and take food from any chest inside the colony claim. Keep a chest of bread, fruit or
  cooked meals near the Town Hall. With no food they slowly starve.
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
`builder`, `townhall`, `warehouse`, `forester` or `farmer` and `<N>` is 1 to 5. `/colony reloadblueprints` picks up
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
