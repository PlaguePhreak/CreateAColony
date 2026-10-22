using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Armor settlers wear: a helmet, body armor and leg armor, kept on the settler's record. A blow lands
/// on the head, body or legs as it does for a player, and the armor there takes it the way the game's
/// own armor does for players (flat reduction, then a share, both weaker against higher-tier weapons),
/// wearing down as it goes. Guards fetch the best pieces from their tower and the warehouse.
/// </summary>
public partial class ColonyManager
{
    /// <summary>Armor slots in order: head, body, legs.</summary>
    public const int ArmorSlots = 3;

    static IWearableStatsSupplier? Wearable(ItemStack stack) => stack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>();

    public static bool IsArmor(ItemStack stack) => ArmorSlotOf(stack) >= 0;

    /// <summary>0 for a helmet, 1 for body armor, 2 for leg armor, -1 for anything else.</summary>
    public static int ArmorSlotOf(ItemStack stack)
    {
        if (Wearable(stack) is not { } wearable) return -1;
        var slot = new DummySlot(stack);
        try
        {
            if (!wearable.IsArmorType(slot)) return -1;
            return wearable.GetDressType(slot) switch
            {
                EnumCharacterDressType.ArmorHead => 0,
                EnumCharacterDressType.ArmorBody => 1,
                EnumCharacterDressType.ArmorLegs => 2,
                _ => -1
            };
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>How well a piece protects, to pick the better of two; worn-out armor counts for nothing.</summary>
    public static float ArmorScore(ItemStack stack)
    {
        if (Wearable(stack) is not { } wearable) return 0;
        if (stack.Collectible.GetMaxDurability(stack) > 0 && stack.Collectible.GetRemainingDurability(stack) <= 0) return 0;
        var mods = wearable.GetProtectionModifiers(new DummySlot(stack));
        if (mods == null) return 0;
        return mods.FlatDamageReduction + mods.RelativeProtection * 10 + mods.ProtectionTier;
    }

    /// <summary>What the settler wears, by slot (head, body, legs); null where the slot is empty.</summary>
    public ItemStack?[] WornArmor(CitizenRecord citizen)
    {
        var worn = new ItemStack?[ArmorSlots];
        foreach (var stack in Resolve(citizen.Armor))
        {
            int slot = ArmorSlotOf(stack);
            if (slot >= 0 && worn[slot] == null) worn[slot] = stack;
        }
        return worn;
    }

    void SaveArmor(Colony colony, CitizenRecord citizen, ItemStack?[] worn)
    {
        citizen.Armor = worn.Where(s => s != null && s.StackSize > 0).Select(s => s!.ToBytes()).ToList();
        MarkDirty(colony);
    }

    /// <summary>A container (tower storage first, then warehouse) with a piece that beats what the guard wears, or null.</summary>
    public BlockPos? ArmorChest(Colony colony, Building? tower, CitizenRecord citizen)
    {
        if (!Config.GuardsWearArmor) return null;
        var worn = WornArmor(citizen);
        foreach (var container in GearContainers(colony, tower))
        {
            if (container.Inventory.Any(s => !s.Empty && Beats(s.Itemstack, worn))) return container.Pos;
        }
        return null;
    }

    static bool Beats(ItemStack stack, ItemStack?[] worn)
    {
        int slot = ArmorSlotOf(stack);
        if (slot < 0) return false;
        float score = ArmorScore(stack);
        return score > 0 && score > (worn[slot] == null ? 0 : ArmorScore(worn[slot]!));
    }

    /// <summary>
    /// Puts on the best pieces in the container, one per slot; what the settler wore before goes into
    /// the container in their place, or into its inventory if the container is full.
    /// </summary>
    public void PutOnArmorFrom(Colony colony, CitizenRecord citizen, BlockEntityContainer container)
    {
        var worn = WornArmor(citizen);
        bool changed = false;
        for (int slot = 0; slot < ArmorSlots; slot++)
        {
            var best = container.Inventory.Where(s => !s.Empty && ArmorSlotOf(s.Itemstack) == slot && Beats(s.Itemstack, worn))
                .OrderByDescending(s => ArmorScore(s.Itemstack)).FirstOrDefault();
            if (best == null) continue;
            var piece = best.TakeOut(1);
            best.MarkDirty();
            var old = worn[slot];
            worn[slot] = piece;
            changed = true;
            if (old != null)
            {
                PutItems(container, old);
                if (old.StackSize > 0) AddToPack(colony, citizen, old);
                if (old.StackSize > 0) sapi.World.SpawnItemEntity(old, container.Pos.ToVec3d().Add(0.5, 1, 0.5));
            }
        }
        if (!changed) return;
        container.MarkDirty(true);
        SaveArmor(colony, citizen, worn);
    }

    /// <summary>Takes all the settler's armor off, for dropping where it died.</summary>
    public List<ItemStack> TakeOffArmor(Colony colony, CitizenRecord citizen)
    {
        var all = Resolve(citizen.Armor);
        citizen.Armor.Clear();
        MarkDirty(colony);
        return all;
    }

    static readonly EnumDamageType[] ArmorStops = { EnumDamageType.BluntAttack, EnumDamageType.PiercingAttack, EnumDamageType.SlashingAttack };

    /// <summary>
    /// The game's armor rules for players, for a settler: the blow picks head (20%), body (50%) or legs
    /// (30%), and the armor there, if any, takes off flat damage and a share of the rest.
    /// </summary>
    float ArmorAbsorb(Colony colony, CitizenRecord citizen, EntitySettler settler, DamageSource source, float damage)
    {
        if (citizen.Armor.Count == 0 || damage <= 0 || Array.IndexOf(ArmorStops, source.Type) < 0) return damage;
        if (source.Source is EnumDamageSource.Internal or EnumDamageSource.Suicide) return damage;

        double roll = sapi.World.Rand.NextDouble();
        int slot = roll < 0.2 ? 0 : roll < 0.7 ? 1 : 2;
        var worn = WornArmor(citizen);
        var piece = worn[slot];
        if (piece == null || Wearable(piece) is not { } wearable) return damage;
        var dummy = new DummySlot(piece);
        if (piece.Collectible.GetMaxDurability(piece) > 0 && piece.Collectible.GetRemainingDurability(piece) <= 0) return damage;

        var mods = wearable.GetProtectionModifiers(dummy);
        int weaponTier = source.DamageTier;
        float flat = mods.FlatDamageReduction;
        float share = mods.RelativeProtection;
        for (int tier = 1; tier <= weaponTier; tier++)
        {
            bool above = tier > mods.ProtectionTier;
            float flatLoss = mods.PerTierFlatDamageReductionLoss?[above ? 1 : 0] ?? 0;
            float shareLoss = mods.PerTierRelativeProtectionLoss?[above ? 1 : 0] ?? 0;
            if (above && mods.HighDamageTierResistant)
            {
                flatLoss /= 2;
                shareLoss /= 2;
            }
            flat -= flatLoss;
            share *= 1 - shareLoss;
        }

        float wear = 0.5f + damage * Math.Max(0.5f, (weaponTier - mods.ProtectionTier) * 3);
        damage = Math.Max(0, damage - flat);
        damage *= 1 - Math.Max(0, share);

        piece.Collectible.DamageItem(sapi.World, settler, dummy, GameMath.RoundRandom(sapi.World.Rand, wear));
        worn[slot] = dummy.Empty ? null : dummy.Itemstack;
        if (dummy.Empty) sapi.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), settler);
        SaveArmor(colony, citizen, worn);
        return damage;
    }
}
