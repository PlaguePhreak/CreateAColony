using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace CreateAColony.Network;

/// <summary>The Requests tab of every hut and the Courier's Station's courier list (0.142.0).</summary>
public partial class ColonyNetwork
{
    void AddRequests(HutSnapshot snapshot, Colony colony, Building building)
    {
        snapshot.HasRequestsTab = ColonyManager.HasOwnStorage(building.Type) || ColonyManager.JobOf(building.Type) != null;
        if (!snapshot.HasRequestsTab) return;
        string ownTag = ColonyManager.RequestTag(building);
        foreach (var row in Manager.RequestRows(colony, building))
        {
            var r = row.Request;
            string forText = "";
            // A builder's requests are tagged with the building it is working on.
            if (r.RequestedBy != ownTag && r.RequestedBy.StartsWith("building:") && int.TryParse(r.RequestedBy.Substring(9), out int id)
                && colony.Buildings.Find(b => b.BuildingId == id) is { } order)
            {
                forText = ColonyManager.BuildingName(order) + (order.TargetLevel > 0 ? " " + order.TargetLevel : "");
            }
            else if (!r.RequestedBy.StartsWith("building:") && !r.RequestedBy.StartsWith("minstock:")) forText = r.RequestedBy;
            string name = row.Sample == null ? r.ItemCode
                : ColonyManager.ParseToolCode(r.ItemCode) is { } tool ? Lang.Get("createacolony:items-tool", Lang.Get("createacolony:tool-" + tool.Type.ToString().ToLowerInvariant()))
                : row.Sample.GetName();
            snapshot.Requests.Add(new RequestView
            {
                Stack = row.Sample?.ToBytes() ?? System.Array.Empty<byte>(),
                Name = name,
                Quantity = r.Quantity,
                Delivered = r.Delivered,
                Extra = r.Extra,
                InColony = row.InColony,
                Status = row.Status,
                Color = row.Color,
                For = forText
            });
        }
    }

    void AddCouriers(HutSnapshot snapshot, Colony colony)
    {
        snapshot.IsCourierStation = true;
        var warehouse = Manager.WarehouseContainers(colony).ToList();
        string warehouseName = Lang.Get("createacolony:building-warehouse");
        foreach (var c in colony.Citizens.Where(c => c.Alive && !c.Visitor).OrderBy(c => c.JobCode != Jobs.Courier).ThenBy(c => c.Name))
        {
            var entity = sapi!.World.GetEntityById(c.EntityId) as EntityAgent;
            var task = entity?.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.ActiveTasksBySlot.OfType<Settlers.SettlerTaskBase>().FirstOrDefault();
            bool courier = c.JobCode == Jobs.Courier;
            // Settlers without a job show up while they are hauling.
            if (!courier && task is not Settlers.AiTaskSettlerCourier) continue;
            var view = new CourierView { Name = c.Name, Helping = !courier };
            if (courier) snapshot.CourierJobs++;
            if (c.IsDowned) view.Doing = Lang.Get("createacolony:activity-downed");
            else view.Doing = task?.Describe(colony, c) ?? Lang.Get("createacolony:activity-idle");
            if (task is Settlers.AiTaskSettlerCourier hauling)
            {
                var (from, to) = hauling.Route(colony);
                view.From = from == null ? "" : Manager.PlaceName(colony, from, warehouse);
                view.To = to == null ? warehouseName : Manager.PlaceName(colony, to, warehouse);
            }
            var load = Manager.PackItems(c).GroupBy(s => s.GetName()).Select(g => g.Sum(s => s.StackSize) + "x " + g.Key).ToList();
            view.Carrying = string.Join(", ", load.Take(3)) + (load.Count > 3 ? ", ..." : "");
            snapshot.Couriers.Add(view);
        }
        snapshot.Info = Lang.Get("createacolony:courierstation-summary", snapshot.CourierJobs, snapshot.Couriers.Count(v => v.Helping), colony.Requests.Count(r => r.Remaining > 0));
    }
}
