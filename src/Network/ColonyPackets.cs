using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace CreateAColony.Network;

/// <summary>Client asks to open the Town Hall window for the Town Hall at Pos.</summary>
[ProtoContract]
public class TownHallOpenRequest
{
    [ProtoMember(1)] public BlockPos Pos = new BlockPos(0);
}

[ProtoContract]
public class CitizenView
{
    [ProtoMember(1)] public int CitizenId;
    [ProtoMember(2)] public string Name = "";
    [ProtoMember(3)] public string Job = "";
    [ProtoMember(4)] public string State = "";
    [ProtoMember(5)] public int FoodPercent;
    [ProtoMember(6)] public bool HasBed;
}

[ProtoContract]
public class BuildingView
{
    [ProtoMember(1)] public int BuildingId;
    [ProtoMember(2)] public string Type = "";
    [ProtoMember(3)] public int Level;
    /// <summary>Highest level with a blueprint; 0 means no blueprint is installed for this type.</summary>
    [ProtoMember(4)] public int MaxLevel;
    [ProtoMember(5)] public int TargetLevel;
    [ProtoMember(6)] public int ProgressPercent;
    /// <summary>What the work order waits on, a lang key suffix ("building", "waiting-materials", ...).</summary>
    [ProtoMember(7)] public string Status = "";
    /// <summary>Remaining materials, already formatted: "12 Oak planks".</summary>
    [ProtoMember(8)] public List<string> Materials = new();
    [ProtoMember(9)] public int Rotation;
}

[ProtoContract]
public class MemberView
{
    [ProtoMember(1)] public string PlayerUid = "";
    [ProtoMember(2)] public string Name = "";
    [ProtoMember(3)] public int Role;
}

/// <summary>Everything the Town Hall window shows. Sent when the window opens and after every change.</summary>
[ProtoContract]
public class ColonySnapshot
{
    [ProtoMember(1)] public string ColonyId = "";
    [ProtoMember(2)] public string Name = "";
    [ProtoMember(3)] public int Level;
    [ProtoMember(4)] public List<CitizenView> Citizens = new();
    [ProtoMember(5)] public List<MemberView> Members = new();
    /// <summary>The receiving player's own role, so the window can hide buttons they may not use.</summary>
    [ProtoMember(6)] public int ViewerRole;
    [ProtoMember(7)] public int BedCount;
    [ProtoMember(8)] public int SupplyDeliveriesLeft;
    /// <summary>True when this snapshot should open the window, false when it only refreshes an open one.</summary>
    [ProtoMember(9)] public bool OpenWindow;
    [ProtoMember(10)] public bool HasWarehouse;
    [ProtoMember(11)] public int WarehouseChests;
    [ProtoMember(12)] public int OpenRequests;
    [ProtoMember(13)] public int DropOffs;
    [ProtoMember(14)] public List<BuildingView> Buildings = new();
    /// <summary>A timed refresh for construction progress; the window only redraws it on the Buildings tab.</summary>
    [ProtoMember(15)] public bool Periodic;
}

public enum ColonyAction
{
    Rename = 1,
    SetJob = 2,
    SetRole = 3,
    RemoveMember = 4,
    CloseWindow = 5,
    Build = 6,
    CancelBuild = 7,
    Rotate = 8
}

/// <summary>A change requested from the Town Hall window. The server checks permissions before applying it.</summary>
[ProtoContract]
public class ColonyActionPacket
{
    [ProtoMember(1)] public string ColonyId = "";
    [ProtoMember(2)] public ColonyAction Action;
    [ProtoMember(3)] public int CitizenId;
    [ProtoMember(4)] public string Text = "";
    [ProtoMember(5)] public string PlayerUid = "";
    [ProtoMember(6)] public int Role;
    [ProtoMember(7)] public int BuildingId;
}
