using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace CreateAColony.Blocks;

/// <summary>
/// A hut's own storage: a plain container inside the hut block (25 slots, set by the block's
/// attributes), opened with sneak + right-click or the Storage button of the hut window. Chests next
/// to the hut add more room. The hut keeps its plain block look.
/// </summary>
public class BlockEntityColonyHut : BlockEntityGenericTypedContainer
{
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator) => false;
}
