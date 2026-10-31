using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Blocks a builder copies with their saved details, as WorldEdit pastes them. A VS Roofing roof block keeps
/// its frame, slope, materials and fill in its block entity, not in its name. A builder pays for what the roof
/// holds (frame, the binding in the other hand, roofing materials, sod plants) and places the roof in one go.
/// VS Roofing is optional: without it the roof blocks are simply missing from the blueprint, as any block from
/// a mod that is not installed. A chiseled block (0.159.0) keeps its shape and materials the same way: the
/// builder pays one full block of each material, as a player chisels it from, and places the finished shape.
/// </summary>
public partial class ColonyManager
{
    const string RoofingDomain = "vsroofing";

    readonly ConditionalWeakTable<SavedBlockEntity, List<MaterialCost>> savedCostCache = new();

    /// <summary>
    /// One roofing material as VS Roofing saves it (its AutoRoofMaterialCollection): item stacks written
    /// the game's way. Only the stacks are read; the mod's other fields are skipped.
    /// </summary>
    [ProtoContract]
    public class RoofMaterialRecord
    {
        [ProtoMember(1)] public byte[]? Primary;
        [ProtoMember(2)] public List<byte[]>? Additional;
        [ProtoMember(3)] public byte[]? Offhand;
    }

    /// <summary>
    /// Gives a just-placed block the details the blueprint saved for it. A block placed from its item
    /// (furniture, banner poles) set itself up from that item, so it only gets its banner, if any.
    /// </summary>
    public void RestoreSavedDetails(BuildStep step, BlockPos pos, IReadOnlyList<ItemStack>? placedFrom = null)
    {
        var ba = sapi.World.BlockAccessor;
        var block = ba.GetBlock(pos);
        var be = ba.GetBlockEntity(pos);
        if (placedFrom != null)
        {
            FinishPlacedItem(block, be, placedFrom);
            return;
        }
        if (step.Saved is not { } saved) return;
        if (be == null || block.EntityClass != sapi.World.ClassRegistry.GetBlockEntityClass(be.GetType())) return;
        try
        {
            // The same steps as the game's own schematic paste.
            var tree = saved.Tree();
            tree.SetInt("posx", pos.X);
            tree.SetInt("posy", pos.InternalY);
            tree.SetInt("posz", pos.Z);
            tree.SetString("blockCode", block.Code.ToShortString());
            be.FromTreeAttributes(tree, sapi.World);
            be.OnLoadCollectibleMappings(sapi.World, saved.Source.BlockCodes, saved.Source.ItemCodes, sapi.World.Rand.Next(), false);
            be.OnPlacementBySchematic(sapi, ba, pos, null, 0, null, false);
            SetUpDoor(block, ba.GetBlockEntity(pos) ?? be, pos);
            (ba.GetBlockEntity(pos) ?? be).MarkDirty(true);
        }
        catch (Exception e)
        {
            sapi.Logger.Warning("[createacolony] Could not copy the saved details of {0} at {1}: {2}", block.Code, pos, e.Message);
        }
    }

    /// <summary>
    /// Gives a block in a ghost preview its saved details, so roofs, furniture and banners look as they will
    /// when built. Only for looks: nothing in a preview ticks or can be used.
    /// </summary>
    public void GhostDetails(IBlockAccessor ba, BuildStep step, BlockPos pos)
    {
        if (step.Saved is not { } saved) return;
        var block = ba.GetBlock(pos);
        var be = ba.GetBlockEntity(pos);
        if (be == null || block.EntityClass != sapi.World.ClassRegistry.GetBlockEntityClass(be.GetType())) return;
        try
        {
            var tree = saved.Tree();
            tree.SetInt("posx", pos.X);
            tree.SetInt("posy", pos.InternalY);
            tree.SetInt("posz", pos.Z);
            tree.SetString("blockCode", block.Code.ToShortString());
            be.FromTreeAttributes(tree, sapi.World);
            be.OnLoadCollectibleMappings(sapi.World, saved.Source.BlockCodes, saved.Source.ItemCodes, 0, false);
            be.MarkDirty(true);
        }
        catch (Exception e)
        {
            sapi.Logger.VerboseDebug("[createacolony] Ghost preview without the saved details of {0}: {1}", block.Code, e.Message);
        }
    }

    /// <summary>
    /// A door turned the way the blueprint has it: its hit boxes follow the turn, it pairs with a door beside it,
    /// and its top half goes in, as when a player hangs it.
    /// </summary>
    void SetUpDoor(Block block, BlockEntity be, BlockPos pos)
    {
        if (be.GetBehavior<BEBehaviorDoor>() is not { } door) return;
        setupDoor ??= AccessTools.Method(typeof(BEBehaviorDoor), "SetupRotationsAndColSelBoxes");
        setupDoor?.Invoke(door, new object[] { true });
        block.GetBehavior<BlockBehaviorDoor>()?.placeMultiblockParts(sapi.World, pos);
    }

    static MethodInfo? setupDoor;

    /// <summary>What a block with saved details costs, or null when the block's own item will do.</summary>
    List<MaterialCost>? SavedCost(BuildStep step, Block target)
    {
        if (step.Saved is not { } saved || (target.Code.Domain != RoofingDomain && target is not BlockMicroBlock)) return null;
        if (savedCostCache.TryGetValue(saved, out var cached)) return cached;

        var costs = new List<MaterialCost>();
        try
        {
            var parts = target is BlockMicroBlock ? ChiseledParts(saved) : RoofParts(saved);
            foreach (var (code, sample, count) in parts)
            {
                int i = costs.FindIndex(c => c.Code == code);
                if (i >= 0) costs[i] = costs[i] with { Count = costs[i].Count + count };
                else costs.Add(new MaterialCost(code, sample, count));
            }
        }
        catch (Exception e)
        {
            sapi.Logger.Warning("[createacolony] Could not read what a {0} in a blueprint is made of, it will cost nothing: {1}", target.Code, e.Message);
            costs.Clear();
        }
        savedCostCache.AddOrUpdate(saved, costs);
        return costs;
    }

    /// <summary>The block a block drops when broken, if it drops exactly one other block (planks of any facing drop upright ones); else null.</summary>
    static Block? DroppedBlock(Block block)
    {
        if (block.Drops is not { Length: 1 } drops || drops[0].ResolvedItemstack?.Block is not { } dropped) return null;
        return dropped.Id != block.Id && drops[0].Quantity.avg <= 1 ? dropped : null;
    }

    /// <summary>
    /// What a chiseled block in a blueprint costs: one full block of each material in it, as a player needs one
    /// to chisel from or to add a material with Ctrl + right-click. A material players cannot carry counts as
    /// what it drops (planks laid sideways as upright planks; raw rock, which only drops stones, as its stones).
    /// </summary>
    IEnumerable<(string Code, ItemStack Sample, int Count)> ChiseledParts(SavedBlockEntity saved)
    {
        var tree = saved.Tree();
        var materials = new List<Block>();
        if (tree["materials"] is IntArrayAttribute ids)
        {
            foreach (int id in ids.value)
            {
                if (saved.Source.BlockCodes.TryGetValue(id, out var code) && sapi.World.GetBlock(code) is { IsMissing: false } b) materials.Add(b);
            }
        }
        else if (tree["materials"] is StringArrayAttribute codes)
        {
            foreach (string code in codes.value)
            {
                if (sapi.World.GetBlock(new AssetLocation(code)) is { IsMissing: false } b) materials.Add(b);
            }
        }

        var parts = new List<(string, ItemStack, int)>();
        foreach (var material in materials.Where(m => m.Id != 0).DistinctBy(m => m.Id))
        {
            if (DroppedBlock(material) is { } carried)
            {
                parts.Add((carried.Code.ToString(), new ItemStack(carried), 1));
            }
            else if (material.Drops is { Length: 1 } drops && drops[0].ResolvedItemstack is { Item: { } item }
                && drops[0].ResolvedItemstack.Block == null)
            {
                int count = Math.Max(1, (int)Math.Round(drops[0].Quantity.avg));
                parts.Add((item.Code.ToString(), new ItemStack(item), count));
            }
            else
            {
                parts.Add((material.Code.ToString(), new ItemStack(material), 1));
            }
        }
        return parts;
    }

    /// <summary>The items a VS Roofing roof block is made of, as it would drop them when broken.</summary>
    IEnumerable<(string Code, ItemStack Sample, int Count)> RoofParts(SavedBlockEntity saved)
    {
        var tree = saved.Tree();
        var parts = new List<(string, ItemStack, int)>();

        void Add(EnumItemClass type, int id, int count)
        {
            var codes = type == EnumItemClass.Block ? saved.Source.BlockCodes : saved.Source.ItemCodes;
            if (count <= 0 || !codes.TryGetValue(id, out var code)) return;
            CollectibleObject? collectible = type == EnumItemClass.Block ? sapi.World.GetBlock(code) : sapi.World.GetItem(code);
            // A hammer held while building is a tool, not something the roof uses up.
            if (collectible == null || collectible.IsMissing || collectible.Tool != null) return;
            // A block counts as what it drops: planks laid sideways (planks-oak-ns) are the planks players carry
            // (planks-oak-ud). Roofs filled with planks asked for ones no player has (Aaron, 0.104.0).
            if (collectible is Block block && DroppedBlock(block) is { } carried) collectible = carried;
            parts.Add((collectible.Code.ToString(), new ItemStack(collectible), count));
        }
        void AddStack(ItemStack? stack)
        {
            if (stack != null) Add(stack.Class, stack.Id, stack.StackSize);
        }
        // The frame's binding: any of the config's bindings will do when the roof used one of them.
        void AddBinding(ItemStack? stack)
        {
            int before = parts.Count;
            AddStack(stack);
            if (parts.Count == before) return;
            var (code, sample, count) = parts[^1];
            var bindings = Config.RoofBindings;
            if (bindings.Any(b => WildcardUtil.Match(new AssetLocation(b), new AssetLocation(code))))
            {
                parts[^1] = (string.Join("|", bindings), sample, count);
            }
        }
        void AddBytes(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            using var reader = new BinaryReader(new MemoryStream(bytes));
            AddStack(new ItemStack(reader));
        }

        int frame = tree.GetAsInt("Frame", -1);
        if (frame >= 0) Add(EnumItemClass.Item, frame, 1);
        AddBinding(tree.GetItemstack("Offhand"));
        var materials = tree.GetBytes("Materials");
        if (materials != null && materials.Length > 0)
        {
            foreach (var material in SerializerUtil.Deserialize<List<RoofMaterialRecord>>(materials) ?? new())
            {
                AddBytes(material.Primary);
                foreach (var extra in material.Additional ?? new()) AddBytes(extra);
                AddBytes(material.Offhand);
            }
        }
        AddStack(tree.GetItemstack("Plant"));
        // Fill added with Ctrl + right-click is a chiseled block inside the roof: one block of each material.
        if (tree["materials"] is IntArrayAttribute fill)
        {
            foreach (int id in fill.value) Add(EnumItemClass.Block, id, 1);
        }
        return parts;
    }
}
