using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Network;

/// <summary>
/// The ghost preview: the building's real blocks, see-through, where it would stand, as the WorldEdit
/// import tool shows a schematic. It works the same way WorldEdit does: each player gets a preview
/// mini-dimension on the server, the blueprint's blocks are put in it, the game sends its chunks to the
/// client like any other chunks, and the client draws that mini-dimension at the building's spot with the
/// game's preview transparency. Every change (turning, moving, another level) makes the preview afresh,
/// as WorldEdit's CreatePreview does: only moving where the client draws it left the old copy on screen.
/// The old blocks are set to air rather than cleared away: chunks that are emptied and unloaded on the server
/// are never sent again, so the client kept drawing them, and a big building previewed earlier (a Town Hall)
/// showed through the next, smaller one. That still happened between buildings (0.89.0: Aaron saw the Town
/// Hall and the Builder's Hut inside the Guard Tower's preview), so each new placement now gets a mini-dimension
/// of its own: the client only draws the preview dimension it was last told, so whatever an old one still
/// holds on the client is never drawn again.
/// </summary>
public partial class ColonyNetwork
{

    /// <summary>Each player's preview mini-dimension, the blueprint and spot in it right now (null when empty), and the blocks it holds.</summary>
    readonly Dictionary<string, (IMiniDimension Dim, Blueprint? Shown, BlockPos? At)> ghosts = new();
    /// <summary>Where each player's preview has blocks now (inside the mini-dimension), so the next preview can air them out.</summary>
    readonly Dictionary<string, List<(BlockPos Pos, bool Fluid)>> ghostBlocks = new();

    bool GhostMode => Manager.Config.PlacementPreview?.Trim().ToLowerInvariant() is not "outline";
    bool OutlineToo => Manager.Config.PlacementPreview?.Trim().ToLowerInvariant() is "both";

    /// <summary>
    /// Shows the building's blueprint as a ghost to the player. False when the ghost is off in the config
    /// or could not be made, so the caller shows the outline instead.
    /// </summary>
    bool ShowGhost(IServerPlayer player, Building building, Blueprint? blueprint)
    {
        if (blueprint == null || !GhostMode)
        {
            HideGhost(player);
            return false;
        }
        var solid = blueprint.Steps.Where(s => s.BlockId != 0).ToList();
        if (solid.Count == 0)
        {
            HideGhost(player);
            return false;
        }
        try
        {
            int minX = solid.Min(s => s.Dx), minY = solid.Min(s => s.Dy), minZ = solid.Min(s => s.Dz);
            var dim = GhostDimension(player, fresh: ghosts.TryGetValue(player.PlayerUID, out var before) && before.Shown == null);
            // The corner of the blueprint is where the mini-dimension's first block is drawn.
            var origin = Manager.StepPos(building, new BuildStep(minX, minY, minZ, 0));
            var shown = ghosts[player.PlayerUID];
            // The window refreshes without changes too; only a real change remakes the preview.
            if (ReferenceEquals(shown.Shown, blueprint) && origin.Equals(shown.At)) return true;
            EmptyGhost(player.PlayerUID, dim);
            dim.CurrentPos.SetPos(origin);
            ghostBlocks[player.PlayerUID] = FillGhost(dim, solid, minX, minY, minZ);
            ghosts[player.PlayerUID] = (dim, blueprint, origin.Copy());
            serverChannel!.SendPacket(new GhostPreviewPacket { DimId = dim.subDimensionId, X = origin.X, Y = origin.Y, Z = origin.Z }, player);
            return true;
        }
        catch (Exception e)
        {
            sapi!.Logger.Warning("[createacolony] Could not make the ghost preview of {0}, showing the outline instead: {1}", building.Type, e);
            HideGhost(player);
            return false;
        }
    }

    /// <summary>Puts the blueprint's blocks in the mini-dimension, its corner at the mini-dimension's origin. Returns where they went.</summary>
    List<(BlockPos Pos, bool Fluid)> FillGhost(IMiniDimension dim, List<BuildStep> solid, int minX, int minY, int minZ)
    {
        var placed = new List<(BlockPos, bool)>(solid.Count);
        var corner = new BlockPos(0, 0, 0, Dimensions.MiniDimensions);
        dim.AdjustPosForSubDimension(corner);
        foreach (var step in solid)
        {
            var block = sapi!.World.GetBlock(step.BlockId);
            if (block == null) continue;
            var pos = corner.AddCopy(step.Dx - minX, step.Dy - minY, step.Dz - minZ);
            if (block.ForFluidsLayer) dim.SetBlock(step.BlockId, pos, BlockLayersAccess.Fluid);
            else dim.SetBlock(step.BlockId, pos);
            placed.Add((pos, block.ForFluidsLayer));
            // Roofs, furniture and doors draw from their saved details, as in a WorldEdit preview.
            if (step.Saved != null) Manager.GhostDetails(dim, step, pos);
        }
        return placed;
    }

    /// <summary>
    /// Sets the blocks of the player's current preview to air. The emptied chunks stay loaded so the client is
    /// sent them empty. They are not unloaded here: turning the building a few times quickly unloaded chunks
    /// before their empty copy had gone out, and the client kept drawing the old turns. ForgetGhost frees them.
    /// </summary>
    void EmptyGhost(string uid, IMiniDimension dim)
    {
        if (!ghostBlocks.Remove(uid, out var old)) return;
        foreach (var (pos, fluid) in old)
        {
            if (fluid) dim.SetBlock(0, pos, BlockLayersAccess.Fluid);
            else dim.SetBlock(0, pos);
        }
    }

    /// <summary>
    /// The player's preview mini-dimension. A new one is made for each new placement (fresh: nothing shown
    /// since the last was hidden); the old one is emptied and its chunks let go on the server.
    /// </summary>
    IMiniDimension GhostDimension(IServerPlayer player, bool fresh)
    {
        string uid = player.PlayerUID;
        if (ghosts.TryGetValue(uid, out var ghost))
        {
            if (!fresh) return ghost.Dim;
            ghostBlocks.Remove(uid);
            ghost.Dim.ClearChunks();
            ghost.Dim.UnloadUnusedServerChunks();
        }

        var dim = sapi!.World.BlockAccessor.CreateMiniDimension(player.Entity.Pos.XYZ);
        int id = sapi.Server.LoadMiniDimension(dim);
        dim.SetSubDimensionId(id);
        // Marks it as a preview: nothing in it ticks, on the server or the client.
        dim.BlocksPreviewSubDimension_Server = id;
        ghosts[uid] = (dim, null, null);
        return dim;
    }

    /// <summary>Takes the ghost away, on the server and the player's screen.</summary>
    void HideGhost(IServerPlayer player)
    {
        if (!ghosts.TryGetValue(player.PlayerUID, out var ghost)) return;
        if (ghost.Shown != null)
        {
            EmptyGhost(player.PlayerUID, ghost.Dim);
            ghosts[player.PlayerUID] = (ghost.Dim, null, null);
        }
        serverChannel!.SendPacket(new GhostPreviewPacket { DimId = -1 }, player);
    }

    /// <summary>A player left: empty their ghost so its chunks are not kept.</summary>
    void ForgetGhost(string uid)
    {
        if (!ghosts.TryGetValue(uid, out var ghost)) return;
        ghost.Dim.ClearChunks();
        ghost.Dim.UnloadUnusedServerChunks();
        ghostBlocks.Remove(uid);
        ghosts[uid] = (ghost.Dim, null, null);
    }

    // ---- Client ---------------------------------------------------------------------------

    /// <summary>The mini-dimension the client draws as the ghost, or -1.</summary>
    int shownGhost = -1;
    /// <summary>The mini-dimension of the last ghost shown, hidden or not, so a new placement's ghost can empty it.</summary>
    int lastGhost = -1;

    void OnGhostPreview(GhostPreviewPacket packet)
    {
        var world = capi!.World;
        if (packet.DimId < 0)
        {
            if (shownGhost >= 0 && world.MiniDimensions.TryGetValue(shownGhost, out var old)) old.selectionTrackingOriginalPos = null;
            // Leave a WorldEdit preview the player has up alone.
            if (shownGhost >= 0 && Dimensions.BlocksPreviewSubDimension_Client == shownGhost) world.SetBlocksPreviewDimension(-1);
            shownGhost = -1;
            return;
        }
        // A new placement's preview comes in a new mini-dimension: empty the client's copy of the old one.
        if (lastGhost >= 0 && lastGhost != packet.DimId && world.MiniDimensions.TryGetValue(lastGhost, out var previous))
        {
            try { previous.ClearChunks(); } catch (Exception) { }
        }
        lastGhost = packet.DimId;
        var pos = new BlockPos(packet.X, packet.Y, packet.Z);
        world.SetBlocksPreviewDimension(packet.DimId);
        // Exactly as WorldEdit's client does it: the game places the mini-dimension at pos.
        var dim = world.GetOrCreateDimension(packet.DimId, pos.ToVec3d());
        dim.selectionTrackingOriginalPos = pos;
        dim.TrackSelection = false;
        shownGhost = packet.DimId;
    }
}
