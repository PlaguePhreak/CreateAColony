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
/// A jeweller works from a Jeweller's Workshop. It makes the jewellery set on the hut's Crafting recipes tab,
/// with a hammer, from metal bits, chains and gems: our own recipes (recipes/grid/jewellery.json) and any
/// jewellery recipes the game or other mods have. The work itself is the base worker's recipe crafting; the
/// jewellery goes to the warehouse, where settlers take pieces to wear (ColonyManager.Clothing.cs).
/// </summary>
public class AiTaskSettlerJeweller : AiTaskSettlerWorker
{
    public AiTaskSettlerJeweller(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Jeweller;
    protected override string HutType => BuildingTypes.Jeweller;
    protected override EnumTool Tool => EnumTool.Hammer;
    protected override ItemMatch Stock => _ => false;
    protected override float WorkSeconds => manager!.Config.HutRecipeSecondsPerCraft;

    /// <summary>It carries its chisel as well as its hammer.</summary>
    protected override bool AlsoKeeps(ItemStack stack) => IsRecipeTool(stack);

    protected override bool PlanFirst(Colony colony, CitizenRecord citizen, Building hut) =>
        FetchRecipeTools(colony, citizen, hut, manager!.HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList());

    /// <summary>All its work is on the Crafting recipes tab; this only says why it is idle.</summary>
    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        SetStatus(hut.RecipeKeep.Count == 0 ? "jeweller-nothing-set" : m.WantedRecipes(colony, hut, citizen).Count == 0 ? "jeweller-stocked" : "jeweller-needs-metal");
        return false;
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut) { }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-jeweller-idle");
}
