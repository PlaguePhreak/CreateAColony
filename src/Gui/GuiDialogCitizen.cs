using System;
using System.Collections.Generic;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Gui;

/// <summary>
/// The settler window, opened by right-clicking a settler, laid out like the player's own character
/// window: a Character panel with the settler's model between two columns of clothing slots and a row
/// of armor slots, a Stats panel (health, satiety, speed, job and what they are doing), and an
/// Inventory panel with their bags, held item and pack. The clothing and armor slots stay empty until
/// settlers can wear things. All slots are for looking only; settlers manage their own things.
/// </summary>
public class GuiDialogCitizen : GuiDialog
{
    const double SlotSize = 51; // GuiElementItemSlotGridBase slot width plus padding
    const int PackColumns = 6;
    const double Gap = 20;
    const double ModelWidth = 250;
    const double GearRows = 6;
    const double StatsWidth = 250;

    /// <summary>The player's clothing slots in the player's order: left column, then right column.</summary>
    static readonly string[] LeftGear = { "hat", "cape", "shirt", "pullover", "trousers", "boots" };
    static readonly string[] RightGear = { "necklace", "medal", "mask", "bracers", "gloves", "belt" };
    static readonly string[] ArmorGear = { "hat", "pullover", "trousers" };

    readonly ColonyNetwork network;
    CitizenSnapshot snapshot;
    ElementBounds? modelBounds;
    float yaw = -GameMath.PIHALF + 0.3f;
    bool rotating;

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

        var composer = capi.Gui.CreateCompo("createacolony-citizen", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(snapshot.Name, () => TryClose())
            .BeginChildElements(bgBounds);

        double top = 30;
        double x = 0;
        x = ComposeCharacter(composer, x, top) + Gap;
        x = ComposeStats(composer, x, top) + Gap;
        ComposeInventory(composer, x, top);

        SingleComposer = composer.EndChildElements().Compose();
        SingleComposer.GetStatbar("health")?.SetValues(snapshot.Health, 0, Math.Max(1, snapshot.MaxHealth));
        SingleComposer.GetStatbar("satiety")?.SetValues(snapshot.FoodPercent, 0, 100);
    }

    /// <summary>The Character panel. Returns its right edge.</summary>
    double ComposeCharacter(GuiComposer composer, double x, double y)
    {
        double width = 2 * SlotSize + ModelWidth + 20;
        string job = Lang.Get("createacolony:job-" + (snapshot.Job.Length == 0 ? "none" : snapshot.Job));
        composer
            .AddStaticText(Lang.Get("createacolony:citizen-tab-character"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(x, y, width, 25))
            .AddStaticText(job, GuiKit.Muted(), ElementBounds.Fixed(x, y + 26, width, 20));
        y += 52;

        double gearHeight = GearRows * SlotSize;
        composer.AddItemSlotGrid(GearInventory("gearleft", LeftGear), _ => { }, 1, ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, 1, (int)GearRows), "gearleft");
        modelBounds = ElementBounds.Fixed(x + SlotSize + 10, y + 2, ModelWidth, gearHeight - 8);
        composer.AddInset(modelBounds, 2);
        if (Model() == null)
        {
            composer.AddStaticText(Lang.Get("createacolony:citizen-model-away"), CairoFont.WhiteDetailText().WithOrientation(EnumTextOrientation.Center),
                ElementBounds.Fixed(modelBounds.fixedX, modelBounds.fixedY + gearHeight / 2 - 20, ModelWidth, 40));
        }
        composer.AddItemSlotGrid(GearInventory("gearright", RightGear), _ => { }, 1,
            ElementStdBounds.SlotGrid(EnumDialogArea.None, x + SlotSize + 20 + ModelWidth, y, 1, (int)GearRows), "gearright");
        y += gearHeight + 8;

        composer.AddStaticText(Lang.Get("createacolony:citizen-armor"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(x, y + 14, SlotSize * 2, 20));
        composer.AddItemSlotGrid(GearInventory("armor", ArmorGear), _ => { }, ArmorGear.Length,
            ElementStdBounds.SlotGrid(EnumDialogArea.None, x + SlotSize + 10, y, ArmorGear.Length, 1), "armor");
        y += SlotSize + 6;
        composer.Text(Lang.Get("createacolony:citizen-gear-hint"), GuiKit.Muted(), x, y, width);
        return x + width;
    }

    /// <summary>The Stats panel. Returns its right edge.</summary>
    double ComposeStats(GuiComposer composer, double x, double y)
    {
        var heading = CairoFont.WhiteSmallText().WithColor(GuiStyle.DialogDefaultTextColor);
        var label = CairoFont.WhiteDetailText();
        var value = CairoFont.WhiteDetailText().WithOrientation(EnumTextOrientation.Right);

        composer.AddStaticText(Lang.Get("createacolony:citizen-tab-stats"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(x, y, StatsWidth, 25));
        y += 36;

        composer.AddStaticText(Lang.Get("createacolony:citizen-physical"), heading, ElementBounds.Fixed(x, y, StatsWidth, 22));
        y += 26;
        void Row(string name, string text)
        {
            composer
                .AddStaticText(name, label, ElementBounds.Fixed(x, y, StatsWidth, 20))
                .AddStaticText(text, value, ElementBounds.Fixed(x, y, StatsWidth, 20));
            y += 22;
        }
        Row(Lang.Get("createacolony:citizen-health-points"), $"{snapshot.Health:0.#} / {snapshot.MaxHealth:0.#}");
        composer.AddStatbar(ElementBounds.Fixed(x, y - 2, StatsWidth, 10), GuiStyle.HealthBarColor, "health");
        y += 14;
        Row(Lang.Get("createacolony:citizen-satiety"), snapshot.FoodPercent + "%");
        composer.AddStatbar(ElementBounds.Fixed(x, y - 2, StatsWidth, 10), GuiStyle.FoodBarColor, "satiety");
        y += 14;
        Row(Lang.Get("createacolony:citizen-walkspeed"), (int)Math.Round(snapshot.WalkSpeed * 100) + "%");
        Row(Lang.Get("createacolony:citizen-state"), Lang.Get("createacolony:state-" + snapshot.State));
        Row(Lang.Get("createacolony:citizen-bed"), Lang.Get(snapshot.HasBed ? "createacolony:has-bed" : "createacolony:no-bed"));
        y += 10;

        composer.AddStaticText(Lang.Get("createacolony:citizen-work"), heading, ElementBounds.Fixed(x, y, StatsWidth, 22));
        y += 26;
        Row(Lang.Get("createacolony:citizen-job-label"), Lang.Get("createacolony:job-" + (snapshot.Job.Length == 0 ? "none" : snapshot.Job)));
        composer.AddStaticText(Lang.Get("createacolony:citizen-doing-label"), label, ElementBounds.Fixed(x, y, StatsWidth, 20));
        y += 20;
        composer.Text(snapshot.Activity, GuiKit.Detail().WithColor(GuiStyle.ActiveButtonTextColor), x, y, StatsWidth);
        return x + StatsWidth;
    }

    /// <summary>The Inventory panel: bags and the held item on top, the pack below, as the player's.</summary>
    void ComposeInventory(GuiComposer composer, double x, double y)
    {
        double width = PackColumns * SlotSize;
        var pack = Stacks(snapshot.Pack);
        var bags = Stacks(snapshot.Bags);
        var hand = snapshot.Hand == null ? new List<ItemStack>() : Stacks(new List<byte[]> { snapshot.Hand });

        composer.AddStaticText(Lang.Get("createacolony:citizen-tab-inventory"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(x, y, width, 25));
        y += 36;
        composer
            .AddStaticText(Lang.Get("createacolony:citizen-bags", bags.Count, 4), CairoFont.WhiteDetailText(), ElementBounds.Fixed(x, y, 4 * SlotSize, 20))
            .AddStaticText(Lang.Get("createacolony:citizen-hand"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(x + 5 * SlotSize, y, SlotSize * 2, 20));
        y += 22;
        var bagInv = ViewInventory("bags", 4, bags);
        foreach (var slot in bagInv) slot.BackgroundIcon = "basket";
        var handInv = ViewInventory("hand", 1, hand);
        handInv[0].BackgroundIcon = "right";
        composer
            .AddItemSlotGrid(bagInv, _ => { }, 4, ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, 4, 1), "bags")
            .AddItemSlotGrid(handInv, _ => { }, 1, ElementStdBounds.SlotGrid(EnumDialogArea.None, x + 5 * SlotSize, y, 1, 1), "hand");
        y += SlotSize + 12;

        int rows = Math.Max(1, (snapshot.Slots + PackColumns - 1) / PackColumns);
        composer.AddStaticText(Lang.Get("createacolony:citizen-inventory", pack.Count, snapshot.Slots), CairoFont.WhiteDetailText(), ElementBounds.Fixed(x, y, width, 20));
        y += 22;
        composer.AddItemSlotGrid(ViewInventory("pack", snapshot.Slots, pack), _ => { }, PackColumns, ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, PackColumns, rows), "pack");
        y += rows * SlotSize + 10;
        composer.Text(Lang.Get("createacolony:citizen-bag-hint"), GuiKit.Muted(), x, y, width);
    }

    /// <summary>The settler's entity on this client, when it is loaded nearby.</summary>
    EntityAgent? Model() => snapshot.EntityId == 0 ? null : capi.World.GetEntityById(snapshot.EntityId) as EntityAgent;

    /// <summary>Draws the settler between the clothing columns, the way the player's character window does.</summary>
    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);
        if (modelBounds == null || Model() is not { } settler) return;

        var render = capi.Render;
        double pad = GuiElement.scaled(GuiElementItemSlotGridBase.unscaledSlotPadding);
        render.GlPushMatrix();
        if (focused) render.GlTranslate(0, 0, 150);
        render.GlRotate(-14, 1, 0, 0);
        render.CurrentActiveShader?.Uniform("lightPosition", new Vec3f(1, -1, -0.5f).Normalize());
        render.PushScissor(modelBounds);
        try
        {
            render.RenderEntityToGui(deltaTime, settler, modelBounds.renderX + pad - GuiElement.scaled(41), modelBounds.renderY + pad - GuiElement.scaled(30),
                GuiElement.scaled(250), yaw, (float)GuiElement.scaled(135), ColorUtil.WhiteArgb);
        }
        catch (Exception e)
        {
            // A settler whose renderer is not ready yet; try again next frame.
            capi.Logger.VerboseDebug("[createacolony] Could not draw settler model: {0}", e.Message);
        }
        render.PopScissor();
        render.CurrentActiveShader?.Uniform("lightPosition", new Vec3f(1, -1, 0).Normalize());
        render.GlPopMatrix();
    }

    /// <summary>Dragging inside the model area turns the settler around.</summary>
    public override void OnMouseDown(MouseEvent args)
    {
        base.OnMouseDown(args);
        rotating = !args.Handled && modelBounds != null && modelBounds.PointInside(args.X, args.Y);
        if (rotating) args.Handled = true;
    }

    public override void OnMouseMove(MouseEvent args)
    {
        base.OnMouseMove(args);
        if (rotating) yaw -= args.DeltaX / 100f;
    }

    public override void OnMouseUp(MouseEvent args)
    {
        base.OnMouseUp(args);
        rotating = false;
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

    /// <summary>Clothing or armor slots showing the player's slot icons; the armor slots show what the settler wears.</summary>
    InventoryGeneric GearInventory(string name, string[] icons)
    {
        var inv = ViewInventory(name, icons.Length, new List<ItemStack>());
        for (int i = 0; i < icons.Length; i++) inv[i].BackgroundIcon = icons[i];
        if (name != "armor") return inv;
        foreach (var slot in inv) slot.HexBackgroundColor = "#77503a";
        // Armor the settler wears, by slot: head, body, legs.
        for (int i = 0; i < snapshot.Armor.Count && i < inv.Count; i++)
        {
            if (snapshot.Armor[i].Length == 0) continue;
            var stack = new ItemStack(snapshot.Armor[i]);
            if (stack.ResolveBlockOrItem(capi.World)) inv[i].Itemstack = stack;
        }
        return inv;
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
