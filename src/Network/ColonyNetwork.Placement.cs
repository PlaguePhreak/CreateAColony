using System.Collections.Generic;
using System.Linq;
using CreateAColony.Blocks;
using CreateAColony.Colonies;
using CreateAColony.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CreateAColony.Network;

/// <summary>
/// The placement preview: when a hut is placed, or a building is right-clicked with the Builder's
/// Wand, the player sees the outline of the building's highest level where it would stand, and a
/// window to turn it, raise or lower it, order it built, or take the hut back.
/// </summary>
public partial class ColonyNetwork
{
    /// <summary>The highlight slot the outline uses, so it never clears another mod's highlight.</summary>
    const int OutlineSlot = 7410;
    const int MaxOutlineBlocks = 30000;
    const int MaxYOffset = 8;

    /// <summary>Who is looking at which building's outline.</summary>
    readonly Dictionary<string, (string ColonyId, int BuildingId)> placementViewers = new();

    public void OpenPlacement(IServerPlayer player, Colony colony, Building building) => SendPlacement(player, colony, building, true);

    void SendPlacement(IServerPlayer player, Colony colony, Building building, bool openWindow)
    {
        int level = Manager.Blueprints.MaxLevel(building.Type);
        var blueprint = level > 0 ? Manager.Blueprints.Get(building.Type, level, building.Rotation) : null;
        var outline = blueprint == null ? (new List<BlockPos>(), new List<int>(), 0, 0) : Manager.Outline(building, blueprint, MaxOutlineBlocks);
        sapi!.World.HighlightBlocks(player, OutlineSlot, outline.Item1, outline.Item2, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);
        placementViewers[player.PlayerUID] = (colony.ColonyId, building.BuildingId);

        var role = colony.RoleOf(player.PlayerUID);
        serverChannel!.SendPacket(new PlacementSnapshot
        {
            ColonyId = colony.ColonyId,
            BuildingId = building.BuildingId,
            Type = building.Type,
            Rotation = building.Rotation,
            YOffset = building.YOffset,
            ShownLevel = blueprint == null ? 0 : level,
            Blocks = outline.Item4,
            Blocked = outline.Item3,
            CanAdjust = role >= ColonyRole.Member && building.Level == 0 && !building.HasWorkOrder,
            CanOrder = role >= ColonyRole.Officer && !building.HasWorkOrder && building.Level < level,
            OpenWindow = openWindow
        }, player);
    }

    void ClearOutline(IServerPlayer player)
    {
        sapi!.World.HighlightBlocks(player, OutlineSlot, new List<BlockPos>(), new List<int>());
        placementViewers.Remove(player.PlayerUID);
    }

    void PlacementAction(IServerPlayer player, Colony colony, ColonyRole role, ColonyActionPacket packet)
    {
        var building = colony.Buildings.Find(b => b.BuildingId == packet.BuildingId);
        if (building == null || role < ColonyRole.Member || packet.Action == ColonyAction.PlacementClose)
        {
            ClearOutline(player);
            return;
        }
        if (packet.Action == ColonyAction.OpenPlacement)
        {
            SendPlacement(player, colony, building, true);
            return;
        }

        bool unbuilt = building.Level == 0 && !building.HasWorkOrder;
        string? error = null;
        switch (packet.Action)
        {
            case ColonyAction.PlacementTurn:
                if (!unbuilt) error = "createacolony:placement-built";
                else
                {
                    building.Rotation = (building.Rotation + (packet.Text == "-1" ? 270 : 90)) % 360;
                    if (sapi!.World.BlockAccessor.GetBlockEntity(building.HutPos) is BlockEntityTownHall townHall) townHall.SetRotation(building.Rotation);
                }
                break;
            case ColonyAction.PlacementHeight:
                if (!unbuilt) error = "createacolony:placement-built";
                else building.YOffset = GameMath.Clamp(building.YOffset + (packet.Text == "-1" ? -1 : 1), -MaxYOffset, MaxYOffset);
                break;
            case ColonyAction.PlacementConfirm:
                error = role < ColonyRole.Officer ? "createacolony:denied-officer" : Manager.StartWorkOrder(colony, building);
                if (error == null)
                {
                    ClearOutline(player);
                    player.SendMessage(0, Lang.GetL(player.LanguageCode, "createacolony:placement-ordered",
                        Lang.GetL(player.LanguageCode, "createacolony:building-" + building.Type)), EnumChatType.Notification);
                    Manager.MarkDirty(colony);
                    RefreshViewers(colony);
                    return;
                }
                break;
            case ColonyAction.PlacementCancel:
                error = TakeBack(player, colony, building, unbuilt);
                if (error == null)
                {
                    ClearOutline(player);
                    RefreshViewers(colony);
                    return;
                }
                break;
        }

        if (error != null) player.SendIngameError("createacolony-placement", Lang.GetL(player.LanguageCode, error));
        else Manager.MarkDirty(colony);
        // Build here and Not here close the window; when they fail it comes back.
        SendPlacement(player, colony, building, packet.Action is ColonyAction.PlacementConfirm or ColonyAction.PlacementCancel);
        RefreshViewers(colony);
    }

    /// <summary>Removes a hut nothing has been built for and gives the block back. Returns an error, or null.</summary>
    string? TakeBack(IServerPlayer player, Colony colony, Building building, bool unbuilt)
    {
        if (!unbuilt) return "createacolony:placement-built";
        if (building.Type is BuildingTypes.TownHall or BuildingTypes.Warehouse) return "createacolony:placement-keep";
        var ba = sapi!.World.BlockAccessor;
        if (ba.GetBlockEntity(building.HutPos) is BlockEntityContainer storage && storage.Inventory.Any(s => !s.Empty)) return "createacolony:placement-storage";

        var block = ba.GetBlock(building.HutPos);
        ba.SetBlock(0, building.HutPos);
        Manager.RemoveBuilding(colony, building.HutPos);
        var stack = new ItemStack(block);
        if (!player.InventoryManager.TryGiveItemstack(stack, true))
        {
            sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
        }
        return null;
    }

    // ---- Client ---------------------------------------------------------------------------

    GuiDialogPlacement? placementDialog;

    void OnPlacementSnapshot(PlacementSnapshot snapshot)
    {
        if (placementDialog != null && placementDialog.IsOpened() && placementDialog.BuildingId == snapshot.BuildingId)
        {
            placementDialog.Update(snapshot);
            return;
        }
        if (!snapshot.OpenWindow) return;
        if (placementDialog != null && placementDialog.IsOpened())
        {
            placementDialog.Replaced = true;
            placementDialog.TryClose();
        }
        placementDialog = new GuiDialogPlacement(capi!, this, snapshot);
        placementDialog.TryOpen();
    }
}
