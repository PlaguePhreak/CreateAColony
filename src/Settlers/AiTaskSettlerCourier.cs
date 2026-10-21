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
/// warehouse. What the courier carries is saved on its record, so nothing is lost if the trip is
/// interrupted by night, hunger or a storm; the next trip finishes it.
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
        // Builders carry their own materials, so their loads are left to the builder task.
        if (citizen == null || colony == null || citizen.JobCode == Jobs.Builder) return false;
        if (citizen.JobCode != Jobs.Courier && citizen.CarriedStack == null) return false;

        nextSearchMs = entity.World.ElapsedMilliseconds + IdleRetryMs;
        return Plan(colony, citizen);
    }

    bool Plan(Colony colony, CitizenRecord citizen)
    {
        requestId = 0;
        var carried = manager!.GetCarried(citizen);
        if (carried != null)
        {
            // Finish the delivery we were on, or bring the load back to the warehouse.
            var own = colony.Requests.Find(r => r.LeasedByCitizenId == citizen.CitizenId);
            if (own != null && ColonyManager.Matcher(own.ItemCode)(carried))
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

    /// <summary>Takes one load from the container at the target, then points the target at its destination.</summary>
    bool Pickup(Colony colony, CitizenRecord citizen)
    {
        if (entity.World.BlockAccessor.GetBlockEntity(target!) is not BlockEntityContainer container) return false;
        var request = colony.Requests.Find(r => r.RequestId == requestId);

        ItemStack? load;
        BlockPos? destination;
        if (request != null)
        {
            load = manager!.TakeItems(new[] { container }, ColonyManager.Matcher(request.ItemCode), request.Remaining);
            destination = request.DestPos;
            if (load == null) manager.ReleaseRequest(colony, request);
        }
        else
        {
            load = manager!.TakeItems(new[] { container }, pickupFilter, int.MaxValue);
            destination = manager.WarehouseContainers(colony).FirstOrDefault()?.Pos;
        }

        if (load == null) return false;
        manager.SetCarried(colony, citizen, load);
        if (destination == null) return false; // warehouse removed mid-trip; the load waits until one exists
        target = destination.Copy();
        return true;
    }

    void Deliver(Colony colony, CitizenRecord citizen)
    {
        var load = manager!.GetCarried(citizen);
        if (load == null) return;

        var request = colony.Requests.Find(r => r.RequestId == requestId);
        int moved;
        if (request != null)
        {
            if (entity.World.BlockAccessor.GetBlockEntity(request.DestPos) is not BlockEntityContainer dest)
            {
                // The chest that asked for this is gone: drop the request, the load goes back to the warehouse.
                manager.CancelRequest(colony, request);
                nextSearchMs = 0;
                return;
            }
            moved = manager.PutItems(dest, load);
            manager.RecordDelivery(colony, request, moved);
        }
        else
        {
            moved = manager.PutInWarehouse(colony, load);
        }

        manager.SetCarried(colony, citizen, load);
        // Straight on to the next job, unless the destination was full.
        nextSearchMs = load.StackSize > 0 ? entity.World.ElapsedMilliseconds + BlockedRetryMs : 0;
    }

    public override void FinishExecute(bool cancelled)
    {
        // Stopped before picking anything up: let another courier take the request.
        var colony = Colony;
        var citizen = Citizen;
        if (step == Step.Pickup && colony != null && citizen != null && citizen.CarriedStack == null)
        {
            var request = colony.Requests.Find(r => r.RequestId == requestId && r.LeasedByCitizenId == citizen.CitizenId);
            if (request != null) manager!.ReleaseRequest(colony, request);
        }
        base.FinishExecute(cancelled);
    }
}
