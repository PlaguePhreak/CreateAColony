using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// Lights settlers carry (Aaron, 0.143.0). Every grown settler keeps a light with it and holds it up in the dark,
/// and the light lights up the ground around it as a player's does (EntitySettler.LightHsv). A lantern is better
/// than a torch, so a settler takes a free lantern from its hut storage or the warehouse, and puts its torch back
/// there in its place. A lantern is only free when no build still needs it: while the colony's work orders need
/// more lanterns than the warehouse and the Builder's Huts hold, settlers leave lanterns alone and bring back the
/// ones they carry for a torch. Builders always carry torches, so their lantern is never mixed up with the build's.
/// The walk to the chest is AiTaskSettlerLight.
/// </summary>
public partial class ColonyManager
{
    /// <summary>What a settler does at the chest: takes a light, swaps its torch for a lantern, or brings back a lantern a build needs.</summary>
    public enum LightAction { Take, Upgrade, GiveBack }

    /// <summary>Lantern: the light to take is a lantern, else a torch.</summary>
    public readonly record struct LightErrand(BlockEntityContainer Chest, LightAction Action, bool Lantern);

    /// <summary>Lanterns the colony's builds still need and lanterns builders can get, worked out at most every few seconds.</summary>
    readonly ConditionalWeakTable<Colony, StrongBox<(long At, int Needed, int Stock)>> lanternBalance = new();

    /// <summary>A lantern as an item, which gives light when held.</summary>
    public static bool IsLanternLight(ItemStack? stack) => stack != null && IsLantern(stack.Block);

    /// <summary>A light a settler can carry: a lit torch or a lantern.</summary>
    public static bool IsHandLight(ItemStack? stack) => IsTorch(stack) || IsLanternLight(stack);

    /// <summary>
    /// The light a settler would hold right now: in the dark, its own light, else a light from its inventory, a
    /// lantern before a torch; otherwise null and the offhand item shows as usual.
    /// </summary>
    public ItemStack? TorchToHold(CitizenRecord citizen, EntityAgent settler)
    {
        if (!IsDark(settler)) return null;
        var pack = PackItems(citizen);
        return OwnLight(citizen) ?? pack.FirstOrDefault(IsLanternLight) ?? pack.FirstOrDefault(IsTorch);
    }

    /// <summary>
    /// The settler's own light: the light in its offhand. Lights in its inventory are a load it carries (a courier's
    /// delivery, a builder's materials, a miner's torches), though it holds one up in the dark when it has no own light.
    /// </summary>
    public ItemStack? OwnLight(CitizenRecord citizen) =>
        ToolBarItems(citizen)[OffhandSlot] is { } offhand && IsHandLight(offhand) ? offhand : null;

    /// <summary>(lanterns the open work orders still have to put up, lanterns in the warehouse and with builders).</summary>
    (int Needed, int Stock) LanternBalance(Colony colony)
    {
        long now = sapi.World.ElapsedMilliseconds;
        var box = lanternBalance.GetOrCreateValue(colony);
        if (box.Value.At > 0 && now - box.Value.At < 10_000) return (box.Value.Needed, box.Value.Stock);
        int needed = 0;
        foreach (var building in colony.Buildings.Where(b => b.HasWorkOrder && !b.Upkeep && !b.Removing))
        {
            if (Blueprints.Get(building, building.TargetLevel) is not { } blueprint) continue;
            foreach (var step in LightSteps(blueprint))
            {
                if (!IsLantern(sapi.World.GetBlock(step.BlockId))) continue;
                var pos = StepPos(building, step);
                if (sapi.World.BlockAccessor.GetChunkAtBlockPos(pos) == null || !IsStepDone(colony, step, pos, building)) needed++;
            }
        }
        int stock = CountItems(WarehouseContainers(colony), IsLanternLight)
            + colony.Buildings.Where(b => b.Type == BuildingTypes.Builder).Sum(b => CountItems(HutStorage(colony, b), IsLanternLight))
            + colony.Citizens.Where(c => c.Alive && c.JobCode == Jobs.Builder).Sum(c => PackCount(c, IsLanternLight));
        box.Value = (now, needed, stock);
        return (needed, stock);
    }

    /// <summary>Forgets the lantern count, after a settler took or brought back a lantern.</summary>
    void LanternsMoved(Colony colony) => lanternBalance.Remove(colony);

    /// <summary>
    /// The chest a settler should walk to for its light, and what to do there, or null when its light is fine.
    /// It takes a light only in the dark; swapping a torch for a free lantern, and bringing back a lantern a
    /// build needs, it does at any time.
    /// </summary>
    public LightErrand? PlanLightErrand(Colony colony, CitizenRecord citizen, EntityAgent settler)
    {
        if (!Config.CarryTorches || citizen.Visitor || citizen.IsChild || !citizen.Alive) return null;
        var own = OwnLight(citizen);
        // The offhand holds something else, such as a shield: no room for a light.
        if (own == null && ToolBarItems(citizen)[OffhandSlot] != null) return null;
        bool builder = citizen.JobCode == Jobs.Builder;
        var (needed, stock) = LanternBalance(colony);

        var hut = citizen.WorkplacePos == null ? null : GetBuilding(colony, citizen.WorkplacePos);
        int range = Config.LightFetchRange;
        bool InReach(BlockEntityContainer c) => c.Pos.DistanceSqTo(settler.Pos.X, settler.Pos.Y, settler.Pos.Z) <= (double)range * range;
        var hutChests = hut == null ? new List<BlockEntityContainer>() : HutStorage(colony, hut).Where(InReach).ToList();
        var warehouse = WarehouseContainers(colony).Where(InReach).ToList();

        if (IsLanternLight(own))
        {
            // Builds need more lanterns than there are: back to the warehouse (or a Builder's Hut) with it.
            if (builder || needed <= stock) return null;
            var store = warehouse.Count > 0 ? warehouse
                : colony.Buildings.Where(b => b.Type == BuildingTypes.Builder).SelectMany(b => HutStorage(colony, b)).Where(InReach).ToList();
            var back = Nearest(settler, store.Where(c => c.Inventory.Any(s => IsTorch(s.Itemstack)))) ?? Nearest(settler, store);
            return back == null ? null : new LightErrand(back, LightAction.GiveBack, false);
        }

        // A lantern from the warehouse is free when builds need fewer than there are; one in a hut is free when
        // the builds have enough without it.
        bool hasTorch = IsTorch(own);
        if (!builder && (hasTorch || IsDark(settler)))
        {
            var lanternChest = (needed < stock ? Nearest(settler, warehouse.Where(HasLantern)) : null)
                ?? (needed <= stock ? Nearest(settler, hutChests.Where(HasLantern)) : null);
            if (lanternChest != null) return new LightErrand(lanternChest, hasTorch ? LightAction.Upgrade : LightAction.Take, true);
        }
        if (hasTorch || !IsDark(settler)) return null;
        // A miner's torches or a load of them count: it holds one of those.
        if (PackItems(citizen).Any(IsTorch)) return null;
        var torchChest = Nearest(settler, hutChests.Concat(warehouse).Where(c => c.Inventory.Any(s => IsTorch(s.Itemstack))));
        return torchChest == null ? null : new LightErrand(torchChest, LightAction.Take, false);

        static bool HasLantern(BlockEntityContainer c) => c.Inventory.Any(s => IsLanternLight(s.Itemstack));
    }

    static BlockEntityContainer? Nearest(EntityAgent settler, IEnumerable<BlockEntityContainer> chests) =>
        chests.OrderBy(c => c.Pos.DistanceSqTo(settler.Pos.X, settler.Pos.Y, settler.Pos.Z)).FirstOrDefault();

    /// <summary>At the chest: takes, swaps or brings back the light. True when the settler's light changed.</summary>
    public bool DoLightErrand(Colony colony, CitizenRecord citizen, LightErrand errand)
    {
        var chest = errand.Chest;
        bool done = false;
        if (errand.Action == LightAction.GiveBack)
        {
            var lantern = TakeOwnLight(colony, citizen);
            if (lantern == null) return false;
            PutItems(chest, lantern);
            if (lantern.StackSize > 0)
            {
                // The chest is full: keep it for now.
                GiveLight(colony, citizen, lantern);
                return false;
            }
            done = true;
            if (TakeOne(chest, IsTorch) is { } torch) GiveLight(colony, citizen, torch);
        }
        else
        {
            ItemMatch want = errand.Lantern ? IsLanternLight : IsTorch;
            if (TakeOne(chest, want) is not { } light) return false;
            if (errand.Action == LightAction.Upgrade && TakeOwnLight(colony, citizen) is { } old)
            {
                PutItems(chest, old);
                if (old.StackSize > 0) AddToPack(colony, citizen, old);
            }
            GiveLight(colony, citizen, light);
            done = true;
        }
        chest.MarkDirty(true);
        LanternsMoved(colony);
        return done;
    }

    ItemStack? TakeOne(BlockEntityContainer chest, ItemMatch match)
    {
        var slot = chest.Inventory.FirstOrDefault(s => !s.Empty && match(s.Itemstack));
        if (slot == null) return null;
        var one = slot.TakeOut(1);
        slot.MarkDirty();
        return one;
    }

    /// <summary>Takes the settler's own light (OwnLight) out of its offhand or inventory.</summary>
    ItemStack? TakeOwnLight(Colony colony, CitizenRecord citizen)
    {
        var bar = ToolBarItems(citizen);
        if (bar[OffhandSlot] is { } offhand && IsHandLight(offhand))
        {
            var one = offhand.Clone();
            one.StackSize = 1;
            offhand.StackSize--;
            if (offhand.StackSize <= 0) bar[OffhandSlot] = null;
            SaveToolBar(colony, citizen, bar);
            return one;
        }
        return null;
    }

    /// <summary>Puts a light in the settler's offhand, or in its inventory if the offhand got taken meanwhile.</summary>
    void GiveLight(Colony colony, CitizenRecord citizen, ItemStack light)
    {
        var bar = ToolBarItems(citizen);
        if (bar[OffhandSlot] == null)
        {
            bar[OffhandSlot] = light;
            SaveToolBar(colony, citizen, bar);
            return;
        }
        AddToPack(colony, citizen, light, false);
    }
}
