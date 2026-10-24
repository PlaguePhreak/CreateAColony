using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// A Miner's Hut's mineshaft built from pieces, as MineColonies' miner does with its mine nodes. The style's
/// mineshaft folder holds an entrance (the way down) and sections
/// (cross, tee, straight). The miner digs and builds the entrance, then grows the mine a section at a time:
/// at an open side with nothing beyond it yet, it picks a section at random, turned so one of its open
/// sides meets that one, and digs and builds it. Each hut level opens MinerMineLevelsPerHutLevel more levels
/// for sections; how many sections there are is not limited unless MinerSectionsPerHutLevel is set.
/// A piece's empty spots are dug out, its natural ground is left as it is, and everything else in it is
/// built from the hut's storage (couriers are asked for what is missing). Without an entrance in any
/// style the miner digs its spiral shaft as before.
/// </summary>
public partial class ColonyManager
{
    /// <summary>A mineshaft piece where it is in the world: its steps are offsets from Origin.</summary>
    public sealed record PlacedPiece(MinePiece Piece, BlockPos Origin)
    {
        public BlockPos Min => Origin.AddCopy(Piece.Min.X, Piece.Min.Y, Piece.Min.Z);
        public BlockPos Max => Origin.AddCopy(Piece.Max.X, Piece.Max.Y, Piece.Max.Z);
        public BlockPos At(BuildStep step) => Origin.AddCopy(step.Dx, step.Dy, step.Dz);
    }

    /// <summary>What each Miner's Hut's mineshaft still needs built from (item codes), so the miner keeps those when it passes the hut.</summary>
    readonly Dictionary<int, HashSet<string>> mineshaftNeeds = new();

    /// <summary>The style a Miner's Hut's mineshaft pieces come from: its own, or the first with an entrance; null when none has one.</summary>
    public string? MineshaftStyle(Building hut)
    {
        foreach (var style in BlueprintStyles.All.Prepend(BlueprintStyles.Normalize(hut.Mine.MinePack)))
        {
            if (Blueprints.GetMinePiece(style, BlueprintLibrary.MineEntrance, 0) != null) return style;
        }
        return null;
    }

    /// <summary>The mineshaft's pieces in digging order, the entrance first; null when there is no entrance or no room for it.</summary>
    public List<PlacedPiece>? MineshaftPieces(Colony colony, Building hut)
    {
        var mine = hut.Mine;
        if (!mine.HasMine) AdoptDerelictFor(colony, hut);
        if (MineshaftStyle(hut) is not { } style || Blueprints.GetMinePiece(style, BlueprintLibrary.MineEntrance, mine.MineTurn) is not { } entrance) return null;
        entrance = entrance.Deeper(MineshaftRepeats(hut));
        int half = Math.Max(Math.Max(-entrance.Min.X, entrance.Max.X), Math.Max(-entrance.Min.Z, entrance.Max.Z));
        if (mine.MineshaftPos == null)
        {
            // A new mine beside its hut (or its Mineshaft Entrance); it keeps this turn and pack if it moves to another hut later.
            mine.MineshaftPos = mine.Type == BuildingTypes.MineshaftEntrance ? EntranceMinePos(mine, entrance) : SpotBeside(colony, mine, half);
            if (mine.MineshaftPos == null) return null;
            mine.MineRotation = mine.MineTurn;
            mine.MineStyle = style;
            MarkDirty(colony);
        }
        // The entrance's ground layer goes in the ground, just under where a settler stands.
        var anchor = mine.MineshaftPos.DownCopy();
        var pieces = new List<PlacedPiece> { new(entrance, anchor) };
        // Level pieces hang one under the other below the entrance's shaft, each just under the floor above.
        if (Blueprints.GetMinePiece(style, BlueprintLibrary.MineLevel, mine.MineTurn) is { Period: > 0 } level && entrance.Open.Count > 0)
        {
            int floor = entrance.Open.Max(o => o.Y);
            for (int k = 0; k < MineshaftHubLevels(hut); k++)
            {
                pieces.Add(new PlacedPiece(level, anchor.AddCopy(entrance.Shaft.X, floor - 1 - k * level.Period, entrance.Shaft.Z)));
            }
        }
        // A section that runs into the entrance or a level (one dug for an older entrance) is left out.
        HashSet<BlockPos>? hubCells = pieces.Count > 1 ? new HashSet<BlockPos>(pieces.SelectMany(p => p.Piece.Steps.Select(p.At))) : null;
        foreach (var section in mine.Sections)
        {
            if (Blueprints.GetMinePiece(style, section.Name, section.Rotation) is not { } piece) continue;
            var placed = new PlacedPiece(piece, section.Origin);
            if (hubCells != null && piece.Steps.Any(step => hubCells.Contains(placed.At(step)))) continue;
            pieces.Add(placed);
        }
        return pieces;
    }

    /// <summary>
    /// The miner's next job on the mineshaft: ore it sees from inside the mine, then piece by piece, top
    /// down, each spot to dig out and each block to build. When all is done the mine grows by a section.
    /// Null with an empty status when the hut has no mineshaft pieces; null with a status when nothing can
    /// be done right now (waiting for materials, or done).
    /// </summary>
    public MineJob? NextMineshaftJob(Colony colony, Building hut, CitizenRecord miner, BlockPos at, System.Func<BlockPos, bool> skip, out string status)
    {
        status = "";
        var pieces = MineshaftPieces(colony, hut);
        if (pieces == null) return null;
        mineshaftWorking.Remove(hut.BuildingId);
        for (int round = 0; round < 3; round++)
        {
            var job = ScanMineshaft(colony, hut, miner, pieces, at, skip, out status, out bool busy, out bool waitingForMaterials);
            if (job != null || busy && waitingForMaterials) return job;
            // Nothing to do, or only spots it could not get to a little while ago: those wait, and the mine grows
            // on meanwhile through its open sides, rather than the miner standing about (Aaron, 0.82.0).
            if (!GrowMineshaft(colony, hut, pieces))
            {
                if (busy) return null;
                status = "mineshaft-done";
                mineshaftNeeds.Remove(hut.BuildingId);
                return null;
            }
            pieces = MineshaftPieces(colony, hut)!;
        }
        return null;
    }

    /// <summary>
    /// The first thing to do on the mineshaft. Busy is set when work is left that may be possible later
    /// (waiting for materials, or a spot that could not be reached a little while ago); spots that can never
    /// be dug (water beside them, someone else's land) do not count.
    /// </summary>
    MineJob? ScanMineshaft(Colony colony, Building hut, CitizenRecord miner, List<PlacedPiece> pieces, BlockPos at, System.Func<BlockPos, bool> skip,
        out string status, out bool busy, out bool waitingForMaterials)
    {
        status = "";
        busy = false;
        waitingForMaterials = false;
        var ba = sapi.World.BlockAccessor;
        ShaftFor(colony, hut, out bool marked);
        var (boxes, keep) = DigLimits(colony, hut, marked);
        // A Mineshaft Entrance's box is the mine's own entrance, which the miners dig.
        if (hut.Mine != hut && BuildingBox(hut.Mine) is var own)
        {
            boxes = boxes.Where(b => !(b.Min.Equals(own.Min) && b.Max.Equals(own.Max))).ToList();
            keep.Add(hut.Mine.HutPos.Copy());
        }
        // The hut's own blueprint blocks (keep) stay, even where a piece would dig or build.
        var cells = MineshaftCells(pieces);

        // Ore in sight from inside the mine comes out first, never the floors or the built blocks.
        if (cells.TryGetValue(at, out var here) && here.BlockId == 0)
        {
            var guard = MineshaftGuard(cells);
            guard.UnionWith(keep);
            if (OreInReach(colony, hut.Trip, at, b => IsOre(b) && MayDig(hut, b), skip, boxes, guard) is { } ore)
            {
                status = "mine-ore";
                mineshaftWorking[hut.BuildingId] = LevelAt(pieces, at.Y);
                return new MineJob(MineWork.Dig, ore, at.Copy(), true);
            }
        }

        var needs = new HashSet<string>();
        MineJob? found = null;
        PlacedPiece? foundIn = null;
        int fillId = FillBlockId(hut);
        // The hut's storage is looked through once per scan, and each kind of cost is checked once, not per block.
        List<BlockEntityContainer>? storage = null;
        var payable = new Dictionary<string, bool>();
        foreach (var piece in WorkOrder(pieces, hut))
        {
            var origin = piece.Origin;
            foreach (var step in StepsWithFill(piece, cells, fillId).OrderByDescending(s => s.Dy).ThenBy(StepOrder).ThenBy(s => Math.Abs(s.Dx) + Math.Abs(s.Dz)))
            {
                var pos = piece.At(step);
                if (ba.GetChunkAtBlockPos(pos) == null)
                {
                    busy = true;
                    if (found != null) continue;
                    status = "mine-travelling";
                    found = new MineJob(MineWork.Travel, origin.Copy(), origin.Copy(), false);
                    continue;
                }
                bool clear = IsClear(pos);
                if (step.BlockId == 0 || !clear)
                {
                    // Dig it out: an empty spot of the piece, or whatever stands where a block is to be built.
                    if (clear || step.BlockId != 0 && KeepsBlock(colony, ba.GetBlock(pos), step.BlockId, pos)) continue;
                    if (keep.Contains(pos) || !CanDig(colony, pos, boxes)) continue;
                    if (skip(pos)) { busy = true; continue; }
                    if (found != null) continue;
                    if (StandFor(pos, at, skip) is not { } stand) continue;
                    status = "mineshaft-digging";
                    found = new MineJob(MineWork.Dig, pos, stand, IsOre(ba.GetBlock(pos)));
                    continue;
                }
                // Build the block, from what the miner carries or the hut's storage.
                var costs = NextPlacement(colony, step, pos).Costs;
                foreach (var cost in costs) needs.Add(cost.Code);
                if (skip(pos)) { busy = true; continue; }
                string costKey = string.Join(";", costs.Select(c => c.Code + "x" + c.Count));
                if (!payable.TryGetValue(costKey, out bool canPay))
                {
                    storage ??= HutStorage(colony, hut).ToList();
                    payable[costKey] = canPay = CanPay(miner, storage, costs);
                    if (!canPay) RequestMineshaftMaterials(colony, hut, miner, costs, storage);
                }
                if (!canPay)
                {
                    busy = true;
                    waitingForMaterials = true;
                    if (found == null) status = "mineshaft-materials";
                    continue;
                }
                if (found != null) continue;
                if (StandFor(pos, at, skip, building: true) is not { } spot) continue;
                status = "mineshaft-building";
                found = new MineJob(MineWork.Build, pos, spot, false, step);
            }
            // Pieces are dug in order: the next waits until this one has nothing left to start.
            if (found != null)
            {
                foundIn = piece;
                break;
            }
        }
        mineshaftNeeds[hut.BuildingId] = needs;
        if (found != null && foundIn != null) mineshaftWorking[hut.BuildingId] = LevelAt(pieces, found.Value.Block.Y);
        // A repair is done once the miner has nothing left to do on that level.
        if (hut.Mine.RepairLevel > 0 && (found == null ? !busy : foundIn != pieces[0] && LevelOf(pieces, foundIn!) != hut.Mine.RepairLevel))
        {
            hut.Mine.RepairLevel = 0;
            MarkDirty(colony);
        }
        if (found != null) busy = true;
        return found;
    }

    /// <summary>On each layer: dig out first, then the solid blocks, then torches, ladders and the like that hang on them.</summary>
    int StepOrder(BuildStep step) => step.BlockId == 0 ? 0 : sapi.World.GetBlock(step.BlockId)?.SideSolid.Any == true ? 1 : 2;

    /// <summary>Every spot of the mineshaft and what goes there (0 = dug out); where pieces meet, the first one's.</summary>
    static Dictionary<BlockPos, BuildStep> MineshaftCells(List<PlacedPiece> pieces)
    {
        var cells = new Dictionary<BlockPos, BuildStep>();
        foreach (var piece in pieces)
        foreach (var step in piece.Piece.Steps)
        {
            cells.TryAdd(piece.At(step), step);
        }
        return cells;
    }

    /// <summary>What miners never dig away in the mineshaft: its built blocks and the floor under every spot it opens.</summary>
    static HashSet<BlockPos> MineshaftGuard(Dictionary<BlockPos, BuildStep> cells)
    {
        var guard = new HashSet<BlockPos>();
        foreach (var (pos, step) in cells)
        {
            if (step.BlockId != 0) guard.Add(pos);
            else if (!cells.TryGetValue(pos.DownCopy(), out var below) || below.BlockId != 0) guard.Add(pos.DownCopy());
        }
        return guard;
    }

    /// <summary>The Miner's Hut's mineshaft guard, for node searches; null when it digs a spiral shaft instead.</summary>
    HashSet<BlockPos>? MineshaftGuard(Colony colony, Building hut) =>
        MineshaftPieces(colony, hut) is { } pieces ? MineshaftGuard(MineshaftCells(pieces)) : null;

    /// <summary>
    /// Where the miner stands to dig out or build at target: where it is now if that will do, or else the
    /// nearest spot two blocks high with a floor (or a ladder) within reach. Never on, in or under the
    /// target itself. Null when there is none yet.
    /// </summary>
    BlockPos? StandFor(BlockPos target, BlockPos at, System.Func<BlockPos, bool> skip, bool building = false)
    {
        var ba = sapi.World.BlockAccessor;
        double reach = Math.Max(1, Config.MinerReach) + 1.5;
        var centre = target.ToVec3d().Add(0.5, 0.5, 0.5);
        bool Passable(BlockPos p) => IsClear(p) || ba.GetBlock(p).Climbable;
        bool Fits(BlockPos p)
        {
            if (p.Equals(target) || p.UpCopy().Equals(target) || p.DownCopy().Equals(target)) return false;
            if (p.ToVec3d().Add(0.5, 1.6, 0.5).DistanceTo(centre) > reach) return false;
            if (ba.GetChunkAtBlockPos(p) == null || !Passable(p) || !Passable(p.UpCopy())) return false;
            if (ba.GetBlock(p, BlockLayersAccess.Fluid).Id != 0) return false;
            return ba.GetBlock(p.DownCopy()).SideSolid[BlockFacing.UP.Index] || ba.GetBlock(p).Climbable || ba.GetBlock(p.DownCopy()).Climbable;
        }
        if (Fits(at)) return at.Copy();
        int r = (int)Math.Ceiling(reach);
        BlockPos? best = null;
        double bestScore = double.MaxValue;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r - 1; dy <= r; dy++)
        for (int dz = -r; dz <= r; dz++)
        {
            var p = target.AddCopy(dx, dy, dz);
            if (skip(p) || !Fits(p)) continue;
            // Close to the target, and a little towards where the miner is, so it does not walk far for each block.
            double score = p.DistanceTo(target) + 0.05 * p.DistanceTo(at);
            if (score >= bestScore) continue;
            (best, bestScore) = (p, score);
        }
        return best;
    }

    /// <summary>True when the miner carries, or the hut's storage holds, everything placing a block costs.</summary>
    bool CanPay(CitizenRecord miner, List<BlockEntityContainer> storage, IReadOnlyList<MaterialCost> costs)
    {
        if (costs.Count == 0) return true;
        foreach (var cost in costs)
        {
            var match = Matcher(cost.Code);
            int have = PackCount(miner, match);
            if (have >= cost.Count) continue;
            foreach (var chest in storage)
            foreach (var slot in chest.Inventory)
            {
                if (!slot.Empty && match(slot.Itemstack)) have += slot.StackSize;
            }
            if (have < cost.Count) return false;
        }
        return true;
    }

    /// <summary>Asks couriers for what a mineshaft block costs, a few at a time, once per item.</summary>
    void RequestMineshaftMaterials(Colony colony, Building hut, CitizenRecord miner, IReadOnlyList<MaterialCost> costs, List<BlockEntityContainer> storage)
    {
        var chest = storage.FirstOrDefault();
        if (chest == null) return;
        string tag = RequestTag(hut);
        foreach (var cost in costs)
        {
            if (colony.Requests.Any(r => r.RequestedBy == tag && r.ItemCode == cost.Code)) continue;
            if (PackCount(miner, Matcher(cost.Code)) >= cost.Count) continue;
            AddRequest(colony, cost.Code, Math.Max(cost.Count, 8), chest.Pos, tag);
        }
    }

    /// <summary>
    /// Puts a mineshaft block in, paying for it from the miner's inventory, as a builder does. False when it
    /// could not: the materials are not at hand, or someone stands where it goes.
    /// </summary>
    public bool BuildMineshaftBlock(Colony colony, CitizenRecord miner, Entity entity, BuildStep step, BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        if (!IsClear(pos) || KeepsBlock(colony, ba.GetBlock(pos), step.BlockId, pos)) return true;
        var (place, costs) = NextPlacement(colony, step, pos);
        var paid = new List<ItemStack>();
        foreach (var cost in costs)
        {
            var taken = TakeFromPack(colony, miner, Matcher(cost.Code), cost.Count);
            if (taken != null) paid.Add(taken);
            if (taken != null && taken.StackSize >= cost.Count) continue;
            foreach (var stack in paid) AddToPack(colony, miner, stack);
            return false;
        }
        place = SwapToMaterial(colony, place, paid.FirstOrDefault());
        if (EntityInTheWay(place, pos) is { } inTheWay && inTheWay.EntityId != entity.EntityId)
        {
            foreach (var stack in paid) AddToPack(colony, miner, stack);
            return false;
        }
        var placedFrom = PlacementStacks(step, place, pos, paid);
        PlaceForBuild(place.Id, pos, placedFrom?.FirstOrDefault());
        FinishPlacement(place, pos);
        if (place.Id == step.BlockId) RestoreSavedDetails(step, pos, placedFrom);
        return true;
    }

    /// <summary>True for things the Miner's Hut's mineshaft still needs built from, which the miner keeps.</summary>
    public bool IsMineshaftMaterial(Building hut, ItemStack stack) =>
        mineshaftNeeds.TryGetValue(hut.BuildingId, out var codes) && codes.Any(code => Matcher(code)(stack));

    /// <summary>
    /// Adds a section to the mineshaft: at the first open side (entrance first, then the sections in the
    /// order they were dug) with nothing beyond it, on a level the hut level has opened, a random section
    /// turned to fit, that keeps clear of the other pieces and of buildings and is in the colony's land.
    /// False when there is nowhere to put one (or a section limit is set and reached).
    /// </summary>
    bool GrowMineshaft(Colony colony, Building hut, List<PlacedPiece> pieces)
    {
        int limit = MineshaftSectionLimit(hut);
        var mine = hut.Mine;
        if (mine.Sections.Count >= limit || MineshaftStyle(hut) is not { } style) return false;
        int levels = MineshaftLevelsOpen(hut, pieces);
        var rand = sapi.World.Rand;
        var others = colony.Buildings.Where(b => b != hut && b != mine).Select(b => BuildingBox(b)).ToList();
        // The entrance and the level pieces are big rooms in a lot of untouched rock, so sections only keep off
        // the spots they dig and build; sections keep off each other's whole box, as before.
        var hubs = pieces.Where(IsHub).ToList();
        var hubCells = new HashSet<BlockPos>(hubs.SelectMany(p => p.Piece.Steps.Select(p.At)));
        var sectionsPlaced = pieces.Where(p => !IsHub(p)).ToList();
        bool Overlaps(PlacedPiece placed)
        {
            BlockPos min = placed.Min, max = placed.Max;
            return sectionsPlaced.Any(p => min.X <= p.Max.X && max.X >= p.Min.X && min.Y <= p.Max.Y && max.Y >= p.Min.Y && min.Z <= p.Max.Z && max.Z >= p.Min.Z)
                || others.Any(o => min.X <= o.Max.X && max.X >= o.Min.X && min.Y <= o.Max.Y && max.Y >= o.Min.Y && min.Z <= o.Max.Z && max.Z >= o.Min.Z)
                || placed.Piece.Steps.Any(step => hubCells.Contains(placed.At(step)));
        }
        bool Taken(BlockPos pos) => hubCells.Contains(pos)
            || sectionsPlaced.Any(p => pos.X >= p.Min.X && pos.X <= p.Max.X && pos.Y >= p.Min.Y && pos.Y <= p.Max.Y && pos.Z >= p.Min.Z && pos.Z <= p.Max.Z);

        // Every open side with nothing beyond it yet, in a random order, so the mine branches out as a maze.
        var sides = pieces.SelectMany(piece => piece.Piece.Open.Select(o => (piece, o.Face, Out: piece.Origin.AddCopy(o.X + o.Face.Normali.X, o.Y, o.Z + o.Face.Normali.Z))))
            .Where(s => LevelAt(pieces, s.Out.Y) <= levels)
            .Where(s => !Taken(s.Out))
            .OrderBy(s => mine.MineLevel > 0 && LevelAt(pieces, s.Out.Y) == mine.MineLevel ? 0 : 1).ThenBy(_ => rand.Next()).ToList();
        int tooHigh = 0, overlap = 0, notOurs = 0;
        foreach (var (_, face, outside) in sides)
        {
            var choices = BlueprintLibrary.MineSections.SelectMany(name => new[] { 0, 90, 180, 270 }.Select(turn => (name, turn)))
                .OrderBy(_ => rand.Next()).ToList();
            foreach (var (name, turn) in choices)
            {
                if (Blueprints.GetMinePiece(style, name, turn) is not { } next) continue;
                var back = next.Open.FirstOrDefault(o => o.Face == face.Opposite);
                if (back.Face == null) continue;
                // Its open side towards this one goes on the spot outside, floors level.
                var origin = new BlockPos(outside.X - back.X, outside.Y - back.Y, outside.Z - back.Z);
                var placed = new PlacedPiece(next, origin);
                // Sections stay under the entrance's ground layer, so none breaks out at the surface.
                if (placed.Min.Y <= 3 || placed.Max.Y >= pieces[0].Origin.Y) { tooHigh++; continue; }
                if (Overlaps(placed)) { overlap++; continue; }
                if (!CanWorkAt(colony, origin)) { notOurs++; continue; }
                mine.Sections.Add(new MineSection { Name = name, Rotation = turn, Origin = origin });
                AddStat(hut, "mine-sections");
                MarkDirty(colony);
                return true;
            }
        }
        // Why not, in the log once per change, for mines that stop growing.
        string why = $"{sides.Count} open sides on levels 1-{levels}; sections tried: {tooHigh} too high or low, {overlap} overlapping, {notOurs} on others' land";
        if (mineshaftStuck.GetValueOrDefault(hut.BuildingId) != why)
        {
            mineshaftStuck[hut.BuildingId] = why;
            Mod.Logger.Notification("Mineshaft of {0} cannot grow: {1}", hut.CustomName ?? "Miner's Hut " + hut.BuildingId, why);
        }
        return false;
    }

    /// <summary>The last reason each Miner's Hut's mineshaft could not grow, so it is logged once. Not saved.</summary>
    readonly Dictionary<int, string> mineshaftStuck = new();

    /// <summary>The Miner's Hut window line for a mineshaft made of pieces: how many sections, and how many blocks are left.</summary>
    public string? MineshaftInfo(Colony colony, Building hut)
    {
        if (MineshaftPieces(colony, hut) is not { } pieces) return null;
        var ba = sapi.World.BlockAccessor;
        int left = 0;
        foreach (var piece in pieces)
        foreach (var step in piece.Piece.Steps)
        {
            var pos = piece.At(step);
            if (ba.GetChunkAtBlockPos(pos) == null) { left++; continue; }
            bool done = step.BlockId == 0 ? IsClear(pos) || NearLiquid(pos) : KeepsBlock(colony, ba.GetBlock(pos), step.BlockId, pos);
            if (!done) left++;
        }
        var o = pieces[0].Origin;
        return Lang.Get("createacolony:mineshaft-info", hut.Mine.Sections.Count, MineshaftLevelsOpen(hut, pieces), LevelCount(pieces), left, o.X, o.Y, o.Z);
    }
}
