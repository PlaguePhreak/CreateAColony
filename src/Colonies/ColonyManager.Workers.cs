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
    public static bool IsWorkerHut(string type) => type == BuildingTypes.Builder || GatherHuts.ContainsKey(type);

    /// <summary>What each forester's or farmer's hut is waiting on, for the hut window. Not saved: workers refresh it.</summary>
    public readonly Dictionary<int, string> WorkStatus = new();

    /// <summary>How many seeds a job keeps on hand.</summary>
    public int SeedReserve(string job) => job == Jobs.Farmer ? Config.FarmerSeedReserve : Config.ForesterSeedReserve;

    /// <summary>The hut of this type a worker works from, assigning a free one if needed.</summary>
    public Building? EnsureWorkplace(Colony colony, CitizenRecord worker, string hutType)
    {
        if (worker.WorkplacePos != null && GetBuilding(colony, worker.WorkplacePos) is { } hut && hut.Type == hutType) return hut;

        var taken = new HashSet<BlockPos>(colony.Citizens
            .Where(c => c.Alive && c != worker && c.JobCode == worker.JobCode && c.WorkplacePos != null)
            .Select(c => c.WorkplacePos!));
        var free = colony.Buildings.Find(b => b.Type == hutType && !taken.Contains(b.HutPos));
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
        if (!GatherHuts.TryGetValue(hut.Type, out var kind)) return "";
        var lines = new List<string>();
        var worker = WorkerAt(colony, hut, kind.Job);
        if (worker == null)
        {
            lines.Add(Lang.Get("createacolony:hut-no-worker", Lang.Get("createacolony:job-" + kind.Job)));
        }
        else
        {
            lines.Add(Lang.Get("createacolony:hut-worker-name", Lang.Get("createacolony:job-" + kind.Job), worker.Name));
            string status = WorkStatus.TryGetValue(hut.BuildingId, out var s) ? s : "starting";
            lines.Add(Lang.Get("createacolony:hut-worker-status", Lang.Get("createacolony:workstatus-" + status)));
        }
        if (hut.Type == BuildingTypes.Farmer)
        {
            int mine = worker == null ? 0 : colony.Fields.Count(f => f.FarmerCitizenId == worker.CitizenId);
            lines.Add(Lang.Get("createacolony:hut-fields", mine, colony.Fields.Count));
        }
        lines.Add("");
        lines.Add(Lang.Get("createacolony:hint-" + hut.Type, GatherRadius(hut)));
        return string.Join("<br>", lines);
    }
}
