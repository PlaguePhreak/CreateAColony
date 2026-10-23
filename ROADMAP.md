# Create a Colony: roadmap

Create a Colony brings the ideas of the Minecraft mod MineColonies to Vintage Story. This page shows what the mod has today (version 0.47.0), what it does not have yet, and the order we plan to build it in. It comes from a side-by-side check against the MineColonies wiki. Plans can change as things get tested in game.

Most of what is listed under "What we have" has been built and checked offline, but has not been tried in game yet.

## What we have

**Huts and jobs**
- Town Hall: founds the colony, claims land, levels 1 to 5, a MineColonies-style window
- Builder's Hut: work orders, an items-needed list, tools that wear down, VS Roofing roofs
- Residence: beds per level, couples and children
- Warehouse (levels 1 to 5), drop-off points and the Courier
- Tavern: recruits for rusty gears, and wanderers who ask to join
- Supply Camp with a starter kit and the Colony Ledger
- Farmer: fields, weeding, watering, fertilizer, crop rotation, berry bushes and fruit trees
- Forester: fells wild trees and replants them
- Miner: a MineColonies-style shaft, digs the Prospector's ore finds, carries torches, fills old mines back in
- Quarry: a stepped pit for stone
- Cook: grilling and pot meals, harder meals as the Cooking skill grows
- Guard Tower: guard, patrol and follow tasks, night watch, alarms, retreat and armor
- Our own jobs: the Prospector (finds ore and marks the map) and the Potter (shapes clay and fires pit kilns)

**Colony systems**
- Colony land that grows with every hut and Town Hall level
- Town Hall window: actions, town map, banners, events, work orders, citizens, statistics, permissions, settings, prospecting and quests
- Settlers who eat real meals, like a varied diet, sleep in beds, shelter from storms and come back after dying (or not, with Permadeath)
- Skills (12) and traits, inherited by children
- Families: couples, children who grow up, surnames, a population cap
- Requests: workers ask for missing items, couriers fetch them, guard towers keep a minimum stock
- Hut levels 1 to 5 for every building in the 12th Century Inspired and KCD2 Inspired asset packs, plus Aaron's Dev's Builds
- Builder's Wand with a see-through preview, turning, nudging, level preview and outlines of nearby buildings
- Importing MineColonies blueprints with `/colony importmc`
- Settlers run from attackers to home or a guard
- Roads, ladder climbing, doors and gates, and Willpower for stuck settlers
- Colony banners, furniture in blueprints, a quest book, and settlers who look like players and wear clothes and armor

## Partly done

- **Cook's Hut**: no baking in clay ovens yet, no place where the Cook serves food, no food-mod cooking stations
- **Town Hall window**: alliances, happiness, reordering work orders, custom ranks, permission logs, auto hiring and housing, and enter and leave messages say "Coming later"
- **Permissions**: no custom ranks, no hostile or neutral list, no log of who did what
- **Requests**: no list players can see and fill, and no crafter huts that make what is asked for
- **Hiring and housing**: no auto-hire switch, and homes can't be picked by hand
- **Guards**: no ranged guards and no Barracks
- **Blueprints**: no in-game scan tool for your own buildings, and the wand's Education, Walls and Decoration tabs are still empty
- **Death**: dead settlers are listed, but there are no graves or mourning
- **Quests**: the quest book works, but Tavern jobs and settler requests are not in it yet
- **Stash and Postbox**: drop-off points exist, but there is no postbox to order items to the player

## Not built yet

**Next, in this order**
1. **Test in game** and fix what breaks: settler looks and clothes, mining, quests and the new asset packs
2. **Smelter**: melts copper and tin in the Potter's crucibles over a fire pit and pours ingots, later a bloomery for iron
3. **Happiness**: food variety, a bed, safety, health, deaths and how full the colony is, added into one score; happy settlers work faster
4. **A request list and the first crafter**: players can see and fill what workers need, and a Sawmill carpenter makes planks and boards for the Builder
5. **Animal herders and a Tanner**: chickens, pigs, sheep and goats in pens for meat, eggs and hides, and a Tanner who makes leather in barrels
6. **Ranged guards, a Barracks and a rally banner**, then **Raids**: now and then, enemies turn up and attack the player's colony. The player is warned in chat and on the Town Hall window. There are several raid factions, from bandits (who use the settler models) to the game's own monsters such as drifters and shivers.
7. **Hospital and Graveyard**: a healer with poultices and bandages, and an undertaker who buries the dead
8. **School, Library and University** with a **research tree** that unlocks faster work and new huts
9. **Blacksmith**: makes and repairs tools and armor, with a simpler rule than the game's blow-by-blow anvil work

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
