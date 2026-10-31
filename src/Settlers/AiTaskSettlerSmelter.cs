using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Settlers;

/// <summary>
/// A smelter works from a Smelter's Hut with the game's own fire pit, crucible and ingot molds: it sets
/// fired ingot molds out beside the fire pit, fills a crucible with nuggets, puts it on the fire with
/// charcoal and lights it, pours the molten metal into the molds once the game has melted it, and takes
/// the ingots out when they have cooled. Ore, fuel, crucibles and molds come from the hut storage, which
/// couriers fill from the warehouse.
/// </summary>
public class AiTaskSettlerSmelter : AiTaskSettlerWorker
{
    enum Work { PlaceMold, Load, Fuel, Pour, Collect, Reheat }

    Work work;
    ColonyManager.SmeltKind? kind;
    int batch;

    public AiTaskSettlerSmelter(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Smelter;
    protected override string HutType => BuildingTypes.Smelter;
    protected override EnumTool Tool => EnumTool.Knife; // unused: a smelter needs no tool
    protected override bool NeedsTool => false;
    protected override bool WorkNeedsTool => false;
    protected override ItemMatch Stock => manager!.IsSmeltable;
    protected override float WorkSeconds => manager!.Config.SmelterSecondsPerStep;

    /// <summary>It carries fuel, crucibles and molds; ingots go to the hut.</summary>
    protected override bool AlsoKeeps(ItemStack stack) =>
        ColonyManager.IsEmptyCrucible(stack) || ColonyManager.IsFullCrucible(stack) || ColonyManager.IsIngotMold(stack)
        || ColonyManager.IsSmeltFuel(stack, manager!.Config.SmelterKeepFuelTemperature) || ColonyManager.IsFirewood(stack);

    protected override string WorkAnimation(ItemSlot? hand) => "interactstatic";

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        var pit = m.SmelterFirepit(hut);
        if (pit == null)
        {
            SetStatus("no-firepit");
            return false;
        }

        // Cooled ingots come out of the molds first, so the molds are free again.
        if (!m.PackFull(citizen) && m.MoldWithIngot(hut) is { } done) return Go(Work.Collect, done.Pos, "smelter-collecting");

        // Molten metal waiting: pour it while it is hot.
        if (ColonyManager.FullCrucibleSlot(pit) is { } full)
        {
            bool molten = m.IsMolten(full.Itemstack);
            if (molten && m.FreeMoldSides(hut) > 0) return Go(Work.Pour, pit.Pos, "smelter-pouring");
            if (m.FreeMoldSides(hut) == 0 && PlanMolds(colony, citizen, hut, storage, 1) is { } moldPlan) return moldPlan;
            // Cooled in the crucible: back on the fire to melt again.
            if (!molten && full == pit.outputSlot && pit.inputSlot.Empty) return Go(Work.Reheat, pit.Pos, "smelter-reheating");
            if (!molten && full == pit.inputSlot && !pit.IsBurning) return PlanFuel(colony, citizen, hut, storage, pit, m.SmelterHeatNeeded(pit));
            SetStatus(molten ? "smelter-needs-molds" : full == pit.outputSlot ? "smelter-cooled" : "smelter-reheating");
            return false;
        }

        if (!pit.inputSlot.Empty)
        {
            // Something a player put there is theirs: leave the fire pit alone.
            if (!ColonyManager.IsEmptyCrucible(pit.inputSlot.Itemstack))
            {
                SetStatus("smelter-firepit-busy");
                return false;
            }
            if (pit.IsBurning)
            {
                SetStatus("smelter-melting");
                return false;
            }
            return PlanFuel(colony, citizen, hut, storage, pit, m.SmelterHeatNeeded(pit));
        }

        var wanted = m.WantedIngots(colony, hut, citizen);
        if (wanted.Count == 0)
        {
            SetStatus("smelter-stocked");
            return false;
        }

        // Molds first: the metal has to go somewhere when it is melted.
        int free = m.FreeMoldSides(hut);
        if (free < m.Config.SmelterMolds && PlanMolds(colony, citizen, hut, storage, m.Config.SmelterMolds - free) is { } placing) return placing;
        if (free == 0)
        {
            SetStatus("smelter-needs-molds");
            return false;
        }

        if (m.PackCount(citizen, ColonyManager.IsEmptyCrucible) == 0)
        {
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && ColonyManager.IsEmptyCrucible(s.Itemstack)));
            if (chest != null && !m.PackFull(citizen))
            {
                SetStatus("smelter-fetching-crucible");
                return FetchSupplies(chest.Pos, ColonyManager.IsEmptyCrucible, 1);
            }
            if (m.CodeInWarehouse(colony, ColonyManager.IsEmptyCrucible) is { } code) m.EnsureCraftRequest(colony, hut, code, 1);
            SetStatus("smelter-needs-crucible");
            return false;
        }

        foreach (var want in wanted)
        {
            int shortBy = m.CraftWant(colony, hut, want.Key) - m.SmeltStock(colony, hut, want, citizen);
            int ingots = System.Math.Max(1, System.Math.Min(shortBy, System.Math.Min(free, m.Config.SmelterIngotsPerBatch)));
            var pack = m.PackItems(citizen);
            var ore = pack.Where(want.IsOre).GroupBy(s => s.Collectible.Code).OrderByDescending(g => g.Sum(s => s.StackSize)).FirstOrDefault();
            int perIngot = ore == null ? 20 : ColonyManager.PiecesPerIngot(want, ore.Key);
            int have = ore?.Sum(s => s.StackSize) ?? 0;
            if (have >= perIngot)
            {
                kind = want;
                batch = System.Math.Min(ingots, have / perIngot);
                int heat = ColonyManager.MeltTemperature(want, pack);
                if (m.PackCount(citizen, s => ColonyManager.IsSmeltFuel(s, heat)) > 0) return Go(Work.Load, pit.Pos, "smelter-loading");
                return FetchFuel(colony, hut, storage, heat);
            }
            var oreChest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && want.IsOre(s.Itemstack)));
            if (oreChest != null && !m.PackFull(citizen))
            {
                SetStatus("smelter-fetching-ore");
                return FetchSupplies(oreChest.Pos, want.IsOre, perIngot * ingots);
            }
            if (m.CodeInWarehouse(colony, want.IsOre) is { } oreCode)
            {
                int each = ColonyManager.PiecesPerIngot(want, new AssetLocation(oreCode));
                m.EnsureCraftRequest(colony, hut, oreCode, each * ingots);
                SetStatus("smelter-waiting-ore");
                return false;
            }
        }
        SetStatus("smelter-needs-ore");
        return false;
    }

    /// <summary>Sets a mold out, fetching one from the hut first, or asks for some. Null when there are none to be had.</summary>
    bool? PlanMolds(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage, int want)
    {
        var m = manager!;
        if (m.PackCount(citizen, ColonyManager.IsIngotMold) > 0)
        {
            var block = m.PackItems(citizen).First(ColonyManager.IsIngotMold).Block;
            var spot = m.FindMoldSpot(colony, hut, block);
            if (spot == null)
            {
                SetStatus("smelter-no-mold-spot");
                return null;
            }
            return Go(Work.PlaceMold, spot, "smelter-placing-mold");
        }
        var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && ColonyManager.IsIngotMold(s.Itemstack)));
        if (chest != null && !m.PackFull(citizen))
        {
            SetStatus("smelter-fetching-molds");
            return FetchSupplies(chest.Pos, ColonyManager.IsIngotMold, want);
        }
        if (m.CodeInWarehouse(colony, ColonyManager.IsIngotMold) is { } code) m.EnsureCraftRequest(colony, hut, code, want);
        return null;
    }

    bool PlanFuel(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage, BlockEntityFirepit pit, int heat)
    {
        var m = manager!;
        if ((!pit.fuelSlot.Empty && ColonyManager.IsSmeltFuel(pit.fuelSlot.Itemstack, heat)) || m.PackCount(citizen, s => ColonyManager.IsSmeltFuel(s, heat)) > 0)
        {
            return Go(Work.Fuel, pit.Pos, "smelter-lighting");
        }
        return FetchFuel(colony, hut, storage, heat);
    }

    bool FetchFuel(Colony colony, Building hut, List<BlockEntityContainer> storage, int heat)
    {
        var m = manager!;
        ItemMatch hot = s => ColonyManager.IsSmeltFuel(s, heat);
        var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && hot(s.Itemstack)));
        if (chest != null)
        {
            SetStatus("smelter-fetching-fuel");
            return FetchSupplies(chest.Pos, hot, m.Config.SmelterFuel * 2);
        }
        string code = m.CodeInWarehouse(colony, hot) ?? m.Config.SmelterFuelCode;
        m.EnsureCraftRequest(colony, hut, code, m.Config.SmelterFuel * 4);
        SetStatus("smelter-needs-fuel");
        return false;
    }

    bool Go(Work next, BlockPos pos, string status)
    {
        work = next;
        SetStatus(status);
        return GoWork(pos);
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        var m = manager!;
        var ba = entity.World.BlockAccessor;
        switch (work)
        {
            case Work.PlaceMold:
                m.PlaceMold(colony, citizen, target);
                break;
            case Work.Collect:
                if (ba.GetBlockEntity(target) is BlockEntityIngotMold mold) m.TakeIngots(colony, citizen, mold);
                break;
            case Work.Load:
                if (ba.GetBlockEntity(target) is BlockEntityFirepit loadPit && kind != null) m.LoadCrucible(colony, citizen, loadPit, kind, batch);
                break;
            case Work.Fuel:
                if (ba.GetBlockEntity(target) is BlockEntityFirepit fuelPit) m.FuelSmelterFire(colony, citizen, fuelPit, m.SmelterHeatNeeded(fuelPit));
                break;
            case Work.Reheat:
                if (ba.GetBlockEntity(target) is BlockEntityFirepit reheatPit) m.ReheatCrucible(colony, citizen, reheatPit);
                break;
            case Work.Pour:
                if (ba.GetBlockEntity(target) is BlockEntityFirepit pourPit) m.PourCrucible(colony, hut, citizen, pourPit);
                break;
        }
    }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-smelter-" + work.ToString().ToLowerInvariant());
}
