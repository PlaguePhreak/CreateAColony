using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// The prospector's trade. It wanders its work area, heading where the readings look best, and takes readings with
/// the game's own prospecting pick (density mode), so it finds exactly what a player's pick would. The
/// readings are kept by the colony for its miners, and put on every member's ore map as well.
/// </summary>
public partial class ColonyManager
{
    /// <summary>The pick's own reading code, which is protected; called on the pick the prospector holds.</summary>
    static readonly MethodInfo? GenProbeResults = AccessTools.Method(typeof(ItemProspectingPick), "GenProbeResults");

    /// <summary>The prospecting pick's density words, weakest first, as the game shows them.</summary>
    static readonly string[] DensityNames = { "verypoor", "poor", "decent", "high", "veryhigh", "ultrahigh" };

    /// <summary>A reading this strong is worth a line in the Town Hall's event log ("decent" and up).</summary>
    const double NotableFactor = 0.267;

    public static bool IsProspectingPick(ItemStack? stack) => stack?.Collectible is ItemProspectingPick;

    /// <summary>The game's name for a density factor, such as "Decent".</summary>
    public static string DensityName(double factor) => Lang.Get("propick-density-" + DensityNames[(int)GameMath.Clamp(factor * 7.5, 0, 5)]);

    /// <summary>The game's name for an ore deposit code, such as "Native copper".</summary>
    public static string OreName(string code) => Lang.Get("ore-" + code);

    /// <summary>Asks couriers to bring a prospecting pick to the hut, once.</summary>
    public void EnsureProspectingPickRequest(Colony colony, Building hut)
    {
        var chest = HutStorage(colony, hut).FirstOrDefault();
        if (chest == null) return;
        const string code = "game:prospectingpick-*";
        string tag = RequestTag(hut);
        if (!colony.Requests.Any(r => r.RequestedBy == tag && r.ItemCode == code)) AddRequest(colony, code, 1, chest.Pos, tag);
    }

    /// <summary>Where each Prospector's Hut's prospector is headed, so it keeps to one spot until it has read there.</summary>
    readonly Dictionary<int, BlockPos> prospectTargets = new();
    /// <summary>Where each Prospector's Hut's prospector took its last reading, which it walks on from.</summary>
    readonly Dictionary<int, BlockPos> lastReadings = new();

    /// <summary>What a reading promises, about 0 to 1: its best ore's density, plus a little for each other ore found there.</summary>
    public static double ReadingQuality(OreSite site)
    {
        if (site.Ores.Count == 0) return 0;
        double best = site.Ores.Max(o => o.Factor);
        return best + 0.25 * (site.Ores.Sum(o => o.Factor) - best);
    }

    /// <summary>
    /// A guess at what a reading at x, z would show, from the readings around it: nearer ones count for
    /// more (inverse distance squared). Prior when no reading is within range.
    /// </summary>
    public static double EstimateQuality(IEnumerable<OreSite> sites, double x, double z, double range, double prior)
    {
        double total = 0, weights = 0;
        foreach (var site in sites)
        {
            double dx = site.Pos.X - x, dz = site.Pos.Z - z, d2 = dx * dx + dz * dz;
            if (d2 > range * range) continue;
            double w = 1 / (d2 + 16);
            total += w * ReadingQuality(site);
            weights += w;
        }
        return weights == 0 ? prior : total / weights;
    }

    /// <summary>True when the colony already has a reading close to this spot.</summary>
    bool Prospected(Colony colony, double x, double z)
    {
        double near = Math.Max(2, Config.ProspectorNearReading);
        return colony.OreSites.Any(s => (s.Pos.X - x) * (s.Pos.X - x) + (s.Pos.Z - z) * (s.Pos.Z - z) < near * near);
    }

    /// <summary>
    /// The next spot to take a reading at, where the prospector stands (the air above the ground), or null
    /// when the work area is read all over. Like a player with a pick, the prospector walks on from its last
    /// reading, or from a good one, a random ProspectorMinStep to ProspectorMaxStep blocks, and it is more
    /// likely to head where the readings around are good: each spot is picked at random, weighted by the
    /// ore the nearby readings promise there (ProspectorPreference sets how strongly).
    /// </summary>
    public BlockPos? NextProspectSpot(Colony colony, Building hut, System.Func<BlockPos, bool> skip)
    {
        var ba = sapi.World.BlockAccessor;
        if (prospectTargets.TryGetValue(hut.BuildingId, out var kept) && !skip(kept) && !Prospected(colony, kept.X, kept.Z)) return kept;
        prospectTargets.Remove(hut.BuildingId);

        var rand = sapi.World.Rand;
        int radius = Math.Max(1, GatherRadius(hut));

        // Ore bits the gatherer found come first: a reading right there, nearest first. A hint is done once read.
        if (colony.OreHints.RemoveAll(h => Prospected(colony, h.Pos.X, h.Pos.Z)) > 0) MarkDirty(colony);
        foreach (var hint in colony.OreHints.OrderBy(h => h.Pos.HorDistanceSqTo(hut.HutPos.X, hut.HutPos.Z)))
        {
            if (hint.Pos.HorDistanceSqTo(hut.HutPos.X, hut.HutPos.Z) > (double)radius * radius) break;
            var at = hint.Pos.Copy();
            if (ba.GetChunkAtBlockPos(at) != null)
            {
                at.Y = ba.GetTerrainMapheightAt(at) + 1;
                if (ba.GetBlock(at.DownCopy(), BlockLayersAccess.Fluid).Id != 0) continue;
            }
            if (skip(at) || !CanWorkAt(colony, at)) continue;
            prospectTargets[hut.BuildingId] = at;
            return at;
        }
        double minStep = Math.Max(4, Config.ProspectorMinStep), maxStep = Math.Max(minStep, Config.ProspectorMaxStep);
        var inRange = colony.OreSites.Where(s => s.Pos.HorDistanceSqTo(hut.HutPos.X, hut.HutPos.Z) <= (double)radius * radius).ToList();

        // Where to walk on from: the last reading, a reading picked by how good it was, or the hut.
        var starts = new List<BlockPos> { lastReadings.GetValueOrDefault(hut.BuildingId) ?? hut.HutPos };
        if (inRange.Count > 0)
        {
            double sum = inRange.Sum(s => Math.Exp(Config.ProspectorPreference * ReadingQuality(s)));
            double roll = rand.NextDouble() * sum;
            foreach (var site in inRange)
            {
                roll -= Math.Exp(Config.ProspectorPreference * ReadingQuality(site));
                if (roll <= 0) { starts.Add(site.Pos); break; }
            }
        }
        starts.Add(hut.HutPos);

        var candidates = new List<(BlockPos Pos, double Weight)>();
        foreach (var from in starts)
        {
            for (int tries = 0; tries < 24; tries++)
            {
                double angle = rand.NextDouble() * Math.PI * 2, step = minStep + rand.NextDouble() * (maxStep - minStep);
                int x = (int)Math.Round(from.X + Math.Cos(angle) * step), z = (int)Math.Round(from.Z + Math.Sin(angle) * step);
                if ((double)(x - hut.HutPos.X) * (x - hut.HutPos.X) + (double)(z - hut.HutPos.Z) * (z - hut.HutPos.Z) > (double)radius * radius) continue;
                if (Prospected(colony, x, z)) continue;
                var pos = new BlockPos(x, hut.HutPos.Y + 1, z);
                if (ba.GetChunkAtBlockPos(pos) != null)
                {
                    pos.Y = ba.GetTerrainMapheightAt(pos) + 1;
                    // Never take a reading under water.
                    if (ba.GetBlock(pos.DownCopy(), BlockLayersAccess.Fluid).Id != 0) continue;
                }
                if (skip(pos) || !CanWorkAt(colony, pos)) continue;
                double guess = EstimateQuality(colony.OreSites, x, z, maxStep * 2, Config.ProspectorUnknownQuality);
                candidates.Add((pos, Math.Exp(Config.ProspectorPreference * guess)));
            }
            if (candidates.Count >= 8) break;
        }
        if (candidates.Count == 0) return null;

        double total = candidates.Sum(c => c.Weight), pick = rand.NextDouble() * total;
        var chosen = candidates[^1].Pos;
        foreach (var (pos, weight) in candidates)
        {
            pick -= weight;
            if (pick <= 0) { chosen = pos; break; }
        }
        prospectTargets[hut.BuildingId] = chosen;
        return chosen;
    }

    /// <summary>About how much of the work area has a reading near it, from 0 to 100.</summary>
    public int ProspectedPercent(Colony colony, Building hut)
    {
        int radius = Math.Max(1, GatherRadius(hut)), step = Math.Max(4, Config.ProspectorMinStep / 2);
        double near = Math.Max(Config.ProspectorNearReading, Config.ProspectorMinStep);
        int all = 0, done = 0;
        for (int x = -radius; x <= radius; x += step)
        for (int z = -radius; z <= radius; z += step)
        {
            if (x * x + z * z > radius * radius) continue;
            all++;
            double px = hut.HutPos.X + x, pz = hut.HutPos.Z + z;
            if (colony.OreSites.Any(s => (s.Pos.X - px) * (s.Pos.X - px) + (s.Pos.Z - pz) * (s.Pos.Z - pz) < near * near)) done++;
        }
        return all == 0 ? 100 : done * 100 / all;
    }

    /// <summary>
    /// Takes a reading at the ground below the spot with the prospecting pick in the worker's hand, wears the
    /// pick, and keeps the reading for the colony. Returns false when the pick could not read here.
    /// </summary>
    public bool TakeReading(Colony colony, Building hut, CitizenRecord worker, EntityAgent entity, BlockPos spot, ItemSlot pick)
    {
        if (pick.Itemstack?.Collectible is not ItemProspectingPick item || GenProbeResults == null) return false;
        var ground = spot.DownCopy();
        if (sapi.World.BlockAccessor.GetChunkAtBlockPos(ground) == null) return false;

        PropickReading? reading;
        try
        {
            reading = GenProbeResults.Invoke(item, new object[] { sapi.World, ground }) as PropickReading;
        }
        catch (Exception e)
        {
            sapi.Logger.Warning("[createacolony] Prospecting at {0} failed: {1}", ground, e.InnerException?.Message ?? e.Message);
            return false;
        }
        if (reading == null) return false;

        for (int i = 0; i < Math.Max(1, Config.ProspectorPickWear); i++) WearTool(entity, pick);

        var site = new OreSite
        {
            Pos = new BlockPos((int)reading.Position.X, (int)reading.Position.Y, (int)reading.Position.Z),
            TotalDays = sapi.World.Calendar.TotalDays
        };
        foreach (var (code, ore) in reading.OreReadings)
        {
            if (ore.DepositCode == "unknown" || ore.TotalFactor <= PropickReading.MentionThreshold) continue;
            site.Ores.Add(new OreFind { Code = code, Factor = ore.TotalFactor, Ppt = ore.PartsPerThousand });
        }
        site.Ores.Sort((a, b) => b.Factor.CompareTo(a.Factor));
        colony.OreSites.Add(site);
        lastReadings[hut.BuildingId] = site.Pos.Copy();
        prospectTargets.Remove(hut.BuildingId);
        AddStat(hut, "readings");
        MarkDirty(colony);

        if (Config.ProspectorSharesOreMap) ShareReading(colony);

        if (site.Ores.FirstOrDefault() is { Factor: >= NotableFactor } best)
        {
            LogEvent(colony, "event-ore-found", worker.Name, OreName(best.Code), DensityName(best.Factor), DirectionFrom(hut.HutPos, site.Pos));
        }
        return true;
    }

    /// <summary>"40 blocks north-east", from one spot to another, for messages.</summary>
    public static string DirectionFrom(BlockPos from, BlockPos to)
    {
        int dx = to.X - from.X, dz = to.Z - from.Z;
        int distance = (int)Math.Round(Math.Sqrt(dx * dx + dz * dz));
        if (distance < 3) return Lang.Get("createacolony:compass-here");
        // Angle from north, clockwise; north is -Z.
        double angle = (Math.Atan2(dx, -dz) * 180 / Math.PI + 360 + 22.5) % 360;
        string[] names = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };
        return Lang.Get("createacolony:compass-blocks", distance, Lang.Get("createacolony:compass-" + names[(int)(angle / 45)]));
    }

    /// <summary>
    /// Shows a new reading on the "Colony prospecting" layer of every online member's world map, kept apart
    /// from the readings they took themselves.
    /// </summary>
    void ShareReading(Colony colony)
    {
        try
        {
            var layer = sapi.ModLoader.GetModSystem<WorldMapManager>()?.MapLayers.OfType<Map.ColonyOreMapLayer>().FirstOrDefault();
            if (layer == null) return;
            foreach (var member in colony.Members)
            {
                if (sapi.World.PlayerByUid(member.PlayerUid) is IServerPlayer { ConnectionState: EnumClientState.Playing } player) layer.Resend(player);
            }
        }
        catch (Exception e)
        {
            sapi.Logger.Warning("[createacolony] Could not show a reading on the colony ore map: {0}", e.InnerException?.Message ?? e.Message);
        }
    }

    /// <summary>One line per ore the colony's prospectors found, strongest first, for the Ore tab.</summary>
    public List<(string Code, double Best, int Sites, int MinedOut, BlockPos BestPos)> OreSummary(Colony colony)
    {
        return colony.OreSites
            .SelectMany(s => s.Ores.Select(o => (Site: s, Ore: o)))
            .Where(p => p.Ore.Factor >= Config.MinerMinFactor)
            .GroupBy(p => p.Ore.Code)
            .Select(g =>
            {
                var best = g.OrderByDescending(p => p.Site.MinedOut.Contains(g.Key) ? -1 : p.Ore.Factor).First();
                return (g.Key, best.Ore.Factor, g.Count(), g.Count(p => p.Site.MinedOut.Contains(g.Key)), best.Site.Pos);
            })
            .OrderByDescending(r => r.Item2)
            .ToList();
    }

    /// <summary>The Prospector's Hut window line: how many readings, how many spots are left, and the best find.</summary>
    string ProspectorInfo(Colony colony, Building hut)
    {
        string line = Lang.Get("createacolony:prospector-progress", colony.OreSites.Count, ProspectedPercent(colony, hut));
        var best = OreSummary(colony).FirstOrDefault();
        if (best.Code != null) line += "<br>" + Lang.Get("createacolony:prospector-best", OreName(best.Code), DensityName(best.Best), DirectionFrom(hut.HutPos, best.BestPos));
        return line;
    }
}
