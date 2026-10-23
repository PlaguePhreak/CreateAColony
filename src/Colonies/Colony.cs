using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

public enum ColonyRole
{
    Visitor = 0,
    Member = 1,
    Officer = 2,
    Owner = 3
}

[ProtoContract]
public class ColonyMember
{
    [ProtoMember(1)] public string PlayerUid = "";
    [ProtoMember(2)] public string LastKnownName = "";
    [ProtoMember(3)] public ColonyRole Role = ColonyRole.Member;
}

/// <summary>
/// Authoritative server-side record for one settler. The entity is only a view of this:
/// if the entity is lost, the colony respawns it from here.
/// </summary>
[ProtoContract]
public class CitizenRecord
{
    [ProtoMember(1)] public int CitizenId;
    [ProtoMember(2)] public long EntityId;
    [ProtoMember(3)] public string Name = "";
    [ProtoMember(4)] public string Sex = "male";
    [ProtoMember(5)] public string? JobCode;
    [ProtoMember(6)] public BlockPos? HomeBedPos;
    [ProtoMember(7)] public BlockPos? WorkplacePos;
    // IsRequired: protobuf-net skips zero values, which would reload as the non-zero initializer.
    [ProtoMember(8, IsRequired = true)] public float Saturation = -1f; // -1 = not set yet, filled to max on first tick
    [ProtoMember(9)] public Vec3d? LastSeenPos;
    [ProtoMember(10, IsRequired = true)] public bool Alive = true;
    /// <summary>Set when Permadeath is off and the settler was knocked out instead of killed.</summary>
    [ProtoMember(11)] public double DownedUntilTotalDays;
    [ProtoMember(12)] public double LastSeenTotalDays;
    /// <summary>Old single carried stack; moved into Pack when first read.</summary>
    [ProtoMember(13)] public byte[]? CarriedStack;
    /// <summary>The settler's own inventory, serialized stacks, so items survive a save mid-trip.</summary>
    [ProtoMember(14)] public List<byte[]> Pack = new();
    /// <summary>Bags the settler wears; each adds its slots to the inventory.</summary>
    [ProtoMember(15)] public List<byte[]> Bags = new();
    /// <summary>A wanderer asking to join: it has no job until an officer welcomes it, and leaves if nobody does.</summary>
    [ProtoMember(16)] public bool Visitor;
    /// <summary>When a visitor nobody welcomed gives up and leaves.</summary>
    [ProtoMember(17)] public double VisitorUntilTotalDays;
    /// <summary>Food groups (EnumFoodCategory) of the settler's last few meals, oldest first.</summary>
    [ProtoMember(18)] public List<int> RecentFood = new();
    /// <summary>Armor the settler wears (helmet, body, legs), serialized stacks; it takes the blows first.</summary>
    [ProtoMember(19)] public List<byte[]> Armor = new();
    /// <summary>The family name, also the last word of Name; children take one of their parents'.</summary>
    [ProtoMember(20)] public string Surname = "";
    /// <summary>The settler this one shares a home with, 0 if single. Stays set after the partner dies.</summary>
    [ProtoMember(21)] public int PartnerId;
    /// <summary>A child's mother and father (citizen ids); empty for settlers who came from outside.</summary>
    [ProtoMember(22)] public List<int> ParentIds = new();
    /// <summary>When the settler joined the colony or was born; 0 in older saves, which counts as long ago.</summary>
    [ProtoMember(23)] public double JoinedTotalDays;
    /// <summary>A child grows up at this time; 0 for adults.</summary>
    [ProtoMember(24)] public double GrowsUpTotalDays;
    /// <summary>When this couple last had a child, so the next one waits a while.</summary>
    [ProtoMember(25)] public double LastChildTotalDays;

    public bool IsDowned => DownedUntilTotalDays > 0;
    public bool IsChild => GrowsUpTotalDays > 0;
}

/// <summary>
/// "Bring N of this item to that container." Builders will create these in a later milestone; for now
/// officers create them with /colony request. A courier leases a request while carrying it so two
/// couriers never fetch the same thing; a lease that runs out (courier died, got stuck) frees it again.
/// </summary>
[ProtoContract]
public class DeliveryRequest
{
    [ProtoMember(1)] public int RequestId;
    /// <summary>Item or block code, wildcards allowed, e.g. "game:plank-*".</summary>
    [ProtoMember(2)] public string ItemCode = "";
    [ProtoMember(3)] public int Quantity;
    [ProtoMember(4)] public int Delivered;
    [ProtoMember(5)] public BlockPos DestPos = new BlockPos(0);
    [ProtoMember(6)] public string RequestedBy = "";
    [ProtoMember(7)] public int LeasedByCitizenId;
    [ProtoMember(8)] public double LeaseUntilTotalHours;

    public int Remaining => Quantity - Delivered;
}

/// <summary>
/// A hut block and the building grown around it from blueprints. Level 0 means only the hut block
/// stands; a work order (TargetLevel above Level) asks a builder to build the next blueprint level.
/// </summary>
[ProtoContract]
public class Building
{
    [ProtoMember(1)] public int BuildingId;
    /// <summary>Building type, also the blueprint folder name: "builder", "townhall", "warehouse".</summary>
    [ProtoMember(2)] public string Type = "";
    [ProtoMember(3)] public BlockPos HutPos = new BlockPos(0);
    [ProtoMember(4)] public int Level;
    /// <summary>Blueprint rotation in degrees (0, 90, 180, 270), from the direction the player faced when placing the hut.</summary>
    [ProtoMember(5)] public int Rotation;
    /// <summary>0 = no work order.</summary>
    [ProtoMember(6)] public int TargetLevel;
    /// <summary>Index of the next build step to check, so a builder does not rescan finished work.</summary>
    [ProtoMember(7)] public int Cursor;
    [ProtoMember(8)] public int BuilderCitizenId;
    /// <summary>How far, in blocks, the worker of a Forester's or Farmer's Hut goes out; 0 uses the config default.</summary>
    [ProtoMember(9)] public int WorkRadius;
    /// <summary>A guard goes along with this hut's worker.</summary>
    [ProtoMember(10)] public bool Escort;
    /// <summary>A Farmer's Hut's farmer leaves berry bushes and fruit trees alone.</summary>
    [ProtoMember(11)] public bool NoBerries;
    /// <summary>Blocks the blueprint is raised (or lowered, below zero) from the hut, set with the Builder's Wand.</summary>
    [ProtoMember(12)] public int YOffset;
    /// <summary>Blocks the blueprint is shifted east (or west, below zero) from the hut.</summary>
    [ProtoMember(13)] public int XOffset;
    /// <summary>Blocks the blueprint is shifted south (or north, below zero) from the hut.</summary>
    [ProtoMember(14)] public int ZOffset;
    /// <summary>A Potter's Hut: how many of each kind of pottery to keep in stock, by kind (see ColonyManager.PotteryKind).</summary>
    [ProtoMember(15)] public Dictionary<string, int> PotteryKeep = new();
    /// <summary>An officer has set PotteryKeep; until then the config defaults apply.</summary>
    [ProtoMember(16)] public bool PotteryChosen;
    /// <summary>The pit a potter dug for its pit kiln.</summary>
    [ProtoMember(17)] public BlockPos? KilnPos;
    /// <summary>Where a potter shapes clay.</summary>
    [ProtoMember(18)] public BlockPos? FormPos;
    /// <summary>A Tavern: the settlers who can be hired there today.</summary>
    [ProtoMember(19)] public List<RecruitOffer> Recruits = new();
    /// <summary>The in-game day the Tavern's recruits arrived; a new day brings new ones.</summary>
    [ProtoMember(20, IsRequired = true)] public int RecruitDay = -1;
    /// <summary>A Cook's Hut: how many of each cooked food to keep in stock, by the cooked food's code.</summary>
    [ProtoMember(21)] public Dictionary<string, int> KitchenKeep = new();
    /// <summary>An officer has set KitchenKeep; until then the config default applies to every food.</summary>
    [ProtoMember(22)] public bool KitchenChosen;
    /// <summary>The blueprint style the building is built in (see BlueprintStyles); empty in older saves means Testing.</summary>
    [ProtoMember(23)] public string Style = "";
    /// <summary>A name the players gave the building; empty uses the type's name.</summary>
    [ProtoMember(24)] public string CustomName = "";
    /// <summary>How soon couriers come to take things away from this hut, 1 to 10 (10 first); 0 means never.</summary>
    [ProtoMember(25, IsRequired = true)] public int PickupPriority = 5;
    /// <summary>An officer pressed Request pickup now: the next courier free takes everything in the hut's storage.</summary>
    [ProtoMember(26)] public bool PickupNow;
    /// <summary>A Guard Tower: what its guards do (see GuardTasks).</summary>
    [ProtoMember(27)] public int GuardTask;
    /// <summary>A Guard Tower: where its guards stand on the Guard task; null means at the tower.</summary>
    [ProtoMember(28)] public BlockPos? GuardSpot;
    /// <summary>A Guard Tower: guards on the Patrol task walk PatrolPoints instead of from hut to hut.</summary>
    [ProtoMember(29)] public bool PatrolManual;
    [ProtoMember(30)] public List<BlockPos> PatrolPoints = new();
    /// <summary>A Guard Tower: the player its guards follow on the Follow task.</summary>
    [ProtoMember(31)] public string FollowPlayerUid = "";
    [ProtoMember(32)] public bool FollowTight;
    /// <summary>A Guard Tower: badly hurt guards walk back to the tower until they have healed.</summary>
    [ProtoMember(33, IsRequired = true)] public bool Retreat = true;
    /// <summary>A Guard Tower: creatures (entity code paths) its guards leave alone; all others are fought.</summary>
    [ProtoMember(34)] public List<string> HostilesOff = new();
    /// <summary>Items couriers keep at least this many of in the hut's storage.</summary>
    [ProtoMember(35)] public List<MinStockEntry> MinStock = new();
    /// <summary>Counts per in-game day for the Statistics tab, for the last 100 days.</summary>
    [ProtoMember(36)] public List<StatEntry> Stats = new();
    /// <summary>All-time totals for the Statistics tab.</summary>
    [ProtoMember(37)] public Dictionary<string, int> StatTotals = new();
    /// <summary>A Miner's Hut: the ore its miner digs for (a deposit code such as "nativecopper"); empty means the best the prospectors found.</summary>
    [ProtoMember(38)] public string MineOre = "";
    /// <summary>A Miner's Hut: the prospecting reading (OreSite position) the miner is working, and the ore it digs there.</summary>
    [ProtoMember(39)] public BlockPos? MineSite;
    [ProtoMember(40)] public string MineSiteOre = "";
    /// <summary>A Miner's Hut: where the mine's staircase starts at the surface (the spot to stand on).</summary>
    [ProtoMember(41)] public BlockPos? MineEntrance;
    /// <summary>A Miner's Hut: the far end of the tunnel dug so far (the spot to stand on); new tunnels start from here.</summary>
    [ProtoMember(42)] public BlockPos? MineEnd;
    /// <summary>A Miner's Hut: the ore block the miner is tunnelling towards.</summary>
    [ProtoMember(43)] public BlockPos? MineTarget;
    /// <summary>A Quarry: the middle of its pit beside the hut, in the air just above the ground; the top layer of the pit is the ground under it.</summary>
    [ProtoMember(44)] public BlockPos? QuarryCenter;
    /// <summary>A Miner's Hut: the miner goes off to dig ore the colony's prospectors found before working its own shaft.</summary>
    [ProtoMember(45, IsRequired = true)] public bool MineProspected = true;
    /// <summary>A Miner's Hut: the middle of its shaft beside the hut, at the height a settler stands on the ground there.</summary>
    [ProtoMember(46)] public BlockPos? ShaftPos;

    public bool HasWorkOrder => TargetLevel > Level;
}

/// <summary>What a Guard Tower's guards do.</summary>
public static class GuardTasks
{
    /// <summary>Stand at the guard spot (or the tower) and fight what comes near.</summary>
    public const int Guard = 0;
    /// <summary>Walk from patrol point to patrol point, or from hut to hut, fighting on the way.</summary>
    public const int Patrol = 1;
    /// <summary>Follow a player as a bodyguard, even outside the colony.</summary>
    public const int Follow = 2;
}

/// <summary>"Keep at least Count of this item here."</summary>
[ProtoContract]
public class MinStockEntry
{
    /// <summary>One of the item, serialized, so the window can show it and couriers can match it.</summary>
    [ProtoMember(1)] public byte[] Stack = System.Array.Empty<byte>();
    [ProtoMember(2)] public string Code = "";
    [ProtoMember(3)] public int Count;
}

/// <summary>How many times something happened at a building on one in-game day.</summary>
[ProtoContract]
public class StatEntry
{
    [ProtoMember(1)] public string Key = "";
    [ProtoMember(2)] public int Day;
    [ProtoMember(3)] public int Count;
}

/// <summary>One line of the Town Hall's event log: a lang key and its arguments, filled in by the reader's own language.</summary>
[ProtoContract]
public class ColonyEvent
{
    [ProtoMember(1)] public double TotalDays;
    [ProtoMember(2)] public string Key = "";
    /// <summary>An argument starting with "@" is itself a lang key, such as "@building-farmer".</summary>
    [ProtoMember(3)] public List<string> Args = new();
}

/// <summary>Someone at the Tavern who joins the colony for a price in rusty gears.</summary>
[ProtoContract]
public class RecruitOffer
{
    [ProtoMember(1)] public string Name = "";
    [ProtoMember(2)] public string Sex = "male";
    [ProtoMember(3)] public int Cost;
}

/// <summary>
/// A field marked out by a Field marker (a scarecrow): the square of ground around it that farmers
/// till, sow and harvest.
/// </summary>
[ProtoContract]
public class FieldRecord
{
    [ProtoMember(1)] public BlockPos Pos = new BlockPos(0);
    /// <summary>Seed code to sow here, e.g. "game:seeds-carrot"; empty means any seed the farmer has.</summary>
    [ProtoMember(2)] public string Seed = "";
    /// <summary>The farmer looking after this field, 0 if none yet.</summary>
    [ProtoMember(3)] public int FarmerCitizenId;
}

/// <summary>
/// One reading a prospector took with the game's prospecting pick (density mode): the spot, and how much
/// of each ore lies in the ground around it, exactly as the pick would tell a player.
/// </summary>
[ProtoContract]
public class OreSite
{
    /// <summary>The ground surface where the reading was taken.</summary>
    [ProtoMember(1)] public BlockPos Pos = new BlockPos(0);
    [ProtoMember(2)] public double TotalDays;
    [ProtoMember(3)] public List<OreFind> Ores = new();
    /// <summary>Ores a miner has dug out here, or found none of close enough to reach.</summary>
    [ProtoMember(4)] public List<string> MinedOut = new();
}

/// <summary>One ore in a reading: the deposit code, the pick's density factor (0 to 1) and parts per thousand.</summary>
[ProtoContract]
public class OreFind
{
    [ProtoMember(1)] public string Code = "";
    [ProtoMember(2)] public double Factor;
    [ProtoMember(3)] public double Ppt;
}

[ProtoContract]
public class Colony
{
    public const int CurrentSchemaVersion = 1;

    [ProtoMember(1, IsRequired = true)] public int SchemaVersion = CurrentSchemaVersion;
    [ProtoMember(2)] public string ColonyId = "";
    [ProtoMember(3)] public string Name = "";
    [ProtoMember(4)] public BlockPos TownHallPos = new BlockPos(0);
    [ProtoMember(5, IsRequired = true)] public int Level = 1;
    [ProtoMember(6)] public int ClaimRadius;
    [ProtoMember(7)] public List<ColonyMember> Members = new();
    [ProtoMember(8)] public List<CitizenRecord> Citizens = new();
    [ProtoMember(9, IsRequired = true)] public int NextCitizenId = 1;
    [ProtoMember(10)] public double FoundedTotalDays;
    // 11 to 13 held the old supply camp's daily deliveries (up to 0.31). The camp no longer belongs to a
    // colony; the numbers stay taken so old saves still load.

    /// <summary>Position of the colony's warehouse marker; chests around it form the warehouse.</summary>
    [ProtoMember(14)] public BlockPos? WarehousePos;
    /// <summary>Drop-off markers: couriers empty the chests touching them into the warehouse.</summary>
    [ProtoMember(15)] public List<BlockPos> DropOffs = new();
    [ProtoMember(16)] public List<DeliveryRequest> Requests = new();
    [ProtoMember(17, IsRequired = true)] public int NextRequestId = 1;
    [ProtoMember(18)] public List<Building> Buildings = new();
    [ProtoMember(19, IsRequired = true)] public int NextBuildingId = 1;
    /// <summary>Builders may use any wood or stone type in place of the one a blueprint asks for.</summary>
    [ProtoMember(20, IsRequired = true)] public bool AnyWoodOrStone = true;
    /// <summary>Fields marked out with Field markers, for farmers.</summary>
    [ProtoMember(21)] public List<FieldRecord> Fields = new();
    /// <summary>The owner's Permadeath switch; null until changed, so the server config decides.</summary>
    [ProtoMember(22)] public bool? Permadeath;
    /// <summary>When the next wanderer may come asking to join; 0 until first worked out.</summary>
    [ProtoMember(23)] public double NextWandererTotalDays;
    /// <summary>The Town Hall's event log, oldest first, the newest ColonyManager.MaxEvents kept.</summary>
    [ProtoMember(24)] public List<ColonyEvent> Events = new();
    /// <summary>The colony banner set on the Town Hall's Banners tab (one banner, serialized), or null.</summary>
    [ProtoMember(25)] public byte[]? BannerStack;
    /// <summary>Puts the colony banner on each building as soon as a builder finishes it.</summary>
    [ProtoMember(26)] public bool BannerOnNew;
    /// <summary>The last in-game day couples and births were looked at.</summary>
    [ProtoMember(27, IsRequired = true)] public int LastFamilyDay = -1;
    /// <summary>Readings the colony's prospectors took with the game's prospecting pick, oldest first.</summary>
    [ProtoMember(28)] public List<OreSite> OreSites = new();

    public string OwnerUid => Members.Find(m => m.Role == ColonyRole.Owner)?.PlayerUid ?? "";

    public ColonyRole RoleOf(string playerUid)
    {
        return Members.Find(m => m.PlayerUid == playerUid)?.Role ?? ColonyRole.Visitor;
    }

    public bool CanManage(string playerUid) => RoleOf(playerUid) >= ColonyRole.Officer;
}
