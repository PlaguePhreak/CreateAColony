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
/// A tailor works from a Tailor's Hut. It makes the clothes set on the hut's Crafting recipes tab from the
/// game's clothing recipes (and Tailor's Delight's, when that mod is installed), and mends worn clothes and
/// leather and linen armor, never metal armor (see ColonyManager.Clothing.cs). Worn pieces are fetched from the
/// hut storage or the warehouse, where settlers leave them when they change; twine, cloth, linen and leather
/// for mending come from the hut storage, which couriers fill. Mended and new clothes go to the warehouse.
/// Mending comes first, unless the Priorities tab puts something to make ahead of it.
/// </summary>
public class AiTaskSettlerTailor : AiTaskSettlerWorker
{
    public AiTaskSettlerTailor(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Tailor;
    protected override string HutType => BuildingTypes.Tailor;
    // The game's clothing recipes cut with a knife.
    protected override EnumTool Tool => EnumTool.Knife;
    protected override bool WorkNeedsTool => false;
    protected override ItemMatch Stock => _ => false;
    protected override float WorkSeconds => manager!.Config.TailorSecondsPerMend;

    /// <summary>It carries its needles and awls, what it is mending, and what it mends with.</summary>
    protected override bool AlsoKeeps(ItemStack stack)
    {
        var m = manager!;
        return IsRecipeTool(stack) || m.MendLeft(stack) > 0.01f
            || m.PackItems(Citizen!).Any(piece => m.MendLeft(piece) > 0.01f && m.Mends(stack, piece));
    }

    protected override string WorkAnimation(ItemSlot? hand) => "interactstatic";

    protected override bool PlanFirst(Colony colony, CitizenRecord citizen, Building hut) =>
        FetchRecipeTools(colony, citizen, hut, manager!.HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList());

    protected override bool WorkBeforeRecipes(Colony colony, Building hut)
    {
        var m = manager!;
        int mend = hut.Priorities.IndexOf(ColonyManager.MendKey);
        if (mend < 0) return true;
        return !m.WantedRecipes(colony, hut, Citizen).Any(k => hut.Priorities.IndexOf(k.Key) is int i && i >= 0 && i < mend);
    }

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        // Something in hand to mend: mend it if it has what that takes, else get it.
        var piece = m.PackItems(citizen).Where(s => m.MendLeft(s) > 0.01f).OrderByDescending(m.MendLeft).FirstOrDefault();
        if (piece != null)
        {
            ItemMatch mends = s => m.Mends(s, piece);
            if (m.PackCount(citizen, mends) > 0)
            {
                SetStatus("tailor-mending");
                return GoWork(hut.HutPos);
            }
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && mends(s.Itemstack)));
            if (chest != null && !m.PackFull(citizen))
            {
                SetStatus("tailor-fetching-thread");
                return FetchSupplies(chest.Pos, mends, m.Config.HutRecipeBatch);
            }
            if (m.CodeInWarehouse(colony, mends) is { } code)
            {
                m.EnsureCraftRequest(colony, hut, code, m.Config.HutRecipeBatch);
                SetStatus("tailor-waiting-thread");
            }
            else SetStatus(m.IsClothArmor(piece) ? "tailor-needs-patch" : "tailor-needs-thread");
            return false;
        }

        // Worn clothes at the hut first, then in the warehouse.
        if (!m.PackFull(citizen))
        {
            var worn = storage.Concat(m.WarehouseContainers(colony))
                .FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && m.NeedsMending(s.Itemstack)));
            if (worn != null)
            {
                SetStatus("tailor-fetching-worn");
                return FetchSupplies(worn.Pos, m.NeedsMending, 1);
            }
        }
        SetStatus("tailor-nothing-to-mend");
        return false;
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        if (!manager!.MendOnce(colony, hut, citizen)) return;
        entity.World.PlaySoundAt(new AssetLocation("sounds/player/build"), target.X + 0.5, target.Y + 0.5, target.Z + 0.5, null, true, 16);
    }

    protected override string DescribeWork()
    {
        var m = manager;
        var piece = m == null || Citizen is not { } c ? null : m.PackItems(c).Where(s => m.MendLeft(s) > 0.01f).OrderByDescending(m.MendLeft).FirstOrDefault();
        return piece == null ? Lang.Get("createacolony:activity-tailor-idle")
            : Lang.Get("createacolony:activity-tailor-mend", piece.GetName(), (int)System.Math.Round((1 - m!.MendLeft(piece)) * 100));
    }
}
