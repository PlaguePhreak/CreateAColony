using System;
using System.Collections.Generic;
using System.IO;
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
/// Blocks a builder copies with their saved details, as WorldEdit pastes them. So far that is VS Roofing:
/// a roof block keeps its frame, slope, materials and fill in its block entity, not in its name. A builder
/// pays for what the roof holds (frame, the binding in the other hand, roofing materials, sod plants) and
/// places the roof in one go. VS Roofing is optional: without it the roof blocks are simply missing from
/// the blueprint, as any block from a mod that is not installed.
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

    /// <summary>Gives a just-placed block the details the blueprint saved for it.</summary>
    public void RestoreSavedDetails(BuildStep step, BlockPos pos)
    {
        if (step.Saved is not { } saved) return;
        var ba = sapi.World.BlockAccessor;
        var block = ba.GetBlock(pos);
        var be = ba.GetBlockEntity(pos);
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
        if (step.Saved is not { } saved || target.Code.Domain != RoofingDomain) return null;
        if (savedCostCache.TryGetValue(saved, out var cached)) return cached;

        var costs = new List<MaterialCost>();
        try
        {
            foreach (var (code, sample, count) in RoofParts(saved))
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
            parts.Add((collectible.Code.ToString(), new ItemStack(collectible), count));
        }
        void AddStack(ItemStack? stack)
        {
            if (stack != null) Add(stack.Class, stack.Id, stack.StackSize);
        }
        void AddBytes(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            using var reader = new BinaryReader(new MemoryStream(bytes));
            AddStack(new ItemStack(reader));
        }

        int frame = tree.GetAsInt("Frame", -1);
        if (frame >= 0) Add(EnumItemClass.Item, frame, 1);
        AddStack(tree.GetItemstack("Offhand"));
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
