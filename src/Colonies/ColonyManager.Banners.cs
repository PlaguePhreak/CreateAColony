using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// The colony banner, as MineColonies' banner on the Town Hall, for the Heraldry: Banners mod: a player
/// hands the Town Hall a banner they designed at the Heraldry Workbench, and its design is copied onto
/// the banners already hanging on the colony's buildings. A Heraldry banner keeps its design in the
/// stack's "types" attributes (shape, layer1, layer2, ...); a copy takes every layer and keeps each
/// banner's own shape, as the mod does when two banners are combined.
/// </summary>
public partial class ColonyManager
{
    const string DesignTree = "types";
    const string ShapeKey = "shape";

    /// <summary>A Heraldry banner (or anything else that hangs on a Heraldry pole and carries a design).</summary>
    public static bool IsHeraldryBanner(ItemStack? stack) =>
        stack?.ItemAttributes?["heraldryPoleStorable"].AsBool() == true && stack.Attributes.GetTreeAttribute(DesignTree) != null;

    /// <summary>Makes the held banner the colony banner. The banner stays with the player. Returns a lang key on failure.</summary>
    public string? SetColonyBanner(Colony colony, ItemStack? held)
    {
        if (!IsHeraldryBanner(held)) return "createacolony:banner-hold";
        var one = held!.Clone();
        one.StackSize = 1;
        colony.BannerStack = one.ToBytes();
        LogEvent(colony, "event-banner");
        MarkDirty(colony);
        return null;
    }

    public ItemStack? ColonyBanner(Colony colony)
    {
        if (colony.BannerStack == null) return null;
        var stack = new ItemStack(colony.BannerStack);
        return stack.ResolveBlockOrItem(sapi.World) ? stack : null;
    }

    /// <summary>
    /// Copies the colony banner's design onto every banner on the given buildings. Returns how many
    /// banners changed and how many buildings could not be reached because their land is not loaded.
    /// </summary>
    public (int Replaced, int Unloaded) ReplaceBanners(Colony colony, IEnumerable<Building> buildings)
    {
        var chosen = ColonyBanner(colony);
        if (!IsHeraldryBanner(chosen)) return (0, 0);
        int replaced = 0, unloaded = 0;
        foreach (var building in buildings)
        {
            int n = ReplaceBanners(building, chosen!);
            if (n < 0) unloaded++;
            else replaced += n;
        }
        return (replaced, unloaded);
    }

    /// <summary>Re-designs the banners on poles inside one building's blueprint box; -1 when its land is not loaded.</summary>
    int ReplaceBanners(Building building, ItemStack chosen)
    {
        var ba = sapi.World.BlockAccessor;
        var (min, max) = BuildingBox(building);
        for (int x = min.X; x <= max.X + GlobalChunkSize - 1; x += GlobalChunkSize)
        {
            for (int z = min.Z; z <= max.Z + GlobalChunkSize - 1; z += GlobalChunkSize)
            {
                if (ba.GetChunkAtBlockPos(new BlockPos(Math.Min(x, max.X), min.Y, Math.Min(z, max.Z))) == null) return -1;
            }
        }

        int replaced = 0;
        var pos = new BlockPos(0);
        for (int x = min.X; x <= max.X; x++)
        for (int y = min.Y; y <= max.Y; y++)
        for (int z = min.Z; z <= max.Z; z++)
        {
            pos.Set(x, y, z);
            // Banners hang on display blocks such as Heraldry poles; never touch chests and other storage.
            if (ba.GetBlockEntity(pos) is not BlockEntityDisplay display || display is BlockEntityGenericTypedContainer) continue;
            bool changed = false;
            foreach (var slot in display.Inventory)
            {
                if (slot.Itemstack is not { } stack || !IsHeraldryBanner(stack) || stack.Collectible != chosen.Collectible) continue;
                if (CopyDesign(chosen, stack))
                {
                    slot.MarkDirty();
                    changed = true;
                    replaced++;
                }
            }
            if (changed) display.MarkDirty(true);
        }
        return replaced;
    }

    const int GlobalChunkSize = 32;

    /// <summary>Gives the banner the colony banner's layers, keeping its own shape. False when it already matches.</summary>
    bool CopyDesign(ItemStack from, ItemStack to)
    {
        var source = from.Attributes.GetTreeAttribute(DesignTree);
        var target = to.Attributes.GetTreeAttribute(DesignTree);
        string? shape = target?.GetString(ShapeKey);
        var design = source.Clone();
        if (shape != null) design.SetString(ShapeKey, shape);
        else design.RemoveAttribute(ShapeKey);
        if (target != null && target.Equals(sapi.World, design)) return false;
        to.Attributes[DesignTree] = design;
        return true;
    }

    /// <summary>The blocks a building's blueprint covers (its current or first level), or the ground around a hut without one.</summary>
    (BlockPos Min, BlockPos Max) BuildingBox(Building b)
    {
        var p = b.HutPos;
        // A work station is only its block; the zone it works in is its host's.
        if (b.IsStation) return (p.Copy(), p.Copy());
        var blueprint = Blueprints.Get(b, Math.Max(1, b.Level));
        if (blueprint == null || blueprint.Steps.Count == 0) return (p.AddCopy(-8, -2, -8), p.AddCopy(8, 8, 8));
        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
        foreach (var step in blueprint.Steps)
        {
            var at = StepPos(b, step);
            minX = Math.Min(minX, at.X); minY = Math.Min(minY, at.Y); minZ = Math.Min(minZ, at.Z);
            maxX = Math.Max(maxX, at.X); maxY = Math.Max(maxY, at.Y); maxZ = Math.Max(maxZ, at.Z);
        }
        // A banner may hang just outside the walls.
        return (new BlockPos(minX - 1, minY, minZ - 1), new BlockPos(maxX + 1, maxY + 1, maxZ + 1));
    }
}
