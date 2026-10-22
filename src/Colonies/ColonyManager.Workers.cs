using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// What foresters and farmers share: each works from its own hut, keeps its tool and a stock of seeds
/// in its inventory, and brings everything else it gathers to the chest at its hut, from where
/// couriers carry it to the warehouse.
/// </summary>
public partial class ColonyManager
{
    /// <summary>
    /// A hut type with a job that works from it, the tool the job needs, the seeds it keeps a stock of,
    /// and other things it keeps (a farmer's watering can and fertilizer).
    /// </summary>
    public readonly record struct WorkerHutKind(string Job, EnumTool Tool, ItemMatch Stock, ItemMatch Keeps);

    static readonly Dictionary<string, WorkerHutKind> GatherHuts = new()
    {
        [BuildingTypes.Forester] = new WorkerHutKind(Jobs.Forester, EnumTool.Axe, IsTreeSeed, _ => false),
        [BuildingTypes.Farmer] = new WorkerHutKind(Jobs.Farmer, EnumTool.Hoe, IsCropSeed, s => IsWateringCan(s) || IsFertilizer(s)),
    };

    /// <summary>Huts a settler works from, whose chests are never warehouse storage.</summary>
    public static bool IsWorkerHut(string type) => JobOf(type) != null;

    /// <summary>The job of the settlers who work from a hut of this type, or null for buildings nobody works at.</summary>
    public static string? JobOf(string type) => type switch
    {
        BuildingTypes.Builder => Jobs.Builder,
        BuildingTypes.Potter => Jobs.Potter,
        BuildingTypes.Cook => Jobs.Cook,
        BuildingTypes.GuardTower => Jobs.Guard,
        _ => GatherHuts.TryGetValue(type, out var kind) ? kind.Job : null
    };

    /// <summary>How many settlers can work from this building at once.</summary>
    public static int WorkerSlots(Building hut) => hut.Type == BuildingTypes.GuardTower ? GuardSlots(hut) : JobOf(hut.Type) == null ? 0 : 1;

    /// <summary>The living settlers working from this hut.</summary>
    public List<CitizenRecord> WorkersAt(Colony colony, Building hut)
    {
        string? job = JobOf(hut.Type);
        return job == null ? new List<CitizenRecord>()
            : colony.Citizens.Where(c => c.Alive && c.JobCode == job && c.WorkplacePos != null && c.WorkplacePos.Equals(hut.HutPos)).OrderBy(c => c.CitizenId).ToList();
    }

    /// <summary>Huts with their own storage (and a Storage button): the worker huts and the Tavern, where gears can be left for hiring.</summary>
    public static bool HasOwnStorage(string type) => IsWorkerHut(type) || type == BuildingTypes.Tavern;

    /// <summary>What each forester's or farmer's hut is waiting on, for the hut window. Not saved: workers refresh it.</summary>
    public readonly Dictionary<int, string> WorkStatus = new();

    /// <summary>How many seeds a job keeps on hand.</summary>
    public int SeedReserve(string job) => job switch
    {
        Jobs.Farmer => Config.FarmerSeedReserve,
        Jobs.Potter => Config.PotterClayReserve,
        Jobs.Cook => Config.CookRawReserve,
        _ => Config.ForesterSeedReserve
    };

    /// <summary>The hut of this type a worker works from, assigning one with a free place if needed.</summary>
    public Building? EnsureWorkplace(Colony colony, CitizenRecord worker, string hutType)
    {
        if (worker.WorkplacePos != null && GetBuilding(colony, worker.WorkplacePos) is { } hut && hut.Type == hutType) return hut;

        var free = colony.Buildings.Where(b => b.Type == hutType).OrderBy(b => b.BuildingId)
            .FirstOrDefault(b => colony.Citizens.Count(c => c.Alive && c != worker && c.JobCode == worker.JobCode
                && c.WorkplacePos != null && c.WorkplacePos.Equals(b.HutPos)) < WorkerSlots(b));
        if (free != null || worker.WorkplacePos != null)
        {
            worker.WorkplacePos = free?.HutPos.Copy();
            MarkDirty(colony);
        }
        return free;
    }

    /// <summary>The living settler working from this hut, or null.</summary>
    public CitizenRecord? WorkerAt(Colony colony, Building hut, string job) =>
        colony.Citizens.Find(c => c.Alive && c.JobCode == job && c.WorkplacePos != null && c.WorkplacePos.Equals(hut.HutPos));

    /// <summary>
    /// At a Forester's or Farmer's Hut everything is for the warehouse except the job's tools and things
    /// it keeps (watering cans, fertilizer), and its seeds while the worker is short of them, so a worker running low can pick them up there.
    /// </summary>
    System.Func<ItemStack, bool> WorkerHutJunk(Colony colony, Building hut)
    {
        if (hut.Type == BuildingTypes.Potter) return PotterHutJunk();
        if (hut.Type == BuildingTypes.Cook) return CookHutJunk();
        if (hut.Type == BuildingTypes.GuardTower) return GuardTowerJunk(hut);
        var kind = GatherHuts[hut.Type];
        var worker = WorkerAt(colony, hut, kind.Job);
        bool keepSeeds = worker != null && PackCount(worker, kind.Stock) < SeedReserve(kind.Job);
        return s => s.Collectible.Tool != kind.Tool && !kind.Keeps(s) && !(keepSeeds && kind.Stock(s));
    }

    /// <summary>
    /// True when a worker may change the block: it is in no land claim, or only in this colony's own.
    /// Foresters and farmers may reach past the colony border, but never into someone else's claim.
    /// </summary>
    public bool CanWorkAt(Colony colony, BlockPos pos)
    {
        var claims = sapi.World.Claims.Get(pos);
        if (claims == null || claims.Length == 0) return true;
        string mine = ClaimDescription(colony);
        return claims.All(c => c.Description == mine);
    }

    /// <summary>What a block would drop if broken by hand, or nothing if the block insists on a player.</summary>
    ItemStack[] DropsOf(Block block, BlockPos pos, float multiplier = 1)
    {
        try
        {
            return block.GetDrops(sapi.World, pos, null, multiplier) ?? Array.Empty<ItemStack>();
        }
        catch (Exception)
        {
            return Array.Empty<ItemStack>();
        }
    }

    /// <summary>Breaks a block without it dropping anything; the caller collects the drops itself.</summary>
    void BreakQuietly(BlockPos pos) => sapi.World.BlockAccessor.BreakBlock(pos, null, 0);

    /// <summary>
    /// Puts what a worker gathered into its inventory. What does not fit goes straight into the hut
    /// chest, and only if that is full too does it land on the ground where it was gathered.
    /// </summary>
    public void StoreHarvest(Colony colony, Building hut, CitizenRecord worker, IEnumerable<ItemStack> drops, BlockPos at)
    {
        var storage = HutStorage(colony, hut).ToList();
        foreach (var drop in drops)
        {
            if (drop == null || drop.StackSize <= 0) continue;
            AddToPack(colony, worker, drop);
            foreach (var chest in storage)
            {
                if (drop.StackSize <= 0) break;
                PutItems(chest, drop);
            }
            if (drop.StackSize > 0) sapi.World.SpawnItemEntity(drop, at.ToVec3d().Add(0.5, 0.5, 0.5));
        }
    }

    /// <summary>Wears the tool in the worker's hand down by one use; a tool that breaks leaves the hand empty.</summary>
    public static void WearTool(EntityAgent worker, ItemSlot hand)
    {
        if (hand.Itemstack == null) return;
        hand.Itemstack.Collectible.DamageItem(worker.World, worker, hand);
        hand.MarkDirty();
    }

    /// <summary>Lines for a Forester's or Farmer's Hut window: who works here, what they are up to, and how the job works.</summary>
    public string WorkerHutInfo(Colony colony, Building hut)
    {
        string job = JobOf(hut.Type) ?? "";
        if (job.Length == 0 || job == Jobs.Builder) return "";
        if (hut.Type == BuildingTypes.GuardTower) return GuardTowerInfo(colony, hut);
        var lines = new List<string>();
        var worker = WorkerAt(colony, hut, job);
        if (worker == null)
        {
            lines.Add(Lang.Get("createacolony:hut-no-worker", Lang.Get("createacolony:job-" + job)));
        }
        else
        {
            lines.Add(Lang.Get("createacolony:hut-worker-name", Lang.Get("createacolony:job-" + job), worker.Name));
            string status = WorkStatus.TryGetValue(hut.BuildingId, out var s) ? s : "starting";
            lines.Add(Lang.Get("createacolony:hut-worker-status", Lang.Get("createacolony:workstatus-" + status)));
        }
        if (hut.Type == BuildingTypes.Farmer)
        {
            int mine = worker == null ? 0 : colony.Fields.Count(f => f.FarmerCitizenId == worker.CitizenId);
            lines.Add(Lang.Get("createacolony:hut-fields", mine, colony.Fields.Count));
        }
        if (hut.Type == BuildingTypes.Potter) lines.Add(KilnInfo(colony, hut));
        if (hut.Type == BuildingTypes.Cook) lines.Add(FirepitInfo(colony, hut));
        lines.Add("");
        lines.Add(Lang.Get("createacolony:hint-" + hut.Type, GatherRadius(hut)));
        return string.Join("<br>", lines);
    }
}
