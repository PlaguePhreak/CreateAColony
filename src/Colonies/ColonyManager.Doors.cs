using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CreateAColony.Settlers;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Settlers go through doors and fence gates. The game's pathfinder treats a closed door as a wall, so
/// settlers walked around buildings or gave up. While it plans a path for a settler it now counts a door
/// or gate that can be opened by hand as open; the settler opens it when it gets there and closes it
/// again once nobody is in the way.
/// </summary>
public partial class ColonyManager
{
    const int DoorTickMs = 500;
    /// <summary>A door a settler opened stays open at least this long, so the settler is through it.</summary>
    const long DoorOpenMinMs = 1500;

    /// <summary>Doors and gates settlers opened, by position, with when they were opened.</summary>
    readonly Dictionary<BlockPos, long> doorsOpened = new();
    Harmony? harmony;

    void StartDoors()
    {
        harmony = new Harmony("createacolony.doors");
        DoorPathing.Patch(harmony);
        PathBlocks.Init(sapi.World, Config);
        PathBlocks.Patch(harmony);
        StepCheck.Patch(harmony);
        sapi.Event.RegisterGameTickListener(OnDoorTick, DoorTickMs);
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll("createacolony.doors");
        harmony = null;
        base.Dispose();
    }

    /// <summary>Opens the closed doors and gates just ahead of a walking settler.</summary>
    public void OpenDoorsAhead(Entity settler)
    {
        DoorPathing.Register(settler);
        var ba = sapi.World.BlockAccessor;
        var pos = settler.Pos;
        var motion = pos.Motion;
        bool moving = motion.X * motion.X + motion.Z * motion.Z > 1e-6;
        const double reach = 0.8;
        var box = settler.CollisionBox;
        int x1 = (int)Math.Floor(pos.X + box.X1 - reach), x2 = (int)Math.Floor(pos.X + box.X2 + reach);
        int z1 = (int)Math.Floor(pos.Z + box.Z1 - reach), z2 = (int)Math.Floor(pos.Z + box.Z2 + reach);
        int y = (int)Math.Floor(pos.Y + 0.1);
        var cell = new BlockPos(pos.Dimension);
        for (int x = x1; x <= x2; x++)
        for (int z = z1; z <= z2; z++)
        {
            // Only doors the settler is walking towards, not ones it passes alongside.
            if (moving && (x + 0.5 - pos.X) * motion.X + (z + 0.5 - pos.Z) * motion.Z < 0) continue;
            for (int dy = 0; dy <= 1; dy++)
            {
                cell.Set(x, y + dy, z);
                if (DoorPathing.ClosedDoorAt(ba, cell) is not { } door) continue;
                if (DoorPathing.SetOpen(sapi.World, door, true)) doorsOpened[door.Copy()] = sapi.World.ElapsedMilliseconds;
            }
        }
    }

    /// <summary>Closes the doors settlers opened once nobody stands in them.</summary>
    void OnDoorTick(float dt)
    {
        if (doorsOpened.Count == 0) return;
        long now = sapi.World.ElapsedMilliseconds;
        foreach (var (door, openedAt) in doorsOpened.ToList())
        {
            if (now - openedAt < DoorOpenMinMs) continue;
            if (sapi.World.BlockAccessor.GetChunkAtBlockPos(door) == null)
            {
                doorsOpened.Remove(door);
                continue;
            }
            if (DoorBlocked(door)) continue;
            DoorPathing.SetOpen(sapi.World, door, false);
            doorsOpened.Remove(door);
        }
    }

    /// <summary>
    /// True while a creature is in the doorway or within a step of it, so closing would hit it or shut the door
    /// in the face of a settler about to walk through. (Only right next to it, the door used to close on settlers
    /// that had slowed down to turn into it, and they walked into the closed door.)
    /// </summary>
    bool DoorBlocked(BlockPos door)
    {
        const double margin = 0.9;
        var area = new Cuboidd(door.X - margin, door.Y, door.Z - margin, door.X + 1 + margin, door.Y + 2, door.Z + 1 + margin);
        return sapi.World.GetEntitiesAround(door.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, e => e is EntityAgent && e.Alive)
            .Any(e => e.CollisionBox.ToDouble().Translate(e.Pos.X, e.Pos.Y, e.Pos.Z).Intersects(area));
    }
}

/// <summary>
/// The game's pathfinder sees doors through their collision boxes. While it plans for a settler, these
/// patches leave out the boxes of closed doors and gates that open by hand, so the path goes through them.
/// The pathfinder runs on its own thread, so "planning for a settler" is kept per thread.
/// </summary>
public static class DoorPathing
{
    static readonly ConditionalWeakTable<Cuboidf, object> settlerBoxes = new();
    static readonly object marker = new();
    [ThreadStatic] static bool planning;

    /// <summary>True on the pathfinder's thread while it plans a path for a settler.</summary>
    public static bool Planning => planning;
    static MethodInfo? openOldDoor;

    /// <summary>The pathfinder is given the walker's own collision box, which is how a settler's search is told apart.</summary>
    public static void Register(Entity settler) => settlerBoxes.AddOrUpdate(settler.CollisionBox, marker);

    public static void Patch(Harmony harmony)
    {
        openOldDoor = AccessTools.Method(typeof(BlockBaseDoor), "Open");
        harmony.Patch(AccessTools.Method(typeof(AStar), nameof(AStar.FindPathOrEscapePath)),
            prefix: new HarmonyMethod(typeof(DoorPathing), nameof(PlanStart)),
            finalizer: new HarmonyMethod(typeof(DoorPathing), nameof(PlanEnd)));
        harmony.Patch(AccessTools.Method(typeof(BlockBehaviorDoor), nameof(BlockBehaviorDoor.GetCollisionBoxes)),
            postfix: new HarmonyMethod(typeof(DoorPathing), nameof(DoorBoxes)));
        harmony.Patch(AccessTools.Method(typeof(BlockBehaviorDoor), nameof(BlockBehaviorDoor.MBGetCollisionBoxes)),
            postfix: new HarmonyMethod(typeof(DoorPathing), nameof(DoorPartBoxes)));
        // Fence gates and old-style doors are blocks of their own, with the game's plain collision boxes.
        harmony.Patch(AccessTools.Method(typeof(Block), nameof(Block.GetCollisionBoxes)),
            prefix: new HarmonyMethod(typeof(DoorPathing), nameof(GateBoxes)));
    }

    static void PlanStart(Cuboidf entityCollBox) => planning = entityCollBox != null && settlerBoxes.TryGetValue(entityCollBox, out _);

    static void PlanEnd() => planning = false;

    static void DoorBoxes(BlockBehaviorDoor __instance, IBlockAccessor blockAccessor, BlockPos pos, ref Cuboidf[] __result)
    {
        if (planning && __instance.handopenable && blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>() is { Opened: false }) __result = null!;
    }

    static void DoorPartBoxes(BlockBehaviorDoor __instance, IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset, ref Cuboidf[] __result)
    {
        if (!planning || !__instance.handopenable) return;
        if (blockAccessor.GetBlockEntity(pos.AddCopy(offset.X, offset.Y, offset.Z))?.GetBehavior<BEBehaviorDoor>() is { Opened: false }) __result = null!;
    }

    static bool GateBoxes(Block __instance, ref Cuboidf[] __result)
    {
        if (!planning || __instance is not BlockBaseDoor door || door.IsOpened()) return true;
        __result = null!;
        return false;
    }

    /// <summary>The position of a closed door or gate a settler can open at pos (a door's lower block), or null.</summary>
    public static BlockPos? ClosedDoorAt(IBlockAccessor ba, BlockPos pos)
    {
        var block = ba.GetBlock(pos);
        var at = pos;
        if (block is BlockMultiblock part)
        {
            at = pos.AddCopy(part.OffsetInv.X, part.OffsetInv.Y, part.OffsetInv.Z);
            block = ba.GetBlock(at);
        }
        if (block is BlockBaseDoor old) return old.IsOpened() ? null : at;
        if (block.GetBehavior<BlockBehaviorDoor>() is not { handopenable: true }) return null;
        return ba.GetBlockEntity(at)?.GetBehavior<BEBehaviorDoor>() is { Opened: false } ? at : null;
    }

    /// <summary>Opens or closes a door or gate. False if there is none there, or it already was that way.</summary>
    public static bool SetOpen(IWorldAccessor world, BlockPos pos, bool open)
    {
        var block = world.BlockAccessor.GetBlock(pos);
        try
        {
            if (block is BlockBaseDoor old)
            {
                if (old.IsOpened() == open || openOldDoor == null) return false;
                openOldDoor.Invoke(old, new object?[] { world, null, pos });
                return true;
            }
            var door = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorDoor>();
            if (door == null || door.Opened == open) return false;
            door.ToggleDoorState(null, open);
            return true;
        }
        catch (Exception)
        {
            // A door that can break when used looks for the player who used it; a settler is not one. Leave it shut.
            return false;
        }
    }
}
