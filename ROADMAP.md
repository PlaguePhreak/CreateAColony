# Vintage Colonies: roadmap

Vintage Colonies brings the ideas of the Minecraft mod MineColonies to Vintage Story. This page shows what the mod has today (version 0.72.0), what it does not have yet, and the order we plan to build it in. It comes from a side-by-side check against the MineColonies wiki. Plans can change as things get tested in game.

Everything listed under "What we have" and "Partly done" has been tested in game.

## What we have

**Huts and jobs**
- Town Hall: founds the colony, claims land, levels, a MineColonies-style window *(tested)*
- Builder's Hut: work orders, an items-needed list, tools that wear down, VS Roofing roofs *(tested)*
- Residence: beds per level, couples and children *(tested)*
- Warehouse, drop-off points and the Courier *(tested)*
- Tavern: recruits for rusty gears, and wanderers who ask to join *(tested)*
- Supply Camp with a starter kit and the Colony Ledger *(tested)*
- Farmer: fields, weeding, watering, fertilizer, crop rotation, berry bushes and fruit trees *(tested)*
- Forester: fells wild trees and replants them *(tested)*
- Miner: a MineColonies-style shaft, digs the Prospector's ore finds, carries torches, fills old mines back in *(tested)*
- Quarry: a stepped pit for stone *(tested)*
- Cook: grilling and pot meals, harder meals as the Cooking skill grows *(tested)*
- Guard Tower: guard, patrol and follow tasks, night watch, alarms, retreat and armor *(tested)*
- Barracks: a bigger guard post, two guards per level, with a wider watch and patrol (0.54.0, placeholder model and building, not tested yet)
- Our own jobs: the Prospector (finds ore and marks the map) and the Potter (shapes clay and fires pit kilns) *(tested)*
- Smelter's Hut: melts nuggets in a crucible over the fire pit and pours ingots into molds (0.50.0, placeholder model and building, not tested yet)
- Blacksmith's Hut: heats ingots in the forge and makes tool heads and finished tools on the anvil (0.50.0, placeholder model and building, not tested yet)
- Sawmill: a sawyer saws logs into planks and makes other wooden things from the game's recipes (0.52.0, placeholder model and building, not tested yet)
- Explorer's Hut and Gatherer's Hut: an explorer marks ore, clay, traders, animals and danger on a Colony markers map layer, and a gatherer forages berries, mushrooms and loose stones and marks plants; works with More Map Icons (0.77.0, placeholder models and buildings, not tested yet)
- Vintage Colonies' own map icons: 120 waypoint icons covering every More Map Icons sheet entry, used by colony markers (0.79.0, not tested yet)

**Colony systems**
- Colony land that grows with every hut and Town Hall level *(tested)*
- Town Hall window: actions, town map, banners, events, work orders, citizens, statistics, permissions, settings, prospecting and quests *(tested)*
- Settlers who eat real meals, like a varied diet, sleep in beds, shelter from storms and come back after dying (or not, with Permadeath) *(tested)*
- Skills (13) and traits, inherited by children *(tested)*
- RimWorld-style skills 0 to 20 with passions, decay and perks, and a Happiness system *(0.61.0, not tested yet)*
- Families: couples, children who grow up, surnames, a population cap *(tested)*
- Requests: workers ask for missing items, couriers fetch them, guard towers keep a minimum stock *(tested)*
- Hut levels, ordered from each hut's window, with the Medieval Oak packs converted from Aaron's MineColonies blueprints (0.48.0) and Aaron's Dev's Builds *(tested)*
- Placing huts by hand without the wand or a colony, for making blueprints (`/colony handplace`, 0.49.0)
- Builder's Wand with a see-through preview, turning, nudging, level preview and outlines of nearby buildings *(tested)*
- Importing MineColonies blueprints with `/colony importmc` *(tested)*
- Settlers run from attackers to home or a guard *(tested)*
- Roads, ladder climbing, doors and gates, and Willpower for stuck settlers *(tested)*
- Colony banners, furniture in blueprints, a quest book, and settlers who look like players and wear clothes and armor *(tested)*

## Partly done

- **Cook's Hut**: baking comes from the Stone Bake Oven mod (0.55.0); still no place where the Cook serves food, no food-mod cooking stations *(tested)*
- **Town Hall window**: alliances, happiness, reordering work orders, custom ranks, permission logs, auto hiring and housing, and enter and leave messages say "Coming later" *(tested)*
- **Permissions**: no custom ranks, no hostile or neutral list, no log of who did what *(tested)*
- **Requests**: no list players can see and fill, and no crafter huts that make what is asked for *(tested)*
- **Hiring and housing**: no auto-hire switch, and homes can't be picked by hand *(tested)*
- **Guards**: no ranged guards yet; the Barracks came in 0.54.0 *(not tested yet)*
- **Blueprints**: no in-game scan tool for your own buildings, and the wand's Education, Walls and Decoration tabs are still empty *(tested)*
- **Death**: dead settlers are listed, but there are no graves or mourning *(tested)*
- **Quests**: the quest book works, but Tavern jobs and settler requests are not in it yet *(tested)*
- **Stash and Postbox**: drop-off points exist, but there is no postbox to order items to the player *(tested)*

## Not built yet

**Next, in this order**
1. **Smelter** (started in 0.50.0): still to do are alloys such as bronze, and a bloomery for iron
2. **Happiness** came in 0.61.0. Still to add: safety (raids), health, and how crowded the colony is
3. **A request list**: players can see and fill what workers need (the Sawmill came first, in 0.52.0)
4. **Animal herders and a Tanner**: chickens, pigs, sheep and goats in pens for meat, eggs and hides, and a Tanner who makes leather in barrels
5. **Ranged guards and a rally banner** (the Barracks came in 0.54.0), then **Raids**: now and then, enemies turn up and attack the player's colony. The player is warned in chat and on the Town Hall window. There are several raid factions, from bandits (who use the settler models) to the game's own monsters such as drifters and shivers.
6. **Hospital and Graveyard**: a healer with poultices and bandages, and an undertaker who buries the dead
7. **School, Library and University** with a **research tree** that unlocks faster work and new huts
8. **Blacksmith** (started in 0.50.0): still to do are repairs, armor, and making the tools workers ask for

**Later**
- More huts from MineColonies: Stonemason, Baker, Fisher, Beekeeper, Plantation (reeds and cattails), Composter, Crusher and Sifter (quern and panning), Florist, Dyer, Fletcher and Mechanic (windmills and gears)
- A Combat Academy and an archery range to train guards
- Charcoal burner for the Smelter and Blacksmith
- Settler chat bubbles where settlers ask for things or complain
- Walls, wells, roads and other decorations to order from the Builder's Wand
- **Outposts**: a ready-made bundle of buildings that can be placed outside the colony's border. Its buildings level up all together or one at a time. An outpost works as its own small base and tries to look after itself. It keeps a small military force away from home to protect workers out in the field, and gives them a safe place to go when they are attacked, or at night when the outpost is closer than home.
- Working with more popular Vintage Story mods, such as cooking with A Culinary Artillery and Expanded Foods

**Left out on purpose**
- Concrete Mixer, Enchanter, Alchemist, Netherworker and the Mystical Site: Vintage Story has nothing to match them. An herbalist making poultices could take the Alchemist's place.

## Far future: AI settlements

Colonies that run themselves, without a player in charge.

- **The Mayor**: a settler role that does the player's job. The Mayor chooses where new buildings go, places them with the Builder's Wand, hands out jobs, hires wanderers and upgrades huts when the colony is ready.
- **Growing on their own**: AI settlements that start from a Town Hall and grow into villages and towns over time.
- **Living in the world**: settlements that appear in the world, so players can find them, visit them, trade with them or join them.
- **Neighbours**: AI settlements that trade with, help or argue with the player's colony and each other.
- **War and diplomacy**: a combat system so settlements can raise armies, go to war, defend their land and take or lose territory. Settlements can also form alliances, make peace, sign trade deals and send envoys, with the Mayor deciding who is a friend and who is an enemy.
- **War camps**: before an attack, an AI settlement builds a war camp near the enemy colony and gathers its forces there.
