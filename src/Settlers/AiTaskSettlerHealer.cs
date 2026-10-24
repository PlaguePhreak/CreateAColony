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
/// A healer works from a Healer's Hut. It makes the game's bandages (and poultices) set on the hut's Crafting
/// recipes tab, and keeps them at the hut. Settlers take one each as first aid. When a settler lies in bed to
/// recover, the healer takes a bandage from the hut, walks to the bed and treats them, which heals more than
/// treating oneself (config HealerHealBonus). Treating comes before crafting.
/// </summary>
public class AiTaskSettlerHealer : AiTaskSettlerWorker
{
    int patientId;

    public AiTaskSettlerHealer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Healer;
    protected override string HutType => BuildingTypes.Healer;
    // The healer needs no tool of its own; Knife is only a placeholder (see NeedsTool).
    protected override EnumTool Tool => EnumTool.Knife;
    protected override ItemMatch Stock => _ => false;
    protected override float WorkSeconds => manager!.Config.HealerTreatSeconds;
    protected override bool NeedsTool => false;
    protected override bool WorkNeedsTool => false;

    /// <summary>It carries its knife, and bandages while someone is in bed waiting for it; otherwise they go back to the hut for others to take.</summary>
    protected override bool AlsoKeeps(ItemStack stack) => IsRecipeTool(stack) || (manager!.Recovering.Count > 0 && manager.IsHealingItem(stack));

    protected override bool PlanFirst(Colony colony, CitizenRecord citizen, Building hut) =>
        FetchRecipeTools(colony, citizen, hut, manager!.HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList());

    protected override bool WorkBeforeRecipes(Colony colony, Building hut) => true;

    /// <summary>A patient in bed: take a bandage from the hut if it has none, then go and treat them.</summary>
    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;
        patientId = 0;
        if (m.NextPatient(colony, hut, citizen) is { } patient && m.Recovering.TryGetValue(patient.CitizenId, out var rest) && !IsUnreachable(rest.Bed))
        {
            if (m.PackCount(citizen, m.IsHealingItem) > 0)
            {
                patientId = patient.CitizenId;
                workRange = 2.5f;
                SetStatus("healer-treating");
                return GoWork(rest.Bed);
            }
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => m.IsHealingItem(s.Itemstack)));
            if (chest != null && !m.PackFull(citizen))
            {
                SetStatus("healer-fetching");
                return FetchSupplies(chest.Pos, m.IsHealingItem, 2);
            }
            SetStatus("healer-no-bandages");
            return false;
        }
        SetStatus(hut.RecipeKeep.Count == 0 ? "healer-nothing-set" : m.WantedRecipes(colony, hut, citizen).Count == 0 ? "healer-stocked" : "healer-needs-linen");
        return false;
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        var m = manager!;
        var patient = colony.Citizens.Find(c => c.CitizenId == patientId);
        if (patient == null || !m.TreatPatient(colony, hut, citizen, patient)) MarkUnreachable(target);
        patientId = 0;
    }

    protected override string DescribeWork()
    {
        var patient = patientId == 0 ? null : Colony?.Citizens.Find(c => c.CitizenId == patientId);
        return patient != null ? Lang.Get("createacolony:activity-healer-treat", patient.Name) : Lang.Get("createacolony:activity-healer-idle");
    }
}
