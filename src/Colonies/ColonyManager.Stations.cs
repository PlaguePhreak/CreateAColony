using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Work stations (Aaron, 0.137.0): a block that goes inside another building's zone and gives one worker a job
/// there, with no hut or blueprint of its own. The first is the Healer's Station, which only goes in a Church.
/// A station is kept as a Building with HostId set to the building it stands in, so the hut window, worker
/// assignment and the job's AI all work as for a hut. Its level follows the host's, it has no work orders, and
/// its worker uses the host's storage (plus the station block's own). Couriers never empty a station: what is
/// in the host's storage is the host's business (ColonyManager.Church.cs keeps the healer's things there).
/// </summary>
public partial class ColonyManager
{
    /// <summary>The building types each station type may stand in.</summary>
    public static readonly Dictionary<string, string[]> StationHosts = new()
    {
        [BuildingTypes.Healer] = new[] { BuildingTypes.Church },
        [BuildingTypes.CourierStation] = new[] { BuildingTypes.Warehouse }
    };

    /// <summary>The building a station stands in, or null when it is gone (or the building is not a station).</summary>
    public Building? HostOf(Colony colony, Building station) =>
        station.HostId == 0 ? null : colony.Buildings.Find(b => b.BuildingId == station.HostId && !b.IsStation);

    /// <summary>The stations standing in a building.</summary>
    public IEnumerable<Building> StationsIn(Colony colony, Building host) => colony.Buildings.Where(b => b.HostId == host.BuildingId);

    /// <summary>
    /// The building a station of this type could go in at this spot: one of its host types whose zone holds the
    /// spot, the smallest first. Null when there is none.
    /// </summary>
    public Building? StationHostAt(Colony colony, string stationType, BlockPos pos)
    {
        if (!StationHosts.TryGetValue(stationType, out var hostTypes)) return null;
        // A Warehouse with no building of its own: anywhere its storage counts (WarehouseRadius).
        if (hostTypes.Contains(BuildingTypes.Warehouse) && colony.WarehousePos is { } w && GetBuilding(colony, w) is { Removing: false } warehouse
            && System.Math.Abs(pos.X - w.X) <= WarehouseRadius && System.Math.Abs(pos.Z - w.Z) <= WarehouseRadius && System.Math.Abs(pos.Y - w.Y) <= WarehouseHeight)
        {
            return warehouse;
        }
        return colony.Buildings
            .Where(b => !b.IsStation && !b.Removing && hostTypes.Contains(b.Type))
            .Select(b => (B: b, Box: BuildingBox(b)))
            .Where(z => InBox(pos, z.Box.Min, z.Box.Max))
            .OrderBy(z => (long)(z.Box.Max.X - z.Box.Min.X + 1) * (z.Box.Max.Y - z.Box.Min.Y + 1) * (z.Box.Max.Z - z.Box.Min.Z + 1))
            .Select(z => z.B)
            .FirstOrDefault();
    }

    /// <summary>Puts a station down in its host. It keeps no pickups of its own and starts at its host's level.</summary>
    public Building AddStation(Colony colony, string type, BlockPos pos, Building host)
    {
        var station = AddBuilding(colony, type, pos, 0);
        station.HostId = host.BuildingId;
        station.Level = System.Math.Max(1, host.Level);
        station.PickupPriority = 0;
        LogEvent(colony, "event-station-placed", "@building-" + type, BuildingName(host));
        MarkDirty(colony);
        return station;
    }

    /// <summary>Part of the colony tick: each station's level follows its host's; one whose host is gone stops at 0.</summary>
    void TickStations(Colony colony)
    {
        foreach (var station in colony.Buildings.Where(b => b.IsStation))
        {
            int level = HostOf(colony, station) is { } host ? System.Math.Max(1, host.Level) : 0;
            if (station.Level == level) continue;
            station.Level = level;
            MarkDirty(colony);
        }
    }

    /// <summary>A line about the station for its window: the building it stands in, or that it has none.</summary>
    public string StationInfo(Colony colony, Building station) => HostOf(colony, station) is { } host
        ? Lang.Get("createacolony:station-in", BuildingName(host))
        : Lang.Get("createacolony:station-no-host", string.Join(", ", (StationHosts.GetValueOrDefault(station.Type) ?? System.Array.Empty<string>()).Select(t => Lang.Get("createacolony:building-" + t))));
}
