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
    [ProtoMember(8)] public float Saturation = 1200f;
    [ProtoMember(9)] public Vec3d? LastSeenPos;
    [ProtoMember(10)] public bool Alive = true;
    /// <summary>Set when Permadeath is off and the settler was knocked out instead of killed.</summary>
    [ProtoMember(11)] public double DownedUntilTotalDays;
}

[ProtoContract]
public class Colony
{
    public const int CurrentSchemaVersion = 1;

    [ProtoMember(1)] public int SchemaVersion = CurrentSchemaVersion;
    [ProtoMember(2)] public string ColonyId = "";
    [ProtoMember(3)] public string Name = "";
    [ProtoMember(4)] public BlockPos TownHallPos = new BlockPos(0);
    [ProtoMember(5)] public int Level = 1;
    [ProtoMember(6)] public int ClaimRadius;
    [ProtoMember(7)] public List<ColonyMember> Members = new();
    [ProtoMember(8)] public List<CitizenRecord> Citizens = new();
    [ProtoMember(9)] public int NextCitizenId = 1;
    [ProtoMember(10)] public double FoundedTotalDays;
    /// <summary>Position of the colony's single supply camp, or null if none is placed.</summary>
    [ProtoMember(11)] public BlockPos? SupplyCampPos;
    /// <summary>Kept on the colony, not the block, so breaking and re-placing the camp cannot reset it.</summary>
    [ProtoMember(12)] public int SupplyDeliveriesTaken;
    [ProtoMember(13)] public int LastSupplyDay = -1;

    public string OwnerUid => Members.Find(m => m.Role == ColonyRole.Owner)?.PlayerUid ?? "";

    public ColonyRole RoleOf(string playerUid)
    {
        return Members.Find(m => m.PlayerUid == playerUid)?.Role ?? ColonyRole.Visitor;
    }

    public bool CanManage(string playerUid) => RoleOf(playerUid) >= ColonyRole.Officer;
}
