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
    [ProtoMember(9)] public int Rotation;
}

/// <summary>Everything the settler window shows: who they are, what they do, and what they carry.</summary>
[ProtoContract]
public class CitizenSnapshot
{
    [ProtoMember(1)] public string ColonyId = "";
    [ProtoMember(2)] public int CitizenId;
    [ProtoMember(3)] public string Name = "";
    [ProtoMember(4)] public string Job = "";
    [ProtoMember(5)] public string State = "";
    [ProtoMember(6)] public int FoodPercent;
    [ProtoMember(7)] public bool HasBed;
    /// <summary>What they are doing right now, already in words.</summary>
    [ProtoMember(8)] public string Activity = "";
    [ProtoMember(9)] public List<byte[]> Pack = new();
    [ProtoMember(10)] public int Slots;
    [ProtoMember(11)] public List<byte[]> Bags = new();
    [ProtoMember(12)] public byte[]? Hand;
    [ProtoMember(13)] public bool OpenWindow;
    /// <summary>The settler's entity, so the window can draw its model; 0 when it is not loaded.</summary>
    [ProtoMember(14)] public long EntityId;
    [ProtoMember(15)] public float Health;
    [ProtoMember(16)] public float MaxHealth;
    /// <summary>Walking speed as a fraction of normal (1 = 100%).</summary>
    [ProtoMember(17)] public float WalkSpeed = 1;
}

/// <summary>Client asks to open the window of the hut block at Pos.</summary>
[ProtoContract]
public class HutOpenRequest
{
    [ProtoMember(1)] public BlockPos Pos = new BlockPos(0);
}

/// <summary>One line of the "Items needed" list: what, how many are needed and how many the builder has.</summary>
[ProtoContract]
public class ResourceView
{
    /// <summary>The item, serialized, so the window can draw its icon.</summary>
    [ProtoMember(1)] public byte[] Stack = System.Array.Empty<byte>();
    [ProtoMember(2)] public int Needed;
    [ProtoMember(3)] public int Have;
    [ProtoMember(4)] public bool IsTool;
    /// <summary>"wood" or "rock" when any type of it will do, else empty.</summary>
    [ProtoMember(5)] public string AnyOf = "";
}

/// <summary>Everything a hut window shows.</summary>
[ProtoContract]
public class HutSnapshot
{
    [ProtoMember(1)] public string ColonyId = "";
    [ProtoMember(2)] public BuildingView Building = new();
    [ProtoMember(3)] public int ViewerRole;
    [ProtoMember(4)] public bool IsBuilderHut;
    /// <summary>The builder working from this hut, empty if none.</summary>
    [ProtoMember(5)] public string BuilderName = "";
    /// <summary>All work orders in the colony, oldest first (Builder's Hut only).</summary>
    [ProtoMember(6)] public List<BuildingView> Orders = new();
    /// <summary>What this hut's builder still needs for its current work order.</summary>
    [ProtoMember(7)] public List<ResourceView> Resources = new();
    /// <summary>Building name the resources are for.</summary>
    [ProtoMember(8)] public string ResourcesFor = "";
    /// <summary>Extra lines for the building tab, e.g. the warehouse summary.</summary>
    [ProtoMember(9)] public string Info = "";
    [ProtoMember(10)] public bool OpenWindow;
    [ProtoMember(11)] public bool Periodic;
    [ProtoMember(12)] public bool AnyWoodOrStone;
    /// <summary>The hut has its own storage, so the window offers a Storage button.</summary>
    [ProtoMember(13)] public bool HasStorage;
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
    Rotate = 8,
    /// <summary>Text "1" lets builders use any wood or stone type, "0" holds them to the blueprint's.</summary>
    SetAnyWoodOrStone = 9,
    /// <summary>Opens the storage of the hut with BuildingId.</summary>
    OpenStorage = 10
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
