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
/// The prospector's trade. It walks a grid of spots around its hut and at each one takes a reading with
/// the game's own prospecting pick (density mode), so it finds exactly what a player's pick would. The
/// readings are kept by the colony for its miners, and put on every member's ore map as well.
/// </summary>
public partial class ColonyManager
{
    /// <summary>The pick's own reading code, which is protected; called on the pick the prospector holds.</summary>
    static readonly MethodInfo? GenProbeResults = AccessTools.Method(typeof(ItemProspectingPick), "GenProbeResults");
    /// <summary>Sends a player's ore map readings to their client, so a new reading shows at once.</summary>
    static readonly MethodInfo? ResendWaypoints = AccessTools.Method(typeof(OreMapLayer), "ResendWaypoints");

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

    /// <summary>
    /// The spots a prospector takes readings at: a grid ProspectorSpacing apart around the hut, as far
    /// as the work area goes, nearest first.
    /// </summary>
    List<(int X, int Z)> ProspectGrid(Building hut)
    {
        int radius = Math.Max(1, GatherRadius(hut));
        int spacing = Math.Max(4, Config.ProspectorSpacing);
        int steps = radius / spacing;
        var spots = new List<(int X, int Z)>();
        for (int i = -steps; i <= steps; i++)
        for (int j = -steps; j <= steps; j++)
        {
            if ((i * i + j * j) * spacing * spacing > radius * radius) continue;
            spots.Add((hut.HutPos.X + i * spacing, hut.HutPos.Z + j * spacing));
        }
        return spots.OrderBy(s => (s.X - hut.HutPos.X) * (s.X - hut.HutPos.X) + (s.Z - hut.HutPos.Z) * (s.Z - hut.HutPos.Z)).ToList();
    }

    /// <summary>True when the colony already has a reading close to this spot.</summary>
    bool Prospected(Colony colony, int x, int z)
    {
        int near = Math.Max(2, Config.ProspectorSpacing / 2);
        return colony.OreSites.Any(s => Math.Abs(s.Pos.X - x) < near && Math.Abs(s.Pos.Z - z) < near);
    }

    /// <summary>
    /// The next spot to take a reading at, where the prospector stands (the air above the ground), or null
    /// when every spot in range is done. Spots in land that is loaded come first; for the others the
    /// height is a guess until the prospector gets there.
    /// </summary>
    public BlockPos? NextProspectSpot(Colony colony, Building hut, System.Func<BlockPos, bool> skip)
    {
        var ba = sapi.World.BlockAccessor;
        BlockPos? unloaded = null;
        foreach (var (x, z) in ProspectGrid(hut))
        {
            if (Prospected(colony, x, z)) continue;
            var pos = new BlockPos(x, hut.HutPos.Y + 1, z);
            if (ba.GetChunkAtBlockPos(pos) == null)
            {
                if (unloaded == null && !skip(pos) && CanWorkAt(colony, pos)) unloaded = pos;
                continue;
            }
            pos.Y = ba.GetTerrainMapheightAt(pos) + 1;
            if (skip(pos) || !CanWorkAt(colony, pos)) continue;
            // Never take a reading under water; the next spot will do.
            if (ba.GetBlock(pos.DownCopy(), BlockLayersAccess.Fluid).Id != 0) continue;
            return pos;
        }
        return unloaded;
    }

    /// <summary>How many grid spots in range still have no reading.</summary>
    public int ProspectSpotsLeft(Colony colony, Building hut) => ProspectGrid(hut).Count(s => !Prospected(colony, s.X, s.Z));

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
        AddStat(hut, "readings");
        MarkDirty(colony);

        if (Config.ProspectorSharesOreMap) ShareReading(colony, reading);

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

    /// <summary>Puts the reading on the ore map of every colony member the server knows.</summary>
    void ShareReading(Colony colony, PropickReading reading)
    {
        try
        {
            var layer = sapi.ModLoader.GetModSystem<WorldMapManager>()?.MapLayers.OfType<OreMapLayer>().FirstOrDefault();
            if (layer == null) return;
            foreach (var member in colony.Members)
            {
                if (sapi.World.PlayerByUid(member.PlayerUid) is not IServerPlayer player) continue;
                var readings = layer.getOrLoadReadings(player);
                readings.Add(new PropickReading { Position = reading.Position.Clone(), OreReadings = reading.OreReadings, Guid = Guid.NewGuid().ToString() });
                if (player.ConnectionState == EnumClientState.Playing) ResendWaypoints?.Invoke(layer, new object[] { player });
            }
        }
        catch (Exception e)
        {
            sapi.Logger.Warning("[createacolony] Could not add a reading to the ore map: {0}", e.InnerException?.Message ?? e.Message);
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
        string line = Lang.Get("createacolony:prospector-progress", colony.OreSites.Count, ProspectSpotsLeft(colony, hut));
        var best = OreSummary(colony).FirstOrDefault();
        if (best.Code != null) line += "<br>" + Lang.Get("createacolony:prospector-best", OreName(best.Code), DensityName(best.Best), DirectionFrom(hut.HutPos, best.BestPos));
        return line;
    }
}
