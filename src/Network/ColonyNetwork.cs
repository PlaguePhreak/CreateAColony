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
        citizen.JobCode = job.Length == 0 ? null : job;
        return null;
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
    public void RefreshViewers(Colony colony)
    {
        if (serverChannel == null || !viewersByColony.TryGetValue(colony.ColonyId, out var viewers)) return;
        foreach (string uid in viewers.ToList())
        {
            if (sapi!.World.PlayerByUid(uid) is IServerPlayer p && p.ConnectionState == EnumClientState.Playing)
            {
                serverChannel.SendPacket(BuildSnapshot(colony, uid, openWindow: false), p);
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
