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

    public bool IsDowned => DownedUntilTotalDays > 0;
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

    public bool HasWorkOrder => TargetLevel > Level;
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
    /// <summary>Position of the colony's single supply camp, or null if none is placed.</summary>
    [ProtoMember(11)] public BlockPos? SupplyCampPos;
    /// <summary>Kept on the colony, not the block, so breaking and re-placing the camp cannot reset it.</summary>
    [ProtoMember(12)] public int SupplyDeliveriesTaken;
    [ProtoMember(13, IsRequired = true)] public int LastSupplyDay = -1;

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

    public string OwnerUid => Members.Find(m => m.Role == ColonyRole.Owner)?.PlayerUid ?? "";

    public ColonyRole RoleOf(string playerUid)
    {
        return Members.Find(m => m.PlayerUid == playerUid)?.Role ?? ColonyRole.Visitor;
    }

    public bool CanManage(string playerUid) => RoleOf(playerUid) >= ColonyRole.Officer;
}
