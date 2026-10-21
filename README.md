# Create a Colony (starter skeleton)

Vintage Story 1.22.x code mod, .NET 10. This is milestone 1 only: found a colony, get a vanilla
land claim, recruit settlers that persist with the save. Not yet compiled against the game DLLs.

## Build
1. Install the .NET 10 SDK and set `VINTAGE_STORY` to your game folder.
2. Recommended: `dotnet new install VintageStory.Mod.Templates`, create a `vsmod` project, then copy
   `src/`, `assets/` and `modinfo.json` into it (gives you CakeBuild packaging and launch profiles).
   Or build this folder directly: `dotnet build CreateAColony.csproj`.

## Try it
- Place a **Town Hall** (creative inventory): founds the colony, claims a 49x49 area, and 3 to 4 settlers arrive.
- `/colony info` inside the claim shows the colony.
- `/colony invite <player>`, then that player types `/colony join`. `/colony leave` leaves the colony you stand in.
- `/colony recruit [name]` (needs controlserver) spawns one more settler, for testing.
- Breaking the Town Hall disbands the colony and removes the claim.
- The mod refuses to start if VS Village is installed.

## Config (`ModConfig/createacolony.json`)
- `Permadeath` (default `true`): settlers who die are gone. Set `false` to have them knocked out and recover after `DownedRecoveryDays`.
- `StartingSettlersMin` / `StartingSettlersMax` (default 3 / 4).

## Layout
- `src/CreateAColonyModSystem.cs` registration, VS Village guard, commands
- `src/ColonyConfig.cs` server config
- `src/Colonies/` Colony data (protobuf) and `ColonyManager` (save/load, claims, members)
- `src/Settlers/` `EntitySettler` and the `colonycitizen` behavior
- `src/Block/` Town Hall block and block entity
- `assets/createacolony/` entity, block and lang JSON

## Known TODOs
- Confirm the two vanilla shape paths marked TODO in the JSON against your 1.22 assets folder.
- Respawn of lost settlers, the Town Hall GUI, and job tasks are milestone 2+.
