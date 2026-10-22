using CreateAColony.Colonies;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CreateAColony.Blocks;

/// <summary>
/// Holds the colony id (the colony itself lives in ColonyManager) and which way the Town Hall faces.
/// The model is turned to match the Town Hall building's blueprint rotation, so the table faces the
/// player who placed it, and turns along when the Town Hall is turned before it is built.
/// </summary>
public class BlockEntityTownHall : BlockEntity
{
    public string? ColonyId;
    /// <summary>The Town Hall building's blueprint rotation in degrees; -1 until known.</summary>
    public int Rotation = -1;

    MeshData? mesh;
    int meshRotation = -1;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        // A Town Hall placed before it could turn takes its building's rotation.
        if (api.Side == EnumAppSide.Server && Rotation < 0 && ColonyId != null
            && api.ModLoader.GetModSystem<ColonyManager>() is { } manager && manager.GetById(ColonyId) is { } colony
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
        if (Rotation < 0 || Block == null) return false;
        if (mesh == null || meshRotation != Rotation)
        {
            tesselator.TesselateBlock(Block, out var turned);
            // Blueprint rotation 90 faces a player looking north. The model's front is a quarter turn to the
            // left of the blueprints' front, so it gets a further 90 degrees clockwise (a negative turn).
            turned.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, (Rotation - 90 - 90) * GameMath.DEG2RAD, 0);
            mesh = turned;
            meshRotation = Rotation;
        }
        mesher.AddMeshData(mesh);
        return true;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        if (ColonyId != null) tree.SetString("colonyId", ColonyId);
        tree.SetInt("rotation", Rotation);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        ColonyId = tree.GetString("colonyId");
        int rotation = tree.GetInt("rotation", -1);
        bool turned = rotation != Rotation;
        Rotation = rotation;
        if (turned && Api?.Side == EnumAppSide.Client) MarkDirty(true);
    }
}
