using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Settlers put out fires (Aaron, 0.123.0). The game's fire spreads through grass and anything wooden, so a lit pit
/// kiln or a lightning strike can burn a colony down. The game tells us when something catches fire (FireWatch);
/// a fire on the colony's land raises the alarm, and settlers nearby fetch a watering can, fill it at the nearest
/// water and pour it on the flames (AiTaskSettlerFirefight), as a player would. Only loose fire counts: fire
/// blocks and burning piles, never a lit pit kiln, fire pit, forge, bloomery or charcoal pit.
///
/// To keep fires from starting, the potter cuts the grass and plants around its pit kiln before lighting it.
/// </summary>
public partial class ColonyManager
{
    const int FireTickMs = 2000;
    /// <summary>The fire alarm goes into the Town Hall's log at most this often for each colony.</summary>
    const long FireAlarmEveryMs = 60_000;
    /// <summary>"Nobody has a watering can" goes into the log at most this often.</summary>
    const long NoCanAlarmEveryMs = 300_000;

    /// <summary>Fires on each colony's land, by colony id.</summary>
    readonly Dictionary<string, HashSet<BlockPos>> colonyFires = new();
    /// <summary>Who is putting out which fire, by the fire's position.</summary>
    readonly Dictionary<BlockPos, HashSet<long>> fireFighters = new();
    readonly Dictionary<string, long> fireAlarmMs = new();
    readonly Dictionary<string, long> noCanAlarmMs = new();
    Harmony? fireHarmony;

    void StartFires()
    {
        FireWatch.Started.Clear();
        if (!Config.Firefighting) return;
        fireHarmony = new Harmony("createacolony.fires");
        try
        {
            FireWatch.Patch(fireHarmony);
        }
        catch (Exception e)
        {
            Mod.Logger.Warning("Could not watch for fires, so settlers will not put them out: {0}", e.Message);
            return;
        }
        sapi.Event.RegisterGameTickListener(OnFireTick, FireTickMs);
    }

    void StopFires()
    {
        fireHarmony?.UnpatchAll("createacolony.fires");
        fireHarmony = null;
        FireWatch.Started.Clear();
    }

    void OnFireTick(float dt)
    {
        long now = sapi.World.ElapsedMilliseconds;
        while (FireWatch.Started.TryDequeue(out var pos))
        {
            if (GetAt(pos) is not { } colony || !IsLooseFire(pos)) continue;
            if (!colonyFires.TryGetValue(colony.ColonyId, out var fires)) colonyFires[colony.ColonyId] = fires = new HashSet<BlockPos>();
            if (!fires.Add(pos)) continue;
            if (now - fireAlarmMs.GetValueOrDefault(colony.ColonyId, long.MinValue / 2) >= FireAlarmEveryMs)
            {
                fireAlarmMs[colony.ColonyId] = now;
                LogEvent(colony, "event-fire", NearestPlaceName(colony, pos));
            }
        }
        var ba = sapi.World.BlockAccessor;
        foreach (var fires in colonyFires.Values)
        {
            // Burnt out, put out, or its land unloaded (nobody can reach it there).
            fires.RemoveWhere(p => ba.GetChunkAtBlockPos(p) == null || !IsLooseFire(p));
        }
        foreach (var gone in fireFighters.Keys.Where(p => !colonyFires.Values.Any(f => f.Contains(p))).ToList()) fireFighters.Remove(gone);
    }

    /// <summary>The building nearest a spot, by name, for the alarm; the Town Hall when there is none close.</summary>
    string NearestPlaceName(Colony colony, BlockPos pos)
    {
        var near = colony.Buildings.OrderBy(b => b.HutPos.DistanceSqTo(pos.X, pos.Y, pos.Z)).FirstOrDefault();
        return near != null ? BuildingName(near) : colony.Name;
    }

    /// <summary>True when the spot holds loose fire: a burning fire block, or a burning pile that is not a pit kiln.</summary>
    public bool IsLooseFire(BlockPos pos) => FireWatch.IsLooseFire(sapi.World.BlockAccessor.GetBlockEntity(pos), Config.FirefightingPiles);

    /// <summary>True when the colony has a fire burning on its land.</summary>
    public bool HasFire(Colony colony) => colonyFires.TryGetValue(colony.ColonyId, out var fires) && fires.Count > 0;

    /// <summary>
    /// The fire a settler should go for: the one it is already on, else the nearest within the alarm range that
    /// has fewer than FirefightersPerFire settlers on it. Null when there is none.
    /// </summary>
    public BlockPos? FireToFight(Colony colony, Entity settler, System.Func<BlockPos, bool> skip)
    {
        if (!colonyFires.TryGetValue(colony.ColonyId, out var fires) || fires.Count == 0) return null;
        long id = settler.EntityId;
        var mine = fireFighters.FirstOrDefault(kv => kv.Value.Contains(id)).Key;
        if (mine != null && fires.Contains(mine) && !skip(mine)) return mine;
        double range = Math.Max(8, Config.FireAlarmRadius);
        var here = settler.Pos.XYZ;
        BlockPos? best = null;
        double bestDist = range * range;
        foreach (var pos in fires)
        {
            double dist = pos.DistanceSqTo(here.X, here.Y, here.Z);
            if (dist >= bestDist || skip(pos)) continue;
            if (fireFighters.TryGetValue(pos, out var on) && on.Count(e => e != id) >= Math.Max(1, Config.FirefightersPerFire)) continue;
            best = pos;
            bestDist = dist;
        }
        return best;
    }

    /// <summary>Puts a settler down as fighting a fire, taking it off any other.</summary>
    public void JoinFire(BlockPos fire, Entity settler)
    {
        LeaveFire(settler);
        if (!fireFighters.TryGetValue(fire, out var on)) fireFighters[fire] = on = new HashSet<long>();
        on.Add(settler.EntityId);
    }

    public void LeaveFire(Entity settler)
    {
        foreach (var on in fireFighters.Values) on.Remove(settler.EntityId);
    }

    /// <summary>Puts out the fire at a spot, as a watering can does. False when nothing was burning there.</summary>
    public bool PutOutFire(Colony colony, BlockPos pos)
    {
        var be = sapi.World.BlockAccessor.GetBlockEntity(pos);
        if (!FireWatch.IsLooseFire(be, Config.FirefightingPiles)) return false;
        if (be is BlockEntityGroundStorage pile) pile.Extinguish();
        else be!.GetBehavior<BEBehaviorBurning>()?.KillFire(false);
        sapi.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), pos, 0, null, false, 16);
        if (colonyFires.TryGetValue(colony.ColonyId, out var fires)) fires.Remove(pos);
        fireFighters.Remove(pos);
        return true;
    }

    /// <summary>Tells the Town Hall's log that a fire has nobody to fight it with, at most every few minutes.</summary>
    public void NoWateringCanAlarm(Colony colony)
    {
        long now = sapi.World.ElapsedMilliseconds;
        if (now - noCanAlarmMs.GetValueOrDefault(colony.ColonyId, long.MinValue / 2) < NoCanAlarmEveryMs) return;
        noCanAlarmMs[colony.ColonyId] = now;
        LogEvent(colony, "event-fire-nocan");
    }

    // ---- Watering cans and water ----------------------------------------------------------

    static float CanWater(ItemStack? stack) => stack?.Block is BlockWateringCan can ? can.GetRemainingWateringSeconds(stack) : 0;

    /// <summary>How many seconds of water a settler has in its watering cans, in its hand and in its inventory, and whether it has a can.</summary>
    public (bool HasCan, float Seconds) FireWater(CitizenRecord citizen, ItemSlot? hand)
    {
        var (hasCan, seconds) = WaterCarried(citizen);
        if (hand?.Itemstack is { } held && IsWateringCan(held)) return (true, seconds + CanWater(held));
        return (hasCan, seconds);
    }

    /// <summary>
    /// The nearest chest with a watering can that a settler may take for a fire: in its home, the warehouse,
    /// or any hut's storage (the Farmer's Hut keeps them).
    /// </summary>
    public BlockEntityContainer? WateringCanSupply(Colony colony, CitizenRecord citizen, Vec3d from)
    {
        IEnumerable<BlockEntityContainer> chests = WarehouseContainers(colony);
        if (HomeOf(colony, citizen) is { } home) chests = HomeChests(home).Concat(chests);
        foreach (var hut in colony.Buildings) chests = chests.Concat(HutStorage(colony, hut));
        return chests.Distinct()
            .Where(c => c.Inventory.Any(s => !s.Empty && IsWateringCan(s.Itemstack)))
            .OrderBy(c => c.Pos.DistanceSqTo(from.X, from.Y, from.Z))
            .FirstOrDefault();
    }

    /// <summary>Pours water from the can in the settler's hand onto a fire. False when the can is empty.</summary>
    public bool PourOnFire(Colony colony, ItemSlot hand, BlockPos fire)
    {
        if (hand.Itemstack?.Block is not BlockWateringCan can) return false;
        float left = can.GetRemainingWateringSeconds(hand.Itemstack);
        if (left <= 0) return false;
        can.SetRemainingWateringSeconds(hand.Itemstack, Math.Max(0, left - Math.Max(0.1f, Config.FirePourSeconds)));
        hand.MarkDirty();
        sapi.World.PlaySoundAt(new AssetLocation("sounds/effect/watering"), fire, 0);
        PutOutFire(colony, fire);
        return true;
    }

    /// <summary>Fills the can in the settler's hand, and every can in its inventory, from the water at pos.</summary>
    public void RefillCansAndHand(Colony colony, CitizenRecord citizen, ItemSlot? hand, BlockPos water)
    {
        if (hand?.Itemstack?.Block is BlockWateringCan can)
        {
            can.SetRemainingWateringSeconds(hand.Itemstack, can.CapacitySeconds);
            hand.MarkDirty();
        }
        RefillCans(colony, citizen, water);
    }

    // ---- Keeping fires from starting ------------------------------------------------------

    /// <summary>
    /// Cuts the grass, flowers and other plants around a spot where a fire will be lit (a pit kiln), so the
    /// fire has nothing to spread through. Crops are left alone. What they drop goes to the settler.
    /// </summary>
    public void ClearAroundFire(Colony colony, CitizenRecord settler, BlockPos center)
    {
        int r = Config.KilnClearRadius;
        if (r <= 0) return;
        var ba = sapi.World.BlockAccessor;
        var pos = new BlockPos(center.dimension);
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        for (int dy = -1; dy <= 2; dy++)
        {
            pos.Set(center.X + dx, center.Y + dy, center.Z + dz);
            var block = ba.GetBlock(pos);
            if (block.Id == 0 || block.BlockMaterial != EnumBlockMaterial.Plant || block.Replaceable < 6000) continue;
            if (block is BlockCrop || ba.GetBlockEntity(pos.DownCopy()) is BlockEntityFarmland || !CanWorkAt(colony, pos)) continue;
            var drops = DropsOf(block, pos);
            BreakQuietly(pos);
            foreach (var drop in drops)
            {
                AddToPack(colony, settler, drop);
                if (drop.StackSize > 0) sapi.World.SpawnItemEntity(drop, pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }
    }
}

/// <summary>
/// Hears from the game when something catches fire. The game has no event for it, so this hooks the start of a
/// fire's burning (BEBehaviorBurning, used by fire blocks) and of a burning pile (ground storage) by name; if a
/// game update renames them, firefighting is off with a warning and the rest of the mod works on.
/// </summary>
static class FireWatch
{
    /// <summary>Spots that started burning since the last fire tick. Only the server side adds to it.</summary>
    public static readonly ConcurrentQueue<BlockPos> Started = new();

    public static void Patch(Harmony harmony)
    {
        var burning = AccessTools.Method(typeof(BEBehaviorBurning), "initSoundsAndTicking")
            ?? throw new MissingMethodException(nameof(BEBehaviorBurning), "initSoundsAndTicking");
        var pile = AccessTools.Method(typeof(BlockEntityGroundStorage), "UpdateBurningState")
            ?? throw new MissingMethodException(nameof(BlockEntityGroundStorage), "UpdateBurningState");
        harmony.Patch(burning, postfix: new HarmonyMethod(typeof(FireWatch), nameof(BurningStarted)));
        harmony.Patch(pile, postfix: new HarmonyMethod(typeof(FireWatch), nameof(PileStarted)));
    }

    static void BurningStarted(BEBehaviorBurning __instance)
    {
        var be = __instance.Blockentity;
        if (be?.Api?.Side != EnumAppSide.Server || !__instance.IsBurning || be is BlockEntityGroundStorage) return;
        if (IsFireBlock(be)) Started.Enqueue(be.Pos.Copy());
    }

    static void PileStarted(BlockEntityGroundStorage __instance)
    {
        if (__instance.Api?.Side != EnumAppSide.Server || !__instance.IsBurning || __instance is BlockEntityPitKiln) return;
        Started.Enqueue(__instance.Pos.Copy());
    }

    static bool IsFireBlock(BlockEntity be) => be.Block?.Code?.Path == "fire";

    /// <summary>A burning fire block, or (when piles count) a burning pile that is not a pit kiln.</summary>
    public static bool IsLooseFire(BlockEntity? be, bool piles) => be switch
    {
        null or BlockEntityPitKiln => false,
        BlockEntityGroundStorage pile => piles && pile.IsBurning,
        _ => IsFireBlock(be) && be.GetBehavior<BEBehaviorBurning>()?.IsBurning == true
    };
}
