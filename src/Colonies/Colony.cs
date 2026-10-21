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
    /// <summary>The stack a courier is carrying, serialized, so items survive a save mid-trip.</summary>
    [ProtoMember(13)] public byte[]? CarriedStack;

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

    public bool HasWorkOrder => TargetLevel > Level;
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

    public string OwnerUid => Members.Find(m => m.Role == ColonyRole.Owner)?.PlayerUid ?? "";

    public ColonyRole RoleOf(string playerUid)
    {
        return Members.Find(m => m.PlayerUid == playerUid)?.Role ?? ColonyRole.Visitor;
    }

    public bool CanManage(string playerUid) => RoleOf(playerUid) >= ColonyRole.Officer;
}
