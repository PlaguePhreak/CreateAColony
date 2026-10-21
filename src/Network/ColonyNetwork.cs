using System.Collections.Generic;
using System.Linq;
using CreateAColony.Blocks;
using CreateAColony.Colonies;
using CreateAColony.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CreateAColony.Network;

/// <summary>
/// The one network channel between the Town Hall window and the server. The client only ever asks;
/// the server checks the player's colony role, applies the change, and sends back a fresh snapshot.
/// </summary>
public class ColonyNetwork : ModSystem
{
    const string ChannelName = "createacolony";
    const int MaxNameLength = 40;
    const int MaxMaterialLines = 6;

    ICoreServerAPI? sapi;
    IServerNetworkChannel? serverChannel;
    IClientNetworkChannel? clientChannel;
    GuiDialogTownHall? dialog;

    // Who has which colony's window open, so changes can refresh everyone watching.
    readonly Dictionary<string, HashSet<string>> viewersByColony = new();

    public override void Start(ICoreAPI api)
    {
        // Message types must be registered in the same order on both sides.
        api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<TownHallOpenRequest>()
            .RegisterMessageType<ColonySnapshot>()
            .RegisterMessageType<ColonyActionPacket>();
    }

    // ---- Server ---------------------------------------------------------------------------

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        serverChannel = api.Network.GetChannel(ChannelName)
            .SetMessageHandler<TownHallOpenRequest>(OnOpenRequest)
            .SetMessageHandler<ColonyActionPacket>(OnAction);
        // Construction progress changes without anyone clicking, so open windows refresh on their own.
        api.Event.RegisterGameTickListener(_ =>
        {
            foreach (string colonyId in viewersByColony.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).ToList())
            {
                if (Manager.GetById(colonyId) is { } c && c.Buildings.Any(b => b.HasWorkOrder)) RefreshViewers(c, periodic: true);
            }
        }, 3000);
        api.Event.PlayerDisconnect += player =>
        {
            foreach (var viewers in viewersByColony.Values) viewers.Remove(player.PlayerUID);
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
        var colony = Manager.GetById(packet.ColonyId);
        if (colony == null) return;
        var role = colony.RoleOf(player.PlayerUID);

        if (packet.Action == ColonyAction.CloseWindow)
        {
            if (viewersByColony.TryGetValue(colony.ColonyId, out var v)) v.Remove(player.PlayerUID);
            return;
        }

        string? error = packet.Action switch
        {
            ColonyAction.Rename => Rename(colony, role, packet.Text),
            ColonyAction.SetJob => SetJob(colony, role, packet.CitizenId, packet.Text),
            ColonyAction.SetRole => SetRole(colony, role, packet.PlayerUid, (ColonyRole)packet.Role),
            ColonyAction.RemoveMember => SetRole(colony, role, packet.PlayerUid, ColonyRole.Visitor),
            ColonyAction.Build or ColonyAction.CancelBuild or ColonyAction.Rotate => BuildAction(colony, role, packet),
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
                return null;
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
            DropOffs = colony.DropOffs.Count
        };

        foreach (var c in colony.Citizens)
        {
            string state = !c.Alive ? "dead"
                : c.IsDowned ? "downed"
                : c.Saturation >= 0 && c.Saturation < config.SaturationMax * config.EatBelowFraction ? "hungry"
                : "ok";
            snapshot.Citizens.Add(new CitizenView
            {
                CitizenId = c.CitizenId,
                Name = c.Name,
                Job = c.JobCode ?? "",
                State = state,
                FoodPercent = c.Saturation < 0 ? 100 : (int)(100 * c.Saturation / config.SaturationMax),
                HasBed = c.HomeBedPos != null
            });
        }

        foreach (var b in colony.Buildings)
        {
            var view = new BuildingView
            {
                BuildingId = b.BuildingId,
                Type = b.Type,
                Level = b.Level,
                MaxLevel = Manager.Blueprints.MaxLevel(b.Type),
                TargetLevel = b.HasWorkOrder ? b.TargetLevel : 0,
                Rotation = b.Rotation,
                Status = b.HasWorkOrder ? Manager.BuildStatus.GetValueOrDefault(b.BuildingId, "queued") : ""
            };
            var blueprint = b.HasWorkOrder ? Manager.Blueprints.Get(b.Type, b.TargetLevel, b.Rotation) : null;
            if (blueprint != null)
            {
                var (done, total) = Manager.Progress(colony, b, blueprint);
                view.ProgressPercent = total == 0 ? 100 : 100 * done / total;
                foreach (var (_, (sample, count)) in Manager.MaterialsNeeded(colony, b, blueprint).OrderByDescending(kv => kv.Value.Count).Take(MaxMaterialLines))
                {
                    view.Materials.Add(count + " " + sample.GetName());
                }
            }
            snapshot.Buildings.Add(view);
        }

        foreach (var m in colony.Members.OrderByDescending(m => m.Role))
        {
            snapshot.Members.Add(new MemberView { PlayerUid = m.PlayerUid, Name = m.LastKnownName, Role = (int)m.Role });
        }
        return snapshot;
    }

    // ---- Client ---------------------------------------------------------------------------

    ICoreClientAPI? capi;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        clientChannel = api.Network.GetChannel(ChannelName)
            .SetMessageHandler<ColonySnapshot>(OnSnapshot);
    }

    /// <summary>Called by the Town Hall block when a player right-clicks it.</summary>
    public void RequestOpen(Vintagestory.API.MathTools.BlockPos pos)
    {
        clientChannel?.SendPacket(new TownHallOpenRequest { Pos = pos.Copy() });
    }

    public void SendAction(ColonyActionPacket packet) => clientChannel?.SendPacket(packet);

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
