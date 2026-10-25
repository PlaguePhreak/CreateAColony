using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// What the Main tab of every hut window does: renaming the building, choosing who works there,
/// calling the workers back, how soon couriers come for the hut's things, the Minimum stock list, and
/// the counts on the Statistics tab.
/// </summary>
public partial class ColonyManager
{
    public const int MaxBuildingNameLength = 40;
    public const int MaxPickupPriority = 10;
    /// <summary>Statistics are kept per day for this many days; older days only count in the all-time totals.</summary>
    public const int StatDays = 100;

    /// <summary>The building's name: the one players gave it, or its type's name.</summary>
    public static string BuildingName(Building b) => b.CustomName.Length > 0 ? b.CustomName : Lang.Get("createacolony:building-" + b.Type);

    /// <summary>Renames a building; an empty name goes back to the type's name. Returns a lang key on failure.</summary>
    public string? RenameBuilding(Colony colony, Building b, string name)
    {
        name = name.Trim();
        if (name.Length > MaxBuildingNameLength) return "createacolony:bad-name";
        b.CustomName = name;
        MarkDirty(colony);
        return null;
    }

    /// <summary>Puts a settler to work at this hut, giving it the hut's job. Returns a lang key on failure.</summary>
    public string? AssignWorker(Colony colony, Building hut, CitizenRecord citizen)
    {
        string? job = JobOf(hut.Type);
        if (job == null) return "createacolony:bad-building";
        if (hut.Removing) return "createacolony:remove-no-workers";
        if (!citizen.Alive || citizen.Visitor) return "createacolony:bad-citizen";
        if (citizen.IsChild) return "createacolony:child-no-job";
        if (citizen.WaitingForRoom) return "createacolony:waiting-no-job";
        if (IncapableOfJob(citizen, job) != null) return "createacolony:job-incapable";
        var workers = WorkersAt(colony, hut);
        if (workers.Contains(citizen)) return null;
        if (workers.Count >= WorkerSlots(hut)) return "createacolony:hut-full";
        citizen.JobCode = job;
        citizen.WorkplacePos = hut.HutPos.Copy();
        EndLoan(citizen);
        MarkDirty(colony);
        return null;
    }

    /// <summary>Takes a settler off this hut's job; it is unemployed until given another.</summary>
    public string? UnassignWorker(Colony colony, Building hut, CitizenRecord citizen)
    {
        if (!WorkersAt(colony, hut).Contains(citizen)) return "createacolony:bad-citizen";
        citizen.JobCode = null;
        citizen.WorkplacePos = null;
        EndLoan(citizen);
        MarkDirty(colony);
        return null;
    }

    /// <summary>Brings this hut's workers to it at once, as MineColonies' Recall does. Returns how many came.</summary>
    public int RecallWorkers(Colony colony, Building hut)
    {
        int came = 0;
        foreach (var worker in WorkersAt(colony, hut))
        {
            if (sapi.World.GetEntityById(worker.EntityId) is not EntityAgent { Alive: true } entity) continue;
            var spot = FreeSpotNear(entity, hut.HutPos.UpCopy(), 3) ?? Beside(hut.HutPos);
            entity.TeleportToDouble(spot.X, spot.Y, spot.Z);
            came++;
        }
        return came;
    }

    // ---- Couriers -------------------------------------------------------------------------

    public void SetPickupPriority(Colony colony, Building hut, int priority)
    {
        hut.PickupPriority = Math.Clamp(priority, 0, MaxPickupPriority);
        MarkDirty(colony);
    }

    /// <summary>Asks the next free courier to take everything in the hut's storage to the warehouse.</summary>
    public void RequestPickupNow(Colony colony, Building hut)
    {
        hut.PickupNow = true;
        MarkDirty(colony);
    }

    /// <summary>
    /// Huts in the order couriers visit them: those with Request pickup now pressed first, then by pickup
    /// priority (10 first); huts set to Never are skipped unless a pickup was asked for.
    /// </summary>
    IEnumerable<Building> HutsForPickup(Colony colony) => colony.Buildings
        .Where(b => HasOwnStorage(b.Type) && (b.PickupNow || (IsWorkerHut(b.Type) && b.PickupPriority > 0)))
        .OrderByDescending(b => b.PickupNow).ThenByDescending(b => b.PickupPriority).ThenBy(b => b.BuildingId);

    // ---- Minimum stock --------------------------------------------------------------------

    public bool IsMinStock(Building hut, ItemStack stack) => hut.MinStock.Any(m => m.Count > 0 && m.Code == stack.Collectible.Code.ToString());

    /// <summary>Adds an item to the hut's Minimum stock list, or raises its count. Returns a lang key on failure.</summary>
    public string? AddMinStock(Colony colony, Building hut, ItemStack stack)
    {
        string code = stack.Collectible.Code.ToString();
        var entry = hut.MinStock.Find(m => m.Code == code);
        if (entry == null)
        {
            if (hut.MinStock.Count >= 27) return "createacolony:minstock-full";
            var one = stack.Clone();
            one.StackSize = 1;
            hut.MinStock.Add(entry = new MinStockEntry { Code = code, Stack = one.ToBytes() });
        }
        entry.Count = Math.Min(999, entry.Count + Math.Max(1, stack.StackSize));
        MarkDirty(colony);
        return null;
    }

    /// <summary>Sets how many of an item to keep; 0 takes it off the list.</summary>
    public void SetMinStock(Colony colony, Building hut, string code, int count)
    {
        var entry = hut.MinStock.Find(m => m.Code == code);
        if (entry == null) return;
        if (count <= 0) hut.MinStock.Remove(entry);
        else entry.Count = Math.Min(999, count);
        // Requests for an item no longer kept are dropped; couriers already carrying it take it back.
        if (count <= 0) colony.Requests.RemoveAll(r => r.RequestedBy == MinStockTag(hut) && r.ItemCode == code && r.LeasedByCitizenId == 0);
        MarkDirty(colony);
    }

    static string MinStockTag(Building hut) => "minstock:" + hut.BuildingId;

    /// <summary>Asks couriers for whatever a hut is short of on its Minimum stock list. Runs with the colony tick.</summary>
    void EnsureMinStock(Colony colony)
    {
        foreach (var hut in colony.Buildings.Where(b => b.MinStock.Count > 0))
        {
            var storage = HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList();
            if (storage.Count == 0) continue;
            string tag = MinStockTag(hut);
            foreach (var entry in hut.MinStock)
            {
                var match = Matcher(entry.Code);
                int have = CountItems(storage, match);
                int coming = colony.Requests.Where(r => r.RequestedBy == tag && r.ItemCode == entry.Code).Sum(r => r.Remaining);
                int shortfall = entry.Count - have - coming;
                if (shortfall > 0) AddRequest(colony, entry.Code, shortfall, storage[0].Pos, tag);
            }
        }
    }

    // ---- Statistics -----------------------------------------------------------------------

    /// <summary>Counts something that happened at a building today, such as "kill:drifter-normal".</summary>
    public void AddStat(Building b, string key, int amount = 1)
    {
        if (amount <= 0) return;
        int today = (int)sapi.World.Calendar.TotalDays;
        var entry = b.Stats.Find(s => s.Key == key && s.Day == today);
        if (entry == null) b.Stats.Add(entry = new StatEntry { Key = key, Day = today });
        entry.Count += amount;
        b.StatTotals[key] = b.StatTotals.GetValueOrDefault(key) + amount;
        b.Stats.RemoveAll(s => s.Day < today - StatDays);
    }

    /// <summary>Counts per key for the Statistics tab: today, yesterday, the last 7 days, the last 100 days and all time.</summary>
    public List<(string Key, int[] Counts)> StatSummary(Building b)
    {
        int today = (int)sapi.World.Calendar.TotalDays;
        var keys = b.StatTotals.Keys.OrderBy(k => k).ToList();
        var rows = new List<(string, int[])>();
        foreach (var key in keys)
        {
            var days = b.Stats.Where(s => s.Key == key).ToList();
            rows.Add((key, new[]
            {
                days.Where(s => s.Day == today).Sum(s => s.Count),
                days.Where(s => s.Day == today - 1).Sum(s => s.Count),
                days.Where(s => s.Day > today - 7).Sum(s => s.Count),
                days.Where(s => s.Day > today - StatDays).Sum(s => s.Count),
                b.StatTotals[key]
            }));
        }
        return rows;
    }

    // ---- Storage list ---------------------------------------------------------------------

    /// <summary>Everything in a hut's storage, by item, with the containers each is in, for the chest list.</summary>
    public List<(ItemStack Sample, int Count, List<BlockPos> Where)> HutItems(Colony colony, Building hut)
    {
        var rows = new List<(ItemStack Sample, int Count, List<BlockPos> Where)>();
        foreach (var container in HutStorage(colony, hut))
        {
            foreach (var slot in container.Inventory)
            {
                if (slot.Empty) continue;
                int at = rows.FindIndex(r => r.Sample.Equals(sapi.World, slot.Itemstack, GlobalConstants.IgnoredStackAttributes));
                if (at < 0)
                {
                    var sample = slot.Itemstack.Clone();
                    sample.StackSize = 1;
                    rows.Add((sample, slot.StackSize, new List<BlockPos> { container.Pos.Copy() }));
                    continue;
                }
                var row = rows[at];
                if (!row.Where.Contains(container.Pos)) row.Where.Add(container.Pos.Copy());
                rows[at] = (row.Sample, row.Count + slot.StackSize, row.Where);
            }
        }
        return rows.OrderByDescending(r => r.Count).ToList();
    }
}
