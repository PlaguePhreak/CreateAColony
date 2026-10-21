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
/// A farmer works from a Farmer's Hut. It looks after the fields marked out with Field markers and the
/// berry bushes and fruit trees around the hut, most urgent first: harvest ripe crops (and sow again),
/// pick ripe berries and fruit, pull weeds, water dry crops, sow bare farmland, till the rest. Seeds come
/// from the harvest, the hut, or the warehouse via couriers; a watering can and fertilizer from the hut.
/// </summary>
public class AiTaskSettlerFarmer : AiTaskSettlerWorker
{
    const float FieldReach = 2f;
    /// <summary>Fruit hangs high, so a farmer picks from a little further away.</summary>
    const float FruitReach = 4.5f;
    const int FertilizerStock = 16;

    FieldWork work;
    FieldRecord? field;

    public AiTaskSettlerFarmer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        workRange = FieldReach;
    }

    protected override string Job => Jobs.Farmer;
    protected override string HutType => BuildingTypes.Farmer;
    protected override EnumTool Tool => EnumTool.Hoe;
    protected override ItemMatch Stock => ColonyManager.IsCropSeed;
    protected override float WorkSeconds => manager!.Config.FarmerSecondsPerBlock;
    protected override bool WorkNeedsTool => work == FieldWork.Till;
    protected override bool AlsoKeeps(ItemStack stack) => ColonyManager.IsWateringCan(stack) || ColonyManager.IsFertilizer(stack);

    protected override string WorkAnimation(ItemSlot? hand) => work switch
    {
        FieldWork.Till => hand?.Itemstack?.Collectible.GetHeldTpUseAnimation(hand, entity) ?? "interactstatic",
        FieldWork.Sow or FieldWork.Water or FieldWork.Refill => SettlerAnimations.PlayerAnimations.Contains("placeblock") ? "placeblock" : "interactstatic",
        _ => "breakhand"
    };

    /// <summary>Fruit up in a tree is picked from the ground below it.</summary>
    protected override Vec3d WalkGoal()
    {
        if (work != FieldWork.Pick) return base.WalkGoal();
        var ba = entity.World.BlockAccessor;
        var pos = target.Copy();
        for (int i = 0; i < 8 && ba.GetBlock(pos.DownCopy()).CollisionBoxes == null; i++) pos.Down();
        return pos.ToVec3d().Add(0.5, 0, 0.5);
    }

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var m = manager!;

        // Pick up a watering can and fertilizer from the hut once; the farmer keeps them.
        if (!m.PackItems(citizen).Any(ColonyManager.IsWateringCan) && Supplies(storage, ColonyManager.IsWateringCan) is { } canChest)
        {
            return FetchSupplies(canChest, ColonyManager.IsWateringCan, 1);
        }
        if (m.PackCount(citizen, ColonyManager.IsFertilizer) == 0 && Supplies(storage, ColonyManager.IsFertilizer) is { } fertChest)
        {
            return FetchSupplies(fertChest, ColonyManager.IsFertilizer, FertilizerStock);
        }

        var fields = m.FieldsFor(colony, citizen, hut);
        var (hasCan, water) = m.WaterCarried(citizen);
        var here = entity.Pos.XYZ;
        (FieldWork Work, FieldRecord? Field, BlockPos Pos)? best = null;
        FieldRecord? shortOfSeeds = null;
        bool thirsty = false;

        void Consider(FieldWork w, FieldRecord? f, BlockPos pos)
        {
            if (best is { } b && (b.Work < w || (b.Work == w && b.Pos.DistanceSqTo(here.X, here.Y, here.Z) <= pos.DistanceSqTo(here.X, here.Y, here.Z)))) return;
            best = (w, f, pos);
        }

        foreach (var f in fields)
        {
            bool hasSeed = m.PackCount(citizen, m.SeedMatcher(f)) > 0;
            foreach (var ground in m.FieldGround(f))
            {
                var w = m.WorkAt(colony, ground);
                if (w == FieldWork.None || IsUnreachable(ground)) continue;
                if (w == FieldWork.Sow && !hasSeed)
                {
                    shortOfSeeds ??= f;
                    continue;
                }
                if (w == FieldWork.Water)
                {
                    if (!hasCan) continue;
                    if (water <= 0)
                    {
                        thirsty = true;
                        continue;
                    }
                }
                Consider(w, f, ground);
            }
        }
        foreach (var fruit in m.RipeFruitNear(colony, hut))
        {
            if (!IsUnreachable(fruit)) Consider(FieldWork.Pick, null, fruit);
        }

        // Out of seeds for bare farmland: pick some up at the hut, or ask couriers to bring them.
        if (shortOfSeeds != null)
        {
            var match = m.SeedMatcher(shortOfSeeds);
            if (Supplies(storage, match) is { } chest) return FetchSupplies(chest, match, m.SeedReserve(Job));
            m.EnsureSeedRequest(colony, hut, shortOfSeeds);
        }

        // The watering can is empty and crops are dry: fill it at the nearest water first.
        if (thirsty && (best == null || best.Value.Work > FieldWork.Water) && m.FindWater(colony, hut) is { } waterPos && !IsUnreachable(waterPos))
        {
            best = (FieldWork.Refill, null, waterPos);
        }

        if (best == null)
        {
            SetStatus(fields.Count == 0 ? "no-fields" : shortOfSeeds != null ? "needs-seeds" : "growing");
            return false;
        }
        work = best.Value.Work;
        field = best.Value.Field;
        workRange = work is FieldWork.Pick or FieldWork.Refill ? FruitReach : FieldReach;
        SetStatus(work == FieldWork.Pick ? "picking" : "farming");
        return GoWork(best.Value.Pos);
    }

    static BlockPos? Supplies(List<BlockEntityContainer> storage, ItemMatch match) =>
        storage.FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && match(s.Itemstack)))?.Pos;

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        var m = manager!;
        switch (work)
        {
            case FieldWork.Pick:
                m.PickFruit(colony, hut, citizen, target);
                return;
            case FieldWork.Refill:
                m.RefillCans(colony, citizen, target);
                return;
        }

        if (field == null || !colony.Fields.Contains(field)) return;
        if (m.WorkAt(colony, target) != work) return; // someone got here first
        switch (work)
        {
            case FieldWork.Harvest:
                string? crop = m.Harvest(colony, hut, citizen, target);
                m.Sow(colony, citizen, entity, target, field, crop);
                break;
            case FieldWork.Weed:
                m.Weed(colony, hut, citizen, target);
                break;
            case FieldWork.Water:
                m.Water(colony, citizen, target);
                break;
            case FieldWork.Sow:
                m.Sow(colony, citizen, entity, target, field, null);
                break;
            case FieldWork.Till:
                var hoe = Hand;
                if (hoe == null || !HoldsTool) return;
                m.Till(colony, hut, citizen, entity, target, hoe);
                SyncHand(); // a hoe that just broke must vanish from the model too
                break;
        }
    }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-farmer-" + work.ToString().ToLowerInvariant());
}
