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
/// on the hut's Crafting recipes tab (recipes/grid/coin.json); the work itself is the base worker's recipe
/// crafting. While the colony uses its own coin (Coinage) it strikes copies of that instead. The coins stay in the Mint, part of the colony's treasury, where the bailiff pays wages from
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

    /// <summary>The colony's own coin (Coinage) comes first: copies of it struck from ingots of its metal.</summary>
    protected override bool WorkBeforeRecipes(Colony colony, Building hut) => manager!.HasOwnCoin(colony);

    /// <summary>
    /// With the colony's own coin: strike copies of it while the Mint holds fewer than MintStartKeep. Otherwise
    /// all its work is on the Crafting recipes tab, and this only says why it is idle.
    /// </summary>
    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        if (m.HasOwnCoin(colony))
        {
            if (m.CountItems(storage, m.CurrencyMatch(colony)) >= m.Config.MintStartKeep)
            {
                SetStatus("mint-stocked");
                return false;
            }
            var metal = m.CoinMetal(colony);
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && s.Itemstack.Collectible.Code.Equals(metal)));
            if (chest == null)
            {
                m.EnsureCraftRequest(colony, hut, metal.ToString(), 4);
                SetStatus("mint-needs-metal");
                return false;
            }
            SetStatus("mint-striking");
            return GoWork(chest.Pos);
        }
        SetStatus(hut.RecipeKeep.Count == 0 ? "mint-nothing-set" : m.WantedRecipes(colony, hut, citizen).Count == 0 ? "mint-stocked" : "mint-needs-copper");
        return false;
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        if (manager!.HasOwnCoin(colony)) manager.StrikeOwnCoins(colony, hut);
    }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-minter-idle");
}
