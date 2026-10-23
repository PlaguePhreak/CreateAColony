using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Blocks;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

public enum FoundColonyResult
{
    Ok,
    AlreadyOwnsColony,
    OverlapsClaim,
    NoPermission
}

/// <summary>
/// Server-only owner of every colony. All mutations go through here so state stays authoritative
/// in multiplayer and is saved in one place.
/// </summary>
public partial class ColonyManager : ModSystem
{
    const string IndexKey = "createacolony:index";
    const string ColonyKeyPrefix = "createacolony:colony:";
    const string ClaimDescriptionPrefix = "Colony: ";

    ICoreServerAPI sapi = null!;
    readonly Dictionary<string, Colony> colonies = new();

    public ColonyConfig Config { get; private set; } = new();

    public IReadOnlyCollection<Colony> All => colonies.Values;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        try
        {
            Config = api.LoadModConfig<ColonyConfig>(ColonyConfig.FileName) ?? new ColonyConfig();
        }
        catch (Exception e)
        {
            Mod.Logger.Error("Bad {0}, using defaults: {1}", ColonyConfig.FileName, e.Message);
        }
        UpgradeConfig(Config);
        api.StoreModConfig(Config, ColonyConfig.FileName);
        PathDebug = Config.PathDebug;

        api.Event.SaveGameLoaded += OnSaveGameLoaded;
        api.Event.GameWorldSave += OnGameWorldSave;
        api.Event.RegisterGameTickListener(OnColonyTick, ColonyTickMs, 1000);
        api.Event.RegisterGameTickListener(OnScanTick, ScanTickMs);
        api.Event.OnEntitySpawn += OnCreatureReady;
        api.Event.OnEntityLoaded += OnCreatureReady;
        api.Event.RegisterGameTickListener(OnFarWorkTick, FarWorkTickMs);
        StartDoors();
    }

    const int ColonyTickMs = 10_000;

    /// <summary>
    /// Brings a config file from an older version up to the new defaults, but only for values the player left
    /// at the old default, so their own changes stay.
    /// </summary>
    static void UpgradeConfig(ColonyConfig config)
    {
        if (config.ConfigVersion < 1)
        {
            // 0.33.0: the Town Hall starts with a bigger square (100 -> 250).
            if (config.TownHallClaimSize == 100) config.TownHallClaimSize = new ColonyConfig().TownHallClaimSize;
        }
        config.ConfigVersion = ColonyConfig.CurrentConfigVersion;
    }

    public CitizenRecord? FindCitizen(string? colonyId, int citizenId)
    {
        if (colonyId == null) return null;
        return GetById(colonyId)?.Citizens.Find(c => c.CitizenId == citizenId);
    }

    // ---- Storms ---------------------------------------------------------------------------

    /// <summary>True while a temporal storm is active or due within StormWarningHours.</summary>
    public bool IsStormNear()
    {
        if (sapi.World.Config.GetString("temporalStorms", "sometimes") == "off") return false;
        var data = sapi.ModLoader.GetModSystem<SystemTemporalStability>()?.StormData;
        if (data == null) return false;
        if (data.nowStormActive) return true;
        double hoursUntil = (data.nextStormTotalDays - sapi.World.Calendar.TotalDays) * sapi.World.Calendar.HoursPerDay;
        return hoursUntil >= 0 && hoursUntil <= Config.StormWarningHours;
    }

    // ---- Beds --------------------------------------------------------------------

    /// <summary>
    /// Returns the citizen's bed, claiming a free bed inside the colony if they have none. Vanilla beds and
    /// W4RD0's Furniture beds both count; a furniture double bed takes two settlers. A settler with a
    /// partner looks for a bed near the partner's, a child near a parent's; others take a bed in a
    /// Residence before one elsewhere, nearest the Town Hall.
    /// </summary>
    public BlockPos? EnsureBed(Colony colony, CitizenRecord citizen)
    {
        var ba = sapi.World.BlockAccessor;
        if (citizen.HomeBedPos != null && ba.GetBlockEntity(citizen.HomeBedPos) is { } own && BedPlaces(own) > 0) return citizen.HomeBedPos;

        citizen.HomeBedPos = null;
        var taken = BedsTaken(colony);
        var family = (PartnerOf(colony, citizen) is { Alive: true } partner ? new[] { partner } : Array.Empty<CitizenRecord>())
            .Concat(citizen.IsChild ? ParentsOf(colony, citizen) : Enumerable.Empty<CitizenRecord>())
            .Select(c => c.HomeBedPos).FirstOrDefault(p => p != null);
        var anchor = family ?? colony.TownHallPos;
        var boxes = ResidenceBoxes(colony);
        BlockEntity? free = FindBlockEntities<BlockEntity>(colony)
            .Where(b => BedPlaces(b) > taken.GetValueOrDefault(b.Pos))
            .OrderBy(b => family == null && ResidenceAt(colony, b.Pos, boxes) == null)
            .ThenBy(b => b.Pos.DistanceSqTo(anchor.X, anchor.Y, anchor.Z))
            .FirstOrDefault();
        if (free == null) return null;

        citizen.HomeBedPos = free.Pos.Copy();
        SaveColony(colony);
        return citizen.HomeBedPos;
    }

    /// <summary>How many living settlers sleep in each bed.</summary>
    Dictionary<BlockPos, int> BedsTaken(Colony colony) => colony.Citizens
        .Where(c => c.Alive && c.HomeBedPos != null)
        .GroupBy(c => c.HomeBedPos!)
        .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>All loaded block entities of a type on the colony's land: the Town Hall's square and every hut's.</summary>
    IEnumerable<T> FindBlockEntities<T>(Colony colony) where T : BlockEntity
    {
        var claim = FindClaim(colony);
        if (claim == null) yield break;
        // Squares overlap, so each chunk is looked at once and each block entity checked against the whole claim.
        int size = GlobalConstants.ChunkSize;
        var chunks = new HashSet<(int, int, int)>();
        foreach (var area in claim.Areas)
        {
            for (int cx = area.X1 / size; cx <= area.X2 / size; cx++)
            for (int cy = Math.Max(0, area.Y1) / size; cy <= area.Y2 / size; cy++)
            for (int cz = area.Z1 / size; cz <= area.Z2 / size; cz++)
            {
                chunks.Add((cx, cy, cz));
            }
        }
        foreach (var (cx, cy, cz) in chunks)
        {
            var chunk = sapi.World.BlockAccessor.GetChunk(cx, cy, cz);
            if (chunk?.BlockEntities == null) continue;
            foreach (var be in chunk.BlockEntities.Values)
            {
                if (be is T match && claim.PositionInside(be.Pos)) yield return match;
            }
        }
    }

    /// <summary>All loaded block entities of a type inside an area.</summary>
    IEnumerable<T> FindBlockEntities<T>(Cuboidi area) where T : BlockEntity
    {
        int size = GlobalConstants.ChunkSize;
        for (int cx = area.X1 / size; cx <= area.X2 / size; cx++)
        for (int cy = Math.Max(0, area.Y1) / size; cy <= area.Y2 / size; cy++)
        for (int cz = area.Z1 / size; cz <= area.Z2 / size; cz++)
        {
            var chunk = sapi.World.BlockAccessor.GetChunk(cx, cy, cz);
            if (chunk?.BlockEntities == null) continue;
            foreach (var be in chunk.BlockEntities.Values)
            {
                if (be is T match && area.ContainsOrTouches(be.Pos)) yield return match;
            }
        }
    }

    // ---- Respawning -----------------------------------------------------------------------

    /// <summary>
    /// Every few seconds: bring knocked-out settlers back once they have recovered, and respawn
    /// settlers whose entity has been missing for a while (chunk lost, killed while unloaded).
    /// Only runs while the Town Hall area is loaded, so unloaded settlers are not duplicated.
    /// </summary>
    void OnColonyTick(float dt)
    {
        double now = sapi.World.Calendar.TotalDays;
        foreach (var colony in colonies.Values)
        {
            if (sapi.World.BlockAccessor.GetChunkAtBlockPos(colony.TownHallPos) == null) continue;
            EnsureCoreBuildings(colony);
            RefreshClaim(colony);
            TickWanderers(colony, now);
            TickFamilies(colony, now);
            EnsureSkills(colony);
            EnsureTraits(colony);
            TickTraits(colony, now);
            EnsureMinStock(colony);

            foreach (var citizen in colony.Citizens.ToList())
            {
                if (!citizen.Alive) continue;

                if (citizen.IsDowned)
                {
                    if (now >= citizen.DownedUntilTotalDays)
                    {
                        citizen.DownedUntilTotalDays = 0;
                        citizen.Saturation = Config.SaturationMax * 0.5f;
                        LogEvent(colony, "event-recovered", citizen.Name);
                        SpawnEntityFor(colony, citizen);
                    }
                    continue;
                }

                var entity = sapi.World.GetEntityById(citizen.EntityId);
                if (entity != null && entity.Alive)
                {
                    citizen.LastSeenTotalDays = now;
                    continue;
                }

                if (citizen.LastSeenTotalDays > 0 && now - citizen.LastSeenTotalDays > Config.RespawnMissingAfterDays)
                {
                    // A lost visitor has simply moved on.
                    if (citizen.Visitor)
                    {
                        ForgetCitizen(colony, citizen);
                        continue;
                    }
                    Mod.Logger.Notification("Settler {0} of {1} was missing, respawning at the Town Hall", citizen.Name, colony.Name);
                    SpawnEntityFor(colony, citizen);
                }
            }
        }
    }

    public Colony? GetById(string colonyId) => colonies.GetValueOrDefault(colonyId);

    public Colony? GetAt(BlockPos pos)
    {
        foreach (var claim in sapi.World.Claims.Get(pos) ?? Array.Empty<LandClaim>())
        {
            if (claim.Description?.StartsWith(ClaimDescriptionPrefix) == true)
            {
                var colony = colonies.Values.FirstOrDefault(c => ClaimDescription(c) == claim.Description);
                if (colony != null) return colony;
            }
        }
        return null;
    }

    public Colony? GetOwnedBy(string playerUid) => colonies.Values.FirstOrDefault(c => c.OwnerUid == playerUid);

    public FoundColonyResult TryFoundColony(IServerPlayer founder, BlockPos townHallPos, string name, out Colony? colony, int? rotation = null)
    {
        colony = null;
        if (GetOwnedBy(founder.PlayerUID) != null) return FoundColonyResult.AlreadyOwnsColony;
        if (!sapi.World.Claims.TryAccess(founder, townHallPos, EnumBlockAccessFlags.BuildOrBreak)) return FoundColonyResult.NoPermission;

        var area = FitClaim(townHallPos, ClaimSize(BuildingTypes.TownHall), null);
        if (area == null) return FoundColonyResult.OverlapsClaim;

        colony = new Colony
        {
            ColonyId = Guid.NewGuid().ToString("N"),
            Name = name,
            TownHallPos = townHallPos.Copy(),
            ClaimRadius = area.SizeX / 2,
            FoundedTotalDays = sapi.World.Calendar.TotalDays
        };
        colony.Members.Add(new ColonyMember { PlayerUid = founder.PlayerUID, LastKnownName = founder.PlayerName, Role = ColonyRole.Owner });
        colony.Events.Add(new ColonyEvent { TotalDays = colony.FoundedTotalDays, Key = "event-founded", Args = { founder.PlayerName } });

        var claim = LandClaim.CreateClaim(founder, protectionLevel: 1);
        claim.Description = ClaimDescription(colony);
        claim.AddArea(area);
        sapi.World.Claims.Add(claim);

        colonies[colony.ColonyId] = colony;
        AddBuilding(colony, BuildingTypes.TownHall, townHallPos, rotation ?? RotationFor(founder));
        SaveColony(colony);
        return FoundColonyResult.Ok;
    }

    public void Disband(Colony colony)
    {
        var claim = FindClaim(colony);
        if (claim != null) sapi.World.Claims.Remove(claim);

        colonies.Remove(colony.ColonyId);
        sapi.WorldManager.SaveGame.StoreData(ColonyKeyPrefix + colony.ColonyId, null);
        SaveIndex();
    }

    /// <summary>
    /// Adds or changes a member's role, and mirrors build access into the vanilla claim
    /// so vanilla protection and the /land commands agree with the colony.
    /// </summary>
    public void SetMemberRole(Colony colony, string playerUid, string playerName, ColonyRole role)
    {
        colony.Members.RemoveAll(m => m.PlayerUid == playerUid);
        if (role > ColonyRole.Visitor)
        {
            colony.Members.Add(new ColonyMember { PlayerUid = playerUid, LastKnownName = playerName, Role = role });
        }

        var claim = FindClaim(colony);
        if (claim != null && claim.OwnedByPlayerUid != playerUid)
        {
            // Claims are replaced rather than edited so the server re-syncs them to clients.
            sapi.World.Claims.Remove(claim);
            if (role > ColonyRole.Visitor)
            {
                claim.PermittedPlayerUids[playerUid] = EnumBlockAccessFlags.BuildOrBreak | EnumBlockAccessFlags.Use;
                claim.PermittedPlayerLastKnownPlayerName[playerUid] = playerName;
            }
            else
            {
                claim.PermittedPlayerUids.Remove(playerUid);
                claim.PermittedPlayerLastKnownPlayerName.Remove(playerUid);
            }
            sapi.World.Claims.Add(claim);
        }

        SaveColony(colony);
    }

    /// <summary>Spawns the colony's first settlers next to the Town Hall.</summary>
    public void SpawnStartingSettlers(Colony colony)
    {
        int min = Math.Max(0, Config.StartingSettlersMin);
        int max = Math.Max(min, Config.StartingSettlersMax);
        int count = sapi.World.Rand.Next(min, max + 1);
        for (int i = 0; i < count; i++) SpawnSettler(colony, null);
    }

    /// <summary>
    /// Creates a citizen record and its entity, beside the Town Hall or at the given spot. A visitor is
    /// a wanderer asking to join. Returns null if the entity type is missing.
    /// </summary>
    public CitizenRecord? SpawnSettler(Colony colony, string? name, string? sex = null, Vec3d? at = null, bool visitor = false, bool child = false)
    {
        sex ??= RandomSex();
        if (sapi.World.GetEntityType(SettlerCode(sex, child)) == null)
        {
            Mod.Logger.Error("Entity type {0} not found", SettlerCode(sex, child));
            return null;
        }
        name ??= RandomName(colony, sex);
        double now = sapi.World.Calendar.TotalDays;

        var record = new CitizenRecord
        {
            CitizenId = colony.NextCitizenId++,
            Name = name,
            Surname = SurnameOf(name),
            Sex = sex,
            Saturation = Config.SaturationMax,
            Visitor = visitor,
            VisitorUntilTotalDays = visitor ? now + Config.WandererWaitDays : 0,
            JoinedTotalDays = now,
            GrowsUpTotalDays = child ? now + Math.Max(0.01, Config.FamilyChildGrowDays) : 0,
            SkillsRolled = true
        };
        // Grown-ups from outside bring what they learned before; children start from nothing (see GrowUp).
        if (!child) record.SkillXp = RollStartingSkills();
        // Traits too; children get theirs from their parents in HaveChild.
        record.TraitsRolled = true;
        if (!child)
        {
            record.Traits = RollTraits();
            ApplyTraitSkillStart(record.SkillXp, record.Traits);
        }
        colony.Citizens.Add(record);
        SpawnEntityFor(colony, record, at);
        return record;
    }

    /// <summary>Spawns (or respawns) the entity for an existing citizen record, next to the Town Hall unless a spot is given.</summary>
    public void SpawnEntityFor(Colony colony, CitizenRecord record, Vec3d? at = null)
    {
        var type = sapi.World.GetEntityType(SettlerCode(record.Sex, record.IsChild));
        if (type == null) return;

        var entity = (EntitySettler)sapi.World.ClassRegistry.CreateEntity(type);
        entity.ColonyId = colony.ColonyId;
        entity.CitizenId = record.CitizenId;

        entity.Pos.SetPos(at ?? Beside(colony.TownHallPos));
        PresetSkin(record, entity);
        sapi.World.SpawnEntity(entity);
        entity.GetBehavior<EntityBehaviorNameTag>()?.SetName(record.Name);

        record.EntityId = entity.EntityId;
        PrepareSettler(colony, record, entity);
        record.LastSeenTotalDays = sapi.World.Calendar.TotalDays;
        SaveColony(colony);
    }

    /// <summary>A random spot two blocks from a building's block, so settlers do not spawn inside each other.</summary>
    public Vec3d Beside(BlockPos pos)
    {
        double angle = sapi.World.Rand.NextDouble() * GameMath.TWOPI;
        return pos.ToVec3d().Add(0.5 + Math.Cos(angle) * 2, 1, 0.5 + Math.Sin(angle) * 2);
    }

    /// <summary>The entity type of a settler: the small one for children.</summary>
    static AssetLocation SettlerCode(string sex, bool child = false) => new AssetLocation(CreateAColonyModSystem.ModId, (child ? "settlerchild-" : "settler-") + sex);

    static string ClaimDescription(Colony colony) => ClaimDescriptionPrefix + colony.ColonyId;

    LandClaim? FindClaim(Colony colony)
    {
        string description = ClaimDescription(colony);
        return sapi.World.Claims.All.FirstOrDefault(c => c.Description == description);
    }

    void OnSaveGameLoaded()
    {
        colonies.Clear();
        var ids = sapi.WorldManager.SaveGame.GetData<List<string>>(IndexKey) ?? new List<string>();
        foreach (string id in ids)
        {
            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData(ColonyKeyPrefix + id);
                if (data == null) continue;
                var colony = SerializerUtil.Deserialize<Colony>(data);
                colonies[colony.ColonyId] = colony;
            }
            catch (Exception e)
            {
                // One corrupt colony must not take the others down with it.
                Mod.Logger.Error("Failed to load colony {0}: {1}", id, e);
            }
        }
        Mod.Logger.Notification("Loaded {0} colonies", colonies.Count);
    }

    void OnGameWorldSave()
    {
        foreach (var colony in colonies.Values) SaveColony(colony, saveIndex: false);
        SaveIndex();
    }

    public void MarkDirty(Colony colony) => SaveColony(colony);

    void SaveColony(Colony colony, bool saveIndex = true)
    {
        sapi.WorldManager.SaveGame.StoreData(ColonyKeyPrefix + colony.ColonyId, SerializerUtil.Serialize(colony));
        if (saveIndex) SaveIndex();
    }

    void SaveIndex()
    {
        sapi.WorldManager.SaveGame.StoreData(IndexKey, colonies.Keys.ToList());
    }
}
