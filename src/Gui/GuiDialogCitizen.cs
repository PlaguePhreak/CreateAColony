using System;
using System.Collections.Generic;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// The settler window, opened by right-clicking a settler: their job, how they are doing, what they
/// are doing right now, and their inventory, bags and held item, drawn like the player's own slots.
/// The slots are for looking only; settlers manage their own things.
/// </summary>
public class GuiDialogCitizen : GuiDialog
{
    const int Columns = 10;
    const double SlotSize = 51; // GuiElementItemSlotGridBase slot width plus padding
    const double Width = Columns * SlotSize;

    readonly ColonyNetwork network;
    CitizenSnapshot snapshot;

    public string ColonyId => snapshot.ColonyId;
    public int CitizenId => snapshot.CitizenId;

    /// <summary>Set when another settler's window takes this one's place, so closing it doesn't tell the server.</summary>
    public bool Replaced;

    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogCitizen(ICoreClientAPI capi, ColonyNetwork network, CitizenSnapshot snapshot) : base(capi)
    {
        this.network = network;
        this.snapshot = snapshot;
        Compose();
    }

    public void Update(CitizenSnapshot fresh)
    {
        snapshot = fresh;
        Compose();
    }

    void Compose()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        string job = Lang.Get("createacolony:job-" + (snapshot.Job.Length == 0 ? "none" : snapshot.Job));
        string status = Lang.Get("createacolony:citizen-status", Lang.Get("createacolony:state-" + snapshot.State), snapshot.FoodPercent,
            Lang.Get(snapshot.HasBed ? "createacolony:has-bed" : "createacolony:no-bed"));

        var composer = capi.Gui.CreateCompo("createacolony-citizen", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(snapshot.Name, () => TryClose())
            .BeginChildElements(bgBounds)
            .AddStaticText(Lang.Get("createacolony:citizen-job", job), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, Width, 25))
            .AddStaticText(status, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 55, Width, 25))
            .AddStaticText(Lang.Get("createacolony:citizen-doing", snapshot.Activity), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 80, Width, 25));

        double y = 115;
        var pack = Stacks(snapshot.Pack);
        int rows = Math.Max(1, (snapshot.Slots + Columns - 1) / Columns);
        composer.AddStaticText(Lang.Get("createacolony:citizen-inventory", pack.Count, snapshot.Slots), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, Width, 25));
        y += 28;
        composer.AddItemSlotGrid(ViewInventory("pack", snapshot.Slots, pack), _ => { }, Columns, ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, y, Columns, rows), "pack");
        y += rows * SlotSize + 12;

        var bags = Stacks(snapshot.Bags);
        var hand = snapshot.Hand == null ? new List<ItemStack>() : Stacks(new List<byte[]> { snapshot.Hand });
        composer
            .AddStaticText(Lang.Get("createacolony:citizen-hand"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, 120, 25))
            .AddStaticText(Lang.Get("createacolony:citizen-bags", bags.Count, 4), CairoFont.WhiteSmallText(), ElementBounds.Fixed(2 * SlotSize, y, 200, 25));
        y += 28;
        composer
            .AddItemSlotGrid(ViewInventory("hand", 1, hand), _ => { }, 1, ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, y, 1, 1), "hand")
            .AddItemSlotGrid(ViewInventory("bags", 4, bags), _ => { }, 4, ElementStdBounds.SlotGrid(EnumDialogArea.None, 2 * SlotSize, y, 4, 1), "bags");
        y += SlotSize + 10;
        composer.AddStaticText(Lang.Get("createacolony:citizen-bag-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 40));

        SingleComposer = composer.EndChildElements().Compose();
    }

    List<ItemStack> Stacks(List<byte[]> data)
    {
        var stacks = new List<ItemStack>();
        foreach (var bytes in data)
        {
            var stack = new ItemStack(bytes);
            if (stack.ResolveBlockOrItem(capi.World)) stacks.Add(stack);
        }
        return stacks;
    }

    /// <summary>A client-only inventory of look-only slots filled with the given stacks.</summary>
    InventoryGeneric ViewInventory(string name, int slots, List<ItemStack> stacks)
    {
        var inv = new InventoryGeneric(Math.Max(1, slots), "createacolony-view", $"{name}-{snapshot.CitizenId}", capi, (id, self) => new ViewOnlySlot(self));
        for (int i = 0; i < stacks.Count && i < inv.Count; i++) inv[i].Itemstack = stacks[i];
        return inv;
    }

    public override void OnGuiClosed()
    {
        if (!Replaced) network.SendAction(new ColonyActionPacket { ColonyId = snapshot.ColonyId, Action = ColonyAction.CloseWindow });
        base.OnGuiClosed();
    }

    /// <summary>A slot that shows its item (with tooltip) but cannot be taken from or put into.</summary>
    class ViewOnlySlot : ItemSlot
    {
        public ViewOnlySlot(InventoryBase inventory) : base(inventory) { }

        public override bool CanTake() => false;

        public override bool CanHold(ItemSlot sourceSlot) => false;

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge) => false;
    }
}
