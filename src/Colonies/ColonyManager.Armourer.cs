using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// The Armourer (Aaron, TRADERS_DRAFT.md): the Blacksmith's armor work. At the anvil it smiths chains, scales,
/// plates and lamellae (ColonyManager.Smithing.cs), puts metal armor together from them on its Crafting
/// recipes tab, and mends worn metal armor with one more of what the armor is made of (a chain for chain
/// mail, a plate for plate armor) of the same metal. Leather and linen armor is the Tailor's. Guards swap
/// worn armor for better pieces in the warehouse, leaving the worn ones there for the Armourer.
/// </summary>
public partial class ColonyManager
{
    /// <summary>For metal armor: how it is made (a key of config ArmourerMendWith) and its metal; null for anything else.</summary>
    (string Make, string Metal)? MetalArmor(ItemStack stack)
    {
        if (!IsArmor(stack) || IsClothArmor(stack)) return null;
        // armor-body-chain-iron: the body part, then how it is made, then the metal.
        var parts = (stack.Collectible.Code?.Path ?? "").Split('-');
        if (parts.Length < 4) return null;
        string make = parts[2];
        if (!Config.ArmourerMendWith.ContainsKey(make)) return null;
        return (make, string.Join("-", parts.Skip(3)));
    }

    /// <summary>How much mending a piece of metal armor could still take, 0 to 1; 0 for anything the Armourer does not mend.</summary>
    public float ArmourerMendLeft(ItemStack stack)
    {
        if (MetalArmor(stack) == null) return 0;
        int max = stack.Collectible.GetMaxDurability(stack);
        return max <= 0 ? 0 : 1 - (float)stack.Collectible.GetRemainingDurability(stack) / max;
    }

    public bool ArmourerNeedsMending(ItemStack stack) => ArmourerMendLeft(stack) > 1 - Config.ArmourerMendBelow;

    /// <summary>True for a chain, scale, plate or lamella, which armor is made and mended with.</summary>
    public bool IsArmourPart(ItemStack stack) => Config.ArmourerParts.Contains(FirstPart(stack.Collectible.Code));

    /// <summary>True when this mends that piece: one of what it is made of, of the same metal.</summary>
    public bool ArmourerMends(ItemStack material, ItemStack piece)
    {
        if (MetalArmor(piece) is not { } armor || !Config.ArmourerMendWith.TryGetValue(armor.Make, out var with)) return false;
        return WildcardUtil.Match(new AssetLocation(with.Replace("{metal}", armor.Metal)), material.Collectible.Code);
    }

    /// <summary>One go at the anvil: mends the most worn armor the Armourer carries by config ArmorMendShare, using one part. False when it could not.</summary>
    public bool ArmourerMendOnce(Colony colony, Building hut, CitizenRecord smith)
    {
        var piece = PackItems(smith).Where(s => ArmourerMendLeft(s) > 0.01f).OrderByDescending(ArmourerMendLeft).FirstOrDefault();
        if (piece == null) return false;
        float left = ArmourerMendLeft(piece);
        if (TakeFromPack(colony, smith, s => ArmourerMends(s, piece), 1) == null) return false;
        var items = PackItems(smith);
        var mended = items.FirstOrDefault(s => s.Collectible.Code.Equals(piece.Collectible.Code) && Math.Abs(ArmourerMendLeft(s) - left) < 0.0001f);
        if (mended == null) return false;
        int max = mended.Collectible.GetMaxDurability(mended);
        int now = mended.Collectible.GetRemainingDurability(mended);
        mended.Attributes.SetInt("durability", Math.Min(max, now + (int)Math.Ceiling(max * Config.ArmorMendShare)));
        SavePack(colony, smith, items);
        AddStat(hut, "mended");
        return true;
    }

    /// <summary>How many worn pieces of metal armor wait for the Armourer: in the warehouse, at the hut and with the Armourer.</summary>
    public int ArmourerMendingCount(Colony colony, Building hut, CitizenRecord? smith)
    {
        ItemMatch worn = ArmourerNeedsMending;
        int count = CountItems(WarehouseContainers(colony), worn) + CountItems(HutStorage(colony, hut), worn);
        if (smith != null) count += PackItems(smith).Count(s => ArmourerMendLeft(s) > 0.01f);
        return count;
    }

    public string ArmourerInfo(Colony colony, Building hut) =>
        Lang.Get("createacolony:armourer-mending-count", ArmourerMendingCount(colony, hut, WorkerAt(colony, hut, Jobs.Armourer)));
}
