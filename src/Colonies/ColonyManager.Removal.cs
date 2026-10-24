using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Remove hut, as MineColonies' Deconstruct (Aaron, 0.90.0): the workers lose their job at once, a builder
/// takes the building down from the top, and the hut block goes back to the player who asked, so a hut can
/// be moved. Only blocks the blueprint put there come down: natural ground, the player's own additions and
/// chests with things in them stay. A Miner's Hut's mineshaft is never taken down; it is left derelict.
/// </summary>
public partial class ColonyManager
{
    /// <summary>Starts taking a building down. Returns a lang key on failure, "createacolony:remove-done" when the hut block came straight back.</summary>
    public string? StartRemoval(Colony colony, Building building, IServerPlayer player)
    {
        if (building.Type is BuildingTypes.TownHall or BuildingTypes.Warehouse) return "createacolony:placement-keep";
        if (building.Removing) return "createacolony:remove-already";
        if (building.Type == BuildingTypes.Builder && !colony.Buildings.Any(b => b != building && b.Type == BuildingTypes.Builder && !b.Removing) && building.Level > 0)
        {
            return "createacolony:remove-last-builder";
        }

        if (building.HasWorkOrder) CancelWorkOrder(colony, building);
        LetWorkersGo(colony, building);

        // Nothing built yet: the block comes straight back.
        // A Mineshaft Entrance's building is its mine, which is never taken down.
        if (building.Level == 0 || building.Type == BuildingTypes.MineshaftEntrance)
        {
            building.RemovedBy = player.PlayerUID;
            TakeHutBlock(colony, building, null);
            return "createacolony:remove-done";
        }

        building.Removing = true;
        building.RemovedBy = player.PlayerUID;
        building.Cursor = 0;
        building.BuilderCitizenId = 0;
        BuildStatus.Remove(building.BuildingId);
        LogEvent(colony, "event-removing", BuildingName(building));
        MarkDirty(colony);
        return null;
    }

    /// <summary>Stops taking a building down; what came down already stays down, and it keeps its level.</summary>
    public void CancelRemoval(Colony colony, Building building)
    {
        building.Removing = false;
        building.Cursor = 0;
        building.BuilderCitizenId = 0;
        BuildStatus.Remove(building.BuildingId);
        MarkDirty(colony);
    }

    /// <summary>The hut's workers lose their job, a Miner's Hut's mine is left derelict, and its guards go back to their tower.</summary>
    void LetWorkersGo(Colony colony, Building building)
    {
        foreach (var c in colony.Citizens.Where(c => c.WorkplacePos != null && c.WorkplacePos.Equals(building.HutPos)))
        {
            if (JobOf(building.Type) is { } job && c.JobCode == job) c.JobCode = null;
            c.WorkplacePos = null;
        }
        building.MineGuards.Clear();
        LeaveMine(colony, building);
        MarkDirty(colony);
    }

    /// <summary>Building blocks left to take down, top first, from the blueprint of the level it stands at.</summary>
    public List<BlockPos> RemovalSpots(Colony colony, Building building)
    {
        var spots = new List<BlockPos>();
        if (Blueprints.Get(building, Math.Max(1, building.Level)) is not { } blueprint) return spots;
        foreach (var step in RemovalOrder(blueprint))
        {
            var pos = StepPos(building, step);
            if (StillStanding(colony, building, step, pos)) spots.Add(pos);
        }
        return spots;
    }

    /// <summary>The first building block left to take down, or null when only the hut block is left.</summary>
    public BlockPos? NextRemovalSpot(Colony colony, Building building)
    {
        if (Blueprints.Get(building, Math.Max(1, building.Level)) is not { } blueprint) return null;
        var order = RemovalOrder(blueprint);
        for (int n = 0; n < order.Count; n++)
        {
            int i = (building.Cursor + n) % order.Count;
            var pos = StepPos(building, order[i]);
            if (!StillStanding(colony, building, order[i], pos)) continue;
            if (building.Cursor != i)
            {
                building.Cursor = i;
                MarkDirty(colony);
            }
            return pos;
        }
        return null;
    }

    /// <summary>Done and total blocks of the take-down, for the progress bar; cached briefly like the build progress.</summary>
    public (int Done, int Total) RemovalProgress(Colony colony, Building building)
    {
        if (Blueprints.Get(building, Math.Max(1, building.Level)) is not { } blueprint) return (0, 0);
        return ForWindow(colony, building, blueprint, "removal", () =>
        {
            int total = blueprint.Steps.Count(s => s.BlockId != 0);
            return (total - RemovalSpots(colony, building).Count, total);
        });
    }

    readonly Dictionary<Blueprint, List<BuildStep>> removalOrders = new();

    /// <summary>The blueprint's blocks top down; on each layer the things hanging on walls (torches, ladders) before the walls.</summary>
    List<BuildStep> RemovalOrder(Blueprint blueprint)
    {
        if (removalOrders.TryGetValue(blueprint, out var cached)) return cached;
        var order = blueprint.Steps.Where(s => s.BlockId != 0)
            .OrderByDescending(s => s.Dy).ThenByDescending(StepOrder).ToList();
        if (removalOrders.Count > 200) removalOrders.Clear();
        removalOrders[blueprint] = order;
        return order;
    }

    /// <summary>True when the blueprint's block still stands there and may come down.</summary>
    bool StillStanding(Colony colony, Building building, BuildStep step, BlockPos pos)
    {
        if (pos.Equals(building.HutPos) || !CanWorkAt(colony, pos)) return false;
        var ba = sapi.World.BlockAccessor;
        var current = ba.GetBlock(pos);
        if (current.Id == 0 || current.IsLiquid() || BlueprintLibrary.IsNaturalGround(current)) return false;
        if (!KeepsBlock(colony, current, step.BlockId, pos)) return false;
        if (GetBuilding(colony, pos) != null) return false;
        // The top half of a door comes off with the bottom half; furniture with its main block.
        if (current is BlockMultiblock || IsFurnitureCell(pos)) return false;
        // A chest with the player's things in it stays.
        if (ba.GetBlockEntity(pos) is BlockEntityContainer c && c.Inventory.Any(s => !s.Empty)) return false;
        return true;
    }

    /// <summary>
    /// The last step: the hut block comes away. What was in its storage goes to the builder's hut (or on the
    /// ground there), and the block goes to the player who asked, or to the builder's hut when they are away.
    /// </summary>
    public void TakeHutBlock(Colony colony, Building building, Building? builderHut)
    {
        var ba = sapi.World.BlockAccessor;
        var pos = building.HutPos.Copy();
        var block = ba.GetBlock(pos);
        var storage = builderHut == null ? new List<BlockEntityContainer>() : HutStorage(colony, builderHut).ToList();
        var player = sapi.World.PlayerByUid(building.RemovedBy) as IServerPlayer;
        if (player?.ConnectionState != EnumClientState.Playing) player = null;

        void Hand(ItemStack stack, bool toPlayer)
        {
            if (toPlayer && player != null && player.InventoryManager.TryGiveItemstack(stack, true) && stack.StackSize <= 0) return;
            foreach (var chest in storage)
            {
                if (stack.StackSize <= 0) return;
                PutItems(chest, stack);
            }
            if (stack.StackSize > 0) sapi.World.SpawnItemEntity(stack, (builderHut?.HutPos ?? pos).ToVec3d().Add(0.5, 1, 0.5));
        }

        if (ba.GetBlockEntity(pos) is BlockEntityContainer own)
        {
            foreach (var slot in own.Inventory.Where(s => !s.Empty))
            {
                var stack = slot.TakeOutWhole();
                Hand(stack, toPlayer: builderHut == null);
            }
        }
        string name = BuildingName(building);
        string by = building.RemovedBy;
        LeaveMine(colony, building);
        // Setting air runs the hut block's OnBlockRemoved, which forgets the building.
        bool hutBlock = block is Blocks.BlockColonyHut;
        if (hutBlock) ba.SetBlock(0, pos);
        RemoveBuilding(colony, pos);
        if (hutBlock) Hand(new ItemStack(block), toPlayer: true);

        if (player != null)
        {
            player.SendMessage(0, Lang.GetL(player.LanguageCode, "createacolony:remove-finished", name), EnumChatType.Notification);
        }
        else if (sapi.World.PlayerByUid(by) == null && builderHut == null)
        {
            Mod.Logger.Notification("Hut block of {0} went on the ground: its player is away", name);
        }
    }
}
