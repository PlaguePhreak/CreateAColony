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
    /// <summary>A wanderer asking to join, and how many in-game hours it still waits.</summary>
    [ProtoMember(7)] public bool Visitor;
    [ProtoMember(8)] public int VisitorHoursLeft;
    /// <summary>Where the settler was last seen, for the Town map.</summary>
    [ProtoMember(9)] public int X;
    [ProtoMember(10)] public int Z;
}

/// <summary>A building on the Town Hall's Town map: what it is and the ground it stands on (block columns, inclusive).</summary>
[ProtoContract]
public class MapBuildingView
{
    [ProtoMember(1)] public string Type = "";
    [ProtoMember(2)] public string Name = "";
    [ProtoMember(3)] public int Level;
    [ProtoMember(4)] public bool UnderConstruction;
    [ProtoMember(5)] public int X1;
    [ProtoMember(6)] public int Z1;
    [ProtoMember(7)] public int X2;
    [ProtoMember(8)] public int Z2;
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
    /// <summary>The blueprint style (see BlueprintStyles).</summary>
    [ProtoMember(10)] public string Style = "";
    /// <summary>The name players gave it, or its type's name.</summary>
    [ProtoMember(11)] public string Name = "";
}

/// <summary>One line of the Town Hall's event log; the window fills the lang key in.</summary>
[ProtoContract]
public class EventView
{
    [ProtoMember(1)] public double TotalDays;
    [ProtoMember(2)] public string Key = "";
    [ProtoMember(3)] public List<string> Args = new();
}

/// <summary>One job on the Town Hall's Statistics tab: settlers with it, and places in huts (-1 when it needs no hut).</summary>
[ProtoContract]
public class JobView
{
    [ProtoMember(1)] public string Job = "";
    [ProtoMember(2)] public int Workers;
    [ProtoMember(3)] public int Places;
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
    /// <summary>Armor worn, by slot (head, body, legs); an empty array where the slot is empty.</summary>
    [ProtoMember(18)] public List<byte[]> Armor = new();
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
    /// <summary>A Forester's or Farmer's Hut, whose window has a Work area tab.</summary>
    [ProtoMember(14)] public bool IsGatherHut;
    [ProtoMember(15)] public int WorkRadius;
    [ProtoMember(16)] public List<int> RadiusChoices = new();
    [ProtoMember(17)] public bool Escort;
    [ProtoMember(18)] public bool CollectBerries;
    /// <summary>The guard escorting this hut's worker, empty if none.</summary>
    [ProtoMember(19)] public string GuardName = "";
    [ProtoMember(20)] public int GuardCount;
    /// <summary>A Potter's Hut, whose window has a Pottery tab.</summary>
    [ProtoMember(21)] public bool IsPotterHut;
    [ProtoMember(22)] public List<PotteryView> Pottery = new();
    [ProtoMember(23)] public List<int> KeepChoices = new();
    /// <summary>A Tavern, whose window has a Recruits tab.</summary>
    [ProtoMember(24)] public bool IsTavern;
    [ProtoMember(25)] public List<CreateAColony.Colonies.RecruitOffer> Recruits = new();
    /// <summary>Rusty gears the viewer carries plus those in the Tavern's storage.</summary>
    [ProtoMember(26)] public int Gears;
    /// <summary>One of the currency item, serialized, for its icon.</summary>
    [ProtoMember(27)] public byte[]? Currency;
    /// <summary>The viewer is in creative mode, where hiring is free.</summary>
    [ProtoMember(28)] public bool Creative;
    /// <summary>A Cook's Hut, whose window has a Kitchen tab. Its rows reuse PotteryView (Clay unused).</summary>
    [ProtoMember(29)] public bool IsCookHut;
    [ProtoMember(30)] public List<PotteryView> Kitchen = new();

    // The Main tab every hut has.
    /// <summary>The building's name (its own or its type's), without the level.</summary>
    [ProtoMember(31)] public string DisplayName = "";
    /// <summary>The job of the hut's workers, empty for buildings nobody works at.</summary>
    [ProtoMember(32)] public string Job = "";
    [ProtoMember(33)] public List<CitizenView> Workers = new();
    [ProtoMember(34)] public int WorkerSlots;
    /// <summary>Settlers who could be put to work here (Manage workers): everyone alive who is not a visitor and not already here.</summary>
    [ProtoMember(35)] public List<CitizenView> Candidates = new();
    [ProtoMember(36)] public int PickupPriority;
    [ProtoMember(37)] public bool PickupNow;
    /// <summary>Everything in the hut's storage, for the chest list.</summary>
    [ProtoMember(38)] public List<HutItemView> Items = new();

    // A Guard Tower's tabs.
    [ProtoMember(39)] public bool IsGuardTower;
    [ProtoMember(40)] public int GuardTask;
    [ProtoMember(41)] public BlockPos? GuardSpot;
    [ProtoMember(42)] public bool PatrolManual;
    [ProtoMember(43)] public List<BlockPos> PatrolPoints = new();
    /// <summary>The player the guards follow on the Follow task, empty if none.</summary>
    [ProtoMember(44)] public string FollowName = "";
    [ProtoMember(45)] public bool FollowTight;
    [ProtoMember(46)] public bool Retreat;
    /// <summary>Every creature type guards fight (entity code paths), and which of them this tower leaves alone.</summary>
    [ProtoMember(47)] public List<string> Hostiles = new();
    [ProtoMember(48)] public List<string> HostilesOff = new();
    [ProtoMember(49)] public List<CreateAColony.Colonies.MinStockEntry> MinStock = new();
    [ProtoMember(50)] public List<StatView> Stats = new();
    /// <summary>The hut block's position, so the window can show patrol points relative to it.</summary>
    [ProtoMember(51)] public BlockPos HutPos = new BlockPos(0);
}

/// <summary>One line of a hut's chest list: an item, how many there are, and which containers hold it.</summary>
[ProtoContract]
public class HutItemView
{
    [ProtoMember(1)] public byte[] Stack = System.Array.Empty<byte>();
    [ProtoMember(2)] public int Count;
    [ProtoMember(3)] public List<BlockPos> Where = new();
}

/// <summary>One line of the Statistics tab: a key such as "kill:drifter-normal" and its counts for today, yesterday, 7 days, 100 days and all time.</summary>
[ProtoContract]
public class StatView
{
    [ProtoMember(1)] public string Key = "";
    [ProtoMember(2)] public int[] Counts = System.Array.Empty<int>();
}

/// <summary>One line of a Potter's Hut's Pottery tab: a kind of pottery, how many to keep and how many there are.</summary>
[ProtoContract]
public class PotteryView
{
    [ProtoMember(1)] public string Key = "";
    /// <summary>A fired piece, serialized, for its icon and name.</summary>
    [ProtoMember(2)] public byte[] Stack = System.Array.Empty<byte>();
    [ProtoMember(3)] public int Keep;
    [ProtoMember(4)] public int Have;
    [ProtoMember(5)] public int Clay;
    /// <summary>The name to show, where the stack's own name would not say it (a pot meal shows a cooking pot).</summary>
    [ProtoMember(6)] public string? Name;
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
    /// <summary>Killed settlers are gone for good (else only knocked out); the owner switches it.</summary>
    [ProtoMember(16)] public bool Permadeath;
    [ProtoMember(17)] public int FreeBeds;
    /// <summary>About how many days pass between wanderers; 0 when wanderers are off.</summary>
    [ProtoMember(18)] public double WandererDays;
    [ProtoMember(19)] public bool WanderersNeedFreeBed;
    /// <summary>Columns of land the colony's claim covers, its number of squares, and the Town Hall's square size.</summary>
    [ProtoMember(20)] public long ClaimColumns;
    [ProtoMember(21)] public int ClaimSquares;
    [ProtoMember(22)] public int TownHallClaimSize;
    /// <summary>The event log for the Information tab, oldest first.</summary>
    [ProtoMember(23)] public List<EventView> Events = new();
    /// <summary>Every building with a work order, in the order builders take them, for the Work orders page.</summary>
    [ProtoMember(24)] public List<BuildingView> WorkOrders = new();
    [ProtoMember(25)] public List<JobView> Jobs = new();
    /// <summary>All huts' Statistics added together.</summary>
    [ProtoMember(26)] public List<StatView> Stats = new();
    [ProtoMember(27)] public double FoundedTotalDays;
    [ProtoMember(28)] public int BuildingCount;
    /// <summary>For the Town map: every building, and the colony's land as x1, z1, x2, z2 (inclusive) per square.</summary>
    [ProtoMember(29)] public List<MapBuildingView> MapBuildings = new();
    [ProtoMember(30)] public List<int> ClaimRects = new();
    /// <summary>The colony banner for the Banners tab (serialized), or null; whether it goes on new buildings; whether Heraldry: Banners is installed.</summary>
    [ProtoMember(31)] public byte[]? BannerStack;
    [ProtoMember(32)] public bool BannerOnNew;
    [ProtoMember(33)] public bool HeraldryInstalled;
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
    OpenStorage = 10,
    /// <summary>Text is how far, in blocks, the worker of the hut with BuildingId goes out.</summary>
    SetWorkRadius = 11,
    /// <summary>Text "1" sends a guard along with the worker of the hut with BuildingId.</summary>
    SetEscort = 12,
    /// <summary>Text "1" has the farmer of the hut with BuildingId pick berry bushes and fruit trees.</summary>
    SetCollectBerries = 13,
    /// <summary>Shows the outline and placement window of the building with BuildingId (the Builder's Wand).</summary>
    OpenPlacement = 14,
    /// <summary>Text "1" turns the building a quarter right, "-1" left, before it is built.</summary>
    PlacementTurn = 15,
    /// <summary>Text "1" raises the building one block, "-1" lowers it, before it is built.</summary>
    PlacementHeight = 16,
    /// <summary>Keeps the building where it is and orders level 1.</summary>
    PlacementConfirm = 17,
    /// <summary>Takes the hut back: the block goes back into the player's inventory.</summary>
    PlacementCancel = 18,
    /// <summary>Hides the outline.</summary>
    PlacementClose = 19,
    /// <summary>Text "forward", "back", "left" or "right" moves the building one block that way, as the player faces.</summary>
    PlacementNudge = 20,
    /// <summary>Opens the placement window again for the outline still showing (the wand used in the air).</summary>
    ReopenPlacement = 21,
    /// <summary>The wand used on the ground with nothing planned: Text "x,y,z" is the spot; opens the building list.</summary>
    WandStart = 22,
    /// <summary>Text is the building type picked from the list; its outline appears at the spot.</summary>
    WandChoose = 23,
    /// <summary>Back to the building list for the planned spot.</summary>
    WandChange = 24,
    /// <summary>Text "kind|amount": how many of a kind of pottery the Potter's Hut with BuildingId keeps in stock.</summary>
    SetPotteryKeep = 25,
    /// <summary>Lets the visitor with CitizenId join the colony.</summary>
    WelcomeVisitor = 26,
    /// <summary>Sends the visitor with CitizenId away.</summary>
    TurnAwayVisitor = 27,
    /// <summary>Text "1" turns permadeath on for the colony, "0" off. Owner only.</summary>
    SetPermadeath = 28,
    /// <summary>Hires the recruit named Text at the Tavern with BuildingId.</summary>
    HireRecruit = 29,
    /// <summary>Text "food|amount": how much of a cooked food the Cook's Hut with BuildingId keeps in stock.</summary>
    SetKitchenKeep = 30,
    /// <summary>Text is the new name of the building with BuildingId; empty goes back to its type's name.</summary>
    RenameBuilding = 31,
    /// <summary>Puts the settler with CitizenId to work at the hut with BuildingId.</summary>
    AssignWorker = 32,
    /// <summary>Takes the settler with CitizenId off the job at the hut with BuildingId.</summary>
    UnassignWorker = 33,
    /// <summary>Brings the workers of the hut with BuildingId to it.</summary>
    RecallWorkers = 34,
    /// <summary>Text 0 to 10: how soon couriers come for the hut's things; 0 is never.</summary>
    SetPickupPriority = 35,
    /// <summary>The next free courier takes everything in the hut's storage.</summary>
    PickupNow = 36,
    /// <summary>Gives the player a Guard Scepter for the Guard Tower with BuildingId.</summary>
    GetGuardScepter = 37,
    /// <summary>Clears the Guard Tower's patrol points and guard spot.</summary>
    ClearGuardPoints = 38,
    /// <summary>Text "code|1" has the tower's guards fight that creature, "code|0" leave it alone; "*" sets all back on.</summary>
    SetHostile = 39,
    /// <summary>Text is the tower's task (GuardTasks); Follow follows the player who chose it.</summary>
    SetGuardTask = 40,
    /// <summary>Text "1" makes badly hurt guards fall back to the tower.</summary>
    SetRetreat = 41,
    /// <summary>Text "1" makes patrols walk the Guard Scepter's points, "0" hut to hut.</summary>
    SetPatrolManual = 42,
    /// <summary>Text "1" keeps following guards close, "0" a little further off.</summary>
    SetFollowTight = 43,
    /// <summary>Adds the item in the player's hand to the hut's Minimum stock list.</summary>
    AddMinStock = 44,
    /// <summary>Text "code|count": how many of an item the hut keeps; 0 takes it off the list.</summary>
    SetMinStock = 45,
    /// <summary>Brings the settler with CitizenId to the Town Hall at once.</summary>
    RecallCitizen = 46,
    /// <summary>Makes the Heraldry banner in the player's hand the colony banner.</summary>
    SetColonyBanner = 47,
    /// <summary>Copies the colony banner onto the banners of the Town Hall ("townhall"), the Guard Towers ("guardtower") or every building ("all").</summary>
    ReplaceBanners = 48,
    /// <summary>Text "1" puts the colony banner on each building once a builder finishes it.</summary>
    SetBannerOnNew = 49
}

/// <summary>The Builder's Wand building list: each building type with how many of its blocks the player carries.</summary>
[ProtoContract]
public class WandChooserSnapshot
{
    [ProtoMember(1)] public List<string> Types = new();
    [ProtoMember(2)] public List<int> Have = new();
    /// <summary>"type|style" for each building type that has blueprints in that style.</summary>
    [ProtoMember(3)] public List<string> WithBlueprints = new();
    /// <summary>The style of the building being planned, when the list was reopened with Change building; empty otherwise.</summary>
    [ProtoMember(4)] public string Style = "";
}

/// <summary>
/// Where the ghost preview of a building stands. Its blocks travel to the client in the player's preview
/// mini-dimension, like the WorldEdit import preview; this only tells the client which one it is and where
/// to draw it. DimId -1 hides it.
/// </summary>
[ProtoContract]
public class GhostPreviewPacket
{
    [ProtoMember(1)] public int DimId = -1;
    [ProtoMember(2)] public int X;
    [ProtoMember(3)] public int Y;
    [ProtoMember(4)] public int Z;
}

/// <summary>What the placement window shows for one building while its outline is on screen.</summary>
[ProtoContract]
public class PlacementSnapshot
{
    [ProtoMember(1)] public string ColonyId = "";
    [ProtoMember(2)] public int BuildingId;
    [ProtoMember(3)] public string Type = "";
    [ProtoMember(4)] public int Rotation;
    [ProtoMember(5)] public int YOffset;
    /// <summary>The level whose outline is shown, the highest there is; 0 when the type has no blueprints.</summary>
    [ProtoMember(6)] public int ShownLevel;
    [ProtoMember(7)] public int Blocks;
    /// <summary>Outline blocks where something already stands that the builder will clear.</summary>
    [ProtoMember(8)] public int Blocked;
    /// <summary>Not built yet, so it can still be turned, moved up or down, or taken back.</summary>
    [ProtoMember(9)] public bool CanAdjust;
    [ProtoMember(10)] public bool CanOrder;
    [ProtoMember(11)] public bool OpenWindow;
    [ProtoMember(12)] public int XOffset;
    [ProtoMember(13)] public int ZOffset;
    /// <summary>A building planned with the wand and not placed yet (BuildingId is 0).</summary>
    [ProtoMember(14)] public bool Planned;
    /// <summary>How many of the building's blocks the player carries.</summary>
    [ProtoMember(15)] public int Have;
    /// <summary>The blueprint style shown (see BlueprintStyles).</summary>
    [ProtoMember(16)] public string Style = "";
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
