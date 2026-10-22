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

namespace CreateAColony.Network;

/// <summary>
/// Planning a building with the Builder's Wand, as in MineColonies: right-click the ground, pick a
/// building from the list, and its outline appears there. Turn and nudge it from the placement
/// window, walk around it (Esc keeps the outline, the wand brings the window back), and on Accept
/// the building's block is taken from the player's inventory and put down where it belongs.
/// </summary>
public partial class ColonyNetwork
{
    /// <summary>Building types put down with the wand, tab by tab.</summary>
    static readonly string[] WandTypes = BuildingCategories.All.SelectMany(c => c.Types).ToArray();

    /// <summary>A building being planned; Level is the one previewed (0 = the highest).</summary>
    record Planned(string Type, BlockPos Pos, int Rotation, string Style, int Level = 0);

    readonly Dictionary<string, Planned> planned = new();
    /// <summary>Where a player started planning, while they pick from the list.</summary>
    readonly Dictionary<string, BlockPos> wandSpots = new();

    void PlannedAction(IServerPlayer player, ColonyActionPacket packet)
    {
        string uid = player.PlayerUID;
        planned.TryGetValue(uid, out var plan);
        switch (packet.Action)
        {
            case ColonyAction.WandStart:
                if (ParsePos(packet.Text) is not { } spot) return;
                wandSpots[uid] = spot;
                SendChooser(player);
                return;
            case ColonyAction.WandChange:
                if (plan != null) wandSpots[uid] = plan.Pos.Copy();
                SendChooser(player);
                return;
            case ColonyAction.WandChoose:
                // Text is "type|style".
                var chosen = packet.Text.Split('|');
                string type = chosen[0], style = BlueprintStyles.Normalize(chosen.Length > 1 ? chosen[1] : null);
                if (!WandTypes.Contains(type) || !wandSpots.TryGetValue(uid, out var at)) return;
                plan = new Planned(type, at, plan?.Rotation ?? Manager.RotationFor(player, type, style), style);
                planned[uid] = plan;
                SendPlanned(player, plan, true);
                return;
        }

        if (plan == null)
        {
            ClearOutline(player);
            return;
        }
        switch (packet.Action)
        {
            case ColonyAction.OpenPlacement or ColonyAction.ReopenPlacement:
                SendPlanned(player, plan, true);
                return;
            case ColonyAction.PlacementCancel or ColonyAction.PlacementClose:
                planned.Remove(uid);
                ClearOutline(player);
                return;
            case ColonyAction.PlacementTurn:
                plan = plan with { Rotation = (plan.Rotation + (packet.Text == "-1" ? 270 : 90)) % 360 };
                break;
            case ColonyAction.PlacementHeight:
                plan = plan with { Pos = plan.Pos.UpCopy(packet.Text == "-1" ? -1 : 1) };
                break;
            case ColonyAction.PlacementLevel:
                var forLevels = new Building { Type = plan.Type, Style = plan.Style };
                int max = Manager.Blueprints.MaxLevel(forLevels);
                plan = plan with { Level = StepLevel(PreviewLevel(plan.Level, max), max, packet.Text) };
                break;
            case ColonyAction.PlacementNudge:
                var moved = new Building { HutPos = plan.Pos.Copy() };
                Nudge(player, moved, packet.Text);
                plan = plan with { Pos = plan.Pos.AddCopy(moved.XOffset, 0, moved.ZOffset) };
                break;
            case ColonyAction.PlacementConfirm:
                string? error = Accept(player, plan);
                if (error == null)
                {
                    planned.Remove(uid);
                    ClearOutline(player);
                    return;
                }
                player.SendIngameError("createacolony-wand", Lang.GetL(player.LanguageCode, error, Lang.GetL(player.LanguageCode, "createacolony:building-" + plan.Type)));
                SendPlanned(player, plan, true);
                return;
        }
        planned[uid] = plan;
        SendPlanned(player, plan, false);
    }

    /// <summary>Takes the building's block from the player's inventory and puts it down. Returns a lang key for why not, or null.</summary>
    string? Accept(IServerPlayer player, Planned plan)
    {
        var block = sapi!.World.GetBlock(new AssetLocation(BlueprintLibrary.HutBlockCode(plan.Type)));
        if (block is not IWandPlaceable placeable) return "createacolony:wand-unknown";
        var slot = HutSlots(player, block).FirstOrDefault();
        if (slot == null && player.WorldData.CurrentGameMode != EnumGameMode.Creative) return "createacolony:wand-need-block";

        string? error = placeable.PlaceByWand(player, plan.Pos, plan.Rotation);
        if (error != null) return error;
        if (Manager.GetAt(plan.Pos) is { } colony && Manager.GetBuilding(colony, plan.Pos) is { } building)
        {
            building.Style = plan.Style;
            Manager.MarkDirty(colony);
        }
        if (slot != null && player.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            slot.TakeOut(1);
            slot.MarkDirty();
        }
        return null;
    }

    /// <summary>The player's hotbar and backpack slots holding this block.</summary>
    static IEnumerable<ItemSlot> HutSlots(IPlayer player, Block block)
    {
        foreach (var name in new[] { GlobalConstants.hotBarInvClassName, GlobalConstants.backpackInvClassName })
        {
            var inv = player.InventoryManager.GetOwnInventory(name);
            if (inv == null) continue;
            foreach (var slot in inv)
            {
                if (!slot.Empty && slot.Itemstack.Block?.Id == block.Id) yield return slot;
            }
        }
    }

    int HutCount(IPlayer player, string type)
    {
        var block = sapi!.World.GetBlock(new AssetLocation(BlueprintLibrary.HutBlockCode(type)));
        return block == null ? 0 : HutSlots(player, block).Sum(s => s.StackSize);
    }

    void SendChooser(IServerPlayer player)
    {
        var snapshot = new WandChooserSnapshot();
        if (planned.TryGetValue(player.PlayerUID, out var plan)) snapshot.Style = plan.Style;
        foreach (var type in WandTypes)
        {
            snapshot.Types.Add(type);
            snapshot.Have.Add(HutCount(player, type));
            foreach (var style in BlueprintStyles.All)
            {
                if (Manager.Blueprints.MaxLevel(style, type) > 0) snapshot.WithBlueprints.Add(type + "|" + style);
            }
        }
        serverChannel!.SendPacket(snapshot, player);
    }

    void SendPlanned(IServerPlayer player, Planned plan, bool openWindow)
    {
        var building = new Building { Type = plan.Type, HutPos = plan.Pos.Copy(), Rotation = plan.Rotation, Style = plan.Style };
        int max = Manager.Blueprints.MaxLevel(building);
        int level = PreviewLevel(plan.Level, max);
        var blueprint = level > 0 ? Manager.Blueprints.Get(building, level) : null;
        var outline = PreviewBuilding(player, building, blueprint);
        // The spot the building's block goes, in yellow.
        outline.Item1.Add(plan.Pos.Copy());
        outline.Item2.Add(ColorUtil.ColorFromRgba(255, 220, 60, 160));
        sapi!.World.HighlightBlocks(player, OutlineSlot, outline.Item1, outline.Item2, EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Arbitrary);

        serverChannel!.SendPacket(new PlacementSnapshot
        {
            Type = plan.Type,
            Rotation = plan.Rotation,
            ShownLevel = blueprint == null ? 0 : level,
            MaxLevel = max,
            Blocks = outline.Item4,
            Blocked = outline.Item3,
            CanAdjust = true,
            CanOrder = true,
            Planned = true,
            Style = plan.Style,
            Have = HutCount(player, plan.Type),
            OpenWindow = openWindow
        }, player);
    }

    static BlockPos? ParsePos(string text)
    {
        var parts = text.Split(',');
        return parts.Length == 3 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y) && int.TryParse(parts[2], out int z)
            ? new BlockPos(x, y, z) : null;
    }

    // ---- Client ---------------------------------------------------------------------------

    GuiDialogWandChooser? chooserDialog;

    /// <summary>The wand used on the ground with nothing planned: asks the server for the building list.</summary>
    public void StartWandPlan(BlockPos spot) =>
        SendAction(new ColonyActionPacket { Action = ColonyAction.WandStart, Text = $"{spot.X},{spot.Y},{spot.Z}" });

    void OnWandChooserSnapshot(WandChooserSnapshot snapshot)
    {
        chooserDialog?.TryClose();
        chooserDialog = new GuiDialogWandChooser(capi!, this, snapshot);
        chooserDialog.TryOpen();
    }
}
