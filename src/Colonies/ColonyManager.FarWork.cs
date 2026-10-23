using System.Collections.Generic;
using System.Linq;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Colonies;

/// <summary>
/// Keeps foresters, farmers and guards going when they work far from every player. The game only
/// runs creatures near players and unloads the land no one is near, which would freeze a forester
/// 300 blocks out. So while such a settler is far from everyone, it stays active and the chunk columns
/// around it stay loaded; once a player comes near or it walks home, they are let go again.
/// </summary>
public partial class ColonyManager
{
    const int FarWorkTickMs = 1000;
    /// <summary>Chunk columns kept loaded around a far settler in each direction: 1 keeps a 3 by 3 block of them.</summary>
    const int FarKeepColumns = 1;

    readonly HashSet<Vec2i> keptColumns = new();
    /// <summary>Columns no settler needs any more, unloaded once no player is near them either.</summary>
    readonly HashSet<Vec2i> releasingColumns = new();

    static bool WorksFarOut(string? job) => job is Jobs.Forester or Jobs.Farmer or Jobs.Guard or Jobs.Prospector or Jobs.Miner or Jobs.Quarrier;

    void OnFarWorkTick(float dt)
    {
        var players = sapi.World.AllOnlinePlayers.Select(p => p.Entity).Where(e => e != null).ToList();
        var want = new HashSet<Vec2i>();
        int size = GlobalConstants.ChunkSize;
        long farSq = (long)Config.FarWorkPlayerDistance * Config.FarWorkPlayerDistance;

        foreach (var colony in colonies.Values)
        {
            foreach (var citizen in colony.Citizens)
            {
                if (!citizen.Alive || !WorksFarOut(citizen.JobCode)) continue;
                if (sapi.World.GetEntityById(citizen.EntityId) is not EntitySettler entity || !entity.Alive) continue;
                // With nobody online the world is left to rest, as the game does.
                bool far = players.Count > 0 && players.All(p => HorizontalDistanceSq(p.Pos.XYZ, entity.Pos.XYZ) > farSq);
                if (entity.AlwaysActive != far) entity.AlwaysActive = far;
                if (!far) continue;
                int cx = (int)entity.Pos.X / size, cz = (int)entity.Pos.Z / size;
                for (int dx = -FarKeepColumns; dx <= FarKeepColumns; dx++)
                for (int dz = -FarKeepColumns; dz <= FarKeepColumns; dz++)
                {
                    want.Add(new Vec2i(cx + dx, cz + dz));
                }
            }
        }

        foreach (var column in want)
        {
            releasingColumns.Remove(column);
            if (!keptColumns.Add(column)) continue;
            sapi.WorldManager.LoadChunkColumnPriority(column.X, column.Y, new ChunkLoadOptions { KeepLoaded = true });
        }
        foreach (var column in keptColumns.Where(c => !want.Contains(c)).ToList())
        {
            keptColumns.Remove(column);
            releasingColumns.Add(column);
        }

        // A column is only unloaded when no player is near it, so no one sees land vanish.
        long releaseSq = (long)(Config.FarWorkPlayerDistance + 2 * size) * (Config.FarWorkPlayerDistance + 2 * size);
        foreach (var column in releasingColumns.ToList())
        {
            var center = new Vec3d(column.X * size + size / 2, 0, column.Y * size + size / 2);
            if (players.Any(p => HorizontalDistanceSq(p.Pos.XYZ, center) <= releaseSq)) continue;
            sapi.WorldManager.UnloadChunkColumn(column.X, column.Y);
            releasingColumns.Remove(column);
        }
    }

    static double HorizontalDistanceSq(Vec3d a, Vec3d b)
    {
        double dx = a.X - b.X, dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }
}
