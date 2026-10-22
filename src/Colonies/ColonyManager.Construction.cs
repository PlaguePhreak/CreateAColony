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
/// Buildings, work orders and what builders need to know to carry them out. The blueprint never
/// remembers which blocks are done: a step is done when the world already has the right block, so a
/// player can help build, and a builder interrupted mid-wall simply carries on where the wall ends.
/// </summary>
public partial class ColonyManager
{
    /// <summary>Chests this close to a hut block add to that building's storage.</summary>
    public const int HutStorageRadius = 2;

    BlueprintLibrary? blueprints;
    public BlueprintLibrary Blueprints => blueprints ??= new BlueprintLibrary(sapi, Config);


    /// <summary>What each work order is waiting on, for the Town Hall window. Not saved: builders refresh it.</summary>
    public readonly Dictionary<int, string> BuildStatus = new();

    // ---- Buildings ------------------------------------------------------------------------

    public Building? GetBuilding(Colony colony, BlockPos hutPos) => colony.Buildings.Find(b => b.HutPos.Equals(hutPos));

    public Building AddBuilding(Colony colony, string type, BlockPos hutPos, int rotation)
    {
        var existing = GetBuilding(colony, hutPos);
        if (existing != null) return existing;

        var building = new Building { BuildingId = colony.NextBuildingId++, Type = type, HutPos = hutPos.Copy(), Rotation = rotation };
        colony.Buildings.Add(building);
        LogEvent(colony, "event-placed", "@building-" + type);
        MarkDirty(colony);
        RefreshClaim(colony);
        return building;
    }

    public void RemoveBuilding(Colony colony, BlockPos hutPos)
    {
        var building = GetBuilding(colony, hutPos);
        if (building == null) return;
        colony.Buildings.Remove(building);
        LogEvent(colony, "event-removed", BuildingName(building));
        CancelBuildRequests(colony, building);
        foreach (var c in colony.Citizens.Where(c => c.WorkplacePos != null && c.WorkplacePos.Equals(hutPos))) c.WorkplacePos = null;
        MarkDirty(colony);
        RefreshClaim(colony);
    }

    /// <summary>Colonies saved before buildings existed get their Town Hall and Warehouse as buildings.</summary>
    void EnsureCoreBuildings(Colony colony)
    {
        if (GetBuilding(colony, colony.TownHallPos) == null) AddBuilding(colony, BuildingTypes.TownHall, colony.TownHallPos, 0);
        if (colony.WarehousePos != null && GetBuilding(colony, colony.WarehousePos) == null)
        {
            AddBuilding(colony, BuildingTypes.Warehouse, colony.WarehousePos, 0);
        }
    }

    /// <summary>Blueprint rotation that turns a blueprint's front towards the player placing the hut.</summary>
    public static int RotationFor(IPlayer player)
    {
        var facing = BlockFacing.HorizontalFromYaw(player.Entity.Pos.Yaw);
        return facing.HorizontalAngleIndex * 90 % 360;
    }

    /// <summary>
    /// Blueprint rotation for a building type: one with a door is turned so the door's side faces the player
    /// placing it. Turning a blueprint by 90 moves each side one step clockwise (east to south).
    /// </summary>
    public int RotationFor(IPlayer player, string type, string style = BlueprintStyles.Testing)
    {
        if (Blueprints.Get(style, type, 1, 0)?.Front is not { } front) return RotationFor(player);
        int towards = BlockFacing.HorizontalFromYaw(player.Entity.Pos.Yaw).HorizontalAngleIndex;
        return GameMath.Mod(front - towards, 4) * 90;
    }

    /// <summary>A hut's storage: the hut block's own inventory first, then any chests next to it.</summary>
    public IEnumerable<BlockEntityGenericTypedContainer> HutStorage(Colony colony, Building building)
    {
        var ba = sapi.World.BlockAccessor;
        var hut = building.HutPos;
        if (ba.GetBlockEntity(hut) is BlockEntityGenericTypedContainer own)
        {
            yield return own;
        }
        else if (ba.GetBlock(hut) is Blocks.BlockColonyHut { EntityClass: { } entityClass })
        {
            // Huts placed before they had their own storage get it now.
            ba.SpawnBlockEntity(entityClass, hut);
            if (ba.GetBlockEntity(hut) is BlockEntityGenericTypedContainer added) yield return added;
        }
        for (int dx = -HutStorageRadius; dx <= HutStorageRadius; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -HutStorageRadius; dz <= HutStorageRadius; dz++)
        {
            if (dx == 0 && dy == 0 && dz == 0) continue;
            if (ba.GetBlockEntity(hut.AddCopy(dx, dy, dz)) is BlockEntityGenericTypedContainer c) yield return c;
        }
    }

    // ---- Work orders ----------------------------------------------------------------------

    /// <summary>Asks for the next level of a building. Returns a lang key on failure.</summary>
    public string? StartWorkOrder(Colony colony, Building building)
    {
        if (building.HasWorkOrder) return "createacolony:build-already";
        int next = building.Level + 1;
        if (Blueprints.Get(building, next) == null) return "createacolony:build-no-blueprint";

        building.TargetLevel = next;
        building.Cursor = 0;
        building.BuilderCitizenId = 0;
        BuildStatus.Remove(building.BuildingId);
        MarkDirty(colony);
        return null;
    }

    public void CancelWorkOrder(Colony colony, Building building)
    {
        building.TargetLevel = 0;
        building.Cursor = 0;
        building.BuilderCitizenId = 0;
        BuildStatus.Remove(building.BuildingId);
        CancelBuildRequests(colony, building);
        MarkDirty(colony);
    }

    /// <summary>Gives the builder the work order it is on, or the oldest one nobody is working on.</summary>
    public Building? TakeWorkOrder(Colony colony, CitizenRecord builder)
    {
        var own = colony.Buildings.Find(b => b.HasWorkOrder && b.BuilderCitizenId == builder.CitizenId);
        if (own != null) return own;

        foreach (var building in colony.Buildings.Where(b => b.HasWorkOrder).OrderBy(b => b.BuildingId))
        {
            var current = colony.Citizens.Find(c => c.CitizenId == building.BuilderCitizenId);
            if (current != null && current.Alive && current.JobCode == Jobs.Builder) continue;
            building.BuilderCitizenId = builder.CitizenId;
            MarkDirty(colony);
            return building;
        }
        return null;
    }

    /// <summary>The builder's hut a builder works from, assigning a free one if needed.</summary>
    public Building? EnsureBuilderHut(Colony colony, CitizenRecord builder) => EnsureWorkplace(colony, builder, BuildingTypes.Builder);

    public void CompleteWorkOrder(Colony colony, Building building)
    {
        building.Level = building.TargetLevel;
        building.TargetLevel = 0;
        building.Cursor = 0;
        building.BuilderCitizenId = 0;
        BuildStatus.Remove(building.BuildingId);
        CancelBuildRequests(colony, building);
        MarkDirty(colony);
        RefreshClaim(colony);

        LogEvent(colony, "event-built", "@building-" + building.Type, building.Level.ToString());
        string message = Lang.Get("createacolony:build-finished", Lang.Get("createacolony:building-" + building.Type), building.Level);
        foreach (var member in colony.Members)
        {
            if (sapi.World.PlayerByUid(member.PlayerUid) is IServerPlayer p && p.ConnectionState == EnumClientState.Playing)
            {
                p.SendMessage(0, message, EnumChatType.Notification);
            }
        }
    }

    public static string RequestTag(Building building) => "building:" + building.BuildingId;

    /// <summary>Turns "any wood or stone" on or off. Open material requests are dropped so builders ask again in the new terms.</summary>
    public void SetAnyWoodOrStone(Colony colony, bool on)
    {
        if (colony.AnyWoodOrStone == on) return;
        colony.AnyWoodOrStone = on;
        foreach (var b in colony.Buildings.Where(b => b.HasWorkOrder)) CancelBuildRequests(colony, b);
        MarkDirty(colony);
    }

    void CancelBuildRequests(Colony colony, Building building)
    {
        string tag = RequestTag(building);
        colony.Requests.RemoveAll(r => r.RequestedBy == tag);
    }

    // ---- Steps ----------------------------------------------------------------------------

    public BlockPos StepPos(Building building, BuildStep step) => building.HutPos.AddCopy(step.Dx + building.XOffset, step.Dy + building.YOffset, step.Dz + building.ZOffset);

    /// <summary>
    /// True when the world already matches the step, or the step must not be touched: in someone else's
    /// land claim, a hut block, or a container (a chest full of the player's things is never torn down).
    /// A hut only claims land once it is built, so its blueprint may reach past the colony's claim into
    /// unclaimed land; builders build there too.
    /// </summary>
    public bool IsStepDone(Colony colony, BuildStep step, BlockPos pos)
    {
        if (!CanWorkAt(colony, pos)) return true;
        var ba = sapi.World.BlockAccessor;
        var current = ba.GetBlock(pos);
        if (current.Id == step.BlockId || Fulfils(colony, current, step.BlockId)) return true;
        if (GetBuilding(colony, pos) != null) return true;
        // A half-built fire pit already has its container (the fuel slot), but the firewood still has to go on.
        if (InProgress(current, step.BlockId)) return false;
        if (ba.GetBlockEntity(pos) is BlockEntityContainer) return true;
        // The top half of a door belongs to the door below it; clearing it would break the door. The same goes
        // for the other cells of a furniture shelf.
        if (step.BlockId == 0 && (current is BlockMultiblock || IsFurnitureCell(pos))) return true;
        // Water and lava are left alone when clearing; builders do not drain lakes.
        if (step.BlockId == 0 && current.IsLiquid()) return true;
        return false;
    }

    /// <summary>Finds the next step that still needs doing, starting at the cursor and wrapping round once.</summary>
    public bool FindNextStep(Colony colony, Building building, Blueprint blueprint, out BuildStep step, out BlockPos pos)
    {
        int count = blueprint.Steps.Count;
        for (int n = 0; n < count; n++)
        {
            int i = (building.Cursor + n) % count;
            step = blueprint.Steps[i];
            pos = StepPos(building, step);
            if (IsStepDone(colony, step, pos)) continue;
            if (building.Cursor != i)
            {
                building.Cursor = i;
                MarkDirty(colony);
            }
            return true;
        }
        step = default;
        pos = building.HutPos;
        return false;
    }

    /// <summary>Materials still needed for the rest of the work order, by item code (a pattern when any wood or stone will do).</summary>
    public Dictionary<string, (ItemStack Sample, int Count)> MaterialsNeeded(Colony colony, Building building, Blueprint blueprint)
    {
        var needs = new Dictionary<string, (ItemStack, int)>();
        if (!Config.BuildersNeedMaterials) return needs;
        foreach (var step in blueprint.Steps)
        {
            if (step.BlockId == 0) continue;
            var pos = StepPos(building, step);
            if (IsStepDone(colony, step, pos)) continue;
            foreach (var cost in RemainingCost(colony, step, pos))
            {
                needs[cost.Code] = needs.TryGetValue(cost.Code, out var n) ? (n.Item1, n.Item2 + cost.Count) : (cost.Sample, cost.Count);
            }
        }
        return needs;
    }

    /// <summary>
    /// Makes sure couriers are asked for whatever the builder's hut is short of. Returns false when
    /// the builder's hut has no chest to deliver to.
    /// </summary>
    public bool EnsureMaterialRequests(Colony colony, Building order, Building builderHut, CitizenRecord builder, Blueprint blueprint)
    {
        var storage = HutStorage(colony, builderHut).Cast<BlockEntityContainer>().ToList();
        if (storage.Count == 0) return false;

        string tag = RequestTag(order);
        foreach (var (code, (sample, count)) in MaterialsNeeded(colony, order, blueprint))
        {
            var match = Matcher(code);
            int have = CountItems(storage, match) + PackCount(builder, match);
            int coming = colony.Requests.Where(r => r.RequestedBy == tag && r.ItemCode == code).Sum(r => r.Remaining);
            int shortfall = count - have - coming;
            if (shortfall > 0) AddRequest(colony, code, shortfall, storage[0].Pos, tag);
        }
        return true;
    }

    /// <summary>
    /// A chest at a worker's hut that holds something its worker does not need (cleared dirt and
    /// leftovers at a Builder's Hut, logs and crops at a Forester's or Farmer's Hut), and a test for
    /// which items are not needed. Couriers take those to the warehouse.
    /// </summary>
    public (BlockEntityContainer Chest, System.Func<ItemStack, bool> IsJunk)? FindHutJunk(Colony colony)
    {
        foreach (var hut in HutsForPickup(colony).ToList())
        {
            // Request pickup now: everything goes, until the hut's storage is empty.
            System.Func<ItemStack, bool> isJunk = hut.PickupNow ? _ => true
                : hut.Type == BuildingTypes.Builder ? BuilderHutJunk(colony, hut) : WorkerHutJunk(colony, hut);
            var chest = HutStorage(colony, hut).FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && isJunk(s.Itemstack)));
            if (chest != null) return (chest, isJunk);
            if (hut.PickupNow)
            {
                hut.PickupNow = false;
                MarkDirty(colony);
            }
        }
        return null;
    }

    /// <summary>At a Builder's Hut everything but the current work order's materials and the builder's tools.</summary>
    System.Func<ItemStack, bool> BuilderHutJunk(Colony colony, Building hut)
    {
        var needed = new List<System.Func<ItemStack, bool>>();
        var builder = colony.Citizens.Find(c => c.Alive && c.JobCode == Jobs.Builder && c.WorkplacePos != null && c.WorkplacePos.Equals(hut.HutPos));
        var order = builder == null ? null : colony.Buildings.Find(b => b.HasWorkOrder && b.BuilderCitizenId == builder.CitizenId);
        var blueprint = order == null ? null : Blueprints.Get(order, order.TargetLevel);
        if (blueprint != null) needed.AddRange(MaterialsNeeded(colony, order!, blueprint).Keys.Select(Matcher));
        return s => !needed.Any(m => m(s)) && !IsBuilderTool(s);
    }

    // ---- Changing the world ---------------------------------------------------------------

    /// <summary>Removes a block for construction; what it drops goes into the builder's hut storage.</summary>
    public void BreakForBuild(Colony colony, Building builderHut, BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var old = ba.GetBlock(pos);
        if (old.Id == 0) return;

        ItemStack[] drops;
        try
        {
            drops = old.GetDrops(sapi.World, pos, null) ?? Array.Empty<ItemStack>();
        }
        catch (Exception)
        {
            drops = Array.Empty<ItemStack>(); // some blocks expect a player when dropping
        }
        ba.BreakBlock(pos, null, 0);

        var storage = HutStorage(colony, builderHut).ToList();
        foreach (var drop in drops)
        {
            foreach (var container in storage)
            {
                if (drop.StackSize <= 0) break;
                PutItems(container, drop);
            }
            if (drop.StackSize > 0) sapi.World.SpawnItemEntity(drop, pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }
    }

    public void PlaceForBuild(int blockId, BlockPos pos, ItemStack? fromItem = null)
    {
        var ba = sapi.World.BlockAccessor;
        var block = sapi.World.GetBlock(blockId);
        // A fence joins the neighbours it has here, as when a player places one.
        if (block is BlockFence fence && sapi.World.GetBlock(fence.CodeWithVariant("type", fence.GetOrientations(sapi.World, pos))) is { } joined)
        {
            block = joined;
            blockId = joined.Id;
        }
        // Furniture and banner poles go down from their item, which gives them their wood and puts down their other parts.
        if (fromItem != null) ba.SetBlock(blockId, pos, fromItem);
        else ba.SetBlock(blockId, pos);
        ba.TriggerNeighbourBlockUpdate(pos);
        if (block.Sounds?.Place != null) sapi.World.PlaySoundAt(block.Sounds.Place, pos, 0);
    }

    bool IsFurnitureCell(BlockPos pos) => sapi.World.BlockAccessor.GetBlockEntity(pos)?.GetType().Name == "BlockEntityFurnitureDisplayCell";

    /// <summary>Done and total steps, for the progress bar.</summary>
    public (int Done, int Total) Progress(Colony colony, Building building, Blueprint blueprint)
    {
        int done = blueprint.Steps.Count(s => IsStepDone(colony, s, StepPos(building, s)));
        return (done, blueprint.Steps.Count);
    }
}
