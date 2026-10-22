using System.Collections.Generic;
using System.Linq;
using CreateAColony.Blocks;
using CreateAColony.Colonies;
using CreateAColony.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.API.Server;

namespace CreateAColony.Network;

/// <summary>
/// The one network channel between the Town Hall window and the server. The client only ever asks;
/// the server checks the player's colony role, applies the change, and sends back a fresh snapshot.
/// </summary>
public partial class ColonyNetwork : ModSystem
{
    const string ChannelName = "createacolony";
    const int MaxNameLength = 40;

    ICoreServerAPI? sapi;
    IServerNetworkChannel? serverChannel;
    IClientNetworkChannel? clientChannel;
    GuiDialogTownHall? dialog;

    // Who has which colony's window open, so changes can refresh everyone watching.
    readonly Dictionary<string, HashSet<string>> viewersByColony = new();
    // Who has which hut window open: player uid -> (colony, hut position).
    readonly Dictionary<string, (string ColonyId, BlockPos HutPos)> hutViewers = new();
    GuiDialogHut? hutDialog;
    // Who has which settler window open: player uid -> (colony, citizen id).
    readonly Dictionary<string, (string ColonyId, int CitizenId)> citizenViewers = new();
    GuiDialogCitizen? citizenDialog;

    public override void Start(ICoreAPI api)
    {
        // Message types must be registered in the same order on both sides.
        api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<TownHallOpenRequest>()
            .RegisterMessageType<ColonySnapshot>()
            .RegisterMessageType<ColonyActionPacket>()
            .RegisterMessageType<HutOpenRequest>()
            .RegisterMessageType<HutSnapshot>()
            .RegisterMessageType<CitizenSnapshot>()
            .RegisterMessageType<PlacementSnapshot>()
            .RegisterMessageType<WandChooserSnapshot>();
    }

    // ---- Server ---------------------------------------------------------------------------

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        serverChannel = api.Network.GetChannel(ChannelName)
            .SetMessageHandler<TownHallOpenRequest>(OnOpenRequest)
            .SetMessageHandler<ColonyActionPacket>(OnAction)
            .SetMessageHandler<HutOpenRequest>(OnHutOpenRequest);
        // Construction progress changes without anyone clicking, so open windows refresh on their own.
        api.Event.RegisterGameTickListener(_ =>
        {
            var watched = viewersByColony.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).Concat(hutViewers.Values.Select(v => v.ColonyId));
            foreach (string colonyId in watched.Distinct().ToList())
            {
                if (Manager.GetById(colonyId) is { } c && c.Buildings.Any(b => b.HasWorkOrder)) RefreshViewers(c, periodic: true);
            }
            // Settlers change what they do all the time, so their windows always refresh.
            foreach (var (uid, view) in citizenViewers.ToList()) SendCitizenWindow(uid, view.ColonyId, view.CitizenId, openWindow: false);
        }, 3000);
        api.Event.PlayerDisconnect += player =>
        {
            foreach (var viewers in viewersByColony.Values) viewers.Remove(player.PlayerUID);
            hutViewers.Remove(player.PlayerUID);
            placementViewers.Remove(player.PlayerUID);
            planned.Remove(player.PlayerUID);
            wandSpots.Remove(player.PlayerUID);
            citizenViewers.Remove(player.PlayerUID);
        };
    }

    ColonyManager Manager => sapi!.ModLoader.GetModSystem<ColonyManager>();

    void OnOpenRequest(IServerPlayer player, TownHallOpenRequest req)
    {
        if (sapi!.World.BlockAccessor.GetBlockEntity(req.Pos) is not BlockEntityTownHall { ColonyId: not null } be) return;
        var colony = Manager.GetById(be.ColonyId);
        if (colony == null) return;

        // Visitors may look but not change anything; the window hides controls by role.
        if (!viewersByColony.TryGetValue(colony.ColonyId, out var viewers)) viewersByColony[colony.ColonyId] = viewers = new();
        viewers.Add(player.PlayerUID);
        serverChannel!.SendPacket(BuildSnapshot(colony, player.PlayerUID, openWindow: true), player);
    }

    void OnAction(IServerPlayer player, ColonyActionPacket packet)
    {
        // A building planned with the wand has no colony or building yet.
        if (packet.BuildingId == 0 && packet.Action is >= ColonyAction.OpenPlacement and <= ColonyAction.WandChange)
        {
            PlannedAction(player, packet);
            return;
        }
        var colony = Manager.GetById(packet.ColonyId);
        if (colony == null) return;
        var role = colony.RoleOf(player.PlayerUID);

        if (packet.Action == ColonyAction.OpenStorage)
        {
            OpenStorage(player, colony, role, packet.BuildingId);
            return;
        }

        if (packet.Action is >= ColonyAction.OpenPlacement and <= ColonyAction.ReopenPlacement)
        {
            PlacementAction(player, colony, role, packet);
            return;
        }

        if (packet.Action == ColonyAction.CloseWindow)
        {
            if (viewersByColony.TryGetValue(colony.ColonyId, out var v)) v.Remove(player.PlayerUID);
            hutViewers.Remove(player.PlayerUID);
            citizenViewers.Remove(player.PlayerUID);
            return;
        }

        string? error = packet.Action switch
        {
            ColonyAction.Rename => Rename(colony, role, packet.Text),
            ColonyAction.SetJob => SetJob(colony, role, packet.CitizenId, packet.Text),
            ColonyAction.SetRole => SetRole(colony, role, packet.PlayerUid, (ColonyRole)packet.Role),
            ColonyAction.RemoveMember => SetRole(colony, role, packet.PlayerUid, ColonyRole.Visitor),
            ColonyAction.Build or ColonyAction.CancelBuild or ColonyAction.Rotate => BuildAction(colony, role, packet),
            ColonyAction.SetAnyWoodOrStone => SetAnyWoodOrStone(colony, role, packet.Text == "1"),
            ColonyAction.SetWorkRadius or ColonyAction.SetEscort or ColonyAction.SetCollectBerries => WorkAreaAction(colony, role, packet),
            ColonyAction.SetPotteryKeep => PotteryAction(colony, role, packet),
            ColonyAction.SetKitchenKeep => KitchenAction(colony, role, packet),
            ColonyAction.WelcomeVisitor or ColonyAction.TurnAwayVisitor => VisitorAction(colony, role, packet),
            ColonyAction.RecallCitizen => RecallCitizen(colony, role, packet.CitizenId),
            ColonyAction.SetPermadeath => SetPermadeath(colony, role, packet.Text == "1"),
            ColonyAction.HireRecruit => HireAction(player, colony, role, packet),
            >= ColonyAction.RenameBuilding and <= ColonyAction.SetMinStock => HutAction(player, colony, role, packet),
            _ => null
        };

        if (error != null)
        {
            player.SendIngameError("createacolony-denied", Lang.GetL(player.LanguageCode, error));
        }
        else
        {
            Manager.MarkDirty(colony);
        }
        RefreshViewers(colony);
    }

    string? SetAnyWoodOrStone(Colony colony, ColonyRole role, bool on)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        Manager.SetAnyWoodOrStone(colony, on);
        return null;
    }

    string? VisitorAction(Colony colony, ColonyRole role, ColonyActionPacket packet)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        var visitor = colony.Citizens.Find(c => c.CitizenId == packet.CitizenId);
        if (visitor == null || !visitor.Alive || !visitor.Visitor) return "createacolony:bad-citizen";
        if (packet.Action == ColonyAction.WelcomeVisitor) Manager.Welcome(colony, visitor);
        else Manager.TurnAway(colony, visitor);
        return null;
    }

    string? RecallCitizen(Colony colony, ColonyRole role, int citizenId)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        var citizen = colony.Citizens.Find(c => c.CitizenId == citizenId);
        if (citizen == null) return "createacolony:bad-citizen";
        return Manager.RecallCitizen(colony, citizen);
    }

    static string? SetPermadeath(Colony colony, ColonyRole role, bool on)
    {
        if (role < ColonyRole.Owner) return "createacolony:denied-owner";
        colony.Permadeath = on;
        return null;
    }

    /// <summary>Any member may hire at the Tavern; they pay for it.</summary>
    string? HireAction(IServerPlayer player, Colony colony, ColonyRole role, ColonyActionPacket packet)
    {
        if (role < ColonyRole.Member) return "createacolony:denied-member";
        var tavern = colony.Buildings.Find(b => b.BuildingId == packet.BuildingId);
        if (tavern == null || tavern.Type != BuildingTypes.Tavern) return "createacolony:bad-building";
        return Manager.Hire(player, colony, tavern, packet.Text);
    }

    static string? Rename(Colony colony, ColonyRole role, string name)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        name = name.Trim();
        if (name.Length == 0 || name.Length > MaxNameLength) return "createacolony:bad-name";
        colony.Name = name;
        return null;
    }

    static string? SetJob(Colony colony, ColonyRole role, int citizenId, string job)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        if (!Jobs.IsValid(job)) return "createacolony:bad-job";
        var citizen = colony.Citizens.Find(c => c.CitizenId == citizenId);
        if (citizen == null || !citizen.Alive) return "createacolony:bad-citizen";
        if (citizen.Visitor) return "createacolony:visitor-no-job";
        if (citizen.JobCode != (job.Length == 0 ? null : job)) citizen.WorkplacePos = null;
        citizen.JobCode = job.Length == 0 ? null : job;
        return null;
    }

    string? BuildAction(Colony colony, ColonyRole role, ColonyActionPacket packet)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        var building = colony.Buildings.Find(b => b.BuildingId == packet.BuildingId);
        if (building == null) return "createacolony:bad-building";

        switch (packet.Action)
        {
            case ColonyAction.Build:
                return Manager.StartWorkOrder(colony, building);
            case ColonyAction.CancelBuild:
                Manager.CancelWorkOrder(colony, building);
                return null;
            default:
                // Turning is only allowed before anything is built, or the old walls would be torn down.
                if (building.Level > 0 || building.HasWorkOrder) return "createacolony:rotate-built";
                building.Rotation = (building.Rotation + 90) % 360;
                TurnModel(building);
                return null;
        }
    }

    /// <summary>The Work area settings of a Forester's or Farmer's Hut.</summary>
    string? WorkAreaAction(Colony colony, ColonyRole role, ColonyActionPacket packet)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        var building = colony.Buildings.Find(b => b.BuildingId == packet.BuildingId);
        if (building == null || !ColonyManager.IsGatherHut(building.Type)) return "createacolony:bad-building";
        switch (packet.Action)
        {
            case ColonyAction.SetWorkRadius:
                if (!int.TryParse(packet.Text, out int radius) || !Manager.Config.GatherRadiusChoices.Contains(radius)) return "createacolony:bad-radius";
                building.WorkRadius = radius;
                break;
            case ColonyAction.SetEscort:
                building.Escort = packet.Text == "1";
                break;
            default:
                building.NoBerries = packet.Text != "1";
                break;
        }
        return null;
    }

    /// <summary>How many of a kind of pottery a Potter's Hut keeps in stock.</summary>
    string? PotteryAction(Colony colony, ColonyRole role, ColonyActionPacket packet)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        var building = colony.Buildings.Find(b => b.BuildingId == packet.BuildingId);
        if (building == null || building.Type != BuildingTypes.Potter) return "createacolony:bad-building";
        int bar = packet.Text.LastIndexOf('|');
        if (bar <= 0 || !int.TryParse(packet.Text[(bar + 1)..], out int keep) || keep < 0 || keep > 999) return "createacolony:bad-building";
        string key = packet.Text[..bar];
        if (!Manager.PotteryKinds().Any(k => k.Key == key)) return "createacolony:bad-building";
        Manager.SetPotteryKeep(colony, building, key, keep);
        return null;
    }

    /// <summary>How much of a cooked food a Cook's Hut keeps in stock.</summary>
    string? KitchenAction(Colony colony, ColonyRole role, ColonyActionPacket packet)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        var building = colony.Buildings.Find(b => b.BuildingId == packet.BuildingId);
        if (building == null || building.Type != BuildingTypes.Cook) return "createacolony:bad-building";
        int bar = packet.Text.LastIndexOf('|');
        if (bar <= 0 || !int.TryParse(packet.Text[(bar + 1)..], out int keep) || keep < 0 || keep > 999) return "createacolony:bad-building";
        string key = packet.Text[..bar];
        if (!Manager.KitchenKinds().Any(k => k.Key == key) && Manager.MealRecipe(key) == null) return "createacolony:bad-building";
        Manager.SetKitchenKeep(colony, building, key, keep);
        return null;
    }

    /// <summary>Opens a hut's own storage for a member, as sneak + right-click on the hut does.</summary>
    void OpenStorage(IServerPlayer player, Colony colony, ColonyRole role, int buildingId)
    {
        var building = colony.Buildings.Find(b => b.BuildingId == buildingId);
        if (role < ColonyRole.Member || building == null) return;
        Manager.HutStorage(colony, building).FirstOrDefault(); // creates the storage of an older hut
        if (sapi!.World.BlockAccessor.GetBlockEntity(building.HutPos) is BlockEntityColonyHut storage)
        {
            storage.OnPlayerRightClick(player, new BlockSelection { Position = building.HutPos.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 0.5, 0.5) });
        }
    }

    string? SetRole(Colony colony, ColonyRole role, string targetUid, ColonyRole newRole)
    {
        if (role < ColonyRole.Owner) return "createacolony:denied-owner";
        if (targetUid == colony.OwnerUid) return "createacolony:owner-fixed";
        if (newRole == ColonyRole.Owner) return "createacolony:owner-fixed";
        var member = colony.Members.Find(m => m.PlayerUid == targetUid);
        if (member == null) return "createacolony:bad-member";
        Manager.SetMemberRole(colony, targetUid, member.LastKnownName, newRole);
        return null;
    }

    /// <summary>Sends an updated snapshot to everyone who has this colony's window open.</summary>
    public void RefreshViewers(Colony colony, bool periodic = false)
    {
        foreach (var (uid, hut) in hutViewers.Where(kv => kv.Value.ColonyId == colony.ColonyId).ToList())
        {
            var building = Manager.GetBuilding(colony, hut.HutPos);
            if (building != null && sapi!.World.PlayerByUid(uid) is IServerPlayer hp && hp.ConnectionState == EnumClientState.Playing)
            {
                var hutSnapshot = BuildHutSnapshot(colony, building, uid, openWindow: false);
                hutSnapshot.Periodic = periodic;
                serverChannel!.SendPacket(hutSnapshot, hp);
            }
            else
            {
                hutViewers.Remove(uid);
            }
        }

        if (serverChannel == null || !viewersByColony.TryGetValue(colony.ColonyId, out var viewers)) return;
        foreach (string uid in viewers.ToList())
        {
            if (sapi!.World.PlayerByUid(uid) is IServerPlayer p && p.ConnectionState == EnumClientState.Playing)
            {
                var snapshot = BuildSnapshot(colony, uid, openWindow: false);
                snapshot.Periodic = periodic;
                serverChannel.SendPacket(snapshot, p);
            }
            else
            {
                viewers.Remove(uid);
            }
        }
    }

    ColonySnapshot BuildSnapshot(Colony colony, string viewerUid, bool openWindow)
    {
        var config = Manager.Config;
        var snapshot = new ColonySnapshot
        {
            ColonyId = colony.ColonyId,
            Name = colony.Name,
            Level = colony.Level,
            ViewerRole = (int)colony.RoleOf(viewerUid),
            BedCount = colony.Citizens.Count(c => c.Alive && c.HomeBedPos != null),
            SupplyDeliveriesLeft = System.Math.Max(0, Manager.SupplyDeliveriesTotal - colony.SupplyDeliveriesTaken),
            OpenWindow = openWindow,
            HasWarehouse = colony.WarehousePos != null,
            WarehouseChests = Manager.WarehouseContainers(colony).Count(),
            OpenRequests = colony.Requests.Count,
            DropOffs = colony.DropOffs.Count,
            Permadeath = Manager.Permadeath(colony),
            FreeBeds = Manager.FreeBeds(colony),
            WandererDays = config.Wanderers ? Manager.WandererEveryDays(colony) : 0,
            WanderersNeedFreeBed = config.WanderersNeedFreeBed,
            ClaimColumns = Manager.ClaimedColumns(colony),
            ClaimSquares = Manager.ClaimAreas(colony).Count,
            TownHallClaimSize = Manager.GetBuilding(colony, colony.TownHallPos) is { } hall ? Manager.ClaimedSize(colony, hall) : 0,
            FoundedTotalDays = colony.FoundedTotalDays,
            BuildingCount = colony.Buildings.Count,
            Events = colony.Events.Select(e => new EventView { TotalDays = e.TotalDays, Key = e.Key, Args = e.Args.ToList() }).ToList(),
            WorkOrders = colony.Buildings.Where(b => b.HasWorkOrder).OrderBy(b => b.BuildingId).Select(b => BuildingViewFor(colony, b)).ToList(),
            Jobs = Manager.JobCounts(colony).Select(j => new JobView { Job = j.Job, Workers = j.Workers, Places = j.Places }).ToList(),
            Stats = Manager.ColonyStatSummary(colony).Select(r => new StatView { Key = r.Key, Counts = r.Counts }).ToList()
        };

        foreach (var c in colony.Citizens)
        {
            string state = !c.Alive ? "dead"
                : c.IsDowned ? "downed"
                : c.Visitor ? "visitor"
                : c.Saturation >= 0 && c.Saturation < config.SaturationMax * config.EatBelowFraction ? "hungry"
                : "ok";
            snapshot.Citizens.Add(new CitizenView
            {
                CitizenId = c.CitizenId,
                Name = c.Name,
                Job = c.JobCode ?? "",
                State = state,
                FoodPercent = c.Saturation < 0 ? 100 : (int)(100 * c.Saturation / config.SaturationMax),
                HasBed = c.HomeBedPos != null,
                Visitor = c.Visitor,
                VisitorHoursLeft = c.Visitor ? (int)System.Math.Ceiling(System.Math.Max(0, c.VisitorUntilTotalDays - sapi!.World.Calendar.TotalDays) * sapi.World.Calendar.HoursPerDay) : 0
            });
        }

        // The Town Hall's own upgrade sits on the Actions tab; every other building has its own hut window.
        if (Manager.GetBuilding(colony, colony.TownHallPos) is { } townHall) snapshot.Buildings.Add(BuildingViewFor(colony, townHall));

        foreach (var m in colony.Members.OrderByDescending(m => m.Role))
        {
            snapshot.Members.Add(new MemberView { PlayerUid = m.PlayerUid, Name = m.LastKnownName, Role = (int)m.Role });
        }
        return snapshot;
    }

    BuildingView BuildingViewFor(Colony colony, Building b)
    {
        var view = new BuildingView
        {
            BuildingId = b.BuildingId,
            Type = b.Type,
            Level = b.Level,
            MaxLevel = Manager.Blueprints.MaxLevel(b),
            TargetLevel = b.HasWorkOrder ? b.TargetLevel : 0,
            Rotation = b.Rotation,
            Style = BlueprintStyles.Normalize(b.Style),
            Name = ColonyManager.BuildingName(b),
            Status = b.HasWorkOrder ? Manager.BuildStatus.GetValueOrDefault(b.BuildingId, "queued") : ""
        };
        var blueprint = b.HasWorkOrder ? Manager.Blueprints.Get(b, b.TargetLevel) : null;
        if (blueprint != null)
        {
            var (done, total) = Manager.Progress(colony, b, blueprint);
            view.ProgressPercent = total == 0 ? 100 : 100 * done / total;
        }
        return view;
    }

    void OnHutOpenRequest(IServerPlayer player, HutOpenRequest req)
    {
        var colony = Manager.GetAt(req.Pos);
        var building = colony == null ? null : Manager.GetBuilding(colony, req.Pos);
        if (colony == null || building == null) return;

        hutViewers[player.PlayerUID] = (colony.ColonyId, building.HutPos.Copy());
        serverChannel!.SendPacket(BuildHutSnapshot(colony, building, player.PlayerUID, openWindow: true), player);
    }

    /// <summary>Opens (or refreshes) the settler window for a player. Called when a player right-clicks a settler.</summary>
    public void OpenCitizenWindow(IServerPlayer player, Colony colony, CitizenRecord citizen)
    {
        citizenViewers[player.PlayerUID] = (colony.ColonyId, citizen.CitizenId);
        SendCitizenWindow(player.PlayerUID, colony.ColonyId, citizen.CitizenId, openWindow: true);
    }

    void SendCitizenWindow(string uid, string colonyId, int citizenId, bool openWindow)
    {
        var colony = Manager.GetById(colonyId);
        var citizen = colony?.Citizens.Find(c => c.CitizenId == citizenId);
        if (colony == null || citizen == null || sapi!.World.PlayerByUid(uid) is not IServerPlayer player
            || player.ConnectionState != EnumClientState.Playing)
        {
            citizenViewers.Remove(uid);
            return;
        }

        var config = Manager.Config;
        var entity = sapi.World.GetEntityById(citizen.EntityId) as EntityAgent;
        string activity;
        if (!citizen.Alive) activity = Lang.Get("createacolony:activity-dead");
        else if (citizen.IsDowned) activity = Lang.Get("createacolony:activity-downed");
        else
        {
            var task = entity?.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.ActiveTasksBySlot.OfType<Settlers.SettlerTaskBase>().FirstOrDefault();
            activity = task?.Describe(colony, citizen)
                ?? Lang.Get(citizen.Visitor ? "createacolony:activity-visitor-waiting" : "createacolony:activity-idle");
            // A settler building Willpower says so, so players can see it is stuck and help before it relocates itself.
            if (config.Willpower && entity?.GetBehavior<Settlers.EntityBehaviorColonyCitizen>()?.Willpower is { Reason: not Settlers.StuckReason.None } will)
            {
                activity += " " + Lang.Get("createacolony:activity-stuck", (int)(will.Fraction * 100));
            }
        }

        var health = entity?.WatchedAttributes.GetTreeAttribute("health");
        var snapshot = new CitizenSnapshot
        {
            ColonyId = colony.ColonyId,
            CitizenId = citizen.CitizenId,
            Name = citizen.Name,
            Job = citizen.JobCode ?? "",
            State = !citizen.Alive ? "dead" : citizen.IsDowned ? "downed" : citizen.Visitor ? "visitor"
                : citizen.Saturation >= 0 && citizen.Saturation < config.SaturationMax * config.EatBelowFraction ? "hungry" : "ok",
            FoodPercent = citizen.Saturation < 0 ? 100 : (int)(100 * citizen.Saturation / config.SaturationMax),
            HasBed = citizen.HomeBedPos != null,
            Activity = activity,
            Pack = Manager.PackItems(citizen).Select(s => s.ToBytes()).ToList(),
            Slots = Manager.PackSlots(citizen),
            Bags = Manager.PackBags(citizen).Select(s => s.ToBytes()).ToList(),
            Hand = entity?.RightHandItemSlot?.Itemstack?.ToBytes(),
            OpenWindow = openWindow,
            EntityId = entity?.EntityId ?? 0,
            Health = health?.GetFloat("currenthealth") ?? 0,
            MaxHealth = health?.GetFloat("maxhealth") ?? 0,
            WalkSpeed = entity?.Stats.GetBlended("walkspeed") ?? 1,
            Armor = Manager.WornArmor(citizen).Select(s => s?.ToBytes() ?? System.Array.Empty<byte>()).ToList()
        };
        serverChannel!.SendPacket(snapshot, player);
    }

    HutSnapshot BuildHutSnapshot(Colony colony, Building building, string viewerUid, bool openWindow)
    {
        var snapshot = new HutSnapshot
        {
            ColonyId = colony.ColonyId,
            Building = BuildingViewFor(colony, building),
            ViewerRole = (int)colony.RoleOf(viewerUid),
            IsBuilderHut = building.Type == BuildingTypes.Builder,
            AnyWoodOrStone = colony.AnyWoodOrStone,
            HasStorage = ColonyManager.HasOwnStorage(building.Type),
            OpenWindow = openWindow
        };
        AddHutAdmin(snapshot, colony, building);

        if (ColonyManager.IsGatherHut(building.Type))
        {
            snapshot.IsGatherHut = true;
            snapshot.WorkRadius = Manager.GatherRadius(building);
            snapshot.RadiusChoices = Manager.Config.GatherRadiusChoices.Union(new[] { snapshot.WorkRadius }).OrderBy(r => r).ToList();
            snapshot.Escort = building.Escort;
            snapshot.CollectBerries = !building.NoBerries;
            snapshot.GuardName = Manager.GuardOf(colony, building)?.Name ?? "";
            snapshot.GuardCount = Manager.GuardCount(colony);
        }

        if (building.Type == BuildingTypes.Warehouse)
        {
            var containers = Manager.WarehouseContainers(colony).ToList();
            snapshot.Info = Lang.Get("createacolony:warehouse-summary", containers.Count, Manager.CountItems(containers, _ => true),
                containers.Sum(c => c.Inventory.Count(s => s.Empty)), containers.Sum(c => c.Inventory.Count));
        }
        if (building.Type is BuildingTypes.Forester or BuildingTypes.Farmer or BuildingTypes.Potter or BuildingTypes.Cook) snapshot.Info = Manager.WorkerHutInfo(colony, building);
        if (building.Type == BuildingTypes.Tavern)
        {
            snapshot.IsTavern = true;
            snapshot.Info = Manager.TavernInfo(colony, building);
            snapshot.Recruits = Manager.TavernRecruits(colony, building).ToList();
            if (sapi!.World.PlayerByUid(viewerUid) is { } viewer)
            {
                snapshot.Gears = Manager.GearsFor(viewer, colony, building);
                snapshot.Creative = viewer.WorldData.CurrentGameMode == EnumGameMode.Creative;
            }
            var code = new AssetLocation(Manager.Config.RecruitCurrency);
            var currency = sapi.World.GetItem(code) is { } item ? new ItemStack(item) : sapi.World.GetBlock(code) is { } block ? new ItemStack(block) : null;
            snapshot.Currency = currency?.ToBytes();
        }
        if (building.Type == BuildingTypes.Potter)
        {
            snapshot.IsPotterHut = true;
            snapshot.KeepChoices = Manager.Config.PotteryKeepChoices.ToList();
            var potter = Manager.WorkerAt(colony, building, Jobs.Potter);
            foreach (var kind in Manager.PotteryKinds())
            {
                snapshot.Pottery.Add(new PotteryView
                {
                    Key = kind.Key,
                    Stack = kind.Sample.ToBytes(),
                    Keep = Manager.PotteryKeep(building, kind),
                    Have = Manager.PotteryStock(colony, building, kind, potter),
                    Clay = kind.Recipes.Min(ColonyManager.ClayCost)
                });
            }
        }
        if (building.Type == BuildingTypes.Cook)
        {
            snapshot.IsCookHut = true;
            snapshot.KeepChoices = Manager.Config.KitchenKeepChoices.ToList();
            var cook = Manager.WorkerAt(colony, building, Jobs.Cook);
            foreach (var kind in Manager.KitchenKinds())
            {
                snapshot.Kitchen.Add(new PotteryView
                {
                    Key = kind.Key,
                    Stack = kind.Sample.ToBytes(),
                    Keep = Manager.KitchenKeep(building, kind),
                    Have = Manager.KitchenStock(colony, building, kind, cook)
                });
            }
            foreach (var recipe in Manager.MealRecipes())
            {
                if (Manager.MealIcon(recipe) is not { } icon) continue;
                snapshot.Kitchen.Add(new PotteryView
                {
                    Key = ColonyManager.MealKey(recipe),
                    Name = Manager.MealName(recipe),
                    Stack = icon.ToBytes(),
                    Keep = Manager.MealKeep(building, recipe),
                    Have = Manager.MealStock(colony, building, recipe, cook)
                });
            }
        }
        if (!snapshot.IsBuilderHut) return snapshot;

        foreach (var order in colony.Buildings.Where(b => b.HasWorkOrder).OrderBy(b => b.BuildingId))
        {
            snapshot.Orders.Add(BuildingViewFor(colony, order));
        }

        var builder = colony.Citizens.Find(c => c.Alive && c.JobCode == Jobs.Builder && c.WorkplacePos != null && c.WorkplacePos.Equals(building.HutPos));
        if (builder == null) return snapshot;
        snapshot.BuilderName = builder.Name;

        var current = colony.Buildings.Find(b => b.HasWorkOrder && b.BuilderCitizenId == builder.CitizenId)
            ?? colony.Buildings.Where(b => b.HasWorkOrder && b.BuilderCitizenId == 0).OrderBy(b => b.BuildingId).FirstOrDefault();
        var blueprint = current == null ? null : Manager.Blueprints.Get(current, current.TargetLevel);
        if (current == null || blueprint == null) return snapshot;
        snapshot.ResourcesFor = Lang.Get("createacolony:building-" + current.Type) + " " + current.TargetLevel;

        var storage = Manager.HutStorage(colony, building).Cast<BlockEntityContainer>().ToList();
        var hand = (sapi!.World.GetEntityById(builder.EntityId) as EntityAgent)?.RightHandItemSlot?.Itemstack;

        foreach (var need in Manager.ToolsNeeded(colony, current, blueprint).OrderBy(n => n.Type))
        {
            var sample = Manager.ToolSample(need.Type);
            if (sample == null) continue;
            bool has = ColonyManager.ToolFits(hand, need.Type, need.Tier)
                || Manager.CountItems(storage, s => ColonyManager.ToolFits(s, need.Type, need.Tier)) > 0
                || Manager.PackCount(builder, s => ColonyManager.ToolFits(s, need.Type, need.Tier)) > 0;
            snapshot.Resources.Add(new ResourceView { Stack = sample.ToBytes(), Needed = 1, Have = has ? 1 : 0, IsTool = true });
        }
        foreach (var (code, (sample, count)) in Manager.MaterialsNeeded(colony, current, blueprint).OrderByDescending(kv => kv.Value.Count))
        {
            var match = ColonyManager.Matcher(code);
            int have = Manager.CountItems(storage, match) + Manager.PackCount(builder, match);
            string anyOf = code.Contains('|') ? "binding" : !code.Contains('*') ? "" : sample.Collectible.Variant?.ContainsKey("wood") == true ? "wood" : "rock";
            snapshot.Resources.Add(new ResourceView { Stack = sample.ToBytes(), Needed = count, Have = have, AnyOf = anyOf });
        }
        return snapshot;
    }

    // ---- Client ---------------------------------------------------------------------------

    ICoreClientAPI? capi;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        clientChannel = api.Network.GetChannel(ChannelName)
            .SetMessageHandler<ColonySnapshot>(OnSnapshot)
            .SetMessageHandler<HutSnapshot>(OnHutSnapshot)
            .SetMessageHandler<CitizenSnapshot>(OnCitizenSnapshot)
            .SetMessageHandler<PlacementSnapshot>(OnPlacementSnapshot)
            .SetMessageHandler<WandChooserSnapshot>(OnWandChooserSnapshot);
    }

    /// <summary>Called by the Town Hall block when a player right-clicks it.</summary>
    public void RequestOpen(BlockPos pos)
    {
        clientChannel?.SendPacket(new TownHallOpenRequest { Pos = pos.Copy() });
    }

    public void SendAction(ColonyActionPacket packet) => clientChannel?.SendPacket(packet);

    /// <summary>Called by hut blocks (Builder's Hut, Warehouse) when a player right-clicks them.</summary>
    public void RequestOpenHut(BlockPos pos) => clientChannel?.SendPacket(new HutOpenRequest { Pos = pos.Copy() });

    void OnCitizenSnapshot(CitizenSnapshot snapshot)
    {
        if (citizenDialog != null && citizenDialog.IsOpened() && citizenDialog.CitizenId == snapshot.CitizenId && citizenDialog.ColonyId == snapshot.ColonyId)
        {
            citizenDialog.Update(snapshot);
            return;
        }
        if (!snapshot.OpenWindow) return;
        if (citizenDialog != null && citizenDialog.IsOpened())
        {
            citizenDialog.Replaced = true;
            citizenDialog.TryClose();
        }
        citizenDialog = new GuiDialogCitizen(capi!, this, snapshot);
        citizenDialog.TryOpen();
    }

    void OnHutSnapshot(HutSnapshot snapshot)
    {
        if (hutDialog != null && hutDialog.IsOpened() && hutDialog.BuildingId == snapshot.Building.BuildingId)
        {
            hutDialog.Update(snapshot);
            return;
        }
        if (!snapshot.OpenWindow) return;

        if (hutDialog != null && hutDialog.IsOpened())
        {
            // The server already moved us to the new hut; closing the old window must not undo that.
            hutDialog.Replaced = true;
            hutDialog.TryClose();
        }
        hutDialog = new GuiDialogHut(capi!, this, snapshot);
        hutDialog.TryOpen();
    }

    void OnSnapshot(ColonySnapshot snapshot)
    {
        if (dialog != null && dialog.IsOpened() && dialog.ColonyId == snapshot.ColonyId)
        {
            dialog.Update(snapshot);
            return;
        }
        if (!snapshot.OpenWindow) return;

        dialog = new GuiDialogTownHall(capi!, this, snapshot);
        dialog.TryOpen();
    }
}
