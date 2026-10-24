using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Blocks;

/// <summary>
/// A framed painting that hangs on a wall (blocktypes/painting.json, made by tools/paintings/paintings.py).
/// Each picture is an "art" variant, and pictures come in four sizes. There is one recipe (8 boards around 4
/// linen), and a crafted painting can be any picture of any size: hold it and press F (the tool mode key) to
/// pick one, and the item in hand turns into that picture. The HorizontalAttachable behavior does the hanging, turning and dropping.
/// </summary>
public class BlockPainting : Block
{
    const string ArtVariant = "art";
    const string SideVariant = "side";

    /// <summary>Every picture, in the order of the blocktype's list (small ones first, then wide, tall and large).</summary>
    static string[]? allArts;
    /// <summary>The F menu, made once on the client.</summary>
    static SkillItem[]? modes;

    public string Size => Attributes?["paintingSize"].AsString("small") ?? "small";
    public string Art => Variant[ArtVariant];

    static string[] AllArts(IWorldAccessor world, string domain) => allArts ??= world
        .SearchBlocks(new AssetLocation(domain, "painting-*-north"))
        .OfType<BlockPainting>()
        .OrderBy(b => b.Id)
        .Select(b => b.Art)
        .ToArray();

    public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
    {
        if (api is not ICoreClientAPI capi) return base.GetToolModes(slot, forPlayer, blockSel);
        if (modes != null) return modes;

        modes = AllArts(api.World, Code.Domain).Select(art =>
        {
            var block = api.World.GetBlock(CodeWithVariants(new[] { ArtVariant, SideVariant }, new[] { art, "north" }));
            var picture = new DummySlot(new ItemStack(block));
            return new SkillItem
            {
                Code = new AssetLocation(art),
                Name = block.GetHeldItemName(picture.Itemstack),
                // Draw the painting itself, as the game's recipe picker does.
                RenderHandler = (code, dt, x, y) =>
                {
                    double size = GuiElement.scaled(GuiElementPassiveItemSlot.unscaledSlotSize - 5);
                    capi.Render.RenderItemstackToGui(picture, x + size / 2, y + size / 2, 100,
                        (float)GuiElement.scaled(GuiElementPassiveItemSlot.unscaledItemSize), ColorUtil.WhiteArgb);
                }
            };
        }).ToArray();
        return modes;
    }

    public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        return System.Array.IndexOf(AllArts(api.World, Code.Domain), Art);
    }

    /// <summary>Turns the held painting into the chosen picture. Runs on both sides.</summary>
    public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
    {
        var arts = AllArts(api.World, Code.Domain);
        if (slot.Itemstack == null || toolMode < 0 || toolMode >= arts.Length || arts[toolMode] == Art) return;
        var chosen = api.World.GetBlock(CodeWithVariants(new[] { ArtVariant, SideVariant }, new[] { arts[toolMode], "north" }));
        if (chosen == null) return;
        slot.Itemstack = new ItemStack(chosen, slot.Itemstack.StackSize);
        slot.MarkDirty();
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        dsc.AppendLine(Lang.Get("createacolony:painting-size-" + Size));
        dsc.AppendLine(Lang.Get("createacolony:painting-hint"));
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);
        allArts = null;
        modes = null;
    }
}
