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
`builder`, `townhall` or `warehouse` and `<N>` is 1 to 5. `/colony reloadblueprints` picks up
changes without a restart. Blueprints in ModConfig override the ones shipped in
`assets/createacolony/config/blueprints/`.
