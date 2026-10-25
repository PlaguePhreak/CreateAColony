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
/// A minter works from the Royal Mint. It strikes the colony's coins with a hammer from copper ingots, as set
/// on the hut's Crafting recipes tab (recipes/grid/coin.json). The work itself is the base worker's recipe
/// crafting; the coins stay in the Mint, part of the colony's treasury, where the bailiff pays wages from
/// (ColonyManager.Economy.cs).
/// </summary>
public class AiTaskSettlerMinter : AiTaskSettlerWorker
{
    public AiTaskSettlerMinter(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Minter;
    protected override string HutType => BuildingTypes.Mint;
    protected override EnumTool Tool => EnumTool.Hammer;
    protected override ItemMatch Stock => _ => false;
    protected override float WorkSeconds => manager!.Config.HutRecipeSecondsPerCraft;

    protected override bool AlsoKeeps(ItemStack stack) => IsRecipeTool(stack);

    protected override bool PlanFirst(Colony colony, CitizenRecord citizen, Building hut) =>
        FetchRecipeTools(colony, citizen, hut, manager!.HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList());

    /// <summary>All its work is on the Crafting recipes tab; this only says why it is idle.</summary>
    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        SetStatus(hut.RecipeKeep.Count == 0 ? "mint-nothing-set" : m.WantedRecipes(colony, hut, citizen).Count == 0 ? "mint-stocked" : "mint-needs-copper");
        return false;
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut) { }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-minter-idle");
}
