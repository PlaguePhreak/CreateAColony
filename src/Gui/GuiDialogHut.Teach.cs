using System;
using System.Linq;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// The Crafting recipes tab with its Teach recipe button, and the Teach recipe page: a 3x3 grid for what the
/// recipe takes and a slot for what it makes, as in MineColonies. The slots only show copies of things (nothing
/// leaves the player's bag); the server is sent the codes and counts (ColonyManager.TeachRecipe).
/// </summary>
public partial class GuiDialogHut
{
    const int TeachGridSlots = 9;
    const int TeachOutputSlot = 9;
    const int TaughtPerPage = 3;
    const double TaughtRowHeight = 52;

    /// <summary>The grid (slots 0-8) and what it makes (slot 9), kept while the window is open.</summary>
    GhostInventory? teachInventory;
    bool teachAnyType;
    /// <summary>How many taught recipes there were when Teach was pressed, so the grid clears once the new one is in; -1 when nothing is waiting.</summary>
    int teachSentCount = -1;
    string teachNote = "";

    bool TeachAllowed => Officer && snapshot.CanTeach;

    GhostInventory TeachInventory()
    {
        if (teachInventory != null) return teachInventory;
        teachInventory = new GhostInventory(capi, BuildingId);
        // Only marks it open here, so its slots take clicks; the server never hears of it.
        teachInventory.Open(capi.World.Player);
        return teachInventory;
    }

    void ComposeRecipes(GuiComposer composer)
    {
        double y = composer.Text(Lang.Get("createacolony:recipes-hint"), GuiKit.Muted(), 0, 30, TeachAllowed ? Width - 180 : Width, 10);
        if (TeachAllowed)
        {
            composer.AddSmallButton(Lang.Get("createacolony:teach-open"), () => { teachNote = ""; return Open(TeachView); }, ElementBounds.Fixed(Width - 170, 28, 170, 28));
            y = Math.Max(y, 66);
        }
        ComposeKeepList(composer, snapshot.Recipes, "recipes", ColonyAction.SetRecipeKeep, y, Math.Max(120, KeepListHeight + 60 - y));
    }

    void ComposeTeach(GuiComposer composer)
    {
        composer
            .AddStaticText(Lang.Get("createacolony:teach-title"), GuiKit.Heading(), ElementBounds.Fixed(0, 30, Width - 110, 26))
            .AddSmallButton(Lang.Get("createacolony:back"), () => Open(RecipesTab), ElementBounds.Fixed(Width - 100, 28, 100, 28));
        double y = composer.Text(Lang.Get("createacolony:teach-hint"), GuiKit.Muted(), 0, 64, Width, 8);

        var inventory = TeachInventory();
        double slot = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding;
        const double OutputX = 230;
        composer.AddStaticText(Lang.Get("createacolony:teach-takes"), GuiKit.Body(), ElementBounds.Fixed(0, y, 200, 22));
        composer.AddStaticText(Lang.Get("createacolony:teach-makes"), GuiKit.Body(), ElementBounds.Fixed(OutputX, y, 120, 22));
        y += 26;
        // The slots never send anything to the server: what they hold is only a copy.
        composer.AddItemSlotGrid(inventory, _ => { }, 3, Enumerable.Range(0, TeachGridSlots).ToArray(), ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, y, 3, 3), "teachgrid");
        composer.AddStaticText("→", GuiKit.Heading().WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(3 * slot + 10, y + slot + 8, OutputX - 3 * slot - 20, 30));
        composer.AddItemSlotGrid(inventory, _ => { }, 1, new[] { TeachOutputSlot }, ElementStdBounds.SlotGrid(EnumDialogArea.None, OutputX, y + slot, 1, 1), "teachoutput");

        double bx = Width - 190;
        composer
            .AddSmallButton(Lang.Get("createacolony:teach-add-held"), AddHeldToGrid, ElementBounds.Fixed(bx, y, 190, 28))
            .AddSmallButton(Lang.Get("createacolony:teach-make-held"), MakeHeld, ElementBounds.Fixed(bx, y + 34, 190, 28))
            .AddSmallButton(Lang.Get("createacolony:teach-clear"), ClearTeach, ElementBounds.Fixed(bx, y + 68, 190, 28))
            .AddSwitch(on => teachAnyType = on, ElementBounds.Fixed(bx, y + 106, 30, 30), "teachany")
            .AddStaticText(Lang.Get("createacolony:teach-anytype"), GuiKit.Detail(), ElementBounds.Fixed(bx + 38, y + 106, 152, 40));
        y += 3 * slot + 12;

        composer.AddSmallButton(Lang.Get("createacolony:teach-save"), SendTeach, ElementBounds.Fixed(0, y, 150, 30));
        if (teachNote.Length > 0) composer.AddStaticText(teachNote, GuiKit.Detail(), ElementBounds.Fixed(160, y + 6, Width - 160, 40));
        y += 44;

        y = composer.Section(Lang.Get("createacolony:teach-taught", snapshot.Taught.Count), y, Width);
        if (snapshot.Taught.Count == 0)
        {
            composer.Text(Lang.Get("createacolony:teach-taught-none"), GuiKit.Detail(), 0, y, Width);
            return;
        }
        int pages = (snapshot.Taught.Count + TaughtPerPage - 1) / TaughtPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        foreach (var row in snapshot.Taught.Skip(page * TaughtPerPage).Take(TaughtPerPage))
        {
            if (row.Stack.Length > 0 && new ItemStack(row.Stack) is { } stack && stack.ResolveBlockOrItem(capi.World))
            {
                composer.AddRichtext(new RichTextComponentBase[] { new ItemstackTextComponent(capi, stack, 36, 6) }, ElementBounds.Fixed(6, y + 4, 44, 40));
            }
            composer.AddStaticText(row.Name ?? "", GuiKit.Body(), ElementBounds.Fixed(KeepTextX, y + 4, Width - KeepTextX - 120, 22));
            composer.AddStaticText(row.Detail ?? "", GuiKit.Muted(), ElementBounds.Fixed(KeepTextX, y + 26, Width - KeepTextX - 120, 22));
            string id = row.Key;
            composer.AddSmallButton(Lang.Get("createacolony:teach-forget"), () => SendText(ColonyAction.ForgetRecipe, BuildingId, id), ElementBounds.Fixed(Width - 100, y + 10, 100, 28));
            y += TaughtRowHeight;
        }
        AddPager(composer, pages, y + 6);
    }

    /// <summary>The item in the player's hand, or null (with a note) when they hold nothing.</summary>
    ItemStack? HeldStack()
    {
        var held = capi.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
        if (held == null) teachNote = Lang.Get("createacolony:teach-hold-something");
        return held;
    }

    bool AddHeldToGrid()
    {
        var inventory = TeachInventory();
        if (HeldStack() is { } held)
        {
            teachNote = "";
            var same = Enumerable.Range(0, TeachGridSlots).Select(i => inventory[i]).FirstOrDefault(s => s.Itemstack?.Collectible.Code == held.Collectible.Code);
            var empty = Enumerable.Range(0, TeachGridSlots).Select(i => inventory[i]).FirstOrDefault(s => s.Empty);
            if (same != null) GhostInventory.AddOne(same, held);
            else if (empty != null) GhostInventory.AddOne(empty, held);
            else teachNote = Lang.Get("createacolony:teach-grid-full");
        }
        Compose();
        return true;
    }

    bool MakeHeld()
    {
        if (HeldStack() is { } held)
        {
            teachNote = "";
            GhostInventory.AddOne(TeachInventory()[TeachOutputSlot], held);
        }
        Compose();
        return true;
    }

    bool ClearTeach()
    {
        var inventory = TeachInventory();
        for (int i = 0; i <= TeachOutputSlot; i++) inventory[i].Itemstack = null;
        teachNote = "";
        teachSentCount = -1;
        Compose();
        return true;
    }

    bool SendTeach()
    {
        var inventory = TeachInventory();
        var inputs = Enumerable.Range(0, TeachGridSlots).Select(i => inventory[i].Itemstack).OfType<ItemStack>().ToList();
        var output = inventory[TeachOutputSlot].Itemstack;
        if (inputs.Count == 0) teachNote = Lang.Get("createacolony:teach-need-inputs");
        else if (output == null) teachNote = Lang.Get("createacolony:teach-need-output");
        else
        {
            string text = (teachAnyType ? "1" : "0") + "|" + string.Join(",", inputs.Select(PartText)) + "|" + PartText(output);
            teachSentCount = snapshot.Taught.Count;
            teachNote = Lang.Get("createacolony:teach-sent");
            SendText(ColonyAction.TeachRecipe, BuildingId, text);
        }
        Compose();
        return true;
    }

    static string PartText(ItemStack stack) =>
        (stack.Class == EnumItemClass.Block ? "b:" : "i:") + stack.Collectible.Code + "*" + stack.StackSize;

    /// <summary>A fresh snapshot came after Teach: once the recipe just sent is in its list, the grid is cleared for the next one.</summary>
    void TeachUpdated(HutSnapshot fresh)
    {
        if (teachSentCount < 0 || teachInventory == null) return;
        if (fresh.Taught.Count <= teachSentCount)
        {
            // Not taught: the server said why (an error on screen), and the grid stays to fix it.
            teachSentCount = -1;
            teachNote = "";
            return;
        }
        for (int i = 0; i <= TeachOutputSlot; i++) teachInventory[i].Itemstack = null;
        teachSentCount = -1;
        teachNote = Lang.Get("createacolony:teach-done");
    }

    /// <summary>
    /// The Teach recipe slots. Clicking one with an item puts a copy in it (one more each click, the whole stack
    /// with a right click); an empty hand takes one off. Nothing ever moves in or out of a real inventory.
    /// </summary>
    sealed class GhostInventory : InventoryGeneric
    {
        public GhostInventory(ICoreAPI api, int buildingId)
            : base(TeachOutputSlot + 1, "createacolony-teach", "createacolony-teach-" + buildingId, api, (_, inv) => new GhostSlot(inv)) { }

        public override object? ActivateSlot(int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            var slot = this[slotId];
            if (slot == null || sourceSlot == slot) return null;
            var held = sourceSlot.Itemstack;
            if (held == null)
            {
                if (slot.Itemstack != null && slot.Itemstack.StackSize > 1 && op.MouseButton != EnumMouseButton.Right) slot.Itemstack.StackSize--;
                else slot.Itemstack = null;
            }
            else if (op.MouseButton == EnumMouseButton.Right)
            {
                var copy = held.Clone();
                copy.StackSize = Math.Clamp(held.StackSize, 1, 64);
                slot.Itemstack = copy;
            }
            else AddOne(slot, held);
            slot.MarkDirty();
            return null;
        }

        /// <summary>One more of the held thing in the slot, or one of it in place of something else.</summary>
        public static void AddOne(ItemSlot slot, ItemStack held)
        {
            if (slot.Itemstack?.Collectible.Code == held.Collectible.Code) slot.Itemstack!.StackSize = Math.Min(64, slot.Itemstack.StackSize + 1);
            else
            {
                var copy = held.Clone();
                copy.StackSize = 1;
                slot.Itemstack = copy;
            }
            slot.MarkDirty();
        }
    }

    /// <summary>A slot nothing can be taken out of or put into but by GhostInventory.</summary>
    sealed class GhostSlot : ItemSlot
    {
        public GhostSlot(InventoryBase inventory) : base(inventory) { }
        public override bool CanTake() => false;
        public override bool CanHold(ItemSlot sourceSlot) => false;
        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) => false;
    }
}
