using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Settlers;

/// <summary>
/// A cook works from a Cook's Hut with the game's own fire pit: it puts raw food the hut keeps too little
/// of cooked in the fire pit by the hut, adds firewood and lights it, and takes the cooked food out when
/// the game has cooked it. Cooked food goes into the hut storage, where hungry settlers eat. Raw food and
/// firewood come from the hut storage, which couriers fill from the warehouse.
/// </summary>
public class AiTaskSettlerCook : AiTaskSettlerWorker
{
    enum Work { Load, Fuel, Empty }

    Work work;
    ColonyManager.KitchenKind? kind;

    public AiTaskSettlerCook(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Cook;
    protected override string HutType => BuildingTypes.Cook;
    protected override EnumTool Tool => EnumTool.Knife; // unused: a cook needs no tool
    protected override bool NeedsTool => false;
    protected override bool WorkNeedsTool => false;
    protected override ItemMatch Stock => manager!.IsGrillable;
    protected override float WorkSeconds => manager!.Config.CookSecondsPerStep;

    /// <summary>It carries firewood from the hut to the fire pit.</summary>
    protected override bool AlsoKeeps(ItemStack stack) => ColonyManager.IsFirewood(stack);

    protected override string WorkAnimation(ItemSlot? hand) => "interactstatic";

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        var pit = m.CookFirepit(colony, hut);
        if (pit == null)
        {
            SetStatus("no-firepit");
            return false;
        }

        // Cooked food waiting in the fire pit comes out first.
        if (!pit.outputSlot.Empty && !m.PackFull(citizen)) return Go(Work.Empty, pit, "firepit-emptying");

        if (!pit.inputSlot.Empty)
        {
            // Something a player put there is theirs: leave the fire pit alone.
            if (!m.IsGrillable(pit.inputSlot.Itemstack))
            {
                SetStatus("firepit-busy");
                return false;
            }
            if (pit.IsBurning)
            {
                SetStatus("cooking");
                return false;
            }
            if (!pit.fuelSlot.Empty || m.PackCount(citizen, ColonyManager.IsFirewood) > 0) return Go(Work.Fuel, pit, "firepit-lighting");
            return FetchFirewood(colony, hut, storage);
        }

        var pack = m.PackItems(citizen);
        var wantedFood = m.WantedFood(colony, hut, citizen);
        foreach (var wanted in wantedFood)
        {
            // Raw food on hand: put it on the fire, with firewood unless the fire pit has some.
            if (pack.Any(wanted.IsRaw))
            {
                kind = wanted;
                if (pit.IsBurning || !pit.fuelSlot.Empty || m.PackCount(citizen, ColonyManager.IsFirewood) > 0) return Go(Work.Load, pit, "cooking");
                return FetchFirewood(colony, hut, storage);
            }
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && wanted.IsRaw(s.Itemstack)));
            if (chest != null && !m.PackFull(citizen))
            {
                SetStatus("fetching-food");
                return FetchSupplies(chest.Pos, wanted.IsRaw, m.Config.CookRawReserve);
            }
            if (m.RawInWarehouse(colony, wanted) is { } raw)
            {
                m.EnsureCookRequest(colony, hut, raw.Collectible.Code.ToString(), m.Config.CookRawReserve);
                SetStatus("waiting-food");
                return false;
            }
        }

        SetStatus(wantedFood.Count > 0 ? "needs-raw-food" : "kitchen-stocked");
        return false;
    }

    bool FetchFirewood(Colony colony, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && ColonyManager.IsFirewood(s.Itemstack)));
        if (chest != null)
        {
            SetStatus("fetching-firewood");
            return FetchSupplies(chest.Pos, ColonyManager.IsFirewood, m.Config.CookFirewood * 2);
        }
        m.EnsureCookRequest(colony, hut, "game:firewood", m.Config.CookFirewood * 4);
        SetStatus("needs-firewood");
        return false;
    }

    bool Go(Work next, BlockEntityFirepit pit, string status)
    {
        work = next;
        SetStatus(status);
        return GoWork(pit.Pos);
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        var m = manager!;
        if (entity.World.BlockAccessor.GetBlockEntity(target) is not BlockEntityFirepit pit) return;
        switch (work)
        {
            case Work.Load:
                if (kind != null) m.LoadFirepit(colony, citizen, pit, kind);
                break;
            case Work.Fuel:
                m.FuelFirepit(colony, citizen, pit);
                break;
            case Work.Empty:
                m.EmptyFirepit(colony, citizen, pit);
                break;
        }
    }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-cook-" + work.ToString().ToLowerInvariant());
}
