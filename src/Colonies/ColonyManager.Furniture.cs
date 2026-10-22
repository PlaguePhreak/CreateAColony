using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Blocks a builder places from their item, as a player does: W4RD0's Furniture and Heraldry : Banners.
/// Both mods keep a block's wood and cloth colours as item attributes ("types", from Attribute Rendering
/// Library), not in the block's name, so the blueprint's block entity says which item the block was placed
/// from. A builder pays for that item in the same wood and colours (any wood when the colony allows it),
/// and for the banner on a flag or banner pole, with the same design. Placing the block with the item lets
/// it set itself up: furniture of several blocks puts down its other parts, a shelf its other cells, and
/// the block takes the item's wood. What furniture holds (a cupboard's contents, a shelf's display) is not
/// copied. Both mods are optional: without them their blocks are simply missing from the blueprint.
/// </summary>
public partial class ColonyManager
{
    const string HeraldryDomain = "heraldrybanners";
    const string TypesKey = "types";

    /// <summary>Costs of blocks with saved details, worked out once per blueprint block: [0] exact, [1] any wood.</summary>
    readonly ConditionalWeakTable<SavedBlockEntity, List<MaterialCost>?[]> placedItemCostCache = new();

    // ---- Item codes that ask for types ----------------------------------------------------

    /// <summary>
    /// An item code that also asks for the item's types, such as "w4rd0sfurniture:rustictable-north{cloth=red&amp;wood=oak}".
    /// Keys in <paramref name="open"/> may be anything ("wood=*"). An item with types the code does not name does not match.
    /// </summary>
    public static string WithTypes(string code, ITreeAttribute? types, ICollection<string>? open = null)
    {
        if (types == null || types.Count == 0) return code;
        var parts = types.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => Uri.EscapeDataString(k) + "=" + (open?.Contains(k) == true ? "*" : Uri.EscapeDataString(types.GetString(k) ?? "")));
        return code + "{" + string.Join("&", parts) + "}";
    }

    /// <summary>The code without its types, and the types it asks for (null: any). See <see cref="WithTypes"/>.</summary>
    public static (string Code, Dictionary<string, string>? Types) SplitTypes(string code)
    {
        int open = code.IndexOf('{');
        if (open < 0 || !code.EndsWith("}")) return (code, null);
        var types = new Dictionary<string, string>();
        foreach (var part in code.Substring(open + 1, code.Length - open - 2).Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            types[Uri.UnescapeDataString(part.Substring(0, eq))] = part.Substring(eq + 1) == "*" ? "*" : Uri.UnescapeDataString(part.Substring(eq + 1));
        }
        return (code.Substring(0, open), types);
    }

    /// <summary>True when the stack's types are the ones asked for.</summary>
    public static bool TypesMatch(ItemStack stack, Dictionary<string, string>? wanted)
    {
        if (wanted == null) return true;
        var have = stack.Attributes?.GetTreeAttribute(TypesKey);
        if (have != null && have.Any(kv => !wanted.ContainsKey(kv.Key))) return false;
        foreach (var (key, value) in wanted)
        {
            if (value == "*") continue;
            if (have?.GetString(key) != value) return false;
        }
        return true;
    }

    /// <summary>True when the code asks for types with one left open ("any wood").</summary>
    public static bool AnyType(string code) => SplitTypes(code).Types?.ContainsValue("*") == true;

    // ---- What they cost -------------------------------------------------------------------

    /// <summary>
    /// What a block placed from its item costs: the item with the blueprint's types, then whatever the block
    /// displayed (a banner on a pole). Null for other blocks, or when the blueprint saved nothing for it.
    /// </summary>
    List<MaterialCost>? PlacedItemCosts(BuildStep step, Block target, BlockPos pos, bool anyType)
    {
        if (step.Saved is not { } saved || !Blueprints.PlacedAsItem(target)) return null;
        var cached = placedItemCostCache.GetValue(saved, _ => new List<MaterialCost>?[2]);
        int slot = anyType ? 1 : 0;
        if (cached[slot] is { } known) return known;

        var costs = new List<MaterialCost>();
        try
        {
            var tree = saved.Tree();
            var stack = PickStack(target, pos);
            var types = tree.GetTreeAttribute(TypesKey);
            if (types != null) stack.Attributes[TypesKey] = types.Clone();
            costs.Add(new MaterialCost(WithTypes(stack.Collectible.Code.ToString(), types, anyType ? SwappableVariants : null), stack, 1));
            foreach (var shown in DisplayedStacks(saved, tree, target))
            {
                costs.Add(new MaterialCost(WithTypes(shown.Collectible.Code.ToString(), shown.Attributes.GetTreeAttribute(TypesKey)), shown, shown.StackSize));
            }
        }
        catch (Exception e)
        {
            sapi.Logger.Warning("[createacolony] Could not read which item a {0} in a blueprint is placed from, it will cost the plain block: {1}", target.Code, e.Message);
            costs.Clear();
            costs.Add(new MaterialCost(target.Code.ToString(), new ItemStack(target), 1));
        }
        cached[slot] = costs;
        return costs;
    }

    /// <summary>
    /// The block's item as a player picks it, without asking its block entity, which is not there yet (or
    /// belongs to whatever stands there now). For turnable blocks that is the item's own facing.
    /// </summary>
    ItemStack PickStack(Block block, BlockPos pos)
    {
        ItemStack? stack = null;
        foreach (var behavior in block.BlockBehaviors)
        {
            if (BlueprintLibrary.IsAttributeBehavior(behavior)) continue;
            var handling = EnumHandling.PassThrough;
            try
            {
                stack = behavior.OnPickBlock(sapi.World, pos, ref handling);
            }
            catch (Exception)
            {
                stack = null;
            }
            if (stack != null) break;
        }
        stack ??= new ItemStack(block);
        stack.StackSize = 1;
        return stack;
    }

    /// <summary>The items a Heraldry pole in the blueprint holds (its banner), turned into this world's items.</summary>
    IEnumerable<ItemStack> DisplayedStacks(SavedBlockEntity saved, ITreeAttribute tree, Block target)
    {
        if (target.Code.Domain != HeraldryDomain) yield break;
        if (tree.GetTreeAttribute("inventory")?.GetTreeAttribute("slots") is not { } slots) yield break;
        foreach (var (_, value) in slots)
        {
            if (value is not ItemstackAttribute { value: { } raw }) continue;
            if (FromBlueprint(saved, raw) is { } stack) yield return stack;
        }
    }

    /// <summary>An item stack saved in a blueprint, with the export's item numbers turned into this world's items.</summary>
    ItemStack? FromBlueprint(SavedBlockEntity saved, ItemStack raw)
    {
        if (raw.StackSize <= 0) return null;
        var codes = raw.Class == EnumItemClass.Block ? saved.Source.BlockCodes : saved.Source.ItemCodes;
        if (!codes.TryGetValue(raw.Id, out var code)) return null;
        CollectibleObject? collectible = raw.Class == EnumItemClass.Block ? sapi.World.GetBlock(code) : sapi.World.GetItem(code);
        if (collectible == null || collectible.IsMissing) return null;
        var stack = new ItemStack(collectible, raw.StackSize);
        if (raw.Attributes != null) stack.Attributes = raw.Attributes.Clone();
        return stack;
    }

    // ---- Placing them ---------------------------------------------------------------------

    /// <summary>
    /// The items a block is placed from: what the builder paid, or, when builders need no materials, what the
    /// blueprint holds. The first is the block's own item. Null for blocks placed the plain way.
    /// </summary>
    public IReadOnlyList<ItemStack>? PlacementStacks(BuildStep step, Block place, BlockPos pos, IReadOnlyList<ItemStack> paid)
    {
        if (!Blueprints.PlacedAsItem(place)) return null;
        if (paid.Count > 0) return paid;
        return PlacedItemCosts(step, place, pos, false)?.Select(c => c.Sample.Clone()).ToList()
            ?? new List<ItemStack> { PickStack(place, pos) };
    }

    /// <summary>
    /// After a block went down from its item: makes sure it took the item's types, and hangs a pole's banner.
    /// Nothing else from the blueprint is copied (a cupboard's contents would come free).
    /// </summary>
    void FinishPlacedItem(Block block, BlockEntity? be, IReadOnlyList<ItemStack>? stacks)
    {
        if (be == null || stacks == null || stacks.Count == 0) return;
        if (stacks[0].Attributes?.GetTreeAttribute(TypesKey) is { } types)
        {
            var tree = new TreeAttribute();
            be.ToTreeAttributes(tree);
            if (tree.GetTreeAttribute(TypesKey) == null)
            {
                tree[TypesKey] = types.Clone();
                be.FromTreeAttributes(tree, sapi.World);
            }
        }
        if (block.Code.Domain == HeraldryDomain && be is BlockEntityContainer holder)
        {
            int i = 0;
            foreach (var stack in stacks.Skip(1))
            {
                while (i < holder.Inventory.Count && !holder.Inventory[i].Empty) i++;
                if (i >= holder.Inventory.Count) break;
                holder.Inventory[i].Itemstack = stack.Clone();
                holder.Inventory[i].MarkDirty();
            }
        }
        be.MarkDirty(true);
    }

    // ---- Beds -----------------------------------------------------------------------------

    const string FurnitureBedEntity = "BlockEntityMonolithicBed";

    /// <summary>How many settlers can sleep in this block entity: 1 for a vanilla bed, a furniture bed's seats (a double bed sleeps 2), else 0.</summary>
    static int BedPlaces(BlockEntity be)
    {
        if (be is BlockEntityBed) return 1;
        if (be.GetType().Name != FurnitureBedEntity) return 0;
        return Math.Max(1, be.Block?.Attributes?["seats"].AsInt(1) ?? 1);
    }

    /// <summary>
    /// Where a settler lies down: on top of a vanilla bed, or on its own side of a furniture bed, as the
    /// furniture mod seats a player (each sleeper of a double bed gets a half).
    /// </summary>
    public Vec3d BedSpot(Colony colony, CitizenRecord citizen, BlockPos bed)
    {
        var fallback = new Vec3d(bed.X + 0.5, bed.Y + 0.6, bed.Z + 0.5);
        var be = sapi.World.BlockAccessor.GetBlockEntity(bed);
        if (be == null || be.GetType().Name != FurnitureBedEntity) return fallback;
        int seat = colony.Citizens.Where(c => c.Alive && bed.Equals(c.HomeBedPos)).OrderBy(c => c.CitizenId).ToList().FindIndex(c => c.CitizenId == citizen.CitizenId);
        seatOffset ??= be.GetType().GetMethod("GetSeatWorldOffset");
        if (seatOffset == null || seat < 0) return fallback;
        try
        {
            var args = new object?[] { seat, 0.0, 0.0, 0.0, 0f };
            seatOffset.Invoke(be, args);
            return new Vec3d(bed.X + (double)args[1]!, bed.Y + (double)args[2]!, bed.Z + (double)args[3]!);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    static System.Reflection.MethodInfo? seatOffset;
}
