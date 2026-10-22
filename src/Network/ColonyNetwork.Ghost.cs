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
/// game's preview transparency. Turning the building refills the blocks; nudging or raising it only
/// moves where the client draws them.
/// </summary>
public partial class ColonyNetwork
{
    const string GhostDimsKey = "createacolony-ghostdims";

    /// <summary>Each player's preview mini-dimension, and the blueprint in it right now (null when empty).</summary>
    readonly Dictionary<string, (IMiniDimension Dim, Blueprint? Shown)> ghosts = new();
    /// <summary>The mini-dimension number each player was given, kept in the save so it is reused after a restart.</summary>
    Dictionary<string, int>? ghostDimIds;

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
            var dim = GhostDimension(player);
            if (!ReferenceEquals(ghosts[player.PlayerUID].Shown, blueprint))
            {
                FillGhost(dim, solid, minX, minY, minZ);
                ghosts[player.PlayerUID] = (dim, blueprint);
            }
            // The corner of the blueprint is where the mini-dimension's first block is drawn.
            var origin = Manager.StepPos(building, new BuildStep(minX, minY, minZ, 0));
            dim.CurrentPos.SetPos(origin.ToVec3d());
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

    /// <summary>Puts the blueprint's blocks in the mini-dimension, its corner at the mini-dimension's origin.</summary>
    void FillGhost(IMiniDimension dim, List<BuildStep> solid, int minX, int minY, int minZ)
    {
        dim.ClearChunks();
        var corner = new BlockPos(0, 0, 0, Dimensions.MiniDimensions);
        dim.AdjustPosForSubDimension(corner);
        foreach (var step in solid)
        {
            var block = sapi!.World.GetBlock(step.BlockId);
            if (block == null) continue;
            var pos = corner.AddCopy(step.Dx - minX, step.Dy - minY, step.Dz - minZ);
            if (block.ForFluidsLayer) dim.SetBlock(step.BlockId, pos, BlockLayersAccess.Fluid);
            else dim.SetBlock(step.BlockId, pos);
            // Roofs, furniture and doors draw from their saved details, as in a WorldEdit preview.
            if (step.Saved != null) Manager.GhostDetails(dim, step, pos);
        }
        dim.UnloadUnusedServerChunks();
    }

    /// <summary>The player's preview mini-dimension, made the first time it is needed.</summary>
    IMiniDimension GhostDimension(IServerPlayer player)
    {
        string uid = player.PlayerUID;
        if (ghosts.TryGetValue(uid, out var ghost)) return ghost.Dim;

        var save = sapi!.WorldManager.SaveGame;
        ghostDimIds ??= save.GetData<Dictionary<string, int>>(GhostDimsKey) ?? new();
        var dim = sapi.World.BlockAccessor.CreateMiniDimension(player.Entity.Pos.XYZ);
        int id;
        if (ghostDimIds.TryGetValue(uid, out int known) && known >= 0) id = sapi.Server.SetMiniDimension(dim, known);
        else
        {
            id = sapi.Server.LoadMiniDimension(dim);
            ghostDimIds[uid] = id;
            save.StoreData(GhostDimsKey, ghostDimIds);
        }
        dim.SetSubDimensionId(id);
        // Marks it as a preview: nothing in it ticks, on the server or the client.
        dim.BlocksPreviewSubDimension_Server = id;
        ghosts[uid] = (dim, null);
        return dim;
    }

    /// <summary>Takes the ghost away, on the server and the player's screen.</summary>
    void HideGhost(IServerPlayer player)
    {
        if (!ghosts.TryGetValue(player.PlayerUID, out var ghost)) return;
        if (ghost.Shown != null)
        {
            ghost.Dim.ClearChunks();
            ghost.Dim.UnloadUnusedServerChunks();
            ghosts[player.PlayerUID] = (ghost.Dim, null);
        }
        serverChannel!.SendPacket(new GhostPreviewPacket { DimId = -1 }, player);
    }

    /// <summary>A player left: empty their ghost so its chunks are not kept.</summary>
    void ForgetGhost(string uid)
    {
        if (!ghosts.TryGetValue(uid, out var ghost)) return;
        ghost.Dim.ClearChunks();
        ghost.Dim.UnloadUnusedServerChunks();
        ghosts[uid] = (ghost.Dim, null);
    }

    // ---- Client ---------------------------------------------------------------------------

    /// <summary>The mini-dimension the client draws as the ghost, or -1.</summary>
    int shownGhost = -1;

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
        var pos = new BlockPos(packet.X, packet.Y, packet.Z);
        world.SetBlocksPreviewDimension(packet.DimId);
        var dim = world.GetOrCreateDimension(packet.DimId, pos.ToVec3d());
        dim.CurrentPos.SetPos(pos.ToVec3d());
        dim.selectionTrackingOriginalPos = pos;
        dim.TrackSelection = false;
        shownGhost = packet.DimId;
    }
}
