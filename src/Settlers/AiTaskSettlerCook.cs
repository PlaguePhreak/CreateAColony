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
/// the game has cooked it. Cooked food goes on the hut's Food Shelves shelves when there is room, else into
/// the hut storage, where hungry settlers eat. Raw food and firewood come from the hut storage and shelves,
/// which couriers fill from the warehouse.
/// </summary>
public class AiTaskSettlerCook : AiTaskSettlerWorker
{
    enum Work { Load, LoadMeal, Fuel, Empty, Shelve }

    Work work;
    ColonyManager.KitchenKind? kind;
    ColonyManager.MealPlan? meal;

    public AiTaskSettlerCook(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Cook;
    protected override string HutType => BuildingTypes.Cook;
    protected override EnumTool Tool => EnumTool.Knife; // unused: a cook needs no tool
    protected override bool NeedsTool => false;
    protected override bool WorkNeedsTool => false;
    protected override ItemMatch Stock => manager!.IsGrillable;
    protected override float WorkSeconds => manager!.Config.CookSecondsPerStep;

    /// <summary>It carries firewood, a cooking pot and the ingredients of the meal it is about to cook.</summary>
    protected override bool AlsoKeeps(ItemStack stack) =>
        ColonyManager.IsFirewood(stack) || ColonyManager.IsCleanPot(stack) || meal?.Uses(stack) == true;

    protected override string WorkAnimation(ItemSlot? hand) => "interactstatic";

    /// <summary>A dish it carries that is not an ingredient of the meal it is about to cook.</summary>
    bool IsDishToShelve(ItemStack s) => manager!.IsDish(s) && meal?.Uses(s) != true && !ColonyManager.IsCleanPot(s);

    /// <summary>
    /// Dishes go on the kitchen shelves before anything goes to the hut storage. With nothing to put away, it
    /// takes dishes from the hut storage to shelves that have room for them.
    /// </summary>
    protected override bool PlanFirst(Colony colony, CitizenRecord citizen, Building hut)
    {
        var m = manager!;
        var shelves = m.KitchenShelves(colony, hut).Where(s => !IsUnreachable(s.Pos)).ToList();
        if (shelves.Count == 0) return false;
        if (m.ShelfFor(colony, hut, citizen, IsDishToShelve, IsUnreachable) is { } shelf)
        {
            work = Work.Shelve;
            SetStatus("shelving");
            return GoWork(shelf.Pos);
        }
        if (m.PackFull(citizen)) return false;
        foreach (var chest in m.HutStorage(colony, hut))
        {
            var dish = chest.Inventory.FirstOrDefault(s => !s.Empty && IsDishToShelve(s.Itemstack) && shelves.Any(sh => m.ShelfRoom(sh, s.Itemstack) > 0));
            if (dish == null) continue;
            var stack = dish.Itemstack!;
            var kind = stack.Collectible;
            int room = shelves.Max(sh => m.ShelfRoom(sh, stack));
            SetStatus("shelving");
            return FetchSupplies(chest.Pos, s => s.Collectible == kind && IsDishToShelve(s), room);
        }
        return false;
    }

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        // Ingredients on the kitchen shelves (a vegetable basket, an egg shelf) are used as well as the hut storage.
        storage = storage.Concat(m.KitchenShelves(colony, hut)).ToList();
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
            // Something a player put there is theirs: leave the fire pit alone. A pot that makes a meal the cook lights the fire under.
            if (pit.inputSlot.Itemstack.Block is BlockCookingContainer && !m.PotWillCook(pit))
            {
                SetStatus("firepit-busy");
                return false;
            }
            if (pit.inputSlot.Itemstack.Block is not BlockCookingContainer && !m.IsGrillable(pit.inputSlot.Itemstack))
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

        // Pot meals first: they feed more for the same food.
        meal = m.NextMeal(colony, hut, citizen);
        if (meal != null && PlanMeal(colony, citizen, hut, storage, pit, meal) is { } planned) return planned;
        meal = null;

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

    /// <summary>
    /// Gets a cooking pot and the meal's ingredients together and puts them on the fire. Null when the
    /// meal cannot be made right now (no pot anywhere), so the cook grills instead.
    /// </summary>
    bool? PlanMeal(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage, BlockEntityFirepit pit, ColonyManager.MealPlan plan)
    {
        var m = manager!;
        if (m.PackCount(citizen, ColonyManager.IsCleanPot) == 0)
        {
            var potChest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && ColonyManager.IsCleanPot(s.Itemstack)));
            if (potChest != null && !m.PackFull(citizen))
            {
                SetStatus("fetching-pot");
                return FetchSupplies(potChest.Pos, ColonyManager.IsCleanPot, 1);
            }
            var warehousePot = m.WarehouseContainers(colony).SelectMany(c => c.Inventory).FirstOrDefault(s => !s.Empty && ColonyManager.IsCleanPot(s.Itemstack));
            if (warehousePot == null) return null;
            m.EnsureCookRequest(colony, hut, warehousePot.Itemstack!.Collectible.Code.ToString(), 1);
            SetStatus("waiting-pot");
            return false;
        }

        foreach (var (sample, count) in plan.Slots)
        {
            var code = sample.Collectible.Code;
            ItemMatch same = s => s.Collectible.Code.Equals(code);
            // Two slots of the same item need it twice over.
            int need = plan.Slots.Where(x => x.Sample.Collectible.Code.Equals(code)).Sum(x => x.Count);
            int have = m.PackCount(citizen, same);
            if (have >= need) continue;
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && same(s.Itemstack)));
            if (chest != null && !m.PackFull(citizen))
            {
                SetStatus("fetching-food");
                return FetchSupplies(chest.Pos, same, need - have);
            }
            m.EnsureCookRequest(colony, hut, code.ToString(), need - have);
            SetStatus("waiting-food");
            return false;
        }

        if (pit.IsBurning || !pit.fuelSlot.Empty || m.PackCount(citizen, ColonyManager.IsFirewood) > 0) return Go(Work.LoadMeal, pit, "cooking-meal");
        return FetchFirewood(colony, hut, storage);
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
        if (work == Work.Shelve)
        {
            if (entity.World.BlockAccessor.GetBlockEntity(target) is BlockEntityContainer shelf && ColonyManager.IsFoodShelf(shelf)
                && m.ShelveFromPack(colony, citizen, shelf, IsDishToShelve) == 0)
            {
                MarkUnreachable(target); // full after all: try another shelf, or the hut storage
            }
            return;
        }
        if (entity.World.BlockAccessor.GetBlockEntity(target) is not BlockEntityFirepit pit) return;
        switch (work)
        {
            case Work.Load:
                if (kind != null) m.LoadFirepit(colony, citizen, pit, kind);
                break;
            case Work.LoadMeal:
                if (meal != null) m.LoadMeal(colony, citizen, pit, meal);
                break;
            case Work.Fuel:
                m.FuelFirepit(colony, citizen, pit);
                break;
            case Work.Empty:
                m.EmptyFirepit(colony, citizen, pit);
                break;
        }
    }

    protected override string DescribeWork() => work == Work.LoadMeal && meal != null
        ? Lang.Get("createacolony:activity-cook-loadmeal", manager!.MealName(meal.Recipe))
        : Lang.Get("createacolony:activity-cook-" + work.ToString().ToLowerInvariant());
}
