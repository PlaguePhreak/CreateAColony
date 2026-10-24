using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Colonies take over traders (Aaron, TRADERS_DRAFT.md). Trader land claims are taken away everywhere. When a
/// settler comes within TraderFindRange of a trader and can see it, the trader leaves the game and a new
/// settler takes its place, skilled for its trade (config/traderjobs.json). Some of its goods go to the
/// warehouse. A full colony keeps the newcomer waiting at the Tavern until there is room. A builder then takes
/// the trader's house down without being asked: clutter is destroyed, everything else goes to the builder's hut.
/// </summary>
public partial class ColonyManager
{
    const string TraderJobsAsset = "config/traderjobs.json";
    /// <summary>A trader found further than this from the Town Hall joins there instead of where it stood.</summary>
    const int TraderJoinAtTownHallFrom = 64;
    /// <summary>A trader house box is searched for this far around the trader.</summary>
    const int TraderHouseSearch = 8;

    class TraderJobsFile { public Dictionary<string, List<TraderJobChoice>> Traders = new(); }
    class TraderJobChoice { public string Job = ""; public string Skill = ""; }

    Dictionary<string, List<TraderJobChoice>>? traderJobs;
    /// <summary>Trader claim areas taken away this session, so a joining trader's house can still be found if the map region forgot it.</summary>
    readonly List<Cuboidi> liftedTraderClaims = new();

    Dictionary<string, List<TraderJobChoice>> TraderJobs() => traderJobs ??=
        sapi.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, TraderJobsAsset))?.ToObject<TraderJobsFile>()?.Traders
        ?? new Dictionary<string, List<TraderJobChoice>>();

    // ---- Claims ---------------------------------------------------------------------------

    /// <summary>Takes away every trader land claim in the world (RemoveAllTraderClaims), remembering where they were.</summary>
    void LiftAllTraderClaims()
    {
        if (!Config.RemoveAllTraderClaims) return;
        foreach (var claim in sapi.World.Claims.All.Where(IsClearedClaim).ToList())
        {
            liftedTraderClaims.AddRange(claim.Areas.Select(a => a.Clone()));
            sapi.World.Claims.Remove(claim);
            Mod.Logger.Notification("Removed the trader land claim '{0}' ({1})", claim.Description, claim.LastKnownOwnerName);
        }
        if (liftedTraderClaims.Count > 2000) liftedTraderClaims.RemoveRange(0, liftedTraderClaims.Count - 2000);
    }

    // ---- Finding traders ------------------------------------------------------------------

    /// <summary>A trader of the base game (or one shaped like it): its type is the end of its code, "foods" for humanoid-trader-foods.</summary>
    static bool IsTrader(Entity e) => e.Alive && (e is EntityTrader || e.Code.Path.StartsWith("humanoid-trader-"));

    static string TraderType(Entity e) => e.Code.Path.StartsWith("humanoid-trader-") ? e.Code.Path["humanoid-trader-".Length..] : "other";

    /// <summary>Part of the colony tick: a settler near a trader it can see brings it into the colony.</summary>
    void TickTraders(Colony colony)
    {
        if (!Config.TradersJoin) return;
        float range = Math.Max(1, Config.TraderFindRange);
        foreach (var citizen in colony.Citizens.Where(c => c.Alive && !c.Visitor && !c.IsChild && !c.IsDowned).ToList())
        {
            if (sapi.World.GetEntityById(citizen.EntityId) is not EntityAgent { Alive: true } finder) continue;
            var eye = finder.Pos.XYZ.Add(0, finder.LocalEyePos.Y, 0);
            foreach (var trader in sapi.World.GetEntitiesAround(eye, range, range, IsTrader))
            {
                // Someone trading with it now: wait until they are done.
                if (trader is EntityTradingHumanoid { interactingWithPlayer.Count: > 0 }) continue;
                if (eye.DistanceTo(trader.Pos.XYZ) > range || !CanSee(eye, trader)) continue;
                TraderJoins(colony, citizen, trader);
            }
        }
    }

    /// <summary>True when no solid block stands between the eye and the entity's head.</summary>
    bool CanSee(Vec3d eye, Entity target)
    {
        var to = target.Pos.XYZ.Add(0, target.LocalEyePos.Y, 0);
        BlockSelection? blockSel = null;
        EntitySelection? entitySel = null;
        sapi.World.RayTraceForSelection(eye, to, ref blockSel, ref entitySel,
            (pos, block) => block.Id != 0 && block.CollisionBoxes is { Length: > 0 } && !block.Code.Path.Contains("glass"),
            _ => false);
        return blockSel == null;
    }

    /// <summary>
    /// The trader leaves and a new settler takes its place: with its name, skilled for its trade, waiting at the
    /// Tavern if the colony is full. Some of its goods go to the warehouse, and a builder is told to take its house down.
    /// </summary>
    public CitizenRecord? TraderJoins(Colony colony, CitizenRecord? finder, Entity trader)
    {
        string type = TraderType(trader);
        string name = trader.GetBehavior<EntityBehaviorNameTag>()?.DisplayName is { Length: > 0 } n ? n : "";
        var at = trader.Pos.XYZ.Clone();
        var rand = sapi.World.Rand;
        bool full = IsFull(colony);

        int stored = TraderGoodsToWarehouse(colony, trader);
        var house = TraderHouse(trader.Pos.AsBlockPos);
        JoinEffect(at);
        trader.Die(EnumDespawnReason.Removed);

        // Far from home (an explorer found it), it makes its own way back and turns up at the Town Hall.
        var spot = at.HorizontalSquareDistanceTo(colony.TownHallPos.X, colony.TownHallPos.Z) > TraderJoinAtTownHallFrom * TraderJoinAtTownHallFrom
            ? Beside(colony.TownHallPos) : at;
        string sex = RandomSex();
        var record = SpawnSettler(colony, name.Length > 0 ? name : null, sex, spot);
        if (record == null) return null;
        record.FormerTrade = type;

        var choice = PickTraderJob(type);
        record.PlannedJob = choice.Job;
        ShapeFormerTrader(record, choice);
        record.WaitingForRoom = full;
        if (!full) GiveTraderJob(colony, record);
        else SendToTavern(colony, record);

        string tradeKey = "@marker-trader-" + (Lang.HasTranslation("createacolony:marker-trader-" + type) ? type : "other");
        LogEvent(colony, "event-trader-joined", record.Name, tradeKey, "@job-" + choice.Job, finder?.Name ?? "");
        TellMembers(colony, full ? "createacolony:trader-joined-waiting" : "createacolony:trader-joined", record.Name, Lang.Get("createacolony:job-" + choice.Job), colony.Name);
        if (stored > 0) LogEvent(colony, "event-trader-goods", record.Name, stored.ToString());
        Mod.Logger.Notification("Trader {0} ({1}) joined colony {2} as a {3}; {4} items to the warehouse", record.Name, type, colony.Name, choice.Job, stored);

        if (house != null) QueueTeardown(colony, record.Name, house);
        MarkDirty(colony);
        return record;
    }

    /// <summary>One of the trade's choices whose job exists; a random job when none does.</summary>
    TraderJobChoice PickTraderJob(string type)
    {
        var rand = sapi.World.Rand;
        var table = TraderJobs();
        var choices = (table.GetValueOrDefault(type) ?? table.GetValueOrDefault("other") ?? new List<TraderJobChoice>())
            .Where(c => Jobs.IsValid(c.Job) && c.Job.Length > 0).ToList();
        if (choices.Count > 0) return choices[rand.Next(choices.Count)];
        // Any job a skill trains, with a random second skill.
        var skilled = Skills().Where(s => s.Job.Length > 0 && Jobs.IsValid(s.Job)).ToList();
        if (skilled.Count == 0) return new TraderJobChoice { Job = Jobs.Courier };
        var main = skilled[rand.Next(skilled.Count)];
        var other = Skills().Where(s => s.Code != main.Code).ToList();
        return new TraderJobChoice { Job = main.Job, Skill = other.Count > 0 ? other[rand.Next(other.Count)].Code : "" };
    }

    /// <summary>
    /// Skills and traits for the trade: the job's skill at TraderMainSkillMin-Max with two passions, the second skill
    /// at TraderSecondSkillMin-Max with one, the job's trait, and no trait that rules the job out.
    /// </summary>
    void ShapeFormerTrader(CitizenRecord record, TraderJobChoice choice)
    {
        var rand = sapi.World.Rand;
        var main = SkillForJob(choice.Job);

        // No trait that stops them doing the job, and the job's own trait in place of any other job trait.
        if (main != null) record.Traits.RemoveAll(t => Trait(t.Code) is { } def && def.Effects.ContainsKey("incapable-" + main.Code));
        var jobTrait = TraitDefs().Where(t => t.Job == choice.Job && !t.Earned).ToList();
        if (jobTrait.Count > 0 && Config.Traits)
        {
            var pick = jobTrait[rand.Next(jobTrait.Count)];
            if (!record.Traits.Any(t => t.Code == pick.Code))
            {
                record.Traits.RemoveAll(t => Trait(t.Code) is { } def && pick.ConflictsWith(def));
                record.Traits.Add(new TraitRecord { Code = pick.Code, Level = RollStartLevel() });
            }
        }
        if (Skill(choice.Skill) is { } second && IncapableBy(record.Traits, second.Code).Any())
        {
            record.Traits.RemoveAll(t => Trait(t.Code) is { } def && def.Effects.ContainsKey("incapable-" + second.Code));
        }

        int Roll(int min, int max)
        {
            min = Math.Clamp(min, 0, MaxLevel);
            max = Math.Clamp(Math.Max(min, max), 0, MaxLevel);
            return rand.Next(min, max + 1);
        }
        if (main != null)
        {
            record.SkillXp[main.Code] = Math.Max(record.SkillXp.GetValueOrDefault(main.Code), XpForLevel(Roll(Config.TraderMainSkillMin, Config.TraderMainSkillMax)));
            record.Passions[main.Code] = 2;
        }
        if (Skill(choice.Skill) is { } other && other != main)
        {
            record.SkillXp[other.Code] = Math.Max(record.SkillXp.GetValueOrDefault(other.Code), XpForLevel(Roll(Config.TraderSecondSkillMin, Config.TraderSecondSkillMax)));
            record.Passions[other.Code] = Math.Max(1, Passion(record, other.Code));
        }
        // Passions on skills a trait now rules out go.
        foreach (var code in record.Passions.Keys.Where(k => IncapableBy(record.Traits, k).Any()).ToList()) record.Passions.Remove(code);
    }

    /// <summary>Puts a former trader to work at a built hut with room for its job; it stays jobless when there is none.</summary>
    void GiveTraderJob(Colony colony, CitizenRecord record)
    {
        if (record.PlannedJob.Length == 0 || !string.IsNullOrEmpty(record.JobCode)) return;
        foreach (var hut in colony.Buildings.Where(b => JobOf(b.Type) == record.PlannedJob && b.Level >= 1 && !b.Removing).OrderBy(b => b.BuildingId))
        {
            if (AssignWorker(colony, hut, record) == null) return;
        }
    }

    /// <summary>A newcomer who must wait for room hangs about the Tavern, or the Town Hall without one.</summary>
    void SendToTavern(Colony colony, CitizenRecord record)
    {
        if (sapi.World.GetEntityById(record.EntityId) is not EntityAgent entity) return;
        var tavern = colony.Buildings.Where(b => b.Type == BuildingTypes.Tavern).OrderByDescending(b => b.Level).FirstOrDefault();
        SetWanderHome(entity, (tavern?.HutPos ?? colony.TownHallPos).ToVec3d().Add(0.5, 1, 0.5));
    }

    /// <summary>Part of the colony tick: those waiting at the Tavern join in, one by one, as room opens up.</summary>
    void TickWaitingForRoom(Colony colony)
    {
        foreach (var c in colony.Citizens.Where(c => c.Alive && c.WaitingForRoom).OrderBy(c => c.JoinedTotalDays).ToList())
        {
            if (IsFull(colony))
            {
                // Respawned or reloaded: back to the Tavern.
                SendToTavern(colony, c);
                continue;
            }
            c.WaitingForRoom = false;
            if (sapi.World.GetEntityById(c.EntityId) is EntityAgent entity) SetWanderHome(entity, colony.TownHallPos.ToVec3d().Add(0.5, 1, 0.5));
            GiveTraderJob(colony, c);
            LogEvent(colony, "event-trader-room", c.Name);
            TellMembers(colony, "createacolony:trader-room", c.Name, colony.Name);
            MarkDirty(colony);
        }
    }

    /// <summary>
    /// Each stack the trader was selling, and its money, goes to the warehouse TraderGoodsChance of the time.
    /// Returns how many items went. Without room (or a warehouse) they are lost with the trader.
    /// </summary>
    int TraderGoodsToWarehouse(Colony colony, Entity trader)
    {
        if (trader is not EntityTradingHumanoid { Inventory: { } inv } || colony.WarehousePos == null) return 0;
        var rand = sapi.World.Rand;
        double chance = Math.Clamp(Config.TraderGoodsChance, 0, 1);
        var stacks = new List<ItemStack>();
        foreach (var slot in inv.SellingSlots)
        {
            if (slot?.Itemstack == null || slot.TradeItem is not { Stock: > 0 } item) continue;
            if (rand.NextDouble() >= chance) continue;
            var stack = slot.Itemstack.Clone();
            stack.StackSize = Math.Max(1, slot.Itemstack.StackSize * item.Stock);
            stacks.Add(stack);
        }
        if (!inv.MoneySlot.Empty && rand.NextDouble() < chance) stacks.Add(inv.MoneySlot.Itemstack.Clone());

        int stored = 0;
        foreach (var stack in stacks)
        {
            // Big stocks come in stacks the game allows.
            int left = stack.StackSize;
            int max = Math.Max(1, stack.Collectible.MaxStackSize);
            while (left > 0)
            {
                var part = stack.Clone();
                part.StackSize = Math.Min(max, left);
                left -= part.StackSize;
                int moved = PutInWarehouse(colony, part);
                stored += moved;
                if (moved == 0) return stored; // warehouse full
            }
        }
        return stored;
    }

    /// <summary>A puff of smoke where the trader stood.</summary>
    void JoinEffect(Vec3d at)
    {
        var props = new SimpleParticleProperties(
            20, 28, ColorUtil.ToRgba(160, 200, 200, 200),
            at.AddCopy(-0.4, 0.1, -0.4), at.AddCopy(0.4, 1.8, 0.4),
            new Vec3f(-0.2f, 0.1f, -0.2f), new Vec3f(0.2f, 0.5f, 0.2f),
            1.5f, -0.02f, 0.4f, 0.8f, EnumParticleModel.Quad)
        {
            SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, 0.6f)
        };
        sapi.World.SpawnParticles(props);
    }

    // ---- The trader's house -----------------------------------------------------------------

    /// <summary>
    /// The box of the trader's house: the structure the world placed there (from its map region), or the land
    /// claim it had. Null when neither is known.
    /// </summary>
    Cuboidi? TraderHouse(BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        int size = sapi.WorldManager.RegionSize;
        var near = new Cuboidi(pos.X - TraderHouseSearch, pos.Y - TraderHouseSearch, pos.Z - TraderHouseSearch,
            pos.X + TraderHouseSearch, pos.Y + TraderHouseSearch, pos.Z + TraderHouseSearch);
        Cuboidi? best = null;
        for (int rx = (pos.X - TraderHouseSearch) / size; rx <= (pos.X + TraderHouseSearch) / size; rx++)
        for (int rz = (pos.Z - TraderHouseSearch) / size; rz <= (pos.Z + TraderHouseSearch) / size; rz++)
        {
            var region = ba.GetMapRegion(rx, rz);
            if (region?.GeneratedStructures == null) continue;
            foreach (var s in region.GeneratedStructures)
            {
                if (s.Location == null || s.Code?.Contains("trader", StringComparison.OrdinalIgnoreCase) != true) continue;
                if (!s.Location.Intersects(near)) continue;
                if (best == null || s.Location.Contains(pos.X, pos.Y, pos.Z)) best = s.Location.Clone();
            }
        }
        if (best != null) return best;
        // From the claim it had, here or in an older session (the claim may still be there if lifting is off).
        var claim = liftedTraderClaims.FirstOrDefault(a => a.Intersects(near))
            ?? sapi.World.Claims.All.Where(IsClearedClaim).SelectMany(c => c.Areas).FirstOrDefault(a => a.Intersects(near));
        return claim?.Clone();
    }

    /// <summary>Asks a builder to take the house down, unless it is too far from the Town Hall.</summary>
    void QueueTeardown(Colony colony, string owner, Cuboidi box)
    {
        if (!Config.TraderHouseTeardown) return;
        var center = new BlockPos((box.X1 + box.X2) / 2, box.Y1, (box.Z1 + box.Z2) / 2);
        double far = Math.Max(0, Config.TraderTeardownMaxDistance);
        if (center.HorDistanceSqTo(colony.TownHallPos.X, colony.TownHallPos.Z) > far * far)
        {
            LogEvent(colony, "event-trader-house-far", owner);
            return;
        }
        if (colony.Teardowns.Any(t => t.Area.Intersects(box))) return;
        var order = new TeardownOrder
        {
            Id = colony.NextTeardownId++, Owner = owner,
            X1 = box.X1, Y1 = box.Y1, Z1 = box.Z1, X2 = box.X2, Y2 = box.Y2, Z2 = box.Z2
        };
        order.Total = TeardownSpots(colony, order).Count();
        colony.Teardowns.Add(order);
        LogEvent(colony, "event-trader-house-order", owner);
        MarkDirty(colony);
    }

    /// <summary>The take-down orders a builder could work on, oldest first.</summary>
    public TeardownOrder? TakeTeardown(Colony colony, CitizenRecord builder)
    {
        var own = colony.Teardowns.Find(t => t.BuilderCitizenId == builder.CitizenId);
        if (own != null) return own;
        foreach (var order in colony.Teardowns.OrderBy(t => t.Id))
        {
            var current = colony.Citizens.Find(c => c.CitizenId == order.BuilderCitizenId);
            if (current != null && current.Alive && current.JobCode == Jobs.Builder) continue;
            order.BuilderCitizenId = builder.CitizenId;
            MarkDirty(colony);
            return order;
        }
        return null;
    }

    /// <summary>Blocks of the house still to come down, top layer first.</summary>
    IEnumerable<BlockPos> TeardownSpots(Colony colony, TeardownOrder order)
    {
        var ba = sapi.World.BlockAccessor;
        var pos = new BlockPos(0, 0, 0);
        for (int y = order.Y2; y >= Math.Max(1, order.Y1); y--)
        for (int x = order.X1; x <= order.X2; x++)
        for (int z = order.Z1; z <= order.Z2; z++)
        {
            pos.Set(x, y, z);
            if (ba.GetChunkAtBlockPos(pos) == null) continue;
            if (IsTeardownBlock(colony, pos)) yield return pos.Copy();
        }
    }

    /// <summary>
    /// The next block to take down, skipping those given; null when the house is gone. Loaded is false (and its
    /// land asked for) while part of the house is not loaded, so it is not taken for finished.
    /// </summary>
    public (BlockPos? Spot, bool Loaded) NextTeardownSpot(Colony colony, TeardownOrder order, ICollection<BlockPos>? skip = null)
    {
        var ba = sapi.World.BlockAccessor;
        int size = GlobalConstants.ChunkSize;
        bool loaded = true;
        for (int cx = order.X1 / size; cx <= order.X2 / size; cx++)
        for (int cz = order.Z1 / size; cz <= order.Z2 / size; cz++)
        {
            if (ba.GetChunk(cx, Math.Max(0, order.Y1) / size, cz) != null) continue;
            loaded = false;
            sapi.WorldManager.LoadChunkColumnPriority(cx, cz);
        }
        if (!loaded) return (null, false);
        foreach (var pos in TeardownSpots(colony, order))
        {
            if (skip == null || !skip.Contains(pos)) return (pos, true);
        }
        return (null, true);
    }

    /// <summary>
    /// A block of the house: not air, water, natural ground, soil, a plant, a tree or snow; not a colony block,
    /// not inside a colony building, and nobody else's land.
    /// </summary>
    bool IsTeardownBlock(Colony colony, BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var block = ba.GetBlock(pos);
        if (block.Id == 0 || block.IsLiquid() || BlueprintLibrary.IsNaturalGround(block)) return false;
        if (block.BlockMaterial is EnumBlockMaterial.Soil or EnumBlockMaterial.Plant or EnumBlockMaterial.Leaves
            or EnumBlockMaterial.Snow or EnumBlockMaterial.Ice or EnumBlockMaterial.Gravel or EnumBlockMaterial.Sand) return false;
        string path = block.Code?.Path ?? "";
        // Standing trees (not logs laid in a wall), and loose things the world scatters.
        if (path.StartsWith("log-grown-") || path.StartsWith("logsection-grown-") || path.StartsWith("loose")) return false;
        if (block.Code?.Domain == CreateAColonyModSystem.ModId) return false;
        // The top half of a door comes off with the bottom half; furniture with its main block.
        if (block is BlockMultiblock || IsFurnitureCell(pos)) return false;
        if (!CanWorkAt(colony, pos) || GetBuilding(colony, pos) != null || ZoneAt(colony, pos) != null) return false;
        return true;
    }

    /// <summary>Clutter (the decorations a player needs glue to pick up): the colony destroys it rather than keep it.</summary>
    public static bool IsClutter(Block block) => block is BlockShapeFromAttributes;

    /// <summary>
    /// Takes one block of the house down. Clutter is destroyed; a chest's things and the block itself go to the
    /// builder's hut (or on the ground there when it is full).
    /// </summary>
    public void TakeDownTraderBlock(Colony colony, TeardownOrder order, Building builderHut, BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var block = ba.GetBlock(pos);
        if (!IsTeardownBlock(colony, pos)) return;
        if (IsClutter(block))
        {
            ba.SetBlock(0, pos);
            ba.TriggerNeighbourBlockUpdate(pos);
        }
        else
        {
            if (ba.GetBlockEntity(pos) is BlockEntityContainer chest)
            {
                var storage = HutStorage(colony, builderHut).ToList();
                foreach (var slot in chest.Inventory.Where(s => !s.Empty))
                {
                    var stack = slot.TakeOutWhole();
                    foreach (var container in storage)
                    {
                        if (stack.StackSize <= 0) break;
                        PutItems(container, stack);
                    }
                    if (stack.StackSize > 0) sapi.World.SpawnItemEntity(stack, builderHut.HutPos.ToVec3d().Add(0.5, 1, 0.5));
                }
                chest.MarkDirty(true);
            }
            BreakForBuild(colony, builderHut, pos);
        }
        order.Done++;
        MarkDirty(colony);
    }

    /// <summary>The house is down: the order goes.</summary>
    public void FinishTeardown(Colony colony, TeardownOrder order)
    {
        colony.Teardowns.Remove(order);
        LogEvent(colony, "event-trader-house-down", order.Owner);
        TellMembers(colony, "createacolony:trader-house-down", order.Owner, colony.Name);
        MarkDirty(colony);
    }

    /// <summary>Stops taking a trader's house down; what came down stays down.</summary>
    public void CancelTeardown(Colony colony, TeardownOrder order)
    {
        colony.Teardowns.Remove(order);
        MarkDirty(colony);
    }

    public static string TeardownName(TeardownOrder order) => Lang.Get("createacolony:teardown-name", order.Owner);
}
