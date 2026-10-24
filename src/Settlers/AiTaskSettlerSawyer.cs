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
/// A sawyer works from a Sawmill: it saws logs into planks and makes other wooden things the game's
/// crafting recipes allow (see ColonyManager.Sawmill.cs), at the hut, with a saw. Logs and other wood come
/// from the hut storage, which couriers fill from the warehouse; what it makes goes to the hut storage and
/// on to the warehouse.
/// </summary>
public class AiTaskSettlerSawyer : AiTaskSettlerWorker
{
    ColonyManager.SawKind? kind;
    ColonyManager.SawRecipe? recipe;

    public AiTaskSettlerSawyer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Sawyer;
    protected override string HutType => BuildingTypes.Sawmill;
    protected override EnumTool Tool => EnumTool.Saw;
    protected override bool WorkNeedsTool => recipe?.Tool != null;
    protected override ItemMatch Stock => _ => false;
    protected override float WorkSeconds => manager!.Config.SawmillSecondsPerCraft;

    /// <summary>It carries the wood for what it is making.</summary>
    protected override bool AlsoKeeps(ItemStack stack) => kind?.IsInput(stack) == true;

    protected override string WorkAnimation(ItemSlot? hand) => HoldsTool ? base.WorkAnimation(hand) : "interactstatic";

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        var wanted = m.WantedSawing(colony, hut, citizen);
        if (wanted.Count == 0)
        {
            kind = null;
            recipe = null;
            SetStatus("sawmill-stocked");
            return false;
        }

        // Make something the wood is on hand for.
        foreach (var want in wanted)
        {
            var ready = want.Recipes.FirstOrDefault(r => m.HasSawInputs(citizen, r));
            if (ready == null) continue;
            kind = want;
            recipe = ready;
            SetStatus("sawmill-sawing");
            return GoWork(hut.HutPos);
        }

        foreach (var want in wanted)
        {
            int batches = System.Math.Clamp((m.CraftWant(colony, hut, want.Key) - m.SawStock(colony, hut, want, citizen)) / System.Math.Max(1, want.Sample.StackSize) + 1,
                1, m.Config.SawmillBatch);
            foreach (var r in want.Recipes)
            {
                // Wood in the hut storage: fetch what is missing.
                foreach (var (part, quantity) in r.Inputs)
                {
                    ItemMatch fits = s => part.SatisfiesAsIngredient(s, false);
                    int missing = quantity * batches - m.PackCount(citizen, fits);
                    if (missing <= 0) continue;
                    var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && fits(s.Itemstack)));
                    if (chest == null || m.PackFull(citizen)) continue;
                    kind = want;
                    SetStatus("sawmill-fetching-wood");
                    return FetchSupplies(chest.Pos, fits, missing);
                }
            }
            foreach (var r in want.Recipes)
            {
                // All the wood in the warehouse: ask couriers for it.
                var codes = r.Inputs.Select(i => (Code: m.CodeInWarehouse(colony, s => i.Part.SatisfiesAsIngredient(s, false)), i.Quantity)).ToList();
                if (codes.Any(c => c.Code == null)) continue;
                foreach (var (code, quantity) in codes) m.EnsureCraftRequest(colony, hut, code!, quantity * batches);
                SetStatus("sawmill-waiting-wood");
                return false;
            }
        }
        SetStatus("sawmill-needs-wood");
        return false;
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        var m = manager!;
        if (recipe == null) return;
        if (recipe.Tool != null && Hand is { } hand && HoldsTool) m.WearTool(entity, hand);
        entity.World.PlaySoundAt(new AssetLocation("sounds/block/planks"), target.X + 0.5, target.Y + 0.5, target.Z + 0.5, null, true, 16);
        m.SawCraft(colony, hut, citizen, recipe);
    }

    protected override string DescribeWork() => kind != null
        ? Lang.Get("createacolony:activity-sawyer-saw", kind.Sample.GetName())
        : Lang.Get("createacolony:activity-sawyer-idle");
}
