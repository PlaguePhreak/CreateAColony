using System;
using System.Collections.Generic;
using System.Linq;
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
public class ColonyManager : ModSystem
{
    const string IndexKey = "createacolony:index";
    const string ColonyKeyPrefix = "createacolony:colony:";
    const string ClaimDescriptionPrefix = "Colony: ";

    public const int BaseClaimRadius = 24;
    public const int ClaimRadiusPerLevel = 8;
    public const int ClaimDepthBelow = 16;
    public const int ClaimHeightAbove = 48;

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
        api.StoreModConfig(Config, ColonyConfig.FileName);

        api.Event.SaveGameLoaded += OnSaveGameLoaded;
        api.Event.GameWorldSave += OnGameWorldSave;
        api.Event.RegisterGameTickListener(OnColonyTick, ColonyTickMs, 1000);
    }

    const int ColonyTickMs = 10_000;

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

    // ---- Beds and food --------------------------------------------------------------------

    /// <summary>Returns the citizen's bed, claiming a free bed inside the colony if they have none.</summary>
    public BlockPos? EnsureBed(Colony colony, CitizenRecord citizen)
    {
        var ba = sapi.World.BlockAccessor;
        if (citizen.HomeBedPos != null && ba.GetBlockEntity(citizen.HomeBedPos) is BlockEntityBed) return citizen.HomeBedPos;

        citizen.HomeBedPos = null;
        var taken = new HashSet<BlockPos>(colony.Citizens.Where(c => c.HomeBedPos != null).Select(c => c.HomeBedPos!));
        BlockEntityBed? free = FindBlockEntities<BlockEntityBed>(colony)
            .Where(b => !taken.Contains(b.Pos))
            .OrderBy(b => b.Pos.DistanceSqTo(colony.TownHallPos.X, colony.TownHallPos.Y, colony.TownHallPos.Z))
            .FirstOrDefault();
        if (free == null) return null;

        citizen.HomeBedPos = free.Pos.Copy();
        SaveColony(colony);
        return citizen.HomeBedPos;
    }

    /// <summary>Finds the nearest container in the colony that holds food the settler can eat.</summary>
    public BlockEntityContainer? FindFoodContainer(Colony colony, Entity forEntity)
    {
        return FindBlockEntities<BlockEntityContainer>(colony)
            .Where(be => FindFoodSlot(be, forEntity) != null)
            .OrderBy(be => be.Pos.DistanceSqTo(forEntity.Pos.X, forEntity.Pos.Y, forEntity.Pos.Z))
            .FirstOrDefault();
    }

    /// <summary>The slot with the most filling food in a container, or null.</summary>
    public ItemSlot? FindFoodSlot(BlockEntityContainer container, Entity forEntity)
    {
        ItemSlot? best = null;
        float bestSatiety = 0;
        foreach (var slot in container.Inventory)
        {
            if (slot.Empty) continue;
            var props = slot.Itemstack.Collectible.GetNutritionProperties(sapi.World, slot.Itemstack, forEntity);
            if (props == null || props.Satiety <= bestSatiety) continue;
            best = slot;
            bestSatiety = props.Satiety;
        }
        return best;
    }

    /// <summary>All loaded block entities of a type inside the colony's claim area.</summary>
    IEnumerable<T> FindBlockEntities<T>(Colony colony) where T : BlockEntity
    {
        var area = ClaimArea(colony.TownHallPos, colony.ClaimRadius);
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

            foreach (var citizen in colony.Citizens.ToList())
            {
                if (!citizen.Alive) continue;

                if (citizen.IsDowned)
                {
                    if (now >= citizen.DownedUntilTotalDays)
                    {
                        citizen.DownedUntilTotalDays = 0;
                        citizen.Saturation = Config.SaturationMax * 0.5f;
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

    public FoundColonyResult TryFoundColony(IServerPlayer founder, BlockPos townHallPos, string name, out Colony? colony)
    {
        colony = null;
        if (GetOwnedBy(founder.PlayerUID) != null) return FoundColonyResult.AlreadyOwnsColony;
        if (!sapi.World.Claims.TryAccess(founder, townHallPos, EnumBlockAccessFlags.BuildOrBreak)) return FoundColonyResult.NoPermission;

        var area = ClaimArea(townHallPos, BaseClaimRadius);
        if (sapi.World.Claims.All.Any(c => c.Intersects(area))) return FoundColonyResult.OverlapsClaim;

        colony = new Colony
        {
            ColonyId = Guid.NewGuid().ToString("N"),
            Name = name,
            TownHallPos = townHallPos.Copy(),
            ClaimRadius = BaseClaimRadius,
            FoundedTotalDays = sapi.World.Calendar.TotalDays
        };
        colony.Members.Add(new ColonyMember { PlayerUid = founder.PlayerUID, LastKnownName = founder.PlayerName, Role = ColonyRole.Owner });

        var claim = LandClaim.CreateClaim(founder, protectionLevel: 1);
        claim.Description = ClaimDescription(colony);
        claim.AddArea(area);
        sapi.World.Claims.Add(claim);

        colonies[colony.ColonyId] = colony;
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

    /// <summary>Creates a citizen record and its entity beside the Town Hall. Returns null if the entity type is missing.</summary>
    public CitizenRecord? SpawnSettler(Colony colony, string? name)
    {
        string sex = sapi.World.Rand.Next(2) == 0 ? "male" : "female";
        if (sapi.World.GetEntityType(SettlerCode(sex)) == null)
        {
            Mod.Logger.Error("Entity type {0} not found", SettlerCode(sex));
            return null;
        }

        var record = new CitizenRecord
        {
            CitizenId = colony.NextCitizenId++,
            Name = name ?? Lang.Get("createacolony:settler-default-name", colony.NextCitizenId - 1),
            Sex = sex,
            Saturation = Config.SaturationMax
        };
        colony.Citizens.Add(record);
        SpawnEntityFor(colony, record);
        return record;
    }

    /// <summary>Spawns (or respawns) the entity for an existing citizen record next to the Town Hall.</summary>
    public void SpawnEntityFor(Colony colony, CitizenRecord record)
    {
        var type = sapi.World.GetEntityType(SettlerCode(record.Sex));
        if (type == null) return;

        var entity = (EntitySettler)sapi.World.ClassRegistry.CreateEntity(type);
        entity.ColonyId = colony.ColonyId;
        entity.CitizenId = record.CitizenId;

        // Spread settlers around the Town Hall so they do not spawn inside each other.
        double angle = sapi.World.Rand.NextDouble() * GameMath.TWOPI;
        var spawn = colony.TownHallPos.ToVec3d().Add(0.5 + Math.Cos(angle) * 2, 1, 0.5 + Math.Sin(angle) * 2);
        entity.Pos.SetPos(spawn);
        sapi.World.SpawnEntity(entity);
        entity.GetBehavior<EntityBehaviorNameTag>()?.SetName(record.Name);

        record.EntityId = entity.EntityId;
        record.LastSeenTotalDays = sapi.World.Calendar.TotalDays;
        SaveColony(colony);
    }

    static AssetLocation SettlerCode(string sex) => new AssetLocation(CreateAColonyModSystem.ModId, "settler-" + sex);

    static string ClaimDescription(Colony colony) => ClaimDescriptionPrefix + colony.ColonyId;

    LandClaim? FindClaim(Colony colony)
    {
        string description = ClaimDescription(colony);
        return sapi.World.Claims.All.FirstOrDefault(c => c.Description == description);
    }

    static Cuboidi ClaimArea(BlockPos center, int radius)
    {
        return new Cuboidi(
            center.X - radius, center.Y - ClaimDepthBelow, center.Z - radius,
            center.X + radius, center.Y + ClaimHeightAbove, center.Z + radius
        );
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
