using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CreateAColony.Colonies;

/// <summary>
/// Clicks in the settler window: colony members take items from a settler and give it items with the
/// mouse, the way they move things between their own slots. The window sends which slot was clicked and
/// how; the item on the player's mouse cursor is moved here, on the server, and the window is refreshed.
/// Areas: "pack" (inventory), "bags", "toolbar" (weapon, 3 tools, offhand), "armor", "clothes" and "lunch"
/// (packed food). Each takes only what belongs there, as the player's own slots do.
/// </summary>
public partial class ColonyManager
{
    public const string AreaPack = "pack", AreaBags = "bags", AreaToolBar = "toolbar", AreaArmor = "armor", AreaClothes = "clothes", AreaLunch = "lunch";

    /// <summary>One area of the settler's things as slots, what each slot takes, at most how many, and how to store it back.</summary>
    sealed class SlotArea
    {
        public required ItemStack?[] Slots;
        public required System.Func<ItemStack, int, bool> Fits;
        public int MaxPerSlot = int.MaxValue;
        public required System.Action<ItemStack?[]> Save;
    }

    SlotArea? AreaOf(Colony colony, CitizenRecord citizen, string area, EntityAgent eater)
    {
        static ItemStack?[] Padded(List<ItemStack> list, int count)
        {
            var slots = new ItemStack?[Math.Max(count, list.Count)];
            for (int i = 0; i < list.Count; i++) slots[i] = list[i];
            return slots;
        }

        return area switch
        {
            AreaPack => new SlotArea
            {
                Slots = Padded(PackItems(citizen), PackSlots(citizen)),
                Fits = (_, _) => true,
                Save = slots => SavePack(colony, citizen, slots.OfType<ItemStack>())
            },
            AreaBags => new SlotArea
            {
                Slots = Padded(PackBags(citizen), MaxBags),
                Fits = (s, _) => BagSlots(s) > 0,
                MaxPerSlot = 1,
                Save = slots =>
                {
                    citizen.Bags = slots.OfType<ItemStack>().Select(s => s.ToBytes()).ToList();
                    MarkDirty(colony);
                }
            },
            AreaToolBar => new SlotArea
            {
                Slots = ToolBarItems(citizen),
                Fits = FitsToolBar,
                MaxPerSlot = 1,
                Save = slots => SaveToolBar(colony, citizen, slots)
            },
            AreaArmor => new SlotArea
            {
                Slots = WornArmor(citizen),
                Fits = (s, i) => ArmorSlotOf(s) == i,
                MaxPerSlot = 1,
                Save = slots => SaveArmor(colony, citizen, slots)
            },
            AreaClothes => new SlotArea
            {
                Slots = WornClothes(citizen),
                Fits = (s, i) => ClothesSlotOf(s) == i,
                MaxPerSlot = 1,
                Save = slots => SaveClothes(colony, citizen, slots)
            },
            AreaLunch => new SlotArea
            {
                Slots = Padded(LunchItems(citizen), LunchSlots),
                Fits = (s, _) => IsLunchFood(s, eater),
                Save = slots => SaveLunch(colony, citizen, slots.OfType<ItemStack>())
            },
            _ => null
        };
    }

    /// <summary>
    /// A click on one of the settler's slots: left takes or puts the whole stack (or swaps), right takes half
    /// or puts one, shift sends the item to the player's inventory. Returns a message for the player when
    /// the click did nothing, else null.
    /// </summary>
    public string? ClickSettlerSlot(Colony colony, CitizenRecord citizen, IServerPlayer player, string area, int index, bool right, bool shift)
    {
        if (colony.RoleOf(player.PlayerUID) < ColonyRole.Member) return Lang.Get("createacolony:gear-members-only");
        if (!citizen.Alive) return null;
        var eater = sapi.World.GetEntityById(citizen.EntityId) as EntityAgent ?? player.Entity;
        if (AreaOf(colony, citizen, area, eater) is not { } slots || index < 0 || index >= slots.Slots.Length) return null;

        var mouse = player.InventoryManager.MouseItemSlot;
        var current = slots.Slots[index];

        if (shift)
        {
            if (current == null) return null;
            player.InventoryManager.TryGiveItemstack(current, true);
            slots.Slots[index] = current.StackSize > 0 ? current : null;
        }
        else if (mouse.Empty)
        {
            if (current == null) return null;
            int n = right ? (current.StackSize + 1) / 2 : current.StackSize;
            var taken = current.Clone();
            taken.StackSize = n;
            current.StackSize -= n;
            slots.Slots[index] = current.StackSize > 0 ? current : null;
            mouse.Itemstack = taken;
        }
        else
        {
            var held = mouse.Itemstack;
            if (!slots.Fits(held, index)) return Lang.Get("createacolony:gear-does-not-fit", held.GetName());
            int max = Math.Min(slots.MaxPerSlot, Math.Max(1, held.Collectible.MaxStackSize));
            if (current == null)
            {
                var put = held.Clone();
                put.StackSize = right ? 1 : Math.Min(held.StackSize, max);
                held.StackSize -= put.StackSize;
                slots.Slots[index] = put;
            }
            else if (current.Equals(sapi.World, held, GlobalConstants.IgnoredStackAttributes))
            {
                int n = Math.Min(right ? 1 : held.StackSize, Math.Min(max, current.Collectible.MaxStackSize) - current.StackSize);
                if (n <= 0) return null;
                current.StackSize += n;
                held.StackSize -= n;
            }
            else if (!right && held.StackSize <= max)
            {
                slots.Slots[index] = held;
                mouse.Itemstack = current;
                held = current;
            }
            else return null;
            if (held.StackSize <= 0) mouse.Itemstack = null;
        }
        mouse.MarkDirty();
        slots.Save(slots.Slots);

        // A bag taken off leaves less room: what no longer fits goes to the player.
        if (area == AreaBags)
        {
            var items = PackItems(citizen);
            int room = PackSlots(citizen);
            if (items.Count > room)
            {
                foreach (var stack in items.Skip(room))
                {
                    if (!player.InventoryManager.TryGiveItemstack(stack, true) || stack.StackSize > 0) sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
                }
                SavePack(colony, citizen, items.Take(room));
            }
        }
        return null;
    }
}
