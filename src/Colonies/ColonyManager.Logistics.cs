using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Warehouse, drop-offs and delivery requests. The warehouse is not one big block: it is every chest
/// (or trunk, or storage vessel) standing near the warehouse marker, so players build it from whatever
/// containers they like and can open it like any other chest.
/// </summary>
public partial class ColonyManager
{
    /// <summary>How far from the warehouse marker a container still counts as part of the warehouse.</summary>
    public const int WarehouseRadius = 5;
    public const int WarehouseHeight = 3;
    /// <summary>A courier who has not finished a request in this many hours lets other couriers take it.</summary>
    const double RequestLeaseHours = 2;

    // ---- Containers -----------------------------------------------------------------------

    public IEnumerable<BlockEntityGenericTypedContainer> WarehouseContainers(Colony colony)
    {
        if (colony.WarehousePos == null) return Enumerable.Empty<BlockEntityGenericTypedContainer>();
        var w = colony.WarehousePos;
        // Drop-off and worker hut chests are never warehouse storage, or couriers would shuffle items in circles.
        var excluded = new HashSet<BlockPos>(DropOffContainers(colony).Select(c => c.Pos));
        foreach (var hut in colony.Buildings.Where(b => IsWorkerHut(b.Type)))
        {
            excluded.UnionWith(HutStorage(colony, hut).Select(c => c.Pos));
        }
        return FindBlockEntities<BlockEntityGenericTypedContainer>(colony)
            .Where(c => Math.Abs(c.Pos.X - w.X) <= WarehouseRadius && Math.Abs(c.Pos.Z - w.Z) <= WarehouseRadius
                && Math.Abs(c.Pos.Y - w.Y) <= WarehouseHeight && !excluded.Contains(c.Pos))
            .OrderBy(c => c.Pos.DistanceSqTo(w.X, w.Y, w.Z));
    }

    /// <summary>Containers touching a drop-off marker, including diagonally.</summary>
    public IEnumerable<BlockEntityGenericTypedContainer> DropOffContainers(Colony colony)
    {
        var ba = sapi.World.BlockAccessor;
        var seen = new HashSet<BlockPos>();
        foreach (var marker in colony.DropOffs)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var pos = marker.AddCopy(dx, dy, dz);
                if (ba.GetBlockEntity(pos) is BlockEntityGenericTypedContainer c && seen.Add(c.Pos)) yield return c;
            }
        }
    }

    public int CountItems(IEnumerable<BlockEntityContainer> containers, ItemMatch match)
    {
        return containers.Sum(c => c.Inventory.Where(s => !s.Empty && match(s.Itemstack)).Sum(s => s.StackSize));
    }

    /// <summary>
    /// Takes up to <paramref name="max"/> items of one kind from the containers: the first matching
    /// stack found decides the kind, and further slots of the same kind top it up to a full stack.
    /// </summary>
    public ItemStack? TakeItems(IEnumerable<BlockEntityContainer> containers, ItemMatch match, int max)
    {
        ItemStack? taken = null;
        foreach (var container in containers)
        {
            bool changed = false;
            foreach (var slot in container.Inventory)
            {
                if (slot.Empty || !match(slot.Itemstack)) continue;
                if (taken != null && !taken.Equals(sapi.World, slot.Itemstack, GlobalConstants.IgnoredStackAttributes)) continue;

                int limit = Math.Min(max, slot.Itemstack.Collectible.MaxStackSize) - (taken?.StackSize ?? 0);
                if (limit <= 0) break;
                var part = slot.TakeOut(Math.Min(limit, slot.StackSize));
                slot.MarkDirty();
                changed = true;
                if (taken == null) taken = part;
                else taken.StackSize += part.StackSize;
            }
            if (changed) container.MarkDirty(true);
            if (taken != null && taken.StackSize >= Math.Min(max, taken.Collectible.MaxStackSize)) break;
        }
        return taken;
    }

    /// <summary>Puts as much of the stack as fits into the container. Returns how many items moved.</summary>
    public int PutItems(BlockEntityContainer container, ItemStack stack)
    {
        var source = new DummySlot(stack);
        int moved = 0;
        var skip = new List<ItemSlot>();
        while (!source.Empty)
        {
            var target = container.Inventory.GetBestSuitedSlot(source, null, skip)?.slot;
            if (target == null) break;
            int n = source.TryPutInto(sapi.World, target, source.StackSize);
            if (n <= 0)
            {
                skip.Add(target);
                continue;
            }
            moved += n;
            target.MarkDirty();
        }
        stack.StackSize = source.Empty ? 0 : source.StackSize;
        if (moved > 0) container.MarkDirty(true);
        return moved;
    }

    /// <summary>Stores the stack in the warehouse, preferring chests that already hold the same item.</summary>
    public int PutInWarehouse(Colony colony, ItemStack stack)
    {
        int moved = 0;
        var containers = WarehouseContainers(colony)
            .OrderByDescending(c => c.Inventory.Any(s => !s.Empty && s.Itemstack.Collectible == stack.Collectible))
            .ToList();
        foreach (var container in containers)
        {
            if (stack.StackSize <= 0) break;
            moved += PutItems(container, stack);
        }
        return moved;
    }

    // ---- Requests -------------------------------------------------------------------------

    public static ItemMatch Matcher(string code)
    {
        // "tool:shovel:1" asks for any shovel of at least that tier, whatever it is made of.
        if (ParseToolCode(code) is { } tool) return stack => ToolFits(stack, tool.Type, tool.Tier);
        var pattern = new AssetLocation(code);
        return stack => WildcardUtil.Match(pattern, stack.Collectible.Code);
    }

    public DeliveryRequest AddRequest(Colony colony, string itemCode, int quantity, BlockPos dest, string requestedBy)
    {
        var request = new DeliveryRequest
        {
            RequestId = colony.NextRequestId++,
            ItemCode = itemCode,
            Quantity = quantity,
            DestPos = dest.Copy(),
            RequestedBy = requestedBy
        };
        colony.Requests.Add(request);
        MarkDirty(colony);
        return request;
    }

    /// <summary>
    /// Gives the courier the oldest request the warehouse can serve, keeping one the courier already
    /// holds. Returns null when there is nothing the warehouse has stock for.
    /// </summary>
    public DeliveryRequest? LeaseRequest(Colony colony, CitizenRecord courier)
    {
        double now = sapi.World.Calendar.TotalHours;
        var own = colony.Requests.Find(r => r.LeasedByCitizenId == courier.CitizenId);
        if (own != null) return own;

        var warehouse = WarehouseContainers(colony).Cast<BlockEntityContainer>().ToList();
        if (warehouse.Count == 0) return null;

        foreach (var request in colony.Requests.OrderBy(r => r.RequestId))
        {
            if (request.LeasedByCitizenId != 0 && request.LeaseUntilTotalHours > now) continue;
            if (CountItems(warehouse, Matcher(request.ItemCode)) <= 0) continue;

            request.LeasedByCitizenId = courier.CitizenId;
            request.LeaseUntilTotalHours = now + RequestLeaseHours;
            MarkDirty(colony);
            return request;
        }
        return null;
    }

    public void ReleaseRequest(Colony colony, DeliveryRequest request)
    {
        request.LeasedByCitizenId = 0;
        MarkDirty(colony);
    }

    /// <summary>Records a delivery, removing the request once it is complete.</summary>
    public void RecordDelivery(Colony colony, DeliveryRequest request, int amount)
    {
        request.Delivered += amount;
        request.LeasedByCitizenId = 0;
        if (request.Remaining <= 0) colony.Requests.Remove(request);
        MarkDirty(colony);
    }

    public void CancelRequest(Colony colony, DeliveryRequest request)
    {
        colony.Requests.Remove(request);
        MarkDirty(colony);
    }
}
