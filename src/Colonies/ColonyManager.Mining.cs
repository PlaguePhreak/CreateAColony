using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// The miner's trade, and the digging it shares with the quarrier. A miner picks the best reading the
/// colony's prospectors took (or the ore an officer chose), finds that ore's blocks in the ground there,
/// and digs a staircase down to them from the surface: one block wide, three high where it steps, so
/// settlers can walk it. From the end of the tunnel it digs out the ore around it, then tunnels on to
/// the next ore block, and when the spot is dug out it moves on to the next reading.
/// </summary>
public partial class ColonyManager
{
    public enum MineWork { Travel, Dig }

    /// <summary>What a miner does next: walk to Stand, then (for Dig) dig out Block from there.</summary>
    public readonly record struct MineJob(MineWork Kind, BlockPos Block, BlockPos Stand, bool Ore);

    /// <summary>The ore blocks found around each Miner's Hut's site, so the ground is searched once per site.</summary>
    readonly Dictionary<int, (BlockPos Site, string Ore, List<BlockPos> Blocks)> oreBlocks = new();
    /// <summary>Ore blocks a miner could not get to (water next to the way, someone else's land, too hard for its pick), with when to try again.</summary>
    readonly Dictionary<int, Dictionary<BlockPos, long>> blockedOre = new();
    const long BlockedOreForgetMs = 600_000;

    static readonly BlockFacing[] Horizontals = BlockFacing.HORIZONTALS;

    /// <summary>A block of ore, such as ore-poor-nativecopper-granite.</summary>
    public static bool IsOre(Block block) => block.Code?.Path.StartsWith("ore-") == true;

    /// <summary>A block of the ore with this deposit code, the code the prospecting pick reports.</summary>
    public static bool IsOreOf(Block block, string ore)
    {
        if (!IsOre(block) || ore.Length == 0) return false;
        string path = block.Code.Path;
        return block.Variant["type"] == ore || path.Contains("-" + ore + "-") || path.EndsWith("-" + ore);
    }

    /// <summary>Air, or something a settler walks through (grass, snow, plants).</summary>
    bool IsClear(BlockPos pos)
    {
        var block = sapi.World.BlockAccessor.GetBlock(pos);
        return block.Id == 0 || block.Replaceable >= 6000;
    }

    /// <summary>True when water or lava is in or next to the block, so digging it would flood the way.</summary>
    bool NearLiquid(BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        if (ba.GetBlock(pos, BlockLayersAccess.Fluid).Id != 0) return true;
        foreach (var face in BlockFacing.ALLFACES)
        {
            if (ba.GetBlock(pos.AddCopy(face), BlockLayersAccess.Fluid).Id != 0) return true;
        }
        return false;
    }

    /// <summary>The boxes of the colony's buildings, which miners and quarriers never dig into.</summary>
    List<(BlockPos Min, BlockPos Max)> DigProtected(Colony colony) =>
        colony.Buildings.Select(b => BuildingBox(b)).ToList();

    static bool Inside(BlockPos pos, List<(BlockPos Min, BlockPos Max)> boxes) =>
        boxes.Any(b => pos.X >= b.Min.X && pos.X <= b.Max.X && pos.Y >= b.Min.Y && pos.Y <= b.Max.Y && pos.Z >= b.Min.Z && pos.Z <= b.Max.Z);

    /// <summary>
    /// True when a settler may dig this block out: something solid, not the unbreakable mantle, nothing
    /// with contents (chests, ovens), not part of a colony building, not in someone else's land, and no
    /// water or lava next to it.
    /// </summary>
    public bool CanDig(Colony colony, BlockPos pos, List<(BlockPos Min, BlockPos Max)> boxes)
    {
        var ba = sapi.World.BlockAccessor;
        var block = ba.GetBlock(pos);
        if (block.Id == 0 || block.Code?.Path == "mantle" || block.Resistance > 100) return false;
        if (ba.GetBlockEntity(pos) != null) return false;
        if (pos.Y <= 1 || Inside(pos, boxes) || !CanWorkAt(colony, pos)) return false;
        return !NearLiquid(pos);
    }

    /// <summary>
    /// Digs a block out with the tool in hand, as a player would: what it drops goes into the worker's
    /// inventory (or the hut's storage), and the tool wears down by one use. False if nothing was there.
    /// </summary>
    public bool DigBlock(Colony colony, Building hut, CitizenRecord worker, EntityAgent entity, BlockPos pos, ItemSlot? hand)
    {
        var block = sapi.World.BlockAccessor.GetBlock(pos);
        if (block.Id == 0) return false;
        bool ore = IsOre(block);
        var drops = DropsOf(block, pos);
        BreakQuietly(pos);
        if (hand?.Itemstack != null) WearTool(entity, hand);
        StoreHarvest(colony, hut, worker, drops, pos);
        AddStat(hut, ore ? "ore-mined" : "blocks-dug");
        return true;
    }

    /// <summary>Seconds to dig a block: ore takes half as long again as rock.</summary>
    public float DigSeconds(BlockPos pos, float perBlock) => IsOre(sapi.World.BlockAccessor.GetBlock(pos)) ? perBlock * 1.5f : perBlock;

    // ---- Choosing where to mine -----------------------------------------------------------

    /// <summary>Forgets the miner's site and tunnel, so it picks a new reading.</summary>
    public void ClearMine(Colony colony, Building hut)
    {
        hut.MineSite = null;
        hut.MineSiteOre = "";
        hut.MineEntrance = null;
        hut.MineEnd = null;
        hut.MineTarget = null;
        oreBlocks.Remove(hut.BuildingId);
        blockedOre.Remove(hut.BuildingId);
        MarkDirty(colony);
    }

    /// <summary>The reading the Miner's Hut is working, or null (and the mine forgotten) when it no longer fits.</summary>
    OreSite? CurrentMineSite(Colony colony, Building hut)
    {
        if (hut.MineSite == null) return null;
        var site = colony.OreSites.Find(s => s.Pos.Equals(hut.MineSite));
        if (site != null && !site.MinedOut.Contains(hut.MineSiteOre) && (hut.MineOre.Length == 0 || hut.MineOre == hut.MineSiteOre)) return site;
        ClearMine(colony, hut);
        return null;
    }

    /// <summary>
    /// The strongest reading in the miner's range of the ore it is set to dig (any ore when none is set),
    /// left alone by the colony's other miners, or null.
    /// </summary>
    OreSite? ChooseMineSite(Colony colony, Building hut)
    {
        int radius = GatherRadius(hut);
        var taken = colony.Buildings.Where(b => b != hut && b.Type == BuildingTypes.Miner && b.MineSite != null).Select(b => b.MineSite!).ToList();
        OreSite? bestSite = null;
        string bestOre = "";
        double bestScore = double.MinValue;
        foreach (var site in colony.OreSites)
        {
            double dx = site.Pos.X - hut.HutPos.X, dz = site.Pos.Z - hut.HutPos.Z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            if (distance > radius || taken.Any(t => t.Equals(site.Pos))) continue;
            foreach (var ore in site.Ores)
            {
                if (ore.Factor < Config.MinerMinFactor || site.MinedOut.Contains(ore.Code)) continue;
                if (hut.MineOre.Length > 0 && ore.Code != hut.MineOre) continue;
                // A stronger reading wins; between equal ones, the nearer.
                double score = ore.Factor - distance / 10000;
                if (score <= bestScore) continue;
                (bestSite, bestOre, bestScore) = (site, ore.Code, score);
            }
        }
        if (bestSite == null) return null;
        hut.MineSite = bestSite.Pos.Copy();
        hut.MineSiteOre = bestOre;
        hut.MineEntrance = hut.MineEnd = hut.MineTarget = null;
        MarkDirty(colony);
        return bestSite;
    }

    /// <summary>
    /// Every block of the ore within MinerOreSearchRadius of the site, from the surface down. Null while
    /// some of that ground is not loaded, so it is not taken for dug out.
    /// </summary>
    List<BlockPos>? OreBlocksAt(Building hut, OreSite site, string ore)
    {
        if (oreBlocks.TryGetValue(hut.BuildingId, out var cached) && cached.Site.Equals(site.Pos) && cached.Ore == ore) return cached.Blocks;

        var ba = sapi.World.BlockAccessor;
        int r = Math.Max(1, Config.MinerOreSearchRadius);
        var found = new List<BlockPos>();
        var pos = new BlockPos(0);
        for (int x = site.Pos.X - r; x <= site.Pos.X + r; x++)
        for (int z = site.Pos.Z - r; z <= site.Pos.Z + r; z++)
        {
            pos.Set(x, site.Pos.Y, z);
            if (ba.GetChunkAtBlockPos(pos) == null) return null;
            int top = ba.GetTerrainMapheightAt(pos);
            for (int y = top; y > 1; y--)
            {
                pos.Y = y;
                var block = ba.GetBlock(pos);
                if (block.Code?.Path == "mantle") break;
                if (IsOreOf(block, ore)) found.Add(pos.Copy());
            }
        }
        oreBlocks[hut.BuildingId] = (site.Pos.Copy(), ore, found);
        return found;
    }

    bool IsBlockedOre(Building hut, BlockPos pos)
    {
        if (!blockedOre.TryGetValue(hut.BuildingId, out var blocked) || !blocked.TryGetValue(pos, out long until)) return false;
        if (sapi.World.ElapsedMilliseconds < until) return true;
        blocked.Remove(pos);
        return false;
    }

    /// <summary>Leaves an ore block alone for a while: its way is flooded, in someone else's land, or too hard for the miner's pick.</summary>
    public void BlockOre(Building hut, BlockPos pos)
    {
        if (!blockedOre.TryGetValue(hut.BuildingId, out var blocked)) blockedOre[hut.BuildingId] = blocked = new();
        blocked[pos.Copy()] = sapi.World.ElapsedMilliseconds + BlockedOreForgetMs;
    }

    /// <summary>Steps along the ground and up or down, the longest way first; a staircase can climb one block per step.</summary>
    static int Horizontal(BlockPos a, BlockPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

    /// <summary>
    /// The next ore block to tunnel to. From the end of the tunnel: the nearest one a staircase can reach
    /// (at least one step across for every step up or down). Without a tunnel: the shallowest, nearest the site.
    /// </summary>
    BlockPos? NextOreBlock(Colony colony, Building hut, List<BlockPos> blocks, string ore, OreSite site, System.Func<BlockPos, bool> skip)
    {
        var ba = sapi.World.BlockAccessor;
        blocks.RemoveAll(p => !IsOreOf(ba.GetBlock(p), ore));
        var open = blocks.Where(p => !skip(p) && !IsBlockedOre(hut, p) && CanWorkAt(colony, p)).ToList();
        if (open.Count == 0) return null;
        if (hut.MineEnd is { } end)
        {
            var reachable = open.Where(p => Horizontal(end, p) >= Math.Abs(end.Y - p.Y) + 1)
                .OrderBy(p => Horizontal(end, p) + Math.Abs(end.Y - p.Y)).FirstOrDefault();
            if (reachable != null) return reachable;
            // Nothing a staircase from here reaches: start a new one from the surface.
            hut.MineEnd = null;
        }
        return open.OrderByDescending(p => p.Y).ThenBy(p => Horizontal(site.Pos, p)).First();
    }

    /// <summary>
    /// Where a staircase down to the ore block starts: a spot on the surface far enough away to the side
    /// (one step across for every block down) in the direction nearest the hut. Null if there is none.
    /// </summary>
    BlockPos? PlanEntrance(Colony colony, Building hut, BlockPos ore, List<(BlockPos Min, BlockPos Max)> boxes)
    {
        var ba = sapi.World.BlockAccessor;
        BlockPos? best = null;
        double bestDistance = double.MaxValue;
        foreach (var face in Horizontals)
        {
            var spot = new BlockPos(0);
            int distance = 1;
            bool ok = false;
            for (int tries = 0; tries < 6; tries++)
            {
                spot.Set(ore.X + face.Normali.X * distance, ore.Y, ore.Z + face.Normali.Z * distance);
                if (ba.GetChunkAtBlockPos(spot) == null) break;
                spot.Y = ba.GetTerrainMapheightAt(spot) + 1;
                int need = spot.Y - ore.Y + 1;
                if (distance >= need) { ok = true; break; }
                distance = need;
            }
            if (!ok || spot.Y <= ore.Y) continue;
            if (ba.GetBlock(spot, BlockLayersAccess.Fluid).Id != 0 || ba.GetBlock(spot.DownCopy(), BlockLayersAccess.Fluid).Id != 0) continue;
            if (Inside(spot, boxes) || !CanWorkAt(colony, spot)) continue;
            double d = spot.DistanceTo(hut.HutPos);
            if (d >= bestDistance) continue;
            (best, bestDistance) = (spot.Copy(), d);
        }
        return best;
    }

    /// <summary>
    /// The tunnel from a standing spot to the ore block, as the spots to stand on along the way, each with
    /// the blocks to dig out (top first) to get there. One step across per step, and one up or down while
    /// the height differs, ending beside the ore at its height.
    /// </summary>
    public static List<(BlockPos Feet, List<BlockPos> Digs)> MineRoute(BlockPos from, BlockPos ore)
    {
        var route = new List<(BlockPos, List<BlockPos>)>();
        var at = from.Copy();
        // The starting spot itself must be open (two high).
        route.Add((at.Copy(), new List<BlockPos> { at.UpCopy(), at.Copy() }));
        for (int guard = 0; guard < 512; guard++)
        {
            int dx = ore.X - at.X, dz = ore.Z - at.Z, dy = ore.Y - at.Y;
            if (Math.Abs(dx) + Math.Abs(dz) <= 1 && dy == 0) break;
            if (Math.Abs(dx) + Math.Abs(dz) <= 1) break; // cannot get level from here; the caller checks the reach
            var next = at.Copy();
            if (Math.Abs(dx) >= Math.Abs(dz)) next.X += Math.Sign(dx);
            else next.Z += Math.Sign(dz);
            int vertical = Math.Sign(dy);
            next.Y += vertical;
            var digs = new List<BlockPos>();
            if (vertical < 0) digs.Add(next.UpCopy(2));
            if (vertical > 0) digs.Add(at.UpCopy(2));
            digs.Add(next.UpCopy());
            digs.Add(next.Copy());
            route.Add((next.Copy(), digs));
            at = next;
        }
        return route;
    }

    /// <summary>An ore block within the miner's reach of the tunnel's end with open air beside it, or null.</summary>
    BlockPos? OreInReach(Colony colony, Building hut, BlockPos end, string ore, System.Func<BlockPos, bool> skip, List<(BlockPos Min, BlockPos Max)> boxes)
    {
        var ba = sapi.World.BlockAccessor;
        int r = Math.Max(1, Config.MinerReach);
        BlockPos? best = null;
        double bestDistance = double.MaxValue;
        var eye = end.UpCopy();
        for (int x = -r; x <= r; x++)
        for (int y = -r; y <= r + 1; y++)
        for (int z = -r; z <= r; z++)
        {
            var pos = end.AddCopy(x, y, z);
            if (!IsOreOf(ba.GetBlock(pos), ore) || skip(pos) || IsBlockedOre(hut, pos)) continue;
            double d = pos.DistanceTo(eye);
            if (d > r + 0.5 || d >= bestDistance) continue;
            if (!BlockFacing.ALLFACES.Any(f => IsClear(pos.AddCopy(f)))) continue;
            if (!CanDig(colony, pos, boxes)) continue;
            (best, bestDistance) = (pos, d);
        }
        return best;
    }

    /// <summary>
    /// The miner's next job, and its status for the hut window: walk to the site, dig the next block of the
    /// tunnel, or dig out ore. Null when there is nothing to mine.
    /// </summary>
    public MineJob? NextMineJob(Colony colony, Building hut, System.Func<BlockPos, bool> skip, out string status)
    {
        var ba = sapi.World.BlockAccessor;
        var boxes = DigProtected(colony);
        for (int tries = 0; tries < 6; tries++)
        {
            var site = CurrentMineSite(colony, hut) ?? ChooseMineSite(colony, hut);
            if (site == null)
            {
                status = colony.OreSites.Count == 0 ? "mine-no-readings" : hut.MineOre.Length > 0 ? "mine-no-site-ore" : "mine-no-site";
                return null;
            }
            string ore = hut.MineSiteOre;

            // The ground must be loaded to look for the ore: walk to the site first.
            var blocks = OreBlocksAt(hut, site, ore);
            if (blocks == null)
            {
                status = "mine-travelling";
                var stand = hut.MineEntrance ?? site.Pos.UpCopy();
                return new MineJob(MineWork.Travel, stand, stand, false);
            }

            // Ore around the end of the tunnel comes out first.
            if (hut.MineEnd != null && OreInReach(colony, hut, hut.MineEnd, ore, skip, boxes) is { } near)
            {
                status = "mine-ore";
                return new MineJob(MineWork.Dig, near, hut.MineEnd.Copy(), true);
            }

            if (hut.MineTarget == null || !IsOreOf(ba.GetBlock(hut.MineTarget), ore) || skip(hut.MineTarget) || IsBlockedOre(hut, hut.MineTarget))
            {
                hut.MineTarget = NextOreBlock(colony, hut, blocks, ore, site, skip);
                MarkDirty(colony);
                if (hut.MineTarget == null)
                {
                    if (blocks.Count > 0)
                    {
                        // Ore is left, but none the miner can get to right now.
                        status = "mine-blocked";
                        return null;
                    }
                    site.MinedOut.Add(ore);
                    LogEvent(colony, "event-mine-done", OreName(ore), DirectionFrom(hut.HutPos, site.Pos));
                    ClearMine(colony, hut);
                    continue;
                }
            }

            if (hut.MineEnd == null)
            {
                var entrance = PlanEntrance(colony, hut, hut.MineTarget, boxes);
                if (entrance == null)
                {
                    BlockOre(hut, hut.MineTarget);
                    hut.MineTarget = null;
                    continue;
                }
                hut.MineEntrance = entrance;
                hut.MineEnd = entrance.Copy();
                MarkDirty(colony);
            }

            // Follow the tunnel to the ore; the first block still in the way is the next to dig.
            var target = hut.MineTarget;
            var standAt = hut.MineEnd.Copy();
            bool blocked = false;
            foreach (var (feet, digs) in MineRoute(hut.MineEnd, target))
            {
                foreach (var dig in digs)
                {
                    if (IsClear(dig)) continue;
                    if (dig.Equals(target)) break;
                    if (skip(dig) || !CanDig(colony, dig, boxes))
                    {
                        blocked = true;
                        break;
                    }
                    status = "mine-tunnelling";
                    return new MineJob(MineWork.Dig, dig, standAt, IsOre(ba.GetBlock(dig)));
                }
                if (blocked) break;
                standAt = feet;
            }
            if (blocked)
            {
                BlockOre(hut, target);
                hut.MineTarget = null;
                continue;
            }

            // The tunnel reaches the ore: dig it out from the tunnel's new end.
            hut.MineEnd = standAt;
            MarkDirty(colony);
            if (!CanDig(colony, target, boxes))
            {
                BlockOre(hut, target);
                hut.MineTarget = null;
                continue;
            }
            status = "mine-ore";
            return new MineJob(MineWork.Dig, target, standAt, true);
        }
        status = "mine-blocked";
        return null;
    }

    /// <summary>The Miner's Hut window line: what ore it digs, where, and how deep the tunnel is.</summary>
    string MineInfo(Colony colony, Building hut)
    {
        string choice = hut.MineOre.Length == 0 ? Lang.Get("createacolony:mine-any-ore") : OreName(hut.MineOre);
        string line = Lang.Get("createacolony:mine-digs-for", choice);
        if (hut.MineSite != null && hut.MineSiteOre.Length > 0)
        {
            var site = colony.OreSites.Find(s => s.Pos.Equals(hut.MineSite));
            var factor = site?.Ores.Find(o => o.Code == hut.MineSiteOre)?.Factor ?? 0;
            line += "<br>" + Lang.Get("createacolony:mine-site", OreName(hut.MineSiteOre), DensityName(factor), DirectionFrom(hut.HutPos, hut.MineSite));
            if (hut.MineEntrance != null && hut.MineEnd != null)
            {
                line += "<br>" + Lang.Get("createacolony:mine-depth", Math.Max(0, hut.MineEntrance.Y - hut.MineEnd.Y), hut.MineEntrance.X, hut.MineEntrance.Y, hut.MineEntrance.Z);
            }
        }
        return line;
    }
}
