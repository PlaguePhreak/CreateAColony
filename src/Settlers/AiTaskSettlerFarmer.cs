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
/// A farmer works from a Farmer's Hut and looks after the fields marked out with Field markers. It
/// harvests ripe crops first and sows the same crop again, then sows bare farmland, then tills the rest
/// of the field with its hoe. Seeds come from the harvest, the hut chest, or the warehouse via couriers.
/// </summary>
public class AiTaskSettlerFarmer : AiTaskSettlerWorker
{
    FieldWork work;
    FieldRecord? field;

    public AiTaskSettlerFarmer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        workRange = 2f;
    }

    protected override string Job => Jobs.Farmer;
    protected override string HutType => BuildingTypes.Farmer;
    protected override EnumTool Tool => EnumTool.Hoe;
    protected override ItemMatch Stock => ColonyManager.IsCropSeed;
    protected override float WorkSeconds => manager!.Config.FarmerSecondsPerBlock;
    protected override bool WorkNeedsTool => work == FieldWork.Till;

    protected override string WorkAnimation(ItemSlot? hand) => work switch
    {
        FieldWork.Till => hand?.Itemstack?.Collectible.GetHeldTpUseAnimation(hand, entity) ?? "interactstatic",
        FieldWork.Sow => SettlerAnimations.PlayerAnimations.Contains("placeblock") ? "placeblock" : "interactstatic",
        _ => "breakhand"
    };

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var fields = manager!.FieldsFor(colony, citizen, hut);
        if (fields.Count == 0)
        {
            SetStatus("no-fields");
            return false;
        }

        // Harvest before sowing before tilling, nearest first within each.
        var here = entity.Pos.XYZ;
        (FieldWork Work, FieldRecord Field, BlockPos Ground)? best = null;
        FieldRecord? shortOfSeeds = null;
        foreach (var f in fields)
        {
            bool hasSeed = manager.PackCount(citizen, manager.SeedMatcher(f)) > 0;
            foreach (var ground in manager.FieldGround(f))
            {
                var w = manager.WorkAt(colony, ground);
                if (w == FieldWork.None || IsUnreachable(ground)) continue;
                if (w == FieldWork.Sow && !hasSeed)
                {
                    shortOfSeeds ??= f;
                    continue;
                }
                if (best is { } b && (b.Work < w || (b.Work == w && b.Ground.DistanceSqTo(here.X, here.Y, here.Z) <= ground.DistanceSqTo(here.X, here.Y, here.Z)))) continue;
                best = (w, f, ground);
            }
        }

        // Out of seeds for bare farmland: pick some up at the hut, or ask couriers to bring them.
        if (shortOfSeeds != null)
        {
            var match = manager.SeedMatcher(shortOfSeeds);
            var chest = storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && match(s.Itemstack)));
            if (chest != null) return FetchSupplies(chest.Pos, match, manager.SeedReserve(Job));
            manager.EnsureSeedRequest(colony, hut, shortOfSeeds);
            if (best == null)
            {
                SetStatus("needs-seeds");
                return false;
            }
        }

        if (best == null)
        {
            SetStatus("growing");
            return false;
        }
        work = best.Value.Work;
        field = best.Value.Field;
        SetStatus("farming");
        return GoWork(best.Value.Ground);
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        if (field == null || !colony.Fields.Contains(field)) return;
        if (manager!.WorkAt(colony, target) != work) return; // someone got here first
        switch (work)
        {
            case FieldWork.Harvest:
                string? crop = manager.Harvest(colony, hut, citizen, target);
                manager.Sow(colony, citizen, entity, target, field, crop);
                break;
            case FieldWork.Sow:
                manager.Sow(colony, citizen, entity, target, field, null);
                break;
            case FieldWork.Till:
                var hoe = Hand;
                if (hoe == null || !HoldsTool) return;
                manager.Till(colony, hut, citizen, entity, target, hoe);
                SyncHand(); // a hoe that just broke must vanish from the model too
                break;
        }
    }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-farmer-" + work.ToString().ToLowerInvariant());
}
