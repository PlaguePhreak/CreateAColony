using System.Collections.Generic;

namespace CreateAColony;

/// <summary>Server config, stored in ModConfig/createacolony.json.</summary>
public class ColonyConfig
{
    public const string FileName = "createacolony.json";
    /// <summary>Bumped when a default changes in a way old config files should pick up; see ColonyManager.UpgradeConfig.</summary>
    public const int CurrentConfigVersion = 2;

    /// <summary>Which defaults this file has been brought up to. Files from before 0.33.0 have none, so read as 0.</summary>
    public int ConfigVersion;

    /// <summary>The mod is designed around permadeath. When false, a killed settler is
    /// knocked out instead and returns to the Town Hall after DownedRecoveryDays.
    /// This is the default for new colonies; each colony's owner can switch it in the Town Hall window.</summary>
    public bool Permadeath = true;
    public double DownedRecoveryDays = 1.0;

    /// <summary>Settlers that arrive when a colony is founded (rolled between min and max).</summary>
    public int StartingSettlersMin = 3;
    public int StartingSettlersMax = 4;

    public int MaxConcurrentPathRequestsPerColony = 4;

    // Needs
    /// <summary>Full stomach. A settler starts here and eats when below EatBelowFraction of it.</summary>
    public float SaturationMax = 1200f;
    /// <summary>Saturation lost per in-game day; 600 means a full settler gets hungry in about a day and a half.</summary>
    public float HungerPerDay = 600f;
    public float EatBelowFraction = 0.4f;
    /// <summary>Health lost per in-game hour while saturation is zero. Settlers have 20 health.</summary>
    public float StarvationDamagePerHour = 0.5f;

    // Sleep
    public float SleepStartHour = 21f;
    public float SleepEndHour = 6f;

    // Storms
    /// <summary>Settlers head home this many in-game hours before a temporal storm starts.</summary>
    public float StormWarningHours = 2f;

    // Respawn
    /// <summary>A settler not seen for this long while the Town Hall area is loaded is treated as lost and respawned.</summary>
    public double RespawnMissingAfterDays = 0.5;

    // Construction
    /// <summary>When false, builders conjure blocks without materials. Handy for creative testing.</summary>
    public bool BuildersNeedMaterials = true;
    /// <summary>Seconds a builder takes per block placed or cleared.</summary>
    public float BuilderSecondsPerBlock = 0.75f;
    /// <summary>
    /// How far a builder can reach to place or clear a block when it cannot walk any closer, such as a roof
    /// block high above the ground or one over a wall.
    /// </summary>
    public float BuilderReach = 12f;
    /// <summary>Builders need a shovel, axe or pickaxe of the right tier to clear blocks, and wear it out.</summary>
    public bool BuildersNeedTools = true;
    /// <summary>
    /// Blocks whose saved details builders copy from a blueprint, such as a VS Roofing roof's frame, shape
    /// and materials. Builders pay for what those details hold. Leave chests out: their contents would come free.
    /// </summary>
    public string[] BlueprintBlockDetails = { "vsroofing:*" };

    /// <summary>
    /// Bindings that VS Roofing takes in the other hand for stick, bamboo and firewood frames. When a roof in a
    /// blueprint was tied with one of these, a builder may use any of them.
    /// </summary>
    public string[] RoofBindings = { "game:flaxfibers", "game:cattailtops", "game:papyrustops", "game:wildvine-*" };

    /// <summary>
    /// Blocks a builder places the way a player does, from the item in hand, so the block sets itself up:
    /// W4RD0's Furniture (wood and cloth colours, shelves of several cells, beds), Heraldry : Banners poles and
    /// Food Shelves (shelves, baskets and cabinets in their wood). Builders pay for the furniture item in the same
    /// wood and colours as the blueprint, and for a banner with the same design when a pole holds one. Nothing
    /// inside furniture comes along. The mods are optional.
    /// </summary>
    public string[] BlueprintPlaceAsItem = { "w4rd0sfurniture:*", "heraldrybanners:*", "foodshelves:*" };

    // Forester
    /// <summary>How far from the Forester's Hut, in blocks, a forester looks for trees.</summary>
    public int ForesterRadius = 16;
    /// <summary>Seconds a forester chops before a tree comes down.</summary>
    public float ForesterSecondsPerTree = 6f;
    /// <summary>Tree seeds or saplings a forester keeps on hand for replanting; the rest go to the warehouse.</summary>
    public int ForesterSeedReserve = 8;

    // Farmer
    /// <summary>How far a field reaches from its Field marker: 4 makes a 9 by 9 field.</summary>
    public int FieldRadius = 4;
    /// <summary>How many fields one farmer looks after.</summary>
    public int FieldsPerFarmer = 4;
    /// <summary>Seconds a farmer takes to till, sow or harvest one block.</summary>
    public float FarmerSecondsPerBlock = 1.5f;
    /// <summary>Seeds a farmer keeps on hand for sowing; the rest go to the warehouse.</summary>
    public int FarmerSeedReserve = 32;
    /// <summary>How far from the Farmer's Hut a farmer picks berries and fruit from bushes and fruit trees.</summary>
    public int FarmerOrchardRadius = 16;
    /// <summary>A farmer with a watering can waters growing crops whose farmland is drier than this (0 dry to 1 soaked).</summary>
    public float FarmerWaterBelow = 0.3f;
    /// <summary>A farmer with fertilizer spreads it before sowing when the crop's nutrient is below this (soil shows 0 to 100).</summary>
    public float FarmerFertilizeBelow = 40f;

    // Prospector
    /// <summary>How far from the Prospector's Hut, in blocks, a prospector takes readings (change it on the Work area tab).</summary>
    public int ProspectorRadius = 64;
    /// <summary>A prospector walks on this many blocks at least from its last reading (or a good one) before the next...</summary>
    public int ProspectorMinStep = 25;
    /// <summary>...and this many at most; each step is a random distance in between.</summary>
    public int ProspectorMaxStep = 50;
    /// <summary>No new reading within this many blocks of one the colony already has.</summary>
    public int ProspectorNearReading = 16;
    /// <summary>How strongly a prospector heads where nearby readings were good: 0 is purely random; at 15 an area that reads "decent" all round is picked about 25 times as often as one that reads "very poor".</summary>
    public double ProspectorPreference = 15;
    /// <summary>What a prospector expects of ground with no reading near it (0 to 1), so new ground is worth a look beside poor readings but not beside good ones.</summary>
    public double ProspectorUnknownQuality = 0.15;
    /// <summary>Seconds a prospector spends tapping the ground for one reading (a player takes three samples).</summary>
    public float ProspectorSecondsPerReading = 9f;
    /// <summary>Wear on the prospecting pick per reading; a player's three samples wear it 3.</summary>
    public int ProspectorPickWear = 3;
    /// <summary>Each reading also shows on the "Colony prospecting" layer of every colony member's world map (kept apart from their own prospecting layer).</summary>
    public bool ProspectorSharesOreMap = true;

    // Miner
    /// <summary>How far from the Miner's Hut, in blocks, the readings a miner may go and dig at can be.</summary>
    public int MinerRadius = 64;
    /// <summary>Readings weaker than this (the pick's density factor, 0 to 1; "poor" starts at 0.13) are not worth a mine.</summary>
    public double MinerMinFactor = 0.05;
    /// <summary>How far around a reading, across the ground, a miner looks for the ore's blocks.</summary>
    public int MinerOreSearchRadius = 12;
    /// <summary>Seconds a miner takes to dig one block of rock; ore takes half as long again.</summary>
    public float MinerSecondsPerBlock = 2.5f;
    /// <summary>How far, in blocks, a miner reaches from where it stands to dig ore around it.</summary>
    public int MinerReach = 3;
    /// <summary>With "Go to prospected ore" on, the miner only leaves its shaft for readings at least this strong ("decent" starts at 0.267), unless an ore is chosen on the Ore tab.</summary>
    public double MinerTripMinFactor = 0.267;
    /// <summary>Levels of the Miner's Hut's shaft per hut level. Each level is one turn of the spiral staircase, 8 blocks deeper, with a tunnel out from each side.</summary>
    public int MinerLevelsPerHutLevel = 2;
    /// <summary>How long, in blocks, each tunnel out from a level of the shaft is.</summary>
    public int MinerTunnelLength = 12;
    /// <summary>A miner puts a torch down where it works when the light there is below this (0 to 24; monsters can spawn at 7 and below). 0 turns torches off.</summary>
    public int MinerTorchLight = 8;
    /// <summary>How many torches a miner takes from the hut's storage, and asks couriers for when there are none.</summary>
    public int MinerTorchesCarried = 8;
    /// <summary>When a mine at prospected ore is finished, the miner fills its staircase back in with dirt this many blocks down from the surface.</summary>
    public int MinerFillDepth = 6;
    /// <summary>
    /// How far, in blocks, a miner's node search with its prospecting pick reaches, as the pick's node search
    /// mode does for a player. 0 goes by the world's own setting for the pick, or 8 when it has none.
    /// </summary>
    public int MinerNodeSearchRadius = 0;
    /// <summary>Seconds a miner takes over a node search.</summary>
    public float MinerNodeSearchSeconds = 2f;
    /// <summary>How much a node search wears the prospecting pick down.</summary>
    public int MinerNodeSearchWear = 1;

    // Quarry
    /// <summary>The pit beside the Quarry goes this many blocks out from its middle at hut level 1 (2 makes a 5 by 5 pit)...</summary>
    public int QuarryRadius = 2;
    /// <summary>...and this many more for each level above that...</summary>
    public int QuarryRadiusPerLevel = 1;
    /// <summary>...up to this many (6 makes a 13 by 13 pit).</summary>
    public int QuarryMaxRadius = 6;
    /// <summary>How many layers deep the pit goes for each hut level. A staircase runs around the pit's edge, so a small pit cannot go very deep.</summary>
    public int QuarryDepthPerLevel = 6;
    /// <summary>Solid blocks up to this high above the pit's top layer are cleared too, so a pit on a slope is open to the sky.</summary>
    public int QuarryClearAbove = 4;
    /// <summary>Seconds a quarrier takes to dig one block.</summary>
    public float QuarrySecondsPerBlock = 2f;

    // Land claim
    /// <summary>
    /// The square of land, in blocks along each side, the Town Hall claims around itself when the colony is founded
    /// and while it is at level 1. 250 claims a 250 by 250 square.
    /// </summary>
    public int TownHallClaimSize = 250;
    /// <summary>How many blocks each side of the Town Hall's square grows for every Town Hall level above 1.</summary>
    public int TownHallClaimPerLevel = 50;
    /// <summary>The Town Hall's square never grows past this, whatever its level. ClaimSizeMax does not apply to it.</summary>
    public int TownHallClaimSizeMax = 500;
    /// <summary>
    /// The square, in blocks along each side, each finished hut adds to the colony's claim around its hut block,
    /// by building type. Types not listed claim ClaimSizeMin, or MilitaryClaimSize when they are on the Builder's
    /// Wand Military tab. Every size is kept between ClaimSizeMin and ClaimSizeMax.
    /// </summary>
    public Dictionary<string, int> HutClaimSizes = new()
    {
        ["builder"] = 40,
        ["warehouse"] = 40,
        ["tavern"] = 50,
        ["farmer"] = 60,
        ["forester"] = 60,
        ["potter"] = 30,
        ["residence"] = 30,
        ["cook"] = 30,
        ["prospector"] = 30,
        ["miner"] = 30,
        ["quarry"] = 40,
        ["smelter"] = 30,
        ["blacksmith"] = 30,
        ["sawmill"] = 30,
    };
    /// <summary>
    /// How many blocks each side of a hut's square grows for every hut level above 1, so a hut near the edge of the
    /// colony pushes the border further out as it is upgraded. Still kept under ClaimSizeMax.
    /// </summary>
    public int HutClaimPerLevel = 10;
    /// <summary>The claim of a military building (Builder's Wand Military tab) that has no entry of its own in HutClaimSizes.</summary>
    public int MilitaryClaimSize = 200;
    public int ClaimSizeMin = 30;
    public int ClaimSizeMax = 200;
    /// <summary>How far below and above each building's hut block its claim reaches.</summary>
    public int ClaimDepthBelow = 32;
    public int ClaimHeightAbove = 64;

    // Work area (Forester's and Farmer's Hut windows)
    /// <summary>The ranges, in blocks, a hut's Work area tab offers for how far its worker goes out.</summary>
    public int[] GatherRadiusChoices = { 16, 32, 64, 128, 200, 300, 500 };
    /// <summary>
    /// A forester, farmer or guard further than this from every player keeps the land around it loaded
    /// while it works, so it does not freeze out there.
    /// </summary>
    public int FarWorkPlayerDistance = 112;

    // Guard
    /// <summary>How far around the settler it protects (or the Town Hall) a guard looks for monsters.</summary>
    public float GuardThreatRadius = 16f;
    /// <summary>Damage a guard does with bare hands; with a weapon it does the weapon's damage.</summary>
    public float GuardFistDamage = 1.5f;
    /// <summary>Seconds between a guard's blows.</summary>
    public float GuardSecondsPerHit = 1.2f;
    /// <summary>The creatures in GuardTargets hunt settlers as they hunt players.</summary>
    public bool HostilesAttackSettlers = true;
    /// <summary>Creatures guards fight, and that hunt settlers, as entity codes; * matches anything.</summary>
    public string[] GuardTargets = { "drifter-*", "locust-*", "bell-*", "bowtorn-*", "shiver-*", "wolf-*", "hyena-*", "bear-*" };

    // Guard Tower
    /// <summary>How far from its guard spot (or the tower) a tower guard on the Guard task goes after monsters.</summary>
    public float GuardTowerRadius = 20f;
    /// <summary>Huts this close to a Guard Tower are on its guards' automatic patrol.</summary>
    public int GuardPatrolRange = 48;
    /// <summary>Seconds a patrolling guard looks around at each patrol point.</summary>
    public float GuardPatrolPauseSeconds = 4f;
    /// <summary>Most patrol points one tower keeps.</summary>
    public int GuardMaxPatrolPoints = 20;
    /// <summary>How close a guard keeps to the player it follows, loose and tight grouping.</summary>
    public float GuardFollowLoose = 7f;
    public float GuardFollowTight = 3f;
    /// <summary>A following guard further than this from its player is brought to them, as tamed animals are.</summary>
    public float GuardFollowTeleport = 48f;
    /// <summary>A guard below this share of its health walks back to its tower (when Retreat is on)...</summary>
    public float GuardRetreatHealth = 0.33f;
    /// <summary>...and goes back to work once healed to this share.</summary>
    public float GuardReturnHealth = 0.75f;
    /// <summary>Guards at a tower stay up at night and sleep by day, from GuardSleepStartHour to GuardSleepEndHour.</summary>
    public bool GuardNightWatch = true;
    public float GuardSleepStartHour = 10f;
    public float GuardSleepEndHour = 16f;
    /// <summary>Tower guards stay out in temporal storms to fight, instead of sheltering with everyone else.</summary>
    public bool GuardsFightInStorms = true;
    /// <summary>When a monster hurts a settler inside the colony, the nearest guard comes running; this far around the claim it still counts.</summary>
    public int GuardAlarmMargin = 16;
    /// <summary>An alarm is forgotten after this many seconds without the monster striking again.</summary>
    public float GuardAlarmSeconds = 30f;
    /// <summary>Guards put on the best armor from the tower storage and the warehouse.</summary>
    public bool GuardsWearArmor = true;

    // Running away
    /// <summary>Settlers who are not guards run home, or to a guard, when a creature hurts them.</summary>
    public bool FleeWhenAttacked = true;
    /// <summary>A hurt settler keeps running (or hiding) this many seconds after the last blow, unless the attacker dies first.</summary>
    public float FleeSeconds = 30f;
    /// <summary>A hurt settler runs to a guard within this many blocks, if that is better than going home; a guard this close also answers the alarm outside the colony.</summary>
    public float FleeGuardRadius = 48f;
    /// <summary>Once safe, a settler stops running when its attacker is further away than this.</summary>
    public float FleeSafeDistance = 24f;

    // Potter
    /// <summary>Seconds a potter takes to shape one layer of clay on the clay forming block.</summary>
    public float PotterSecondsPerLayer = 2.5f;
    /// <summary>Seconds a potter takes to dig the kiln pit, load it, add one layer of fuel or light it.</summary>
    public float PotterSecondsPerKilnStep = 3f;
    /// <summary>Clay a potter keeps on hand; more goes back to the hut storage.</summary>
    public int PotterClayReserve = 24;
    /// <summary>Clay a potter asks couriers to bring from the warehouse at a time.</summary>
    public int PotterClayRequest = 16;
    /// <summary>How far from the Potter's Hut, in blocks, the potter digs its kiln pit and shapes clay.</summary>
    public int PotterSpotRadius = 8;
    /// <summary>The "Keep in stock" amounts the Pottery tab offers.</summary>
    public int[] PotteryKeepChoices = { 0, 1, 2, 4, 8, 16, 32 };
    /// <summary>
    /// What a new Potter's Hut keeps in stock until an officer changes it: a crucible and ingot molds,
    /// what it takes to smelt copper. Keys are item codes, * matches anything.
    /// </summary>
    public System.Collections.Generic.Dictionary<string, int> PotteryDefaultKeep = new()
    {
        ["*crucible*"] = 1,
        ["*ingotmold*"] = 2
    };

    // Cook
    /// <summary>Seconds a cook takes to load, fuel or empty the fire pit.</summary>
    public float CookSecondsPerStep = 2f;
    /// <summary>Raw food a cook keeps on hand, and asks couriers to bring at a time.</summary>
    public int CookRawReserve = 8;
    /// <summary>Raw food a cook puts in the fire pit at once.</summary>
    public int CookBatch = 4;
    /// <summary>Firewood a cook puts in the fire pit at once.</summary>
    public int CookFirewood = 4;
    /// <summary>How far from the Cook's Hut, in blocks, the cook looks for a fire pit.</summary>
    public int CookFirepitRadius = 8;
    /// <summary>Raw food that needs more heat than this to cook is left to players (a fire pit with firewood reaches about 700).</summary>
    public int CookMaxTemperature = 500;
    /// <summary>How many of each cooked food a new Cook's Hut keeps in stock until an officer changes it.</summary>
    public int KitchenDefaultKeep = 8;
    /// <summary>How many servings of each pot meal a new Cook's Hut keeps in stock until an officer changes it.</summary>
    public int KitchenDefaultMealKeep = 6;
    /// <summary>The "Keep in stock" amounts the Kitchen tab offers.</summary>
    public int[] KitchenKeepChoices = { 0, 2, 4, 8, 16, 32, 64 };

    // Crafter huts (Smelter, Blacksmith, Sawmill)
    /// <summary>The "Keep in stock" amounts the Smelting, Smithing and Sawmill tabs offer.</summary>
    public int[] CraftKeepChoices = { 0, 1, 2, 4, 8, 16, 32, 64 };

    // Smelter
    /// <summary>Seconds a smelter takes to set out a mold, load the crucible, light the fire, pour or take ingots out.</summary>
    public float SmelterSecondsPerStep = 3f;
    /// <summary>How far from the Smelter's Hut, in blocks, the smelter looks for a fire pit.</summary>
    public int SmelterRadius = 8;
    /// <summary>How far from the fire pit, in blocks, the smelter sets out ingot molds and pours into them.</summary>
    public int SmelterMoldRadius = 2;
    /// <summary>How many ingot molds the smelter keeps set out beside the fire pit, when it has them.</summary>
    public int SmelterMolds = 4;
    /// <summary>The most ingots the smelter melts in one crucible (the crucible holds about 5 ingots of nuggets).</summary>
    public int SmelterIngotsPerBatch = 4;
    /// <summary>Fuel pieces the smelter puts in the fire pit at once.</summary>
    public int SmelterFuel = 6;
    /// <summary>What the smelter asks couriers for when the warehouse has no fuel hot enough.</summary>
    public string SmelterFuelCode = "game:charcoal";
    /// <summary>Fuel at least this hot stays at the Smelter's and Blacksmith's Huts (charcoal and coal).</summary>
    public int SmelterKeepFuelTemperature = 1000;
    /// <summary>Nuggets and metal bits a smelter keeps on hand; more goes back to the hut storage.</summary>
    public int SmelterOreReserve = 100;
    /// <summary>
    /// How many ingots of each metal a new Smelter's Hut keeps in stock until an officer changes it, by ingot
    /// code (wildcards allowed). Every other metal is 0.
    /// </summary>
    public System.Collections.Generic.Dictionary<string, int> SmelterDefaultKeep = new()
    {
        ["game:ingot-copper"] = 4
    };

    // Blacksmith
    /// <summary>Seconds a blacksmith takes to load or fuel the forge, or take the hot ingots out.</summary>
    public float BlacksmithSecondsPerStep = 2f;
    /// <summary>Seconds between the blacksmith's hammer strikes on the anvil.</summary>
    public float BlacksmithSecondsPerStrike = 1.5f;
    /// <summary>Voxels of the piece each hammer strike stands for; bigger pieces take more strikes.</summary>
    public int BlacksmithVoxelsPerStrike = 8;
    /// <summary>How far from the Blacksmith's Hut, in blocks, the blacksmith looks for a forge and an anvil.</summary>
    public int BlacksmithRadius = 8;
    /// <summary>Charcoal the blacksmith keeps in the forge.</summary>
    public int BlacksmithFuel = 4;
    /// <summary>Pieces that take more ingots than this are left to players.</summary>
    public int BlacksmithMaxIngots = 8;
    /// <summary>What a new Blacksmith's Hut keeps in stock until an officer changes it, by code (wildcards allowed). Everything else is 0.</summary>
    public System.Collections.Generic.Dictionary<string, int> BlacksmithDefaultKeep = new();

    // Sawmill
    /// <summary>Seconds a sawyer takes to make one thing at the Sawmill (one crafting recipe, such as a log into planks).</summary>
    public float SawmillSecondsPerCraft = 5f;
    /// <summary>
    /// What counts as wood for the sawyer, by the first part of a code (plank in plank-oak). The sawyer only
    /// uses crafting recipes whose ingredients are all wood.
    /// </summary>
    public string[] SawmillWoodParts = { "log", "logsection", "debarkedlog", "plank", "planks", "plankslab", "firewood", "stick", "supportbeam" };
    /// <summary>The tools a sawyer's recipe may take, by the first part of the code. Recipes with any other tool are left to players.</summary>
    public string[] SawmillToolParts = { "saw" };
    /// <summary>Recipes that take more pieces of wood than this are left to players.</summary>
    public int SawmillMaxInputs = 16;
    /// <summary>The most times over the sawyer fetches or asks couriers for the wood of one recipe at once.</summary>
    public int SawmillBatch = 4;
    /// <summary>What a new Sawmill keeps in stock until an officer changes it, by code (wildcards allowed). Everything else is 0.</summary>
    public System.Collections.Generic.Dictionary<string, int> SawmillDefaultKeep = new()
    {
        ["game:plank-*"] = 16
    };

    // Recruiting
    /// <summary>How many settlers a built Tavern offers for hire each day.</summary>
    public int TavernRecruits = 3;
    /// <summary>What hiring a settler at the Tavern costs, rolled between min and max for each one.</summary>
    public int RecruitCostMin = 4;
    public int RecruitCostMax = 12;
    /// <summary>The item recruits are paid in.</summary>
    public string RecruitCurrency = "game:gear-rusty";
    /// <summary>Wanderers now and then walk up to the Town Hall and ask to join the colony.</summary>
    public bool Wanderers = true;
    /// <summary>About how many in-game days pass between wanderers, without and with a built Tavern.</summary>
    public double WandererEveryDays = 4;
    public double WandererEveryDaysWithTavern = 2;
    /// <summary>How long a wanderer waits to be welcomed before it moves on, in in-game days.</summary>
    public double WandererWaitDays = 2;
    /// <summary>Wanderers only come while the colony has a bed nobody sleeps in.</summary>
    public bool WanderersNeedFreeBed = true;

    // Families
    /// <summary>Settlers pair up, share a home and have children who grow up into new settlers.</summary>
    public bool Families = true;
    /// <summary>How many in-game days a settler lives in the colony before it may pair up with another.</summary>
    public double FamilyCoupleAfterDays = 2;
    /// <summary>At most this many new couples a colony gets each in-game day.</summary>
    public int FamilyCouplesPerDay = 1;
    /// <summary>Two settlers of the same sex may also pair up. Only a man and a woman have children.</summary>
    public bool FamilyCouplesAnySex = false;
    /// <summary>The chance each in-game day that a couple who can have a child has one.</summary>
    public double FamilyChildChancePerDay = 0.35;
    /// <summary>In-game days a couple waits after a child before the next one.</summary>
    public double FamilyChildCooldownDays = 3;
    /// <summary>The most children one couple has.</summary>
    public int FamilyMaxChildren = 4;
    /// <summary>In-game days a child takes to grow up into an adult settler who can work.</summary>
    public double FamilyChildGrowDays = 6;
    /// <summary>Children are only born while the colony has a bed nobody sleeps in, so housing limits growth.</summary>
    public bool FamilyChildNeedsFreeBed = true;
    /// <summary>No children are born once the colony has this many living settlers (children count); 0 means no limit.</summary>
    public int FamilyMaxColonySize = 0;

    // Paths (Pathblocks)
    /// <summary>
    /// Blocks settlers treat as roads, as block codes; * matches anything. Settlers walk faster on them and
    /// the pathfinder prefers routes along them. Stairs listed here count as roads up and down; ladders listed
    /// here are the ones settlers climb when there is no other way up (see LadderClimbing).
    /// Use /colony findblock to look up codes, e.g. /colony findblock *path*.
    /// </summary>
    public string[] PathBlocks = { "game:stonepath*", "game:ladder-*" };
    /// <summary>How much faster a settler walks while standing on a Pathblock: 1.4 is 40% faster.</summary>
    public float PathSpeedMultiplier = 1.4f;
    /// <summary>
    /// What one step along a Pathblock costs the pathfinder, compared to a step on anything else. 0.65 means a
    /// settler will take a road up to about one and a half times as long as the direct way. 1 turns the
    /// preference off; keep it above 0.3 or the pathfinder slows down a lot.
    /// </summary>
    public float PathStepCost = 0.65f;
    /// <summary>
    /// When the pathfinder finds no way up or down to a goal (the game's pathfinder only walks, steps up one
    /// block and drops down), settlers look for a ladder nearby and climb it.
    /// </summary>
    public bool LadderClimbing = true;
    /// <summary>How far around itself, in blocks, a settler looks for a ladder to climb.</summary>
    public int LadderSearchRadius = 8;
    /// <summary>
    /// The deepest drop, in blocks, a settler's path may walk down. Anything deeper (a hole, a cliff edge, a pit
    /// kiln shaft) is planned around, or climbed by ladder. The game's own limit for creatures is 4.
    /// </summary>
    public int MaxDropHeight = 2;

    // Getting stuck: Willpower
    /// <summary>
    /// A settler that cannot get where it is going slowly builds Willpower and, once it has enough, moves
    /// itself to a safe spot nearby. Turn off to leave stuck settlers for the player to help.
    /// </summary>
    public bool Willpower = true;
    /// <summary>A walking settler that has got no closer to its goal (and moved less than 3 blocks) for this many seconds counts as stuck.</summary>
    public float StuckNoProgressSeconds = 10f;
    /// <summary>Seconds of being stuck it takes to build full Willpower and relocate.</summary>
    public float WillpowerSeconds = 20f;
    /// <summary>Each settler's threshold is rolled up to this fraction above or below WillpowerSeconds each time, so not everyone pops at once.</summary>
    public float WillpowerJitter = 0.2f;
    /// <summary>Willpower builds this many times faster while the settler is boxed in (walls all around, a hole with no way out).</summary>
    public float WillpowerTrappedRate = 2f;
    /// <summary>Willpower lost per second while the settler makes real progress again.</summary>
    public float WillpowerDecayPerSecond = 1f;
    /// <summary>Willpower lost per second while the settler is not trying to walk anywhere.</summary>
    public float WillpowerIdleDecayPerSecond = 0.1f;
    /// <summary>How far, in blocks, a relocation may move a settler.</summary>
    public int WillpowerRadius = 10;
    /// <summary>
    /// Chance that a relocating settler does not look before it leaps: it only makes sure it is not inside a
    /// block and has floor under it, and may land next to lava, in deep water or among monsters.
    /// Guards never check for monsters.
    /// </summary>
    public float WillpowerRecklessChance = 0.05f;
    /// <summary>At most this many settlers relocate each second across the server, so a pile-up does not cause lag.</summary>
    public int WillpowerMaxRelocationsPerSecond = 2;
    /// <summary>Show Willpower in the game: sparks over stuck settlers (blue to red as it fills) and lines in the server log. /colony pathdebug switches it until restart.</summary>
    public bool PathDebug = false;
    /// <summary>
    /// For making blueprints: the Town Hall, huts and the Warehouse go down by hand like any block, anywhere,
    /// without the Builder's Wand or a colony. A block placed this way is only a block: it founds no colony and
    /// adds no building. /colony handplace switches it and saves it here.
    /// </summary>
    public bool PlaceHutsByHand = false;
    /// <summary>
    /// How the Builder's Wand shows a building before it is built: "ghost" draws the blueprint's real blocks
    /// see-through where the building would stand, as the WorldEdit import tool does, with blocks in the way
    /// marked red; "outline" is the old blue and red outline of the walls; "both" shows the two together. If
    /// the ghost cannot be made, the outline is shown instead.
    /// </summary>
    public string PlacementPreview = "ghost";
    /// <summary>
    /// While a building is being placed, the colonies' buildings within this many blocks of it show box
    /// outlines (purple, orange where the new one would overlap), so buildings can go right next to each
    /// other. 0 turns this off.
    /// </summary>
    public int NearbyOutlineRange = 32;
    /// <summary>At most this many nearby buildings get an outline, the nearest first.</summary>
    public int NearbyOutlineMax = 30;

    // Skills (config/skills.json lists them; see ColonyManager.Skills.cs)
    /// <summary>Settlers learn skills by doing their job and get faster at it. Off: no XP, no effects, no Skills panel.</summary>
    public bool Skills = true;
    /// <summary>The highest level a skill can reach.</summary>
    public int SkillLevelMax = 10;
    /// <summary>Level n costs this many XP times n on top of the level below: 75, 150, 225... (4,125 in all for level 10).</summary>
    public int SkillXpPerLevel = 75;
    /// <summary>Speeds up or slows down all skill XP, like Project Zomboid's sandbox XP multiplier.</summary>
    public float SkillXpMultiplier = 1f;
    /// <summary>
    /// How much of a skill a settler can use at a building of level 1, 2, 3, 4 and up (the last entry covers
    /// higher levels). A job skill is limited by the hut they work at, Strength and Fitness by the Residence
    /// they sleep in; a settler with no such building uses the first entry. XP keeps growing past the cap.
    /// </summary>
    public int[] SkillCapByBuildingLevel = { 4, 6, 8, 10 };
    /// <summary>No skill effect multiplies a number by more than this (or less than one over it).</summary>
    public float SkillEffectMax = 2f;

    // Traits (config/colonytraits.json lists them; see ColonyManager.Traits.cs)
    /// <summary>Settlers are born with traits: strengths and flaws that change how they work, eat, fight and live.</summary>
    public bool Traits = true;
    /// <summary>How many traits a settler from outside rolls (children inherit theirs and can have more than the max).</summary>
    public int TraitsMin = 2;
    public int TraitsMax = 10;
    /// <summary>Every settler has at least this many good and this many bad traits.</summary>
    public int TraitsMinGood = 1;
    public int TraitsMinBad = 1;
    /// <summary>Safety cap on a child's inherited traits, so the window stays readable (0 = none).</summary>
    public int TraitsChildMax = 20;
    /// <summary>The Tavern asks this many more rusty gears per point of trait score (and one more per 10 levels of good traits).</summary>
    public int TraitGearsPerPoint = 1;
    /// <summary>Chance a trait both parents have passes to a child, and a trait only one parent has.</summary>
    public double TraitInheritShared = 0.75;
    public double TraitInheritSingle = 0.35;
    /// <summary>Fresh random traits every child gets on top of what it inherits.</summary>
    public int TraitNewPerChild = 1;
    /// <summary>The highest trait level. A good trait gets 5% stronger a level; a bad one loses 4% of its penalty a level.</summary>
    public int TraitLevelMax = 20;
    /// <summary>Trait XP cost per level step: level L to L+1 costs (L+1) times this. Every day gives 1 XP, 2 more on a day the trait was used.</summary>
    public int TraitXpPerLevel = 3;
    /// <summary>A guard becomes a Veteran after this many kills.</summary>
    public int TraitVeteranKills = 25;
    /// <summary>Knocked out this many times within TraitShakenDays makes a settler Shaken for TraitShakenDays.</summary>
    public int TraitShakenCount = 3;
    public double TraitShakenDays = 10;

    // Population
    /// <summary>Settlers the colony can hold: PopulationBase plus PopulationPerTownHallLevel for each Town Hall level (a freshly placed Town Hall is level 0).</summary>
    public int PopulationBase = 20;
    public int PopulationPerTownHallLevel = 5;
    // Settler gear and inventory (see ColonyManager.Gear.cs and ColonyManager.Lunch.cs)
    /// <summary>The character class whose starting clothes new settlers wear ("commoner" is the game's default class).</summary>
    public string SettlerClothesClass = "commoner";
    /// <summary>Settlers pick up items lying near them and take them to their hut (or the warehouse).</summary>
    public bool SettlersPickUpItems = true;
    /// <summary>Settlers leave items alone for this long after they land, so they do not grab what a player just dropped.</summary>
    public float PickUpDelaySeconds = 5f;
    /// <summary>An idle settler walks to items on the ground within this many blocks. 0: only what it walks over.</summary>
    public int PickUpRange = 10;
    /// <summary>
    /// A settler with no job hut keeps what it picks up until its pack is full or it has carried it this many
    /// seconds, then takes it all to the warehouse in one trip (if the colony has one).
    /// </summary>
    public float LooseItemsDepositSeconds = 120f;
    /// <summary>Settlers carry food to eat on the go and snack while they work, so their stomach stays nearly full.</summary>
    public bool CarryFood = true;
    /// <summary>How much food a settler packs when it gets some, in saturation: 1200 is one full stomach.</summary>
    public float CarryFoodSaturation = 1200f;
    /// <summary>In the dark settlers hold a lit torch they carry, and fetch one from their hut or the warehouse when they have none.</summary>
    public bool CarryTorches = true;
    /// <summary>Workers within this many blocks of their hut drop off what they do not need on their way past.</summary>
    public int DepositNearHut = 8;

    // Jobless settlers (see ColonyManager.Zones.cs and AiTaskSettlerChores.cs)
    /// <summary>
    /// Settlers without a job look after their home: the building their bed is in. They tend the farmland inside
    /// its footprint (harvest, pull weeds, sow, water, till), using seeds, hoes and watering cans from chests in
    /// the home, else from the warehouse.
    /// </summary>
    public bool JoblessTendHome = true;
    /// <summary>Settlers without a job help out with hauling: deliveries to huts and builders, drop-off chests, clearing huts.</summary>
    public bool JoblessHelpHauling = true;
    /// <summary>Seeds a settler takes along for its home plot at a time.</summary>
    public int JoblessSeedTake = 8;
}
