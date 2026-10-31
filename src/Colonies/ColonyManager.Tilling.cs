using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Farmland in a blueprint is made the way a player makes it (Aaron, 0.97.0: farmland can't be picked up in
/// survival): the builder fills the spot with soil if it isn't soil already, then tills it with a hoe, as the
/// game's ItemHoe does. The farmland is as fertile as the soil it was tilled from.
/// </summary>
public partial class ColonyManager
{
    /// <summary>True when the blueprint wants farmland at pos and soil stands there, ready to be tilled.</summary>
    public bool Tilling(BuildStep step, BlockPos pos) =>
        step.BlockId != 0 && sapi.World.GetBlock(step.BlockId) is { } target && IsFarmland(target) && IsSoil(sapi.World.BlockAccessor.GetBlock(pos));

    /// <summary>The farmland tilling the soil there gives: dry, as fertile as the soil.</summary>
    Block? TilledFrom(Block soil) => sapi.World.GetBlock(new AssetLocation("game", "farmland-dry-" + soil.LastCodePart(1)));

    /// <summary>Tills the soil at pos into farmland, keeping its nutrients, as a player's hoe does. False when it can't.</summary>
    public bool TillSoil(BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var soil = ba.GetBlock(pos);
        if (!IsSoil(soil) || TilledFrom(soil) is not { } farmland) return false;
        TreeAttribute? before = null;
        if (ba.GetBlockEntity(pos) is BlockEntitySoilNutrition nutrition)
        {
            before = new TreeAttribute();
            nutrition.ToTreeAttributes(before);
        }
        if (soil.Sounds != null) sapi.World.PlaySoundAt(soil.Sounds.Place, pos, 0.4, null);
        ba.SetBlock(farmland.BlockId, pos);
        if (ba.GetBlockEntity(pos) is BlockEntityFarmland created) created.OnCreatedFromSoil(soil, before);
        ba.MarkBlockDirty(pos);
        return true;
    }
}
