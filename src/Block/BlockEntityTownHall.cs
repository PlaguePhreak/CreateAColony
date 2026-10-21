using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace CreateAColony.Blocks;

/// <summary>Holds only the colony id; the colony itself lives in ColonyManager.</summary>
public class BlockEntityTownHall : BlockEntity
{
    public string? ColonyId;

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        if (ColonyId != null) tree.SetString("colonyId", ColonyId);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        ColonyId = tree.GetString("colonyId");
    }
}
