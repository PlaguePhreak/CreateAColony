using CreateAColony.Colonies;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Blocks;

/// <summary>
/// A hut's own storage: a plain container inside the hut block (25 slots, set by the block's
/// attributes), opened with sneak + right-click or the Storage button of the hut window. Chests next
/// to the hut add more room. A hut with a model ("drawtype": "json") turns it like the Town Hall does,
/// to its building's blueprint rotation; a plain cube hut keeps its plain block look.
/// </summary>
public class BlockEntityColonyHut : BlockEntityGenericTypedContainer
{
    /// <summary>The hut building's blueprint rotation in degrees; -1 until known.</summary>
    public int Rotation = -1;

    MeshData? mesh;
    int meshRotation = -1;

    bool HasModel => Block?.DrawType == EnumDrawType.JSON;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        // A hut placed before it could turn takes its building's rotation.
        if (api.Side == EnumAppSide.Server && Rotation < 0
            && api.ModLoader.GetModSystem<ColonyManager>() is { } manager && manager.GetAt(Pos) is { } colony
            && manager.GetBuilding(colony, Pos) is { } building)
        {
            Rotation = building.Rotation;
            MarkDirty(true);
        }
    }

    /// <summary>Turns the model to a blueprint rotation and sends it to players.</summary>
    public void SetRotation(int rotation)
    {
        Rotation = rotation;
        MarkDirty(true);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (!HasModel || Rotation < 0) return false;
        if (mesh == null || meshRotation != Rotation)
        {
            tesselator.TesselateBlock(Block, out var turned);
            // Blueprint rotation 90 faces a player looking north, whose model needs no turn.
            turned.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, (Rotation - 90) * GameMath.DEG2RAD, 0);
            mesh = turned;
            meshRotation = Rotation;
        }
        mesher.AddMeshData(mesh);
        return true;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt("rotation", Rotation);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        int rotation = tree.GetInt("rotation", -1);
        bool turned = rotation != Rotation;
        Rotation = rotation;
        if (turned && Api?.Side == EnumAppSide.Client) MarkDirty(true);
    }
}
