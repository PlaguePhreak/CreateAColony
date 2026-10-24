using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using CreateAColony.Colonies;
using CreateAColony.Network;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CreateAColony.Map;

/// <summary>One chunk of the Town map's ground: 32 by 32 pixels, one per block, coloured as the world map colours them.</summary>
[ProtoContract]
public class LandTile
{
    [ProtoMember(1)] public int ChunkX;
    [ProtoMember(2)] public int ChunkZ;
    /// <summary>Goes up every time any tile changes, so a client can ask for "everything newer than what I have".</summary>
    [ProtoMember(3)] public long Version;
    /// <summary>The pixels, compressed (see ColonyLandMap.Pack).</summary>
    [ProtoMember(4)] public byte[] Pixels = Array.Empty<byte>();
    [ProtoMember(5)] public int Hash;
}

/// <summary>Client asks for the Town map ground around a colony that changed since Since.</summary>
[ProtoContract]
public class LandMapRequest
{
    [ProtoMember(1)] public string ColonyId = "";
    [ProtoMember(2)] public long Since;
}

[ProtoContract]
public class LandMapPacket
{
    [ProtoMember(1)] public string ColonyId = "";
    [ProtoMember(2)] public List<LandTile> Tiles = new();
    /// <summary>What the client should send as Since next time.</summary>
    [ProtoMember(3)] public long Latest;
    /// <summary>More tiles are waiting; ask again straight away.</summary>
    [ProtoMember(4)] public bool More;
    /// <summary>The server forgot older tiles (it restarted without saving); drop the ones kept.</summary>
    [ProtoMember(5)] public bool Reset;
    /// <summary>The land the Town map may show, in chunks, inclusive.</summary>
    [ProtoMember(6)] public int MinChunkX;
    [ProtoMember(7)] public int MinChunkZ;
    [ProtoMember(8)] public int MaxChunkX;
    [ProtoMember(9)] public int MaxChunkZ;
}

[ProtoContract]
public class LandMapSave
{
    [ProtoMember(1)] public long Version;
    [ProtoMember(2)] public List<LandTile> Tiles = new();
}

/// <summary>
/// The ground for the Town map. The server draws every chunk near a colony that is loaded, whether a player
/// or a settler loaded it, in the same colours and shading as the world map, and keeps the pictures in the
/// save so land stays on the map after it unloads. The client asks for the ones it does not have yet while
/// the Town map is open and keeps them as textures of 8 by 8 chunks.
/// </summary>
public class ColonyLandMap : ModSystem
{
    const string ChannelName = "createacolony-landmap";
    const string SaveKey = "createacolony:landmap";
    const int ChunkSize = GlobalConstants.ChunkSize;
    const int TilesPerPacket = 64;
    /// <summary>Chunks drawn every quarter second, so a big first look does not stall the server.</summary>
    const int TilesPerTick = 12;
    /// <summary>A loaded chunk is drawn again after this long even if nothing told us it changed.</summary>
    const long RedrawMs = 5 * 60_000;

    public override double ExecuteOrder() => 0.4;

    public override void Start(ICoreAPI api)
    {
        api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<LandMapRequest>()
            .RegisterMessageType<LandMapPacket>();
    }

    // ---- Server ---------------------------------------------------------------------------

    ICoreServerAPI? sapi;
    IServerNetworkChannel? serverChannel;
    readonly Dictionary<long, LandTile> tiles = new();
    readonly Dictionary<long, long> drawnAt = new();
    readonly Queue<long> toDraw = new();
    readonly HashSet<long> queued = new();
    readonly HashSet<long> changed = new();
    long version;
    int[] colors = Array.Empty<int>();
    byte[] blockColor = Array.Empty<byte>();
    int wateredge;

    static long Key(int cx, int cz) => ((long)cx << 32) | (uint)cz;
    static (int X, int Z) Unkey(long key) => ((int)(key >> 32), (int)key);

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        serverChannel = api.Network.GetChannel(ChannelName).SetMessageHandler<LandMapRequest>(OnRequest);
        api.Event.SaveGameLoaded += () =>
        {
            var save = Load(api.WorldManager.SaveGame.GetData(SaveKey));
            version = save.Version;
            foreach (var t in save.Tiles) tiles[Key(t.ChunkX, t.ChunkZ)] = t;
        };
        api.Event.GameWorldSave += Save;
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, MakeColors);
        api.Event.ChunkColumnLoaded += (coord, _) => Queue(coord.X, coord.Y);
        api.Event.ChunkDirty += (coord, _, reason) =>
        {
            if (reason != EnumChunkDirtyReason.NewlyLoaded) changed.Add(Key(coord.X, coord.Z));
        };
        api.Event.RegisterGameTickListener(_ => FindLoaded(), 5000, 2000);
        api.Event.RegisterGameTickListener(_ => DrawSome(), 250);
    }

    static LandMapSave Load(byte[]? data)
    {
        if (data == null) return new LandMapSave();
        try { return SerializerUtil.Deserialize<LandMapSave>(data) ?? new LandMapSave(); }
        catch (Exception) { return new LandMapSave(); }
    }

    ColonyManager? Manager => sapi?.ModLoader.GetModSystem<ColonyManager>();

    /// <summary>The land, in chunks, that a colony's Town map may show: its claim and buildings, and TownMapRange blocks around them.</summary>
    public (int MinX, int MinZ, int MaxX, int MaxZ) Range(Colony colony)
    {
        var manager = Manager!;
        int minX = colony.TownHallPos.X, minZ = colony.TownHallPos.Z, maxX = minX, maxZ = minZ;
        foreach (var a in manager.ClaimAreas(colony))
        {
            minX = Math.Min(minX, a.MinX); minZ = Math.Min(minZ, a.MinZ);
            maxX = Math.Max(maxX, a.MaxX); maxZ = Math.Max(maxZ, a.MaxZ);
        }
        int range = Math.Max(64, manager.Config.TownMapRange);
        return ((minX - range) / ChunkSize, (minZ - range) / ChunkSize, (maxX + range) / ChunkSize, (maxZ + range) / ChunkSize);
    }

    List<(int MinX, int MinZ, int MaxX, int MaxZ)> Ranges() => Manager?.All.Select(Range).ToList() ?? new();

    static bool Near(List<(int MinX, int MinZ, int MaxX, int MaxZ)> ranges, int cx, int cz) =>
        ranges.Any(r => cx >= r.MinX && cx <= r.MaxX && cz >= r.MinZ && cz <= r.MaxZ);

    void Queue(int cx, int cz)
    {
        long key = Key(cx, cz);
        if (queued.Contains(key) || !Near(Ranges(), cx, cz)) return;
        queued.Add(key);
        toDraw.Enqueue(key);
    }

    /// <summary>
    /// Queues the loaded chunks near a colony that have no picture yet, have changed, or were drawn long ago,
    /// those nearest a Town Hall first so the colony itself fills in before the land around it.
    /// </summary>
    void FindLoaded()
    {
        if (sapi == null || Manager == null || Manager.All.Count == 0) return;
        var ranges = Ranges();
        var halls = Manager.All.Select(c => (X: c.TownHallPos.X / ChunkSize, Z: c.TownHallPos.Z / ChunkSize)).ToList();
        int mapChunksX = sapi.WorldManager.MapSizeX / ChunkSize;
        long now = sapi.World.ElapsedMilliseconds;
        var stale = new List<(int X, int Z)>();
        foreach (long index in sapi.WorldManager.AllLoadedMapchunks.Keys.ToList())
        {
            int cx = (int)(index % mapChunksX), cz = (int)(index / mapChunksX);
            long key = Key(cx, cz);
            if (queued.Contains(key) || !Near(ranges, cx, cz)) continue;
            if (!tiles.ContainsKey(key) || changed.Contains(key) || now - drawnAt.GetValueOrDefault(key, long.MinValue / 2) > RedrawMs) stale.Add((cx, cz));
        }
        foreach (var (cx, cz) in stale.OrderBy(c => halls.Min(h => Math.Abs(h.X - c.X) + Math.Abs(h.Z - c.Z))))
        {
            long key = Key(cx, cz);
            queued.Add(key);
            toDraw.Enqueue(key);
        }
    }

    /// <summary>Writes the pictures to the save, leaving out land no colony is near any more.</summary>
    void Save()
    {
        var ranges = Ranges();
        foreach (var key in tiles.Keys.Where(k => !Near(ranges, Unkey(k).X, Unkey(k).Z)).ToList()) tiles.Remove(key);
        sapi!.WorldManager.SaveGame.StoreData(SaveKey, SerializerUtil.Serialize(new LandMapSave { Version = version, Tiles = tiles.Values.ToList() }));
    }

    void DrawSome()
    {
        if (sapi == null || colors.Length == 0) return;
        for (int n = 0; n < TilesPerTick && toDraw.Count > 0; n++)
        {
            long key = toDraw.Dequeue();
            queued.Remove(key);
            var (cx, cz) = Unkey(key);
            int[]? pixels;
            try { pixels = Draw(cx, cz); }
            catch (Exception e)
            {
                sapi.Logger.Warning("[createacolony] Could not draw chunk {0},{1} for the Town map: {2}", cx, cz, e.Message);
                pixels = null;
            }
            if (pixels == null) continue;
            changed.Remove(key);
            drawnAt[key] = sapi.World.ElapsedMilliseconds;
            int hash = Hash(pixels);
            if (tiles.TryGetValue(key, out var old) && old.Hash == hash) continue;
            tiles[key] = new LandTile { ChunkX = cx, ChunkZ = cz, Version = ++version, Pixels = Pack(pixels), Hash = hash };
        }
    }

    void OnRequest(IServerPlayer player, LandMapRequest req)
    {
        if (Manager?.GetById(req.ColonyId) is not { } colony) return;
        // Only someone with the colony's Town Hall window open gets its map.
        if (sapi!.ModLoader.GetModSystem<ColonyNetwork>()?.IsViewing(player.PlayerUID, colony.ColonyId) != true) return;
        var r = Range(colony);
        bool reset = req.Since > version;
        long since = reset ? 0 : req.Since;
        var newer = tiles.Values
            .Where(t => t.Version > since && t.ChunkX >= r.MinX && t.ChunkX <= r.MaxX && t.ChunkZ >= r.MinZ && t.ChunkZ <= r.MaxZ)
            .OrderBy(t => t.Version)
            .Take(TilesPerPacket + 1)
            .ToList();
        bool more = newer.Count > TilesPerPacket;
        if (more) newer.RemoveAt(newer.Count - 1);
        serverChannel!.SendPacket(new LandMapPacket
        {
            ColonyId = colony.ColonyId, Tiles = newer, More = more, Reset = reset,
            Latest = more ? newer[^1].Version : version,
            MinChunkX = r.MinX, MinChunkZ = r.MinZ, MaxChunkX = r.MaxX, MaxChunkZ = r.MaxZ
        }, player);
    }

    /// <summary>The world map's colours for every block, as the game's own map works them out.</summary>
    void MakeColors()
    {
        var byCode = new Vintagestory.API.Datastructures.OrderedDictionary<string, int>();
        foreach (var kv in ChunkMapLayer.hexColorsByCode) byCode[kv.Key] = ColorUtil.ReverseColorBytes(ColorUtil.Hex2Int(kv.Value));
        colors = new int[byCode.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = byCode.GetValueAtIndex(i);
        wateredge = byCode["wateredge"];
        int land = byCode.IndexOfKey("land");
        var blocks = sapi!.World.Blocks;
        blockColor = new byte[blocks.Count];
        for (int i = 0; i < blockColor.Length; i++)
        {
            var block = blocks[i];
            string? code = "land";
            if (block?.Attributes != null)
            {
                code = block.Attributes["mapColorCode"].AsString();
                if (code == null && !ChunkMapLayer.defaultMapColorCodes.TryGetValue(block.BlockMaterial, out code)) code = "land";
            }
            int index = byCode.IndexOfKey(code);
            blockColor[i] = (byte)(index < 0 ? land : index);
        }
    }

    static bool IsLake(Block block) => block.BlockMaterial == EnumBlockMaterial.Water || (block.BlockMaterial == EnumBlockMaterial.Ice && block.Code.Path != "glacierice");

    int ColorOf(Block block) => colors[block.Id < blockColor.Length ? blockColor[block.Id] : 0];

    /// <summary>
    /// One chunk's picture, made the way the game draws its world map (ChunkMapLayer.GenerateChunkImage without
    /// colour accurate mode): a colour for what is on top, lake edges inked, and slopes shaded and blurred.
    /// Null while the chunk is not fully loaded.
    /// </summary>
    int[]? Draw(int cx, int cz)
    {
        var world = sapi!.WorldManager;
        var ba = sapi.World.BlockAccessor;
        var mc = world.GetMapChunk(cx, cz);
        if (mc == null) return null;
        int columnHeight = world.MapSizeY / ChunkSize;
        for (int cy = 0; cy < columnHeight; cy++)
        {
            if (world.GetChunk(cx, cy, cz) == null) return null;
        }
        var neibNW = world.GetMapChunk(cx - 1, cz - 1);
        var neibW = world.GetMapChunk(cx - 1, cz);
        var neibN = world.GetMapChunk(cx, cz - 1);

        var image = new int[ChunkSize * ChunkSize];
        var shadow = new byte[image.Length];
        Array.Fill(shadow, (byte)128);
        var pos = new BlockPos(0);

        Block BlockAt(int x, int y, int z)
        {
            pos.Set(x, y, z);
            return ba.GetBlock(pos, BlockLayersAccess.FluidOrSolid) ?? sapi.World.Blocks[0];
        }

        for (int i = 0; i < image.Length; i++)
        {
            int y = mc.RainHeightMap[i];
            if (y / ChunkSize >= columnHeight) continue;
            int lx = i % ChunkSize, lz = i / ChunkSize;
            int topX = lx - 1, leftZ = lz - 1;

            IMapChunk? leftTop = mc, rightTop = mc, leftBot = mc;
            if (topX < 0 && leftZ < 0) { leftTop = neibNW; rightTop = neibW; leftBot = neibN; }
            else
            {
                if (topX < 0) { leftTop = neibW; rightTop = neibW; }
                if (leftZ < 0) { leftTop = neibN; leftBot = neibN; }
            }
            topX = GameMath.Mod(topX, ChunkSize);
            leftZ = GameMath.Mod(leftZ, ChunkSize);
            int dLeftTop = leftTop == null ? 0 : y - leftTop.RainHeightMap[leftZ * ChunkSize + topX];
            int dRightTop = rightTop == null ? 0 : y - rightTop.RainHeightMap[lz * ChunkSize + topX];
            int dLeftBot = leftBot == null ? 0 : y - leftBot.RainHeightMap[leftZ * ChunkSize + lx];
            float slopeDir = Math.Sign(dLeftTop) + Math.Sign(dRightTop) + Math.Sign(dLeftBot);
            float steepness = Math.Max(Math.Max(Math.Abs(dLeftTop), Math.Abs(dRightTop)), Math.Abs(dLeftBot));
            float b = 1;
            if (slopeDir > 0) b = 1.08f + Math.Min(0.5f, steepness / 10f) / 1.25f;
            if (slopeDir < 0) b = 0.92f - Math.Min(0.5f, steepness / 10f) / 1.25f;

            int x = cx * ChunkSize + lx, z = cz * ChunkSize + lz;
            var block = BlockAt(x, y, z);
            if (block.BlockMaterial == EnumBlockMaterial.Snow && y > 0)
            {
                y--;
                block = BlockAt(x, y, z);
            }

            if (IsLake(block))
            {
                // Water: inked where it meets land, as the world map does. Unloaded neighbours count as water.
                bool Water(int nx, int nz) => ba.GetMapChunk(nx / ChunkSize, nz / ChunkSize) == null || IsLake(BlockAt(nx, y, nz));
                bool open = Water(x - 1, z) && Water(x + 1, z) && Water(x, z - 1) && Water(x, z + 1);
                image[i] = open ? ColorOf(block) : wateredge;
            }
            else
            {
                shadow[i] = (byte)(shadow[i] * b);
                image[i] = ColorOf(block);
            }
        }

        var sharp = (byte[])shadow.Clone();
        BlurTool.Blur(shadow, ChunkSize, ChunkSize, 2);
        for (int i = 0; i < shadow.Length; i++)
        {
            float b = ((int)((shadow[i] / 128f - 1f) * 5)) / 5f;
            b += (((sharp[i] / 128f - 1f) * 5) % 1) / 5f;
            image[i] = ColorUtil.ColorMultiply3Clamped(image[i], b + 1f) | 255 << 24;
        }
        return image;
    }

    static int Hash(int[] pixels)
    {
        unchecked
        {
            int h = (int)2166136261;
            foreach (int p in pixels) h = (h ^ p) * 16777619;
            return h;
        }
    }

    public static byte[] Pack(int[] pixels)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest))
        {
            deflate.Write(MemoryMarshal.AsBytes(pixels.AsSpan()));
        }
        return output.ToArray();
    }

    public static int[]? Unpack(byte[] data)
    {
        var pixels = new int[ChunkSize * ChunkSize];
        try
        {
            using var deflate = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
            deflate.ReadExactly(MemoryMarshal.AsBytes(pixels.AsSpan()));
            return pixels;
        }
        catch (Exception) { return null; }
    }

    // ---- Client ---------------------------------------------------------------------------

    /// <summary>Chunks a side in one texture.</summary>
    public const int RegionChunks = 8;
    const int RegionPixels = RegionChunks * ChunkSize;
    /// <summary>How often an open Town map asks for changes.</summary>
    const long PollMs = 2000;

    ICoreClientAPI? capi;
    IClientNetworkChannel? clientChannel;
    readonly Dictionary<long, int[]> clientTiles = new();
    readonly Dictionary<long, Region> regions = new();
    // Per colony, since each colony's map is sent only the land around it.
    readonly Dictionary<string, long> clientSince = new();
    string? asking;
    long askedAt = long.MinValue / 2;
    readonly Dictionary<string, (int MinX, int MinZ, int MaxX, int MaxZ)> ranges = new();

    class Region
    {
        public LoadedTexture? Texture;
        public bool Dirty = true;
        public bool Empty = true;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        clientChannel = api.Network.GetChannel(ChannelName).SetMessageHandler<LandMapPacket>(OnPacket);
    }

    /// <summary>Called every frame by an open Town map: asks for new ground every couple of seconds.</summary>
    public void Poll(string colonyId)
    {
        if (capi == null || clientChannel == null) return;
        long now = capi.ElapsedMilliseconds;
        // An answer that never came (the window was shut on the server) is given up on after a while.
        if (asking != null && now - askedAt < 10_000) return;
        if (asking == null && now - askedAt < PollMs) return;
        asking = colonyId;
        askedAt = now;
        clientChannel.SendPacket(new LandMapRequest { ColonyId = colonyId, Since = clientSince.GetValueOrDefault(colonyId) });
    }

    /// <summary>The land, in chunks, that the server lets this colony's map show; null until it has answered.</summary>
    public (int MinX, int MinZ, int MaxX, int MaxZ)? RangeOf(string colonyId) => ranges.TryGetValue(colonyId, out var r) ? r : null;

    public bool HasAnyTiles => clientTiles.Count > 0;

    void OnPacket(LandMapPacket packet)
    {
        ranges[packet.ColonyId] = (packet.MinChunkX, packet.MinChunkZ, packet.MaxChunkX, packet.MaxChunkZ);
        if (packet.Reset)
        {
            clientTiles.Clear();
            clientSince.Clear();
            foreach (var r in regions.Values) r.Dirty = true;
        }
        foreach (var t in packet.Tiles)
        {
            if (Unpack(t.Pixels) is not { } pixels) continue;
            clientTiles[Key(t.ChunkX, t.ChunkZ)] = pixels;
            long regionKey = Key(FloorDiv(t.ChunkX, RegionChunks), FloorDiv(t.ChunkZ, RegionChunks));
            if (!regions.TryGetValue(regionKey, out var region)) regions[regionKey] = region = new Region();
            region.Dirty = true;
        }
        clientSince[packet.ColonyId] = packet.Latest;
        asking = null;
        // More waiting: ask again on the next frame.
        if (packet.More) askedAt = long.MinValue / 2;
    }

    /// <summary>
    /// The texture of region (rx, rz) (RegionChunks chunks a side, its top left block at rx*RegionPixels),
    /// or 0 when nothing there is known. Remakes at most budget changed regions per call to keep frames smooth.
    /// </summary>
    public int RegionTexture(int rx, int rz, ref int budget)
    {
        if (capi == null || !regions.TryGetValue(Key(rx, rz), out var region)) return 0;
        if (region.Dirty && budget > 0)
        {
            budget--;
            region.Dirty = false;
            var pixels = new int[RegionPixels * RegionPixels];
            region.Empty = true;
            for (int dz = 0; dz < RegionChunks; dz++)
            {
                for (int dx = 0; dx < RegionChunks; dx++)
                {
                    if (!clientTiles.TryGetValue(Key(rx * RegionChunks + dx, rz * RegionChunks + dz), out var tile)) continue;
                    region.Empty = false;
                    for (int row = 0; row < ChunkSize; row++)
                    {
                        Array.Copy(tile, row * ChunkSize, pixels, (dz * ChunkSize + row) * RegionPixels + dx * ChunkSize, ChunkSize);
                    }
                }
            }
            region.Texture ??= new LoadedTexture(capi);
            capi.Render.LoadOrUpdateTextureFromRgba(pixels, false, 0, ref region.Texture);
        }
        return region.Empty || region.Texture == null ? 0 : region.Texture.TextureId;
    }

    public static int RegionSize => RegionPixels;

    public static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);

    public override void Dispose()
    {
        foreach (var r in regions.Values) r.Texture?.Dispose();
        regions.Clear();
        base.Dispose();
    }
}
