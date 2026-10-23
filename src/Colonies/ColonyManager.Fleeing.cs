using System;
using System.Collections.Generic;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// Settlers who are not guards run when a creature hurts them: home (their bed, else their work hut,
/// else the Town Hall) or to a guard, whichever is closer and does not mean running past the attacker.
/// The blow raises the alarm as before, so the nearest guard comes for the attacker. The running itself
/// is AiTaskSettlerFlee.
/// </summary>
public partial class ColonyManager
{
    /// <summary>Settlers running away right now, by entity id: who from, when they may stop (ms), and how many blows so far. Not saved.</summary>
    readonly Dictionary<long, (long Attacker, long Until, int Hits)> fleeing = new();

    /// <summary>True for settlers who run when hurt: everyone but guards, when FleeWhenAttacked is on.</summary>
    public bool FleesWhenHurt(CitizenRecord citizen) => Config.FleeWhenAttacked && citizen.JobCode != Jobs.Guard && !citizen.IsDowned;

    /// <summary>A creature that makes a settler run when it hurts it: any living creature but players and other settlers.</summary>
    static bool IsAttackerToFlee(Entity? e) => e is EntityAgent { Alive: true } && e is not EntityPlayer && e is not EntitySettler;

    void StartFleeing(Colony colony, CitizenRecord citizen, EntitySettler settler, Entity attacker)
    {
        int hits = FleeState(settler) is { } running ? running.Hits + 1 : 1;
        if (hits == 1) LogEvent(colony, "event-fled", citizen.Name, attacker.GetName());
        fleeing[settler.EntityId] = (attacker.EntityId, sapi.World.ElapsedMilliseconds + (long)(Config.FleeSeconds * 1000), hits);
    }

    /// <summary>The creature this settler runs from, or null when it is not running (or the attacker is dead, gone, or long ago).</summary>
    public Entity? FleeingFrom(Entity settler) => FleeState(settler)?.Attacker;

    /// <summary>The creature this settler runs from and how many blows it has taken while running.</summary>
    public (Entity Attacker, int Hits)? FleeState(Entity settler)
    {
        if (!fleeing.TryGetValue(settler.EntityId, out var f)) return null;
        var attacker = sapi.World.GetEntityById(f.Attacker);
        if (!settler.Alive || f.Until < sapi.World.ElapsedMilliseconds || attacker == null || !attacker.Alive)
        {
            fleeing.Remove(settler.EntityId);
            return null;
        }
        return (attacker, f.Hits);
    }

    public void StopFleeing(Entity settler) => fleeing.Remove(settler.EntityId);

    /// <summary>
    /// Where a hurt settler runs: home, or the guard it is safest to run to. Each choice costs its distance,
    /// three times as much when the way there leads towards the attacker or it ends next to it; a guard costs
    /// a quarter less, since it fights back. Home is the bed, else the work hut, else the Town Hall.
    /// </summary>
    public (Vec3d Spot, EntityAgent? Guard) FleeDestination(Colony colony, CitizenRecord citizen, EntityAgent settler, Entity attacker)
    {
        var here = settler.Pos.XYZ;
        var from = attacker.Pos.XYZ;
        BlockPos homePos = citizen.HomeBedPos ?? citizen.WorkplacePos ?? colony.TownHallPos;
        Vec3d best = homePos.ToVec3d().Add(0.5, 0, 0.5);
        double bestCost = FleeCost(here, from, best);
        EntityAgent? bestGuard = null;

        double reach = Config.FleeGuardRadius;
        foreach (var other in colony.Citizens)
        {
            if (!other.Alive || other.IsDowned || other.JobCode != Jobs.Guard) continue;
            if (sapi.World.GetEntityById(other.EntityId) is not EntityAgent { Alive: true } guard || GuardsRetreating.Contains(guard.EntityId)) continue;
            if (guard.Pos.SquareDistanceTo(here) > reach * reach) continue;
            double cost = FleeCost(here, from, guard.Pos.XYZ) * 0.75;
            if (cost < bestCost)
            {
                bestCost = cost;
                best = guard.Pos.XYZ;
                bestGuard = guard;
            }
        }
        return (best, bestGuard);
    }

    static double FleeCost(Vec3d here, Vec3d attacker, Vec3d to)
    {
        double tx = to.X - here.X, tz = to.Z - here.Z;
        double ax = attacker.X - here.X, az = attacker.Z - here.Z;
        double toLen = Math.Sqrt(tx * tx + tz * tz), atLen = Math.Sqrt(ax * ax + az * az);
        double cost = to.DistanceTo(here);
        // Running past the attacker: it stands between here and there, roughly in the way.
        bool pastIt = toLen > 1 && atLen > 0.1 && atLen < toLen && (tx * ax + tz * az) / (toLen * atLen) > 0.6;
        bool endsBesideIt = to.SquareDistanceTo(attacker) < 4 * 4;
        return pastIt || endsBesideIt ? cost * 3 : cost;
    }

    /// <summary>A spot about this far straight away from the attacker where the settler can stand, or null.</summary>
    public Vec3d? RunAwaySpot(EntityAgent settler, Entity attacker, double distance)
    {
        double dx = settler.Pos.X - attacker.Pos.X, dz = settler.Pos.Z - attacker.Pos.Z;
        double len = Math.Sqrt(dx * dx + dz * dz);
        if (len < 0.1) { dx = 1; dz = 0; len = 1; }
        var ba = sapi.World.BlockAccessor;
        var column = new BlockPos((int)Math.Floor(settler.Pos.X + dx / len * distance), (int)settler.Pos.Y, (int)Math.Floor(settler.Pos.Z + dz / len * distance));
        if (ba.GetChunkAtBlockPos(column) == null) return null;
        // Out in the open the ground may rise or fall on the way: look on the surface there too.
        return FreeSpotNear(settler, column, 3) ?? FreeSpotNear(settler, new BlockPos(column.X, ba.GetTerrainMapheightAt(column) + 1, column.Z), 3);
    }
}
