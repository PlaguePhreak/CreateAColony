using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Ladders settlers carry and put up against a cliff they cannot walk up, as Aaron asked: workers keep a
/// few ladders in their inventory (topped up from their hut's storage), and when there is no way up and no
/// ladder nearby they put a column of them against the rock and climb it. The ladders stay, so the next
/// trip (and every other settler) climbs the same way.
/// </summary>
public partial class ColonyManager
{
    /// <summary>A ladder of any kind: anything with the game's ladder behaviour.</summary>
    public static bool IsLadderStack(ItemStack? stack) => stack?.Block is { } block && block.HasBehavior<BlockBehaviorLadder>();

    /// <summary>
    /// The best cliff face within radius to put ladders against, towards goal: a spot to stand on at the
    /// foot, a wall rising 2 to have blocks in front of it, and room to step off at the top. Null if none.
    /// </summary>
    public LadderClimb? FindScaffoldSpot(Colony colony, EntityAgent entity, Vec3d goal, int radius, int have, ICollection<BlockPos> skip)
    {
        var ba = sapi.World.BlockAccessor;
        var here = entity.Pos.AsBlockPos;
        if (goal.Y - here.Y < 2 || have < 2) return null;
        int maxHeight = Math.Min(have, Math.Max(2, Config.ScaffoldMaxHeight));

        LadderClimb? best = null;
        double bestScore = double.MaxValue;
        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx * dx + dz * dz > radius * radius) continue;
            var foot = new BlockPos(here.X + dx, here.Y + dy, here.Z + dz, here.dimension);
            if (ba.GetChunkAtBlockPos(foot) == null || skip.Contains(foot)) continue;
            if (!Open(ba, foot) || !Open(ba, foot.UpCopy()) || !HasFloor(ba, foot)) continue;
            foreach (var face in BlockFacing.HORIZONTALS)
            {
                // How high the wall in front rises with open space in front of it.
                int height = 0;
                while (height <= maxHeight && Open(ba, foot.UpCopy(height)) && Holds(ba, foot.AddCopy(face).Up(height), face)) height++;
                if (height < 2 || height > maxHeight) continue;
                var top = foot.AddCopy(face).Up(height);
                if (!Open(ba, top) || !Open(ba, top.UpCopy()) || !Open(ba, foot.UpCopy(height)) || !Open(ba, foot.UpCopy(height + 1))) continue;
                var ladders = Enumerable.Range(0, height).Select(i => foot.UpCopy(i)).ToList();
                if (ladders.Any(p => !CanWorkAt(colony, p))) continue;

                var start = new Vec3d(foot.X + 0.5, foot.Y, foot.Z + 0.5);
                var exit = new Vec3d(top.X + 0.5, top.Y, top.Z + 0.5);
                double score = start.DistanceTo(entity.Pos.XYZ) + Horizontal(exit, goal) + 2 * Math.Abs(goal.Y - exit.Y) - height;
                if (score >= bestScore) continue;
                bestScore = score;
                best = new LadderClimb { Start = start, Exit = exit, Column = foot.Copy(), Up = true, ToPlace = ladders, WallSide = face };
            }
        }
        return best;
    }

    /// <summary>Puts the climb's ladders up from the settler's inventory. False (nothing placed) if it has too few or the spot changed.</summary>
    public bool PlaceScaffold(Colony colony, CitizenRecord citizen, LadderClimb climb)
    {
        var ba = sapi.World.BlockAccessor;
        if (climb.ToPlace == null || climb.WallSide == null) return false;
        if (climb.ToPlace.Any(p => !Open(ba, p)) || PackCount(citizen, IsLadderStack) < climb.ToPlace.Count) return false;
        var stack = TakeFromPack(colony, citizen, IsLadderStack, climb.ToPlace.Count);
        if (stack?.Block == null) return false;
        var ladder = sapi.World.GetBlock(stack.Block.CodeWithVariant("side", climb.WallSide.Code)) ?? stack.Block;
        int placed = 0;
        foreach (var pos in climb.ToPlace)
        {
            if (placed >= stack.StackSize) break;
            ba.SetBlock(ladder.Id, pos);
            ba.MarkBlockDirty(pos);
            placed++;
        }
        stack.StackSize -= placed;
        if (stack.StackSize > 0) AddToPack(colony, citizen, stack);
        return placed == climb.ToPlace.Count;
    }

    /// <summary>Takes ladders from the hut's storage until the settler carries ScaffoldCarry of them.</summary>
    public void TopUpLadders(Colony colony, CitizenRecord citizen, IEnumerable<BlockEntityContainer> storage)
    {
        if (!Config.ScaffoldClimbing) return;
        int want = Config.ScaffoldCarry - PackCount(citizen, IsLadderStack);
        if (want <= 0 || PackFull(citizen)) return;
        if (TakeItems(storage, IsLadderStack, want) is { } ladders) AddToPack(colony, citizen, ladders);
    }

    /// <summary>Air, grass and the like: nothing a settler bumps into, and no water.</summary>
    static bool Open(IBlockAccessor ba, BlockPos pos)
    {
        var block = ba.GetBlock(pos);
        if (ba.GetBlock(pos, BlockLayersAccess.Fluid).Id != 0) return false;
        if (block.Id == 0) return true;
        var boxes = block.GetCollisionBoxes(ba, pos);
        return block.Replaceable >= 6000 && (boxes == null || boxes.Length == 0);
    }

    static bool HasFloor(IBlockAccessor ba, BlockPos foot)
    {
        var below = foot.DownCopy();
        var boxes = ba.GetBlock(below).GetCollisionBoxes(ba, below);
        return boxes != null && boxes.Length > 0;
    }

    /// <summary>A block a ladder in front of it (on the side facing away from face) can hang on.</summary>
    static bool Holds(IBlockAccessor ba, BlockPos wall, BlockFacing face) => ba.GetBlock(wall).SideSolid[face.Opposite.Index];

    static double Horizontal(Vec3d a, Vec3d b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
}
