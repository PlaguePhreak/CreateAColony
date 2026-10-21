using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// A building block (Town Hall, hut, Warehouse) that is put down with the Builder's Wand rather than
/// by hand: the wand shows where the building would stand, and on Accept the block is placed here.
/// </summary>
public interface IWandPlaceable
{
    /// <summary>Places the block at pos for the player, facing the rotation. Returns a lang key for why not, or null.</summary>
    string? PlaceByWand(IServerPlayer player, BlockPos pos, int rotation);
}
