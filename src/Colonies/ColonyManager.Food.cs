using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// What settlers eat. Besides plain food (bread, berries, cheese) they eat one serving of a meal from a
/// bowl, cooking pot or crock, and one slice of a pie, using the game's own meal nutrition. Food mods
/// such as Expanded Foods and A Culinary Artillery change that same nutrition code, so their foods and
/// meals count properly without this mod knowing about them. Settlers remember the food groups of their
/// last few meals and prefer food from groups they have had least of lately.
/// </summary>
public partial class ColonyManager
{
    /// <summary>How many food groups a settler remembers; each meal adds the groups it had.</summary>
    const int RecentFoodMemory = 8;
    /// <summary>A container this many blocks further away counts as half as good.</summary>
    const float FoodDistanceHalving = 24f;

    /// <summary>One thing a settler could eat right now, and how much it would give.</summary>
    public sealed class FoodChoice
    {
        public required BlockEntityContainer Container;
        public required ItemSlot Slot;
        /// <summary>Saturation and health per food group for the part the settler would eat.</summary>
        public required FoodNutritionProperties[] Props;
        public float Satiety => Props.Sum(p => p.Satiety);
        public float Health => Props.Sum(p => p.Health);
        public float Score;
        /// <summary>Set when the food is a meal placed in the world rather than an item in a slot.</summary>
        public IBlockEntityMealContainer? Placed;
    }

    /// <summary>The best food in the colony for this settler, weighing variety against distance. Containers skip says yes to are left out.</summary>
    public FoodChoice? FindFood(Colony colony, CitizenRecord citizen, EntityAgent eater, System.Func<BlockPos, bool>? skip = null)
    {
        FoodChoice? best = null;
        float bestValue = 0;
        foreach (var container in FindBlockEntities<BlockEntityContainer>(colony))
        {
            if (skip != null && skip(container.Pos)) continue;
            var choice = FindFoodIn(container, citizen, eater);
            if (choice == null) continue;
            float dist = (float)Math.Sqrt(container.Pos.DistanceSqTo(eater.Pos.X, eater.Pos.Y, eater.Pos.Z));
            float value = choice.Score / (1 + dist / FoodDistanceHalving);
            if (value <= bestValue) continue;
            best = choice;
            bestValue = value;
        }
        return best;
    }

    /// <summary>The best food in one container, or null.</summary>
    public FoodChoice? FindFoodIn(BlockEntityContainer container, CitizenRecord citizen, EntityAgent eater)
    {
        if (!IsFoodStore(container)) return null;

        // A bowl, pot or crock placed in the world keeps its servings on the block, not in its slots.
        if (container is IBlockEntityMealContainer placed)
        {
            if (placed.QuantityServings <= 0) return null;
            var mealStack = container.Block.OnPickBlock(sapi.World, container.Pos);
            if (mealStack?.Block is not IBlockMealContainer) return null;
            var placedChoice = EvaluateFood(container, new DummySlot(mealStack, placed.inventory), eater);
            if (placedChoice == null) return null;
            placedChoice.Placed = placed;
            placedChoice.Score = VarietyScore(placedChoice.Props, citizen);
            return placedChoice;
        }

        // From a fire pit only what has finished cooking, such as a pot of stew left in it.
        IEnumerable<ItemSlot> slots = container is BlockEntityFirepit firepit ? new[] { firepit.outputSlot } : container.Inventory;
        FoodChoice? best = null;
        foreach (var slot in slots)
        {
            if (slot.Empty) continue;
            var choice = EvaluateFood(container, slot, eater);
            if (choice == null) continue;
            choice.Score = VarietyScore(choice.Props, citizen);
            if (best == null || choice.Score > best.Score) best = choice;
        }
        return best;
    }

    /// <summary>
    /// Containers settlers may take food from. Ovens, querns, troughs, barrels and buckets are left
    /// alone so settlers do not eat what is being baked, ground, fed to animals or pickled, and so are
    /// the cooking stations of A Culinary Artillery, which this mod only knows by name. Fire pits are
    /// handled in FindFoodIn.
    /// </summary>
    static bool IsFoodStore(BlockEntityContainer container)
    {
        if (container is BlockEntityOven or BlockEntityQuern or BlockEntityTrough or BlockEntityLiquidContainer) return false;
        string name = container.GetType().Name;
        return !(name.Contains("MixingBowl") || name.Contains("Saucepan") || name.Contains("Cauldron") || name.Contains("Kiln"));
    }

    /// <summary>
    /// What eating from this slot would give: one serving of a meal, one slice of a pie, or one item of
    /// plain food. Null for anything that is not food, is still raw dough, gives nothing or would hurt.
    /// </summary>
    public FoodChoice? EvaluateFood(BlockEntityContainer container, ItemSlot slot, EntityAgent eater)
    {
        var stack = slot.Itemstack;
        if (stack == null) return null;
        var world = sapi.World;

        FoodNutritionProperties[]? props;
        try
        {
            if (stack.Block is BlockPie pie)
            {
                if (pie.State == "raw") return null;
                // A whole pie is one serving of four slices; a settler eats one slice.
                props = Scale(pie.GetContentNutritionProperties(world, slot, eater), 0.25f);
            }
            else if (stack.Block is IBlockMealContainer meal)
            {
                float servings = meal.GetQuantityServings(world, stack);
                var contents = meal.GetNonEmptyContents(world, stack);
                if (servings <= 0 || contents == null || contents.Length == 0) return null;
                props = MealServingProps(slot, meal, contents, eater);
                props = Scale(props, Math.Min(1, servings));
            }
            else
            {
                props = PlainFoodProps(slot, eater);
            }
        }
        catch (Exception e)
        {
            // A food mod's meal that the game's nutrition code cannot read should not stop settlers eating.
            sapi.Logger.Debug("[createacolony] Could not read the nutrition of {0}: {1}", stack.Collectible.Code, e.Message);
            return null;
        }

        if (props == null || props.Length == 0) return null;
        var choice = new FoodChoice { Container = container, Slot = slot, Props = props };
        if (choice.Satiety <= 0 || choice.Health < 0) return null;
        return choice;
    }

    /// <summary>Nutrition of one serving of a meal in a bowl, cooking pot or crock.</summary>
    FoodNutritionProperties[]? MealServingProps(ItemSlot slot, IBlockMealContainer meal, ItemStack[] contents, EntityAgent eater)
    {
        if (meal is BlockMeal bowl) return bowl.GetContentNutritionProperties(sapi.World, slot, eater);
        // Pots and crocks hold servings the game measures as if they were in a bowl.
        bool noRecipe = string.IsNullOrEmpty(meal.GetRecipeCode(sapi.World, slot.Itemstack));
        return BlockMeal.GetContentNutritionProperties(sapi.World, slot, contents, eater, noRecipe);
    }

    /// <summary>
    /// Nutrition of one plain food item, less for spoiling food, plus the extra nutrients some
    /// Expanded Foods items carry (A Culinary Artillery stores them on the item as "expandedSats").
    /// </summary>
    FoodNutritionProperties[]? PlainFoodProps(ItemSlot slot, EntityAgent eater)
    {
        var world = sapi.World;
        var stack = slot.Itemstack!;
        var props = stack.Collectible.GetNutritionProperties(world, stack, eater);
        if (props == null) return null;

        var state = stack.Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
        float spoil = state?.TransitionLevel ?? 0;
        float satMul = GlobalConstants.FoodSpoilageSatLossMul(spoil, stack, eater);
        float healthMul = GlobalConstants.FoodSpoilageHealthLossMul(spoil, stack, eater);

        var list = new List<FoodNutritionProperties>();
        var main = props.Clone();
        main.Satiety *= satMul;
        main.Health *= healthMul;
        list.Add(main);

        // Layout: [health, fruit, vegetable, protein, grain, dairy], each times the item's "satMult".
        if (stack.Attributes["expandedSats"] is FloatArrayAttribute extra && extra.value?.Length >= 6)
        {
            float mult = stack.Collectible.Attributes?["satMult"].AsFloat(1f) ?? 1f;
            for (int i = 1; i <= 5; i++)
            {
                if (extra.value[i] == 0) continue;
                list.Add(new FoodNutritionProperties { FoodCategory = (EnumFoodCategory)(i - 1), Satiety = extra.value[i] * mult * satMul });
            }
            if (extra.value[0] != 0) main.Health += extra.value[0] * mult * healthMul;
        }
        return list.ToArray();
    }

    static FoodNutritionProperties[]? Scale(FoodNutritionProperties[]? props, float factor)
    {
        if (props == null) return null;
        return props.Where(p => p != null).Select(p =>
        {
            var c = p.Clone();
            c.Satiety *= factor;
            c.Health *= factor;
            return c;
        }).ToArray();
    }

    /// <summary>
    /// Saturation, with each food group counted for less the more of the settler's recent meals had
    /// it. A settler who has eaten only bread lately values a bowl of fish stew well above more bread.
    /// </summary>
    static float VarietyScore(FoodNutritionProperties[] props, CitizenRecord citizen)
    {
        float score = 0;
        foreach (var p in props)
        {
            int had = citizen.RecentFood.Count(c => c == (int)p.FoodCategory);
            score += p.Satiety / (1 + had);
        }
        return score;
    }

    /// <summary>
    /// Eats the choice: takes one serving, slice or item out of the container, leaving the empty bowl or
    /// pot behind, and gives the settler its saturation and health. Returns the food's name, or null if
    /// the food is gone or changed since it was picked.
    /// </summary>
    public string? EatFood(Colony colony, CitizenRecord citizen, EntityAgent eater, BlockEntityContainer container)
    {
        var choice = FindFoodIn(container, citizen, eater);
        if (choice == null) return null;

        var slot = choice.Slot;
        string name = slot.Itemstack!.GetName();
        if (!(choice.Placed != null ? TakePlacedServing(container, choice.Placed) : TakeServing(container, slot))) return null;

        citizen.Saturation = Math.Min(Config.SaturationMax, citizen.Saturation + choice.Satiety);
        if (choice.Health > 0)
        {
            eater.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, choice.Health);
        }

        foreach (var cat in choice.Props.Where(p => p.Satiety > 0).Select(p => (int)p.FoodCategory).Distinct())
        {
            citizen.RecentFood.Add(cat);
        }
        if (citizen.RecentFood.Count > RecentFoodMemory) citizen.RecentFood.RemoveRange(0, citizen.RecentFood.Count - RecentFoodMemory);

        // The block may be gone: a finished placed pie, or a bowl or pot swapped for its empty version.
        if (sapi.World.BlockAccessor.GetBlockEntity(container.Pos) == container)
        {
            if (choice.Placed == null) slot.MarkDirty();
            container.MarkDirty(true);
        }
        SaveColony(colony);
        return name;
    }

    /// <summary>Removes what one bite of EvaluateFood stands for from the slot.</summary>
    bool TakeServing(BlockEntityContainer container, ItemSlot slot)
    {
        var world = sapi.World;
        var stack = slot.Itemstack;
        if (stack == null) return false;

        if (stack.Block is BlockPie)
        {
            // A placed pie removes its own block when the last slice is taken.
            if (container is BlockEntityPie placedPie) return placedPie.TakeSlice() != null;

            var pie = SplitOne(container, slot);
            int slices = pie.Attributes.GetAsInt("pieSize", 4);
            if (slices <= 1)
            {
                slot.Itemstack = null;
                return true;
            }
            pie.Attributes.SetInt("pieSize", slices - 1);
            if (pie.Attributes.HasAttribute("quantityServings"))
            {
                pie.Attributes.SetFloat("quantityServings", Math.Max(0.25f, pie.Attributes.GetFloat("quantityServings") - 0.25f));
            }
            return true;
        }

        if (stack.Block is IBlockMealContainer meal)
        {
            var one = SplitOne(container, slot);
            float left = meal.GetQuantityServings(world, one) - 1;
            one.Attributes.RemoveAttribute("sealed");
            if (left > 0.001f)
            {
                meal.SetQuantityServings(world, one, left);
                return true;
            }

            meal.SetQuantityServings(world, one, 0);
            // The bowl or pot stays behind empty, the way a player leaves it.
            string? emptyCode = one.Block.Attributes?["eatenBlock"].AsString() ?? one.Block.Attributes?["emptiedBlockCode"].AsString();
            var empty = emptyCode == null ? null : world.GetBlock(new AssetLocation(emptyCode));
            if (empty != null) slot.Itemstack = new ItemStack(empty);
            else if (one.Block is BlockMeal) slot.Itemstack = null;
            return true;
        }

        return slot.TakeOut(1) != null;
    }

    /// <summary>Takes one serving from a bowl, cooking pot or crock placed in the world.</summary>
    bool TakePlacedServing(BlockEntityContainer container, IBlockEntityMealContainer placed)
    {
        placed.QuantityServings -= 1;
        if (placed.QuantityServings > 0.001f)
        {
            placed.MarkDirty(true);
            return true;
        }

        var world = sapi.World;
        var block = container.Block;
        string? emptyCode = container switch
        {
            BlockEntityMeal => block.Attributes?["eatenBlock"].AsString(),
            BlockEntityCookedContainer => block.Attributes?["emptiedBlockCode"].AsString() ?? block.CodeWithVariant("type", "fired").ToString(),
            _ => null
        };
        var empty = emptyCode == null ? null : world.GetBlock(new AssetLocation(emptyCode));
        if (empty != null)
        {
            world.BlockAccessor.SetBlock(empty.BlockId, container.Pos);
            return true;
        }

        // A crock (or a food mod's container) stays where it is, empty.
        placed.QuantityServings = 0;
        placed.RecipeCode = "";
        placed.inventory.DiscardAll();
        placed.MarkDirty(true);
        return true;
    }

    /// <summary>
    /// Makes sure the slot holds a single item so a serving can be taken from it, moving the rest of the
    /// stack to another slot of the container, or dropping it beside the container if there is no room.
    /// </summary>
    ItemStack SplitOne(BlockEntityContainer container, ItemSlot slot)
    {
        var stack = slot.Itemstack!;
        if (stack.StackSize <= 1) return stack;

        var rest = stack.Clone();
        rest.StackSize = stack.StackSize - 1;
        stack.StackSize = 1;

        var restSlot = new DummySlot(rest);
        foreach (var other in container.Inventory)
        {
            if (other == slot || restSlot.Empty) continue;
            if (other.Empty || other.Itemstack.Equals(sapi.World, rest, GlobalConstants.IgnoredStackAttributes))
            {
                restSlot.TryPutInto(sapi.World, other, restSlot.StackSize);
                other.MarkDirty();
            }
        }
        if (!restSlot.Empty) sapi.World.SpawnItemEntity(restSlot.Itemstack, container.Pos.ToVec3d().Add(0.5, 1.1, 0.5));
        return stack;
    }
}
