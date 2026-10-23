using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// Keeping settlers out of walls: builders do not place a block where someone stands, and a settler
/// that ends up inside a block (a player built over it) is moved to the nearest free spot.
/// </summary>
public partial class ColonyManager
{
    /// <summary>A living creature whose body would end up inside this block if it were placed at pos, or null.</summary>
    public Entity? EntityInTheWay(Block block, BlockPos pos)
    {
        var boxes = block.CollisionBoxes;
        if (boxes == null || boxes.Length == 0) return null;
        var center = pos.ToVec3d().Add(0.5, 0.5, 0.5);
        foreach (var e in sapi.World.GetEntitiesAround(center, 2, 3, e => e is EntityAgent && e.Alive))
        {
            var body = BodyAt(e, e.Pos.X, e.Pos.Y, e.Pos.Z);
            if (boxes.Any(b => b.ToDouble().Translate(pos.X, pos.Y, pos.Z).Intersects(body))) return e;
        }
        return null;
    }

    /// <summary>True when the entity's body is inside the block space at pos.</summary>
    public bool Overlaps(Entity entity, BlockPos pos) =>
        BodyAt(entity, entity.Pos.X, entity.Pos.Y, entity.Pos.Z).Intersects(new Cuboidd(pos.X, pos.Y, pos.Z, pos.X + 1, pos.Y + 1, pos.Z + 1));

    /// <summary>
    /// The nearest spot around a position where the entity can stand: its body fits, there is floor under
    /// it, and it does not overlap the block at avoid. Null if there is none within the radius.
    /// </summary>
    public Vec3d? FreeSpotNear(Entity entity, BlockPos around, int radius, BlockPos? avoid = null)
    {
        var ba = sapi.World.BlockAccessor;
        var avoidBox = avoid == null ? null : new Cuboidd(avoid.X, avoid.Y, avoid.Z, avoid.X + 1, avoid.Y + 1, avoid.Z + 1);
        int[] heights = { 0, 1, -1, 2 };
        for (int r = 0; r <= radius; r++)
        {
            Vec3d? best = null;
            double bestDist = double.MaxValue;
            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue; // only the ring at this distance
                foreach (int dy in heights)
                {
                    var p = around.AddCopy(dx, dy, dz);
                    var spot = new Vec3d(p.X + 0.5, p.Y, p.Z + 0.5);
                    if (sapi.World.CollisionTester.IsColliding(ba, entity.CollisionBox, spot, false)) continue;
                    var floor = ba.GetBlock(p.DownCopy()).GetCollisionBoxes(ba, p.DownCopy());
                    if (floor == null || floor.Length == 0) continue;
                    if (avoidBox != null && BodyAt(entity, spot.X, spot.Y, spot.Z).Intersects(avoidBox)) continue;
                    double dist = spot.SquareDistanceTo(entity.Pos.XYZ);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = spot;
                    }
                    break; // the lowest free height in this column will do
                }
            }
            if (best != null) return best;
        }
        return null;
    }

    /// <summary>The entity's collision box at a position, a little smaller so touching a wall is not "inside" it.</summary>
    static Cuboidd BodyAt(Entity e, double x, double y, double z)
    {
        var box = e.CollisionBox.ToDouble().Translate(x, y, z);
        const double inset = 0.05;
        return new Cuboidd(box.X1 + inset, box.Y1 + inset, box.Z1 + inset, box.X2 - inset, box.Y2 - inset, box.Z2 - inset);
    }
}
