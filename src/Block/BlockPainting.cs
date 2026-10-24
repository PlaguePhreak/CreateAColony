using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Blocks;

/// <summary>
/// A framed painting that hangs on a wall (blocktypes/painting.json, made by tools/paintings/paintings.py).
/// Each picture is an "art" variant, and pictures come in four sizes. The crafting recipe makes a blank
/// canvas; a blank canvas in the middle of the grid with buckets of dye around it makes a picture
/// (PaintingRecipes). The HorizontalAttachable behavior does the hanging, turning and dropping.
/// </summary>
public class BlockPainting : Block
{
    public const string Blank = "blank";

    public string Size => Attributes?["paintingSize"].AsString("small") ?? "small";
    public string Art => Variant["art"];
    /// <summary>The dye colours that make this picture, in grid order; empty for the blank canvas.</summary>
    public string[] Dyes => Attributes?["dyes"].AsArray<string>() ?? System.Array.Empty<string>();

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        if (Art == Blank)
        {
            dsc.AppendLine(Lang.Get("createacolony:painting-blank-hint"));
            return;
        }
        dsc.AppendLine(Lang.Get("createacolony:painting-size-" + Size));
        string dyes = string.Join(", ", Dyes.Select(colour => PaintingRecipes.DyeName(world, colour)));
        dsc.AppendLine(Lang.Get("createacolony:painting-dyes", dyes));
    }
}
