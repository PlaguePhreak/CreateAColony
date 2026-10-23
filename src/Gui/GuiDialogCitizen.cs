using System;
using System.Collections.Generic;
using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Gui;

/// <summary>
/// The settler window, opened by right-clicking a settler, laid out like the player's own character
/// window, with tabs along the top:
/// - Character &amp; Armour: the settler's model between two columns of clothing slots, and the armor row;
/// - Stats: health, satiety, speed, job, what they are doing and their family;
/// - Inventory: the tool bar (weapon, three tools, offhand), bags, the item in hand, the pack and packed food;
/// - Traits &amp; Skills: skills, with room for traits.
/// Colony members move items in and out of the settler's slots with the mouse as with their own slots;
/// the clicks go to the server (ColonyManager.SettlerSlots.cs), which moves the item and refreshes the window.
/// Visitors to the colony only look.
/// </summary>
public class GuiDialogCitizen : GuiDialog
{
    const double SlotSize = 51; // GuiElementItemSlotGridBase slot width plus padding
    const int PackColumns = 6;
    const double ModelWidth = 250;
    const double GearRows = 6;
    const double StatsWidth = 250;

    enum Tab { Character, Stats, Inventory, TraitsSkills }

    /// <summary>The player's clothing slots in the player's order: left column, then right column, with their dress slot numbers.</summary>
    static readonly (string Icon, int Slot)[] LeftGear = { ("hat", 0), ("cape", 1), ("shirt", 2), ("pullover", 11), ("trousers", 3), ("boots", 4) };
    static readonly (string Icon, int Slot)[] RightGear = { ("necklace", 6), ("medal", 7), ("mask", 8), ("bracers", 10), ("gloves", 5), ("belt", 9) };
    static readonly string[] ArmorIcons = { "hat", "pullover", "trousers" };

    /// <summary>The tab shown last, so the next settler's window opens on it.</summary>
    static Tab lastTab = Tab.Character;

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

        var tabs = new[]
        {
            new GuiTab { Name = Lang.Get("createacolony:citizen-tab-character-armor"), DataInt = (int)Tab.Character },
            new GuiTab { Name = Lang.Get("createacolony:citizen-tab-stats"), DataInt = (int)Tab.Stats },
            new GuiTab { Name = Lang.Get("createacolony:citizen-tab-inventory"), DataInt = (int)Tab.Inventory },
            new GuiTab { Name = Lang.Get("createacolony:citizen-tab-traits-skills"), DataInt = (int)Tab.TraitsSkills }
        };
        var tabFont = CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold);

        modelBounds = null;
        var composer = capi.Gui.CreateCompo("createacolony-citizen", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(snapshot.Name, () => TryClose())
            .AddHorizontalTabs(tabs, ElementBounds.Fixed(0, -24, 620, 25), id => { lastTab = (Tab)id; Compose(); }, tabFont,
                tabFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "tabs")
            .BeginChildElements(bgBounds);

        double top = 30;
        switch (lastTab)
        {
            case Tab.Character: ComposeCharacter(composer, 0, top); break;
            case Tab.Stats: ComposeStats(composer, 0, top); break;
            case Tab.Inventory: ComposeInventory(composer, 0, top); break;
            case Tab.TraitsSkills: ComposeTraitsAndSkills(composer, 0, top); break;
        }

        SingleComposer = composer.EndChildElements().Compose();
        SingleComposer.GetHorizontalTabs("tabs").SetValue((int)lastTab, false);
        SingleComposer.GetStatbar("health")?.SetValues(snapshot.Health, 0, Math.Max(1, snapshot.MaxHealth));
        SingleComposer.GetStatbar("satiety")?.SetValues(snapshot.FoodPercent, 0, 100);
        foreach (var s in snapshot.Skills)
        {
            // From this level's XP to the next level's; a full bar at the top level.
            bool atTop = s.NextXp <= 0;
            SingleComposer.GetStatbar("skill-" + s.Code)?.SetValues(atTop ? 1 : s.Xp - s.LevelXp, 0, atTop ? 1 : Math.Max(1, s.NextXp - s.LevelXp));
        }
    }

    string JobLine() => snapshot.Child ? Lang.Get("createacolony:citizen-child", snapshot.GrowsUpHours)
        : Lang.Get("createacolony:job-" + (snapshot.Job.Length == 0 ? "none" : snapshot.Job));

    /// <summary>The Character &amp; Armour tab: clothes on both sides of the model, armor below.</summary>
    void ComposeCharacter(GuiComposer composer, double x, double y)
    {
        double width = 2 * SlotSize + ModelWidth + 20;
        composer.AddStaticText(JobLine(), GuiKit.Muted(), ElementBounds.Fixed(x, y, width, 20));
        y += 26;

        double gearHeight = GearRows * SlotSize;
        composer.AddItemSlotGrid(ClothesInventory("gearleft", LeftGear), _ => { }, 1, ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, 1, (int)GearRows), "gearleft");
        modelBounds = ElementBounds.Fixed(x + SlotSize + 10, y + 2, ModelWidth, gearHeight - 8);
        composer.AddInset(modelBounds, 2);
        if (Model() == null)
        {
            composer.AddStaticText(Lang.Get("createacolony:citizen-model-away"), CairoFont.WhiteDetailText().WithOrientation(EnumTextOrientation.Center),
                ElementBounds.Fixed(modelBounds.fixedX, modelBounds.fixedY + gearHeight / 2 - 20, ModelWidth, 40));
        }
        composer.AddItemSlotGrid(ClothesInventory("gearright", RightGear), _ => { }, 1,
            ElementStdBounds.SlotGrid(EnumDialogArea.None, x + SlotSize + 20 + ModelWidth, y, 1, (int)GearRows), "gearright");
        y += gearHeight + 8;

        composer.AddStaticText(Lang.Get("createacolony:citizen-armor"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(x, y + 14, SlotSize * 2, 20));
        var armor = SlotInventory(ColonyManager.AreaArmor, ArmorIcons.Length, snapshot.Armor);
        for (int i = 0; i < armor.Count; i++)
        {
            armor[i].BackgroundIcon = ArmorIcons[i];
            armor[i].HexBackgroundColor = "#77503a";
        }
        composer.AddItemSlotGrid(armor, _ => { }, ArmorIcons.Length,
            ElementStdBounds.SlotGrid(EnumDialogArea.None, x + SlotSize + 10, y, ArmorIcons.Length, 1), "armor");
        y += SlotSize + 6;
        composer.Text(Lang.Get(snapshot.CanEdit ? "createacolony:citizen-gear-hint-edit" : "createacolony:citizen-gear-hint",
            Lang.Get("characterclass-" + snapshot.ClothesClass)), GuiKit.Muted(), x, y, width);
    }

    /// <summary>The Stats tab. Returns its right edge.</summary>
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
        Row(Lang.Get("createacolony:citizen-job-label"), snapshot.Child ? Lang.Get("createacolony:job-child")
            : Lang.Get("createacolony:job-" + (snapshot.Job.Length == 0 ? "none" : snapshot.Job)));
        composer.AddStaticText(Lang.Get("createacolony:citizen-doing-label"), label, ElementBounds.Fixed(x, y, StatsWidth, 20));
        y += 20;
        y = composer.Text(snapshot.Activity, GuiKit.Detail().WithColor(GuiStyle.ActiveButtonTextColor), x, y, StatsWidth);
        y += 10;

        // Family: home, then partner, parents, children, brothers and sisters, then whether a child may come.
        composer.AddStaticText(Lang.Get("createacolony:citizen-family"), heading, ElementBounds.Fixed(x, y, StatsWidth, 22));
        y += 26;
        Row(Lang.Get("createacolony:citizen-home"), snapshot.Home.Length > 0 ? snapshot.Home : Lang.Get("createacolony:citizen-no-home"));
        if (snapshot.Family.Count == 0) y = composer.Text(Lang.Get("createacolony:citizen-no-family"), GuiKit.Muted(), x, y, StatsWidth);
        foreach (var m in snapshot.Family)
        {
            string name = m.Name + (!m.Alive ? " " + Lang.Get("createacolony:family-deceased") : m.Child ? " " + Lang.Get("createacolony:family-child-tag") : "");
            composer
                .AddStaticText(m.Relation, label, ElementBounds.Fixed(x, y, StatsWidth, 20))
                .AddStaticText(name, m.Alive ? value : value.Clone().WithColor(GuiKit.MutedColor), ElementBounds.Fixed(x, y, StatsWidth, 20));
            y += 22;
        }
        if (snapshot.FamilyNote.Length > 0) composer.Text(snapshot.FamilyNote, GuiKit.Muted(), x, y + 2, StatsWidth);
        return x + StatsWidth;
    }

    /// <summary>
    /// The Traits &amp; Skills tab. Traits are still being designed; their section says so, and skills follow.
    /// </summary>
    void ComposeTraitsAndSkills(GuiComposer composer, double x, double y)
    {
        // Traits in the left column, skills in the right.
        const double width = 230;
        if (snapshot.Traits.Count > 0) ComposeTraits(composer, x, y, width);
        else
        {
            composer.AddStaticText(Lang.Get("createacolony:citizen-traits"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(x, y, width, 25));
            composer.Text(Lang.Get(snapshot.Child ? "createacolony:trait-child" : "createacolony:traits-off"), GuiKit.Muted(), x, y + 30, width);
        }
        x += width + Gap;
        if (snapshot.Skills.Count > 0 || snapshot.Child) ComposeSkills(composer, x, y);
        else
        {
            composer.AddStaticText(Lang.Get("createacolony:citizen-tab-skills"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(x, y, width, 25));
            composer.Text(Lang.Get("createacolony:skills-off"), GuiKit.Muted(), x, y + 30, width);
        }
    }

    /// <summary>
    /// The Skills panel: every skill with its level, a bar to the next level, and what the settler can use
    /// of it here. "Fighting 10 (using 4)" means their Guard Tower only lets them fight like a level 4 until
    /// it is upgraded; the hover text names the building. Returns its right edge.
    /// </summary>
    double ComposeSkills(GuiComposer composer, double x, double y)
    {
        const double width = 230;
        var label = CairoFont.WhiteDetailText();
        var value = CairoFont.WhiteDetailText().WithOrientation(EnumTextOrientation.Right);
        var capped = value.Clone().WithColor(GuiKit.WarnColor);

        composer.AddStaticText(Lang.Get("createacolony:citizen-tab-skills"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(x, y, width, 25));
        y += 36;
        if (snapshot.Child)
        {
            composer.Text(Lang.Get("createacolony:skills-child"), GuiKit.Muted(), x, y, width);
            return x + width;
        }
        foreach (var s in snapshot.Skills)
        {
            bool limited = s.Usable < s.Level;
            string text = limited ? Lang.Get("createacolony:skill-using", s.Level, s.Usable) : s.Level.ToString();
            composer
                .AddStaticText(Lang.Get("createacolony:skill-" + s.Code), label, ElementBounds.Fixed(x, y, width, 20))
                .AddStaticText(text, limited ? capped : value, ElementBounds.Fixed(x, y, width, 20));
            // The bar fills towards the next level; full at the top level.
            var bar = ElementBounds.Fixed(x, y + 20, width, 6);
            composer.AddStatbar(bar, limited ? GuiKit.WarnColor : GuiStyle.FoodBarColor, "skill-" + s.Code);
            string hover = s.NextXp > 0 ? Lang.Get("createacolony:skill-xp", s.Xp, s.NextXp) : Lang.Get("createacolony:skill-max");
            if (limited)
            {
                string by = s.CapNote.StartsWith('@') ? Lang.Get("createacolony:" + s.CapNote[1..]) : s.CapNote;
                hover += "\n" + Lang.Get("createacolony:skill-capped-by", s.Usable, by);
            }
            composer.AddHoverText(hover, CairoFont.WhiteDetailText(), 260, ElementBounds.Fixed(x, y, width, 26));
            y += 30;
        }
        composer.Text(Lang.Get("createacolony:skills-hint"), GuiKit.Muted(), x, y + 4, width);
        return x + width;
    }

    /// <summary>
    /// The Traits block above the skills: the trait score, then each trait with its level, green for good,
    /// orange for bad, grey when it does nothing right now (a job trait in another job). Hovering a trait
    /// says what it does at its level. Returns the y below the block.
    /// </summary>
    double ComposeTraits(GuiComposer composer, double x, double y, double width)
    {
        var good = CairoFont.WhiteDetailText().WithColor(GuiKit.GoodColor);
        var bad = CairoFont.WhiteDetailText().WithColor(GuiKit.WarnColor);
        var quirk = CairoFont.WhiteDetailText();
        var idle = GuiKit.Muted();
        var level = CairoFont.WhiteDetailText().WithOrientation(EnumTextOrientation.Right);

        string score = (snapshot.TraitScore >= 0 ? "+" : "") + snapshot.TraitScore;
        composer
            .AddStaticText(Lang.Get("createacolony:citizen-traits"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(x, y, width, 25))
            .AddStaticText(Lang.Get("createacolony:trait-score", score), GuiKit.Muted().WithOrientation(EnumTextOrientation.Right), ElementBounds.Fixed(x, y + 4, width, 20));
        y += 30;
        foreach (var t in snapshot.Traits)
        {
            var font = !t.Active ? idle : t.Points > 0 ? good : t.Points < 0 ? bad : quirk;
            string name = Lang.Get("createacolony:trait-" + t.Code);
            if (t.Job.Length > 0) name = Lang.Get("createacolony:trait-job-name", name, Lang.Get("createacolony:job-" + t.Job));
            composer
                .AddStaticText(name, font, ElementBounds.Fixed(x, y, width - 40, 20))
                .AddStaticText(t.Level.ToString(), t.Active ? level : level.Clone().WithColor(GuiKit.MutedColor), ElementBounds.Fixed(x, y, width, 20));
            string hover = Lang.Get("createacolony:trait-desc-" + t.Code) + "\n" + t.Effects;
            if (!t.Active) hover += "\n" + Lang.Get(snapshot.Child ? "createacolony:trait-child" : t.Code == "brave" ? "createacolony:trait-suppressed" : "createacolony:trait-idle", Lang.Get("createacolony:job-" + (t.Job.Length == 0 ? "none" : t.Job)));
            if (t.Earned) hover += "\n" + Lang.Get("createacolony:trait-earned");
            hover += "\n" + Lang.Get("createacolony:trait-level-hint", t.Level);
            composer.AddHoverText(hover, CairoFont.WhiteDetailText(), 280, ElementBounds.Fixed(x, y, width, 20));
            y += 21;
        }
        return y;
    }

    /// <summary>
    /// The Inventory tab: the tool bar on a line of its own (weapon, three tools, offhand) with the item in
    /// hand, then bags, the pack and the packed food.
    /// </summary>
    void ComposeInventory(GuiComposer composer, double x, double y)
    {
        double width = PackColumns * SlotSize;
        var label = CairoFont.WhiteDetailText();
        composer.AddStaticText(JobLine(), GuiKit.Muted(), ElementBounds.Fixed(x, y, width, 20));
        y += 26;

        // Tool bar: weapon | tools x3 | offhand, with the item in hand at the end.
        composer
            .AddStaticText(Lang.Get("createacolony:citizen-toolbar"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, 22))
            .AddStaticText(Lang.Get("createacolony:citizen-weapon"), label, ElementBounds.Fixed(x, y + 24, SlotSize, 20))
            .AddStaticText(Lang.Get("createacolony:citizen-tools"), label, ElementBounds.Fixed(x + SlotSize + 10, y + 24, 3 * SlotSize, 20))
            .AddStaticText(Lang.Get("createacolony:citizen-offhand"), label, ElementBounds.Fixed(x + 4 * SlotSize + 20, y + 24, SlotSize * 1.5, 20))
            .AddStaticText(Lang.Get("createacolony:citizen-hand"), label, ElementBounds.Fixed(x + 5 * SlotSize + 40, y + 24, SlotSize * 1.5, 20));
        y += 46;
        var bar = SlotInventory(ColonyManager.AreaToolBar, ColonyManager.ToolBarSlots, snapshot.ToolBar);
        bar[ColonyManager.OffhandSlot].BackgroundIcon = "left";
        foreach (var slot in bar) slot.HexBackgroundColor = "#4a5a6a";
        composer
            .AddItemSlotGrid(bar, _ => { }, 1, new[] { ColonyManager.WeaponSlot }, ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, 1, 1), "weapon")
            .AddItemSlotGrid(bar, _ => { }, 3, new[] { 1, 2, 3 }, ElementStdBounds.SlotGrid(EnumDialogArea.None, x + SlotSize + 10, y, 3, 1), "tools")
            .AddItemSlotGrid(bar, _ => { }, 1, new[] { ColonyManager.OffhandSlot }, ElementStdBounds.SlotGrid(EnumDialogArea.None, x + 4 * SlotSize + 20, y, 1, 1), "offhand");
        var hand = ViewInventory("hand", 1, snapshot.Hand == null ? new List<byte[]>() : new List<byte[]> { snapshot.Hand });
        hand[0].BackgroundIcon = "right";
        composer.AddItemSlotGrid(hand, _ => { }, 1, ElementStdBounds.SlotGrid(EnumDialogArea.None, x + 5 * SlotSize + 40, y, 1, 1), "hand");
        y += SlotSize + 14;

        var bags = SlotInventory(ColonyManager.AreaBags, ColonyManager.MaxBags, snapshot.Bags);
        foreach (var slot in bags) slot.BackgroundIcon = "basket";
        composer.AddStaticText(Lang.Get("createacolony:citizen-bags", snapshot.Bags.Count, ColonyManager.MaxBags), label, ElementBounds.Fixed(x, y, width, 20));
        y += 22;
        composer.AddItemSlotGrid(bags, _ => { }, ColonyManager.MaxBags, ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, ColonyManager.MaxBags, 1), "bags");
        y += SlotSize + 12;

        int rows = Math.Max(1, (snapshot.Slots + PackColumns - 1) / PackColumns);
        composer.AddStaticText(Lang.Get("createacolony:citizen-inventory", snapshot.Pack.Count, snapshot.Slots), label, ElementBounds.Fixed(x, y, width, 20));
        y += 22;
        composer.AddItemSlotGrid(SlotInventory(ColonyManager.AreaPack, snapshot.Slots, snapshot.Pack), _ => { }, PackColumns,
            ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, PackColumns, rows), "pack");
        y += rows * SlotSize + 12;

        composer.AddStaticText(Lang.Get("createacolony:citizen-lunch"), label, ElementBounds.Fixed(x, y, width, 20));
        y += 22;
        composer.AddItemSlotGrid(SlotInventory(ColonyManager.AreaLunch, ColonyManager.LunchSlots, snapshot.Lunch), _ => { }, ColonyManager.LunchSlots,
            ElementStdBounds.SlotGrid(EnumDialogArea.None, x, y, ColonyManager.LunchSlots, 1), "lunch");
        y += SlotSize + 10;
        composer.Text(Lang.Get(snapshot.CanEdit ? "createacolony:citizen-inventory-hint-edit" : "createacolony:citizen-bag-hint"), GuiKit.Muted(), x, y, width);
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

    /// <summary>Clothing slots showing the player's slot icons and what the settler wears there; a click names the dress slot.</summary>
    SettlerSlots ClothesInventory(string name, (string Icon, int Slot)[] gear)
    {
        var map = Array.ConvertAll(gear, g => g.Slot);
        var inv = new SettlerSlots(gear.Length, $"{name}-{snapshot.CitizenId}", capi, snapshot.CanEdit ? Click : null, ColonyManager.AreaClothes, map);
        var stacks = Stacks(snapshot.Clothes);
        for (int i = 0; i < gear.Length; i++)
        {
            if (gear[i].Slot < stacks.Count) inv[i].Itemstack = stacks[gear[i].Slot];
            inv[i].BackgroundIcon = gear[i].Icon;
        }
        return inv;
    }

    List<ItemStack?> Stacks(List<byte[]> data)
    {
        var stacks = new List<ItemStack?>();
        foreach (var bytes in data)
        {
            if (bytes.Length == 0)
            {
                stacks.Add(null);
                continue;
            }
            var stack = new ItemStack(bytes);
            stacks.Add(stack.ResolveBlockOrItem(capi.World) ? stack : null);
        }
        return stacks;
    }

    /// <summary>
    /// The settler's slots of one area. Members can click them (the click is sent to the server); for
    /// anyone else they are for looking only.
    /// </summary>
    SettlerSlots SlotInventory(string area, int slots, List<byte[]> data, string? id = null)
    {
        var inv = new SettlerSlots(Math.Max(1, slots), $"{id ?? area}-{snapshot.CitizenId}", capi, snapshot.CanEdit ? Click : null, area);
        var stacks = Stacks(data);
        for (int i = 0; i < stacks.Count && i < inv.Count; i++) inv[i].Itemstack = stacks[i];
        return inv;
    }

    void Click(string area, int index, bool right, bool shift) => network.SendSettlerSlot(new SettlerSlotPacket
    {
        ColonyId = snapshot.ColonyId,
        CitizenId = snapshot.CitizenId,
        Area = area,
        Index = index,
        Right = right,
        Shift = shift
    });

    /// <summary>A client-only inventory of look-only slots filled with the given stacks.</summary>
    InventoryGeneric ViewInventory(string name, int slots, List<byte[]> data)
    {
        var inv = new InventoryGeneric(Math.Max(1, slots), "createacolony-view", $"{name}-{snapshot.CitizenId}", capi, (id, self) => new ViewOnlySlot(self));
        var stacks = Stacks(data);
        for (int i = 0; i < stacks.Count && i < inv.Count; i++) inv[i].Itemstack = stacks[i];
        return inv;
    }

    public override void OnGuiClosed()
    {
        if (!Replaced) network.SendAction(new ColonyActionPacket { ColonyId = snapshot.ColonyId, Action = ColonyAction.CloseWindow });
        base.OnGuiClosed();
    }

    /// <summary>
    /// Slots that show the settler's items. A click does not move anything here: it is sent to the server,
    /// which moves the item between the settler and the mouse cursor and sends the window again.
    /// </summary>
    class SettlerSlots : InventoryGeneric
    {
        readonly System.Action<string, int, bool, bool>? onClick;
        readonly string area;
        /// <summary>The settler's slot number for each slot here, when they differ (the two clothing columns).</summary>
        readonly int[]? map;

        public SettlerSlots(int slots, string id, ICoreClientAPI capi, System.Action<string, int, bool, bool>? onClick, string area, int[]? map = null)
            : base(slots, "createacolony-view", id, capi, (_, self) => onClick == null ? new ViewOnlySlot(self) : new ItemSlot(self))
        {
            this.onClick = onClick;
            this.area = area;
            this.map = map;
        }

        public override object? ActivateSlot(int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (onClick != null && op.MouseButton is EnumMouseButton.Left or EnumMouseButton.Right)
            {
                onClick(area, map != null && slotId < map.Length ? map[slotId] : slotId, op.MouseButton == EnumMouseButton.Right, op.ShiftDown);
            }
            return null;
        }
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
