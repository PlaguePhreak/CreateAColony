using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Wattle and daub, built the way a player builds it (the game's BlockWattleFence and BlockDaubWattle). A daub
/// block in a blueprint (daub-tan-wattle, daub-tan-cracked, daub-tan-normal) starts as a wattle fence; the
/// builder puts raw daub (daubraw-{color}) on it for the first coat ("wattle"), then more for "cracked" and
/// more again for "normal", stopping at the stage the blueprint shows. How much each coat takes is the
/// blocks' own "daubUpgradeAmount". When the colony allows any wood or stone, any colour will do for the first
/// coat and the wall takes that colour; later coats, as in the game, must be the wall's own colour.
/// </summary>
public partial class ColonyManager
{
    /// <summary>A daub block's stages, from the first coat to the smooth finish.</summary>
    static readonly string[] DaubStages = { "wattle", "cracked", "normal" };
    const string RawDaubPrefix = "daubraw";

    Dictionary<string, Item>? rawDaubByColor;
    Block? wattleFence;

    /// <summary>A daub block's stage (0 wattle, 1 cracked, 2 normal) and colour, or -1 for any other block.</summary>
    static int DaubStage(Block? block, out string color)
    {
        color = "";
        if (block is not BlockDaubWattle || VariantOf(block, "type") is not { } type || VariantOf(block, "color") is not { } c) return -1;
        int stage = Array.IndexOf(DaubStages, type);
        if (stage >= 0) color = c;
        return stage;
    }

    static bool IsWattleFence(Block? block) => block is BlockWattleFence;

    /// <summary>Raw daub one coat takes: the game reads "daubUpgradeAmount" from the fence (first coat) or the daub block (later coats).</summary>
    static int DaubCoat(Block? block) => block?.Attributes?["daubUpgradeAmount"].AsInt(2) ?? 2;

    Block? DaubBlock(string color, int stage) => sapi.World.GetBlock(new AssetLocation("game", $"daub-{color}-{DaubStages[stage]}"));

    /// <summary>Raw daub items (daubraw-{color}) by colour, for the colours that have daub blocks.</summary>
    Dictionary<string, Item> RawDaub()
    {
        if (rawDaubByColor != null) return rawDaubByColor;
        rawDaubByColor = new Dictionary<string, Item>();
        foreach (var item in sapi.World.Items)
        {
            if (item?.Code == null || !item.Code.Path.StartsWith(RawDaubPrefix) || VariantOf(item, "color") is not { } color) continue;
            if (DaubBlock(color, 0) != null) rawDaubByColor.TryAdd(color, item);
        }
        return rawDaubByColor;
    }

    /// <summary>The wattle fence a daub wall starts from (its joins sort themselves out once placed).</summary>
    Block? WattleFence()
    {
        if (wattleFence != null) return wattleFence;
        var fences = sapi.World.SearchBlocks(new AssetLocation("game:wattle-*")).Where(IsWattleFence).ToList();
        wattleFence = fences.FirstOrDefault(b => b.Code.Path.EndsWith("-empty-free"))
            ?? fences.FirstOrDefault(b => b.Code.Path.EndsWith("-ew-free"))
            ?? fences.FirstOrDefault();
        return wattleFence;
    }

    /// <summary>True when daub of this colour will do: the blueprint's own, or any when the colony allows any type.</summary>
    static bool DaubColorFits(Colony colony, string have, string want) => colony.AnyWoodOrStone || have == want;

    /// <summary>True when the daub standing there is the blueprint's stage or smoother, in a colour that will do.</summary>
    bool DaubFulfils(Colony colony, Block current, Block target, out bool isDaub)
    {
        int want = DaubStage(target, out string wantColor);
        isDaub = want >= 0;
        if (!isDaub) return false;
        int have = DaubStage(current, out string haveColor);
        return have >= want && DaubColorFits(colony, haveColor, wantColor);
    }

    /// <summary>True when a wattle fence or a thinner coat of daub stands where the blueprint has daub, so the builder adds to it.</summary>
    bool DaubInProgress(Colony colony, Block current, Block? target)
    {
        int want = DaubStage(target, out string wantColor);
        if (want < 0 || RawDaub().Count == 0) return false;
        if (IsWattleFence(current)) return true;
        int have = DaubStage(current, out string haveColor);
        return have >= 0 && have < want && DaubColorFits(colony, haveColor, wantColor);
    }

    /// <summary>Raw daub to pay: the colour asked for, or any colour (the asked one as its picture) when any will do.</summary>
    MaterialCost? RawDaubCost(string color, int count, bool anyColor)
    {
        if (!Config.BuildersNeedMaterials) return null;
        var daub = RawDaub();
        if (daub.Count == 0) return null;
        var sample = daub.TryGetValue(color, out var own) ? own : daub.Values.First();
        string code = own != null && !anyColor ? own.Code.ToString() : sample.CodeWithVariant("color", "*").ToString();
        return new MaterialCost(code, new ItemStack(sample), count);
    }

    /// <summary>
    /// The next step towards a daub block: a wattle fence, the first coat on the fence, or one more coat. Null
    /// when the blueprint's block is not daub, or the game has no raw daub to make it from.
    /// </summary>
    (Block Place, IReadOnlyList<MaterialCost> Costs)? DaubPlacement(Colony colony, Block target, BlockPos pos)
    {
        int want = DaubStage(target, out string wantColor);
        if (want < 0 || RawDaub().Count == 0) return null;
        var current = sapi.World.BlockAccessor.GetBlock(pos);
        int have = DaubStage(current, out string haveColor);
        if (have >= 0 && have < want && DaubColorFits(colony, haveColor, wantColor))
        {
            return (DaubBlock(haveColor, have + 1) ?? target, Costs(RawDaubCost(haveColor, DaubCoat(current), false)));
        }
        if (IsWattleFence(current)) return (DaubBlock(wantColor, 0) ?? target, Costs(RawDaubCost(wantColor, DaubCoat(current), colony.AnyWoodOrStone)));
        if (WattleFence() is not { } fence) return null;
        return (fence, Costs(PickCost(colony, fence, pos)));
    }

    /// <summary>Everything still to pay for a daub block: the wattle fence if it is not up yet, and the raw daub for each coat to go.</summary>
    List<MaterialCost>? DaubRemaining(Colony colony, Block target, BlockPos pos)
    {
        int want = DaubStage(target, out string wantColor);
        if (want < 0 || RawDaub().Count == 0) return null;
        var costs = new List<MaterialCost>();
        var current = sapi.World.BlockAccessor.GetBlock(pos);
        int have = DaubStage(current, out string haveColor);
        bool started = have >= 0 && have < want && DaubColorFits(colony, haveColor, wantColor);
        string color = started ? haveColor : wantColor;
        int daub = 0;
        if (!started)
        {
            var fence = IsWattleFence(current) ? current : WattleFence();
            if (fence != current && fence != null && PickCost(colony, fence, pos) is { } fenceCost) costs.Add(fenceCost);
            daub = DaubCoat(fence);
        }
        for (int stage = started ? have : 0; stage < want; stage++) daub += DaubCoat(DaubBlock(color, stage));
        // Later coats have to match the wall, so only a wall not yet started can take any colour.
        if (RawDaubCost(color, daub, colony.AnyWoodOrStone && !started) is { } rawDaub) costs.Add(rawDaub);
        return costs;
    }

    /// <summary>The first coat takes the colour of the raw daub the builder brought; later coats keep the wall's colour.</summary>
    Block? SwapDaubColor(Block place, ItemStack? used)
    {
        if (DaubStage(place, out string color) != 0 || used?.Item is not { } item || !item.Code.Path.StartsWith(RawDaubPrefix)) return null;
        string? brought = VariantOf(item, "color");
        return brought == null || brought == color ? null : DaubBlock(brought, 0);
    }
}
