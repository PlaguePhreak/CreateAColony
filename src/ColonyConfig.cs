namespace CreateAColony;

/// <summary>Server config, stored in ModConfig/createacolony.json.</summary>
public class ColonyConfig
{
    public const string FileName = "createacolony.json";

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
}
