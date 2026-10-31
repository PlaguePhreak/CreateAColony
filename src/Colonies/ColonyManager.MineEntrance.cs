using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// The Mineshaft Entrance (Aaron, 0.90.0): a hut block put at a mine's entrance that holds the mine, so
/// several Miner's Huts can work the one mine. Each linked hut's miner digs and builds the same pieces, and
/// the Levels and Settings tabs of every linked hut change the shared mine.
/// A mine whose Miner's Hut or entrance is removed is left derelict: its blocks stay and nobody works it.
/// The next Miner's Hut without a mine near it, or a Mineshaft Entrance put near it, takes it on.
/// </summary>
public partial class ColonyManager
{
    /// <summary>How near (in blocks, sideways) a Mineshaft Entrance must be to a mine's entrance to take it on.</summary>
    public const int EntranceReach = 16;
    /// <summary>How near a Miner's Hut without a mine must be to a derelict mine to take it on.</summary>
    public const int DerelictReach = 32;

    static int Sideways(BlockPos a, BlockPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));

    /// <summary>Where a Mineshaft Entrance's own mine starts: lined up with its building's preview, beside the hut block.</summary>
    BlockPos EntranceMinePos(Building entrance, MinePiece piece)
    {
        // The piece's anchor is the ground under MineshaftPos; the hut block is at the spot's offset from it.
        var (ex, ey, ez) = BlueprintLibrary.EntranceBlockSpot(piece, entrance.Rotation);
        return entrance.HutPos.AddCopy(entrance.XOffset - ex, entrance.YOffset - ey + 1, entrance.ZOffset - ez);
    }

    /// <summary>
    /// Build on a Mineshaft Entrance: no builder; it starts its mine where the preview showed (unless it already
    /// has one), and the linked Miner's Huts' miners dig and build it.
    /// </summary>
    string? StartEntrance(Colony colony, Building entrance)
    {
        if (entrance.Level >= 1) return "createacolony:build-already";
        if (!entrance.HasMine)
        {
            if (Blueprints.EntranceStyle(entrance.Style) is not { } style || Blueprints.GetMinePiece(style, BlueprintLibrary.MineEntrance, entrance.Rotation) is not { } piece)
                return "createacolony:build-no-blueprint";
            entrance.MineshaftPos = EntranceMinePos(entrance, piece);
            entrance.MineRotation = entrance.Rotation;
            entrance.MineStyle = style;
        }
        entrance.Level = 1;
        LogEvent(colony, "event-built", "@building-" + entrance.Type, "1");
        MarkDirty(colony);
        RefreshClaim(colony);
        return null;
    }

    /// <summary>The building whose level sets how deep the mine goes: the highest linked Miner's Hut, or the building itself.</summary>
    public static Building LevelHut(Building hut) =>
        hut.Mine.LinkedHuts.Count > 0 ? hut.Mine.LinkedHuts.OrderByDescending(h => h.Level).First() : hut;

    /// <summary>Moves a mine from one building (or derelict record) to another; the first is left without one.</summary>
    static void MoveMine(Building from, Building to)
    {
        to.MineRotation = from.MineTurn;
        to.MineStyle = from.MinePack;
        to.MineshaftPos = from.MineshaftPos?.Copy();
        to.Sections = from.Sections.ToList();
        to.MineLevel = from.MineLevel;
        to.MaxMineLevels = from.MaxMineLevels;
        to.FillBlock = from.FillBlock;
        to.RepairLevel = 0;
        from.MineshaftPos = null;
        from.Sections = new List<MineSection>();
        from.MineRotation = -1;
        from.MineStyle = "";
        from.MineLevel = from.RepairLevel = from.MaxMineLevels = 0;
    }

    /// <summary>Puts a building's own mine on the colony's derelict list, if it has one.</summary>
    void MakeDerelict(Colony colony, Building owner)
    {
        if (!owner.HasMine) return;
        var record = new Building { BuildingId = owner.BuildingId, Type = BuildingTypes.Miner, HutPos = owner.HutPos.Copy(), CustomName = BuildingName(owner) };
        MoveMine(owner, record);
        colony.DerelictMines.Add(record);
        LogEvent(colony, "event-mine-derelict", BuildingName(owner));
        MarkDirty(colony);
    }

    /// <summary>
    /// A building going away lets go of its mine: a Miner's Hut working an entrance's mine just leaves it; one
    /// with its own mine, or an entrance, leaves the mine derelict (an entrance's huts are left without a mine).
    /// </summary>
    void LeaveMine(Colony colony, Building building)
    {
        if (building.Type == BuildingTypes.Miner && building.MineEntranceId != 0)
        {
            building.MineEntranceId = 0;
            colony.LinkMines();
            MarkDirty(colony);
            return;
        }
        if (building.Type == BuildingTypes.MineshaftEntrance)
        {
            foreach (var hut in colony.Buildings.Where(b => b.MineEntranceId == building.BuildingId)) hut.MineEntranceId = 0;
            colony.LinkMines();
        }
        MakeDerelict(colony, building);
    }

    /// <summary>Takes on the nearest derelict mine within reach of pos, if there is one. True when it did.</summary>
    bool AdoptDerelict(Colony colony, Building to, BlockPos pos, int reach)
    {
        var mine = colony.DerelictMines.Where(m => m.MineshaftPos != null && Sideways(m.MineshaftPos, pos) <= reach)
            .OrderBy(m => Sideways(m.MineshaftPos!, pos)).FirstOrDefault();
        if (mine == null) return false;
        MoveMine(mine, to);
        colony.DerelictMines.Remove(mine);
        LogEvent(colony, "event-mine-taken", BuildingName(to));
        MarkDirty(colony);
        return true;
    }

    /// <summary>
    /// A Miner's Hut with no mine of its own and no entrance looks for a derelict mine near it before it starts
    /// a new one. Runs when the mine's pieces are first needed.
    /// </summary>
    void AdoptDerelictFor(Colony colony, Building hut)
    {
        if (hut.Type != BuildingTypes.Miner || hut.Entrance != null || hut.HasMine) return;
        AdoptDerelict(colony, hut, hut.HutPos, DerelictReach);
    }

    /// <summary>
    /// After the wand puts a hut down. A Mineshaft Entrance takes on the mine whose entrance is nearest, a
    /// Miner's Hut's or a derelict one, and links that Miner's Hut. A new Miner's Hut joins the nearest
    /// Mineshaft Entrance near it. Returns a lang key to tell the player, or null.
    /// </summary>
    public string? AfterHutPlaced(Colony colony, Building building)
    {
        if (building.Type == BuildingTypes.MineshaftEntrance)
        {
            var hut = colony.Buildings.Where(b => b.Type == BuildingTypes.Miner && b.Entrance == null && b.MineshaftPos != null
                    && Sideways(b.MineshaftPos, building.HutPos) <= EntranceReach)
                .OrderBy(b => Sideways(b.MineshaftPos!, building.HutPos)).FirstOrDefault();
            if (hut != null)
            {
                MoveMine(hut, building);
                building.Level = 1;
                hut.MineEntranceId = building.BuildingId;
                colony.LinkMines();
                MarkDirty(colony);
                return "createacolony:entrance-took-hut";
            }
            if (!AdoptDerelict(colony, building, building.HutPos, EntranceReach)) return "createacolony:entrance-new";
            building.Level = 1;
            return "createacolony:entrance-took-derelict";
        }
        if (building.Type == BuildingTypes.Miner)
        {
            var entrance = colony.Buildings.Where(b => b.Type == BuildingTypes.MineshaftEntrance && !b.Removing && Sideways(b.HutPos, building.HutPos) <= DerelictReach)
                .OrderBy(b => Sideways(b.HutPos, building.HutPos)).FirstOrDefault();
            if (entrance != null && LinkHut(colony, entrance, building) == null) return "createacolony:entrance-hut-joined";
        }
        return null;
    }

    /// <summary>
    /// Has a Miner's Hut work the entrance's mine. If the entrance has no mine yet and the hut's own mine is near
    /// it, the entrance takes that one on; otherwise the hut's own mine is left derelict. Returns a lang key on failure.
    /// </summary>
    public string? LinkHut(Colony colony, Building entrance, Building hut)
    {
        if (entrance.Type != BuildingTypes.MineshaftEntrance || hut.Type != BuildingTypes.Miner || entrance.Removing || hut.Removing) return "createacolony:bad-building";
        if (hut.MineEntranceId == entrance.BuildingId) return null;
        if (hut.Entrance != null) hut.MineEntranceId = 0;
        if (hut.HasMine)
        {
            if (!entrance.HasMine && Sideways(hut.MineshaftPos!, entrance.HutPos) <= EntranceReach) MoveMine(hut, entrance);
            else MakeDerelict(colony, hut);
        }
        hut.MineEntranceId = entrance.BuildingId;
        // Its trip to prospected ore and its mine guards stay its own.
        mineshaftNeeds.Remove(hut.BuildingId);
        mineshaftWorking.Remove(hut.BuildingId);
        colony.LinkMines();
        MarkDirty(colony);
        return null;
    }

    /// <summary>Stops a Miner's Hut working the entrance's mine; it looks for a mine of its own again.</summary>
    public string? UnlinkHut(Colony colony, Building entrance, Building hut)
    {
        if (hut.MineEntranceId != entrance.BuildingId) return "createacolony:bad-building";
        hut.MineEntranceId = 0;
        mineshaftNeeds.Remove(hut.BuildingId);
        mineshaftWorking.Remove(hut.BuildingId);
        colony.LinkMines();
        MarkDirty(colony);
        return null;
    }

    /// <summary>The level each hut sharing this mine works on, for the Levels tab's red numbers.</summary>
    HashSet<int> WorkingLevels(Building hut)
    {
        var mine = hut.Mine;
        var ids = mine.LinkedHuts.Select(h => h.BuildingId).Append(hut.BuildingId).Append(mine.BuildingId);
        return ids.Select(id => mineshaftWorking.GetValueOrDefault(id)).Where(l => l > 0).ToHashSet();
    }
}
