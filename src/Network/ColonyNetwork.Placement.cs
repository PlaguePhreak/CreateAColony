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
    const int MaxNudge = 16;

    /// <summary>Who is looking at which building's outline.</summary>
    readonly Dictionary<string, (string ColonyId, int BuildingId)> placementViewers = new();
    /// <summary>The level a player picked to preview for a placed building; the highest when not set.</summary>
    readonly Dictionary<string, (string ColonyId, int BuildingId, int Level)> placementLevels = new();

    /// <summary>The level to preview: the one picked if the blueprints have it, else the highest.</summary>
    static int PreviewLevel(int picked, int max) => picked >= 1 && picked <= max ? picked : max;

    /// <summary>One level up or down from the shown one, kept between 1 and the highest.</summary>
    static int StepLevel(int shown, int max, string way) => GameMath.Clamp(shown + (way == "-1" ? -1 : 1), 1, System.Math.Max(1, max));

    public void OpenPlacement(IServerPlayer player, Colony colony, Building building) => SendPlacement(player, colony, building, true);

    void SendPlacement(IServerPlayer player, Colony colony, Building building, bool openWindow)
    {
        int max = Manager.Blueprints.MaxLevel(building);
        int picked = placementLevels.TryGetValue(player.PlayerUID, out var pick) && pick.ColonyId == colony.ColonyId && pick.BuildingId == building.BuildingId ? pick.Level : 0;
        int level = PreviewLevel(picked, max);
        var blueprint = level > 0 ? Manager.Blueprints.Get(building, level) : null;
        var outline = PreviewBuilding(player, building, blueprint);
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
            XOffset = building.XOffset,
            ZOffset = building.ZOffset,
            ShownLevel = blueprint == null ? 0 : level,
            MaxLevel = max,
            Blocks = outline.Item4,
            Blocked = outline.Item3,
            CanAdjust = role >= ColonyRole.Member && building.Level == 0 && !building.HasWorkOrder,
            CanOrder = role >= ColonyRole.Officer && !building.HasWorkOrder && building.Level < max,
            OpenWindow = openWindow,
            Style = Manager.Blueprints.StyleFor(building)
        }, player);
    }

    /// <summary>
    /// Shows the building's ghost (or its outline, if the ghost is off or fails) and returns the outline
    /// blocks to highlight: all of them for the outline, only the ones in the way beside the ghost.
    /// </summary>
    (List<BlockPos>, List<int>, int, int) PreviewBuilding(IServerPlayer player, Building building, Blueprint? blueprint)
    {
        if (blueprint == null)
        {
            HideGhost(player);
            return (new List<BlockPos>(), new List<int>(), 0, 0);
        }
        bool ghost = ShowGhost(player, building, blueprint);
        return Manager.Outline(building, blueprint, MaxOutlineBlocks, blockedOnly: ghost && !OutlineToo);
    }

    void ClearOutline(IServerPlayer player)
    {
        placementLevels.Remove(player.PlayerUID);
        HideGhost(player);
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
        if (packet.Action is ColonyAction.OpenPlacement or ColonyAction.ReopenPlacement)
        {
            SendPlacement(player, colony, building, true);
            return;
        }
        if (packet.Action == ColonyAction.PlacementLevel)
        {
            // Only looking: any member may flip through the levels, built or not.
            int max = Manager.Blueprints.MaxLevel(building);
            int shown = placementLevels.TryGetValue(player.PlayerUID, out var pick) && pick.ColonyId == colony.ColonyId && pick.BuildingId == building.BuildingId
                ? PreviewLevel(pick.Level, max) : max;
            placementLevels[player.PlayerUID] = (colony.ColonyId, building.BuildingId, StepLevel(shown, max, packet.Text));
            SendPlacement(player, colony, building, false);
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
                    TurnModel(building);
                }
                break;
            case ColonyAction.PlacementHeight:
                if (!unbuilt) error = "createacolony:placement-built";
                else building.YOffset = GameMath.Clamp(building.YOffset + (packet.Text == "-1" ? -1 : 1), -MaxYOffset, MaxYOffset);
                break;
            case ColonyAction.PlacementNudge:
                if (!unbuilt) error = "createacolony:placement-built";
                else Nudge(player, building, packet.Text);
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

    /// <summary>Turns the Town Hall's or a hut's model along with its building.</summary>
    void TurnModel(Building building)
    {
        var be = sapi!.World.BlockAccessor.GetBlockEntity(building.HutPos);
        if (be is BlockEntityTownHall townHall) townHall.SetRotation(building.Rotation);
        else if (be is BlockEntityColonyHut hut) hut.SetRotation(building.Rotation);
    }

    /// <summary>Moves the building one block forward, back, left or right as the player looks.</summary>
    static void Nudge(IServerPlayer player, Building building, string way)
    {
        var view = player.Entity.Pos.GetViewVector();
        // The axis the player looks along most, as a whole step.
        int fx = 0, fz = 0;
        if (System.Math.Abs(view.X) >= System.Math.Abs(view.Z)) fx = view.X >= 0 ? 1 : -1;
        else fz = view.Z >= 0 ? 1 : -1;
        var (dx, dz) = way switch
        {
            "forward" => (fx, fz),
            "back" => (-fx, -fz),
            "left" => (fz, -fx),
            _ => (-fz, fx)
        };
        building.XOffset = GameMath.Clamp(building.XOffset + dx, -MaxNudge, MaxNudge);
        building.ZOffset = GameMath.Clamp(building.ZOffset + dz, -MaxNudge, MaxNudge);
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
    /// <summary>The building whose outline is showing, so the wand used in the air brings its window back.</summary>
    (string ColonyId, int BuildingId)? shownOutline;

    /// <summary>The Builder's Wand used away from any building: reopens the window of the outline showing. False if none is.</summary>
    public bool ReopenPlacement()
    {
        if (shownOutline is not { } shown) return false;
        if (placementDialog != null && placementDialog.IsOpened()) return true;
        SendAction(new ColonyActionPacket { ColonyId = shown.ColonyId, Action = ColonyAction.ReopenPlacement, BuildingId = shown.BuildingId });
        return true;
    }

    /// <summary>Called by the placement window when its outline is cleared (built, taken back or hidden).</summary>
    public void OutlineCleared() => shownOutline = null;

    void OnPlacementSnapshot(PlacementSnapshot snapshot)
    {
        shownOutline = (snapshot.ColonyId, snapshot.BuildingId);
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
