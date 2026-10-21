using System.Collections.Generic;
using System.Linq;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace CreateAColony.Colonies;

/// <summary>
/// Guards: settlers with the Guard job. Each Forester's or Farmer's Hut set to "Escort by guard" gets
/// one guard, in hut order, who follows its worker and fights monsters near it. Guards left over stay
/// by the Town Hall and fight what comes near it. A guard fights with the best weapon in its inventory,
/// fetching one from the warehouse if it has none, or with bare hands.
/// </summary>
public partial class ColonyManager
{
    static readonly HashSet<EnumTool> Weapons = new()
    {
        EnumTool.Sword, EnumTool.Spear, EnumTool.Club, EnumTool.Mace, EnumTool.Warhammer, EnumTool.Poleaxe,
        EnumTool.Halberd, EnumTool.Pike, EnumTool.Polearm, EnumTool.Knife
    };

    public static bool IsWeapon(ItemStack stack) => stack.Collectible.Tool is { } tool && Weapons.Contains(tool);

    /// <summary>How hard a weapon hits; the better of two weapons is the one that hits harder.</summary>
    public static float WeaponDamage(ItemStack stack) => stack.Collectible.GetAttackPower(stack);

    /// <summary>Workers whose hut asks for an escort, each paired with a guard in order.</summary>
    List<(CitizenRecord Guard, CitizenRecord Worker, Building Hut)> Escorts(Colony colony)
    {
        var guards = colony.Citizens.Where(c => c.Alive && !c.IsDowned && c.JobCode == Jobs.Guard).OrderBy(c => c.CitizenId).ToList();
        var pairs = new List<(CitizenRecord, CitizenRecord, Building)>();
        foreach (var hut in colony.Buildings.Where(b => b.Escort && GatherHuts.ContainsKey(b.Type)).OrderBy(b => b.BuildingId))
        {
            if (pairs.Count >= guards.Count) break;
            if (WorkerAt(colony, hut, GatherHuts[hut.Type].Job) is { } worker) pairs.Add((guards[pairs.Count], worker, hut));
        }
        return pairs;
    }

    /// <summary>The worker this guard escorts, or null when it guards the Town Hall.</summary>
    public CitizenRecord? EscortedBy(Colony colony, CitizenRecord guard) =>
        Escorts(colony).Find(p => p.Guard == guard).Worker;

    /// <summary>The guard escorting this hut's worker, or null.</summary>
    public CitizenRecord? GuardOf(Colony colony, Building hut) =>
        Escorts(colony).Find(p => p.Hut == hut).Guard;

    public int GuardCount(Colony colony) => colony.Citizens.Count(c => c.Alive && c.JobCode == Jobs.Guard);

    /// <summary>A living creature guards fight: one of the GuardTargets in the config.</summary>
    public bool IsThreat(Entity e) =>
        e.Alive && e is EntityAgent && e is not EntityPlayer && e is not EntitySettler
        && Config.GuardTargets.Any(pattern => WildcardUtil.Match(pattern, e.Code.Path));

    /// <summary>The nearest threat within the guard radius of a spot, or null.</summary>
    public Entity? FindThreat(Vec3d around) =>
        sapi.World.GetNearestEntity(around, Config.GuardThreatRadius, Config.GuardThreatRadius / 2, IsThreat);

    /// <summary>A warehouse chest holding a weapon, or null.</summary>
    public BlockPos? WarehouseWeapon(Colony colony) =>
        WarehouseContainers(colony).FirstOrDefault(c => c.Inventory.Any(s => !s.Empty && IsWeapon(s.Itemstack)))?.Pos;
}
