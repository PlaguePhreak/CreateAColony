using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// The courier's working day, one trip at a time: deliver what it is carrying, otherwise fetch the
/// oldest delivery request the warehouse can serve, otherwise empty a drop-off chest into the
/// warehouse. It carries as much as its inventory holds, and what it carries is saved on its record,
/// so nothing is lost if the trip is interrupted by night, hunger or a storm; the next trip finishes it.
/// </summary>
public class AiTaskSettlerCourier : SettlerTaskBase
{
    const long IdleRetryMs = 5_000;
    const long BlockedRetryMs = 60_000;

    enum Step { Pickup, Deliver }

    Step step;
    BlockPos? target;
    int requestId;
    System.Func<ItemStack, bool> pickupFilter = _ => true;
    long nextSearchMs;

    public AiTaskSettlerCourier(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    public override bool ShouldExecute()
    {
        if (manager == null || entity.World.ElapsedMilliseconds < nextSearchMs) return false;
        var citizen = Citizen;
        var colony = Colony;
        // A settler whose courier job was taken away still puts its last load back first.
        // Builders, foresters and farmers carry their own tools and supplies, so their loads are left to them.
        if (citizen == null || colony == null || Jobs.CarriesOwnLoad(citizen.JobCode)) return false;
        if (citizen.JobCode != Jobs.Courier && manager.PackEmpty(citizen)) return false;

        nextSearchMs = entity.World.ElapsedMilliseconds + IdleRetryMs;
        return Plan(colony, citizen);
    }

    bool Plan(Colony colony, CitizenRecord citizen)
    {
        requestId = 0;
        if (!manager!.PackEmpty(citizen))
        {
            // Finish the delivery we were on, or bring the load back to the warehouse.
            var own = colony.Requests.Find(r => r.LeasedByCitizenId == citizen.CitizenId);
            if (own != null && manager.PackCount(citizen, ColonyManager.Matcher(own.ItemCode)) > 0)
            {
                requestId = own.RequestId;
                return Go(Step.Deliver, own.DestPos);
            }
            return Go(Step.Deliver, manager.WarehouseContainers(colony).FirstOrDefault()?.Pos);
        }
        if (citizen.JobCode != Jobs.Courier) return false;

        var request = manager.LeaseRequest(colony, citizen);
        if (request != null)
        {
            var match = ColonyManager.Matcher(request.ItemCode);
            var source = manager.WarehouseContainers(colony).FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && match(s.Itemstack)));
            if (source == null)
            {
                manager.ReleaseRequest(colony, request);
                return false;
            }
            requestId = request.RequestId;
            return Go(Step.Pickup, source.Pos);
        }

        if (colony.WarehousePos == null) return false;
        pickupFilter = _ => true;
        var dropOff = manager.DropOffContainers(colony).FirstOrDefault(c => c.Inventory.Any(s => !s.Empty));
        if (dropOff != null) return Go(Step.Pickup, dropOff.Pos);

        // Clear out builders' chests: rubble from site clearing and materials no longer needed.
        if (manager.FindHutJunk(colony) is { } junk)
        {
            pickupFilter = junk.IsJunk;
            return Go(Step.Pickup, junk.Chest.Pos);
        }
        return false;
    }

    bool Go(Step next, BlockPos? pos)
    {
        if (pos == null) return false;
        step = next;
        target = pos.Copy();
        return true;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        WalkTo(target!.ToVec3d().Add(0.5, 0, 0.5), 1.5f);
    }

    public override bool ContinueExecute(float dt)
    {
        if (PathFailed)
        {
            nextSearchMs = entity.World.ElapsedMilliseconds + BlockedRetryMs;
            return false;
        }
        if (!Arrived)
        {
            KeepWalking();
            return true;
        }
        StopWalking();

        var colony = Colony;
        var citizen = Citizen;
        if (colony == null || citizen == null) return false;

        if (step == Step.Pickup)
        {
            if (!Pickup(colony, citizen)) return false;
            step = Step.Deliver;
            WalkTo(target!.ToVec3d().Add(0.5, 0, 0.5), 1.5f);
            return true;
        }

        Deliver(colony, citizen);
        return false;
    }

    /// <summary>Fills the inventory from the container at the target, then points the target at its destination.</summary>
    bool Pickup(Colony colony, CitizenRecord citizen)
    {
        if (entity.World.BlockAccessor.GetBlockEntity(target!) is not BlockEntityContainer container) return false;
        var request = colony.Requests.Find(r => r.RequestId == requestId);

        var match = request != null ? ColonyManager.Matcher(request.ItemCode) : pickupFilter;
        int wanted = request?.Remaining ?? int.MaxValue;
        int taken = 0;
        while (taken < wanted)
        {
            // One stack at a time; whatever does not fit goes straight back.
            var part = manager!.TakeItems(new[] { container }, match, wanted - taken);
            if (part == null) break;
            int moved = manager.AddToPack(colony, citizen, part);
            taken += moved;
            if (part.StackSize > 0)
            {
                manager.PutItems(container, part);
                break;
            }
        }

        if (taken == 0)
        {
            if (request != null) manager!.ReleaseRequest(colony, request);
            return false;
        }
        var destination = request != null ? request.DestPos : manager!.WarehouseContainers(colony).FirstOrDefault()?.Pos;
        if (destination == null) return false; // warehouse removed mid-trip; the load waits until one exists
        target = destination.Copy();
        return true;
    }

    void Deliver(Colony colony, CitizenRecord citizen)
    {
        if (manager!.PackEmpty(citizen)) return;

        var request = colony.Requests.Find(r => r.RequestId == requestId);
        bool blocked;
        if (request != null)
        {
            if (entity.World.BlockAccessor.GetBlockEntity(request.DestPos) is not BlockEntityContainer dest)
            {
                // The chest that asked for this is gone: drop the request, the load goes back to the warehouse.
                manager.CancelRequest(colony, request);
                nextSearchMs = 0;
                return;
            }
            var match = ColonyManager.Matcher(request.ItemCode);
            int before = manager.PackCount(citizen, match);
            manager.UnloadPack(colony, citizen, new[] { dest }, match);
            int moved = before - manager.PackCount(citizen, match);
            manager.RecordDelivery(colony, request, moved);
            blocked = manager.PackCount(citizen, match) > 0;
        }
        else
        {
            var items = manager.PackItems(citizen);
            foreach (var stack in items) manager.PutInWarehouse(colony, stack);
            manager.SavePack(colony, citizen, items);
            blocked = !manager.PackEmpty(citizen);
        }

        // Straight on to the next job (anything left over goes to the warehouse), unless the destination was full.
        nextSearchMs = blocked ? entity.World.ElapsedMilliseconds + BlockedRetryMs : 0;
    }

    public override void FinishExecute(bool cancelled)
    {
        // Stopped before picking anything up: let another courier take the request.
        var colony = Colony;
        var citizen = Citizen;
        if (step == Step.Pickup && colony != null && citizen != null && manager!.PackEmpty(citizen))
        {
            var request = colony.Requests.Find(r => r.RequestId == requestId && r.LeasedByCitizenId == citizen.CitizenId);
            if (request != null) manager!.ReleaseRequest(colony, request);
        }
        base.FinishExecute(cancelled);
    }

    public override string Describe(Colony colony, CitizenRecord citizen)
    {
        var request = colony.Requests.Find(r => r.RequestId == requestId);
        string what = request != null ? request.ItemCode : "";
        if (request != null && ColonyManager.ParseToolCode(request.ItemCode) is { } tool)
        {
            what = Vintagestory.API.Config.Lang.Get("createacolony:tool-" + tool.Type.ToString().ToLowerInvariant());
        }
        else if (request != null)
        {
            var sample = manager!.PackItems(citizen).FirstOrDefault(ColonyManager.Matcher(request.ItemCode));
            if (sample != null) what = sample.GetName();
        }
        string key = (request != null, step) switch
        {
            (true, Step.Pickup) => "activity-courier-fetch",
            (true, Step.Deliver) => "activity-courier-deliver",
            (false, Step.Pickup) => "activity-courier-collect",
            _ => "activity-courier-store"
        };
        return Vintagestory.API.Config.Lang.Get("createacolony:" + key, request?.Remaining ?? 0, what);
    }
}
