using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// The window of a hut block. Every hut shows its own level with build and upgrade buttons; the
/// Builder's Hut also lists the colony's work orders and the items its builder still needs, each with
/// a picture and have/need counts. Like the Town Hall window it only asks the server for changes.
/// </summary>
public class GuiDialogHut : GuiDialog
{
    const double Width = 560;
    const int ItemRowsPerPage = 7;
    const int ItemColumns = 2;
    const double ItemRowHeight = 46;
    const int OrdersPerPage = 5;
    const double OrderRowHeight = 62;
    const int WorkAreaTab = 3;
    const int PotteryTab = 4;
    const int PotteryRowsPerPage = 7;
    const double PotteryRowHeight = 44;
    const int RecruitsTab = 5;
    const int KitchenTab = 6;
    const double RecruitRowHeight = 44;

    readonly ColonyNetwork network;
    HutSnapshot snapshot;
    int currentTab;
    int page;

    public int BuildingId => snapshot.Building.BuildingId;

    /// <summary>Set when another hut's window takes this one's place, so closing it doesn't tell the server.</summary>
    public bool Replaced;

    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogHut(ICoreClientAPI capi, ColonyNetwork network, HutSnapshot snapshot) : base(capi)
    {
        this.network = network;
        this.snapshot = snapshot;
        Compose();
    }

    public void Update(HutSnapshot fresh)
    {
        snapshot = fresh;
        Compose();
    }

    ColonyRole ViewerRole => (ColonyRole)snapshot.ViewerRole;

    void Compose()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        if (!snapshot.IsBuilderHut && !(snapshot.IsGatherHut && currentTab == WorkAreaTab) && !(snapshot.IsPotterHut && currentTab == PotteryTab)
            && !(snapshot.IsTavern && currentTab == RecruitsTab) && !(snapshot.IsCookHut && currentTab == KitchenTab)) currentTab = 0;

        var composer = capi.Gui.CreateCompo("createacolony-hut", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("createacolony:building-" + snapshot.Building.Type), () => TryClose());

        if (snapshot.IsBuilderHut)
        {
            var tabs = new[]
            {
                new GuiTab { Name = Lang.Get("createacolony:tab-building"), DataInt = 0, Active = currentTab == 0 },
                new GuiTab { Name = Lang.Get("createacolony:tab-orders"), DataInt = 1, Active = currentTab == 1 },
                new GuiTab { Name = Lang.Get("createacolony:tab-items"), DataInt = 2, Active = currentTab == 2 },
            };
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 3 * 30), (id, tab) => { currentTab = id; page = 0; Compose(); }, "tabs");
        }
        else if (snapshot.IsGatherHut)
        {
            var tabs = new[]
            {
                new GuiTab { Name = Lang.Get("createacolony:tab-building"), DataInt = 0, Active = currentTab == 0 },
                new GuiTab { Name = Lang.Get("createacolony:tab-workarea"), DataInt = WorkAreaTab, Active = currentTab == WorkAreaTab },
            };
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 2 * 30), (id, tab) => { currentTab = tab.DataInt; Compose(); }, "tabs");
        }
        else if (snapshot.IsPotterHut)
        {
            var tabs = new[]
            {
                new GuiTab { Name = Lang.Get("createacolony:tab-building"), DataInt = 0, Active = currentTab == 0 },
                new GuiTab { Name = Lang.Get("createacolony:tab-pottery"), DataInt = PotteryTab, Active = currentTab == PotteryTab },
            };
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 2 * 30), (id, tab) => { currentTab = tab.DataInt; page = 0; Compose(); }, "tabs");
        }
        else if (snapshot.IsCookHut)
        {
            var tabs = new[]
            {
                new GuiTab { Name = Lang.Get("createacolony:tab-building"), DataInt = 0, Active = currentTab == 0 },
                new GuiTab { Name = Lang.Get("createacolony:tab-kitchen"), DataInt = KitchenTab, Active = currentTab == KitchenTab },
            };
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 2 * 30), (id, tab) => { currentTab = tab.DataInt; page = 0; Compose(); }, "tabs");
        }
        else if (snapshot.IsTavern)
        {
            var tabs = new[]
            {
                new GuiTab { Name = Lang.Get("createacolony:tab-building"), DataInt = 0, Active = currentTab == 0 },
                new GuiTab { Name = Lang.Get("createacolony:tab-recruits"), DataInt = RecruitsTab, Active = currentTab == RecruitsTab },
            };
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 2 * 30), (id, tab) => { currentTab = tab.DataInt; Compose(); }, "tabs");
        }

        composer.BeginChildElements(bgBounds);
        switch (currentTab)
        {
            case 0: ComposeBuilding(composer); break;
            case 1: ComposeOrders(composer); break;
            case 2: ComposeItems(composer); break;
            case WorkAreaTab: ComposeWorkArea(composer); break;
            case PotteryTab: ComposeKeepList(composer, snapshot.Pottery, "pottery", ColonyAction.SetPotteryKeep); break;
            case KitchenTab: ComposeKeepList(composer, snapshot.Kitchen, "kitchen", ColonyAction.SetKitchenKeep); break;
            case RecruitsTab: ComposeRecruits(composer); break;
        }
        SingleComposer = composer.EndChildElements().Compose();
        SingleComposer.GetSwitch("anytype")?.SetValue(snapshot.AnyWoodOrStone);
        SingleComposer.GetSwitch("escort")?.SetValue(snapshot.Escort);
        SingleComposer.GetSwitch("berries")?.SetValue(snapshot.CollectBerries);
    }

    void ComposeBuilding(GuiComposer composer)
    {
        var b = snapshot.Building;
        double y = 30;

        // Level and style on a sunken panel, with the order buttons under it.
        composer.AddInset(ElementBounds.Fixed(0, y, Width, b.TargetLevel > 0 ? 62 : 38), 3, 0.8f);
        composer
            .AddStaticText(LevelText(b), GuiKit.Body(), ElementBounds.Fixed(10, y + 8, Width / 2, 25))
            .AddStaticText(Lang.Get("createacolony:placement-style", Lang.Get("createacolony:style-" + BlueprintStyles.Normalize(b.Style))),
                GuiKit.Detail().WithOrientation(EnumTextOrientation.Right), ElementBounds.Fixed(Width / 2, y + 10, Width / 2 - 10, 22));
        if (b.TargetLevel > 0)
        {
            composer.AddStaticText(StatusText(b), GuiKit.Detail().WithColor(GuiStyle.ActiveButtonTextColor), ElementBounds.Fixed(10, y + 34, Width - 20, 22));
        }
        y += (b.TargetLevel > 0 ? 62 : 38) + 8;

        double buttons = AddOrderButtons(composer, b, y);
        if (snapshot.HasStorage && ViewerRole >= ColonyRole.Member)
        {
            composer.AddSmallButton(Lang.Get("createacolony:hut-open-storage"), OpenStorage, ElementBounds.Fixed(Width - 120, y, 120, 28));
            buttons = Math.Max(buttons, y + 34);
        }
        y = buttons + 8;

        if (snapshot.Info.Length > 0)
        {
            // A worker's hut shows who works there, what they are doing and how the job works.
            y = composer.Rich(snapshot.Info, GuiKit.Detail(), 0, y, Width, 10);
        }

        if (snapshot.IsBuilderHut)
        {
            y = composer.Section(Lang.Get("createacolony:hut-builder-section"), y, Width);
            string builder = snapshot.BuilderName.Length > 0
                ? Lang.Get("createacolony:hut-builder-name", snapshot.BuilderName)
                : Lang.Get("createacolony:hut-no-builder");
            y = composer.Text(builder, snapshot.BuilderName.Length > 0 ? GuiKit.Body() : GuiKit.Body().WithColor(GuiKit.WarnColor), 0, y, Width, 8);
            y = AddAnyTypeSwitch(composer, y);
            composer.Text(Lang.Get("createacolony:build-hint"), GuiKit.Muted(), 0, y, Width);
        }
    }

    /// <summary>
    /// The Work area tab of a Forester's or Farmer's Hut: how far out its worker goes, whether a guard
    /// goes along, and for a farmer whether it collects berries and fruit. Officers change them.
    /// </summary>
    void ComposeWorkArea(GuiComposer composer)
    {
        bool officer = ViewerRole >= ColonyRole.Officer;
        bool farmer = snapshot.Building.Type == BuildingTypes.Farmer;
        string job = Lang.Get("createacolony:job-" + (farmer ? Jobs.Farmer : Jobs.Forester));
        double y = 30;

        y = composer.Section(Lang.Get("createacolony:workarea-range", job), y, Width);
        string range = Lang.Get("createacolony:workarea-blocks", snapshot.WorkRadius);
        if (officer)
        {
            var choices = snapshot.RadiusChoices;
            int at = Math.Max(0, choices.IndexOf(snapshot.WorkRadius));
            composer
                .AddSmallButton("<", () => SetRadius(choices[Math.Max(0, at - 1)]), ElementBounds.Fixed(0, y, 40, 28))
                .AddStaticText(range, GuiKit.Body().WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(45, y + 4, 130, 25))
                .AddSmallButton(">", () => SetRadius(choices[Math.Min(choices.Count - 1, at + 1)]), ElementBounds.Fixed(180, y, 40, 28));
        }
        else
        {
            composer.AddStaticText(range, GuiKit.Body(), ElementBounds.Fixed(0, y + 4, Width, 25));
        }
        y += 36;
        y = composer.Text(Lang.Get(farmer ? "createacolony:workarea-range-hint-farmer" : "createacolony:workarea-range-hint-forester"), GuiKit.Muted(), 0, y, Width, 14);

        y = composer.Section(Lang.Get("createacolony:workarea-escort"), y, Width);
        y = AddSetting(composer, "escort", ColonyAction.SetEscort, snapshot.Escort, y);
        string guard = !snapshot.Escort ? Lang.Get("createacolony:workarea-escort-hint")
            : snapshot.GuardName.Length > 0 ? Lang.Get("createacolony:workarea-escort-by", snapshot.GuardName)
            : Lang.Get("createacolony:workarea-escort-none", snapshot.GuardCount);
        y = composer.Text(guard, snapshot.Escort && snapshot.GuardName.Length == 0 ? GuiKit.Detail().WithColor(GuiKit.WarnColor) : GuiKit.Muted(), 0, y, Width, 14);

        if (farmer)
        {
            y = composer.Section(Lang.Get("createacolony:workarea-berries"), y, Width);
            y = AddSetting(composer, "berries", ColonyAction.SetCollectBerries, snapshot.CollectBerries, y);
            composer.Text(Lang.Get("createacolony:workarea-berries-hint"), GuiKit.Muted(), 0, y, Width);
        }
    }

    /// <summary>
    /// A "keep in stock" list: the Pottery tab of a Potter's Hut (every kind of pottery the game's clay
    /// forming recipes make) or the Kitchen tab of a Cook's Hut (every food the game cooks in a fire pit),
    /// with how many the colony has and how many to keep. Kinds kept in stock come first. Lang keys start
    /// with the prefix: "-hint", "-none", "-have" and "-keep".
    /// </summary>
    void ComposeKeepList(GuiComposer composer, List<PotteryView> list, string prefix, ColonyAction action)
    {
        bool officer = ViewerRole >= ColonyRole.Officer;
        double y = composer.Text(Lang.Get("createacolony:" + prefix + "-hint"), GuiKit.Muted(), 0, 30, Width, 10);
        var rows = list.OrderByDescending(p => p.Keep > 0).ThenBy(p => p.Key).ToList();
        if (rows.Count == 0)
        {
            composer.Text(Lang.Get("createacolony:" + prefix + "-none"), GuiKit.Body(), 0, y, Width);
            return;
        }
        int pages = (rows.Count + PotteryRowsPerPage - 1) / PotteryRowsPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        int shown = Math.Min(PotteryRowsPerPage, rows.Count - page * PotteryRowsPerPage);
        composer.AddInset(ElementBounds.Fixed(0, y, Width, shown * PotteryRowHeight + 8), 3, 0.8f);
        y += 4;
        foreach (var row in rows.Skip(page * PotteryRowsPerPage).Take(PotteryRowsPerPage))
        {
            var stack = new ItemStack(row.Stack);
            if (!stack.ResolveBlockOrItem(capi.World)) continue;
            var countFont = CairoFont.WhiteDetailText().WithColor(row.Keep == 0 ? GuiKit.MutedColor
                : row.Have >= row.Keep ? GuiKit.GoodColor : GuiKit.WarnColor);
            composer.AddRichtext(new RichTextComponentBase[]
            {
                new ItemstackTextComponent(capi, stack, 38, 10),
                new RichTextComponent(capi, (row.Name ?? stack.GetName()) + "\n", CairoFont.WhiteSmallText()) { VerticalAlign = EnumVerticalAlign.Top },
                new RichTextComponent(capi, Lang.Get("createacolony:" + prefix + "-have", row.Have, row.Clay), countFont)
            }, ElementBounds.Fixed(6, y, 330, PotteryRowHeight));

            string keep = Lang.Get("createacolony:" + prefix + "-keep", row.Keep);
            if (officer)
            {
                var choices = snapshot.KeepChoices.Union(new[] { row.Keep }).OrderBy(c => c).ToList();
                int at = choices.IndexOf(row.Keep);
                string key = row.Key;
                composer
                    .AddSmallButton("<", () => SetKeep(action, key, choices[Math.Max(0, at - 1)]), ElementBounds.Fixed(Width - 210, y + 6, 36, 28))
                    .AddStaticText(keep, GuiKit.Body().WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(Width - 170, y + 10, 120, 25))
                    .AddSmallButton(">", () => SetKeep(action, key, choices[Math.Min(choices.Count - 1, at + 1)]), ElementBounds.Fixed(Width - 46, y + 6, 36, 28));
            }
            else
            {
                composer.AddStaticText(keep, GuiKit.Body().WithOrientation(EnumTextOrientation.Right), ElementBounds.Fixed(Width - 210, y + 10, 200, 25));
            }
            y += PotteryRowHeight;
        }
        AddPager(composer, pages, y + 14);
    }

    /// <summary>
    /// The Recruits tab of a Tavern: today's people looking for work, what each asks in rusty gears,
    /// and a Hire button. The viewer pays, from what they carry and then from the Tavern's storage.
    /// </summary>
    void ComposeRecruits(GuiComposer composer)
    {
        if (snapshot.Building.Level < 1)
        {
            composer.Text(Lang.Get("createacolony:tavern-closed"), GuiKit.Body(), 0, 30, Width);
            return;
        }

        ItemStack? currency = null;
        if (snapshot.Currency != null)
        {
            currency = new ItemStack(snapshot.Currency);
            if (!currency.ResolveBlockOrItem(capi.World)) currency = null;
        }
        string have = snapshot.Creative ? Lang.Get("createacolony:recruits-creative") : Lang.Get("createacolony:recruits-have", snapshot.Gears);
        var header = new List<RichTextComponentBase>();
        if (currency != null) header.Add(new ItemstackTextComponent(capi, currency, 32, 5));
        header.Add(new RichTextComponent(capi, have, GuiKit.Body()) { VerticalAlign = EnumVerticalAlign.Middle });
        composer.AddRichtext(header.ToArray(), ElementBounds.Fixed(0, 30, Width, 36));
        double y = composer.Text(Lang.Get("createacolony:recruits-hint"), GuiKit.Muted(), 0, 70, Width, 10);

        if (snapshot.Recruits.Count == 0)
        {
            composer.Text(Lang.Get("createacolony:recruits-none"), GuiKit.Body(), 0, y, Width);
            return;
        }

        bool canHire = ViewerRole >= ColonyRole.Member;
        composer.AddInset(ElementBounds.Fixed(0, y, Width, snapshot.Recruits.Count * RecruitRowHeight + 8), 3, 0.8f);
        y += 4;
        foreach (var r in snapshot.Recruits)
        {
            bool afford = snapshot.Creative || snapshot.Gears >= r.Cost;
            var costFont = CairoFont.WhiteDetailText().WithColor(afford ? GuiKit.GoodColor : GuiKit.WarnColor);
            composer.AddRichtext(new RichTextComponentBase[]
            {
                new RichTextComponent(capi, r.Name + "\n", GuiKit.Body()),
                new RichTextComponent(capi, Lang.Get("createacolony:recruit-line", Lang.Get("createacolony:sex-" + r.Sex), r.Cost), costFont)
            }, ElementBounds.Fixed(10, y + 2, 400, RecruitRowHeight));
            if (canHire)
            {
                string name = r.Name;
                composer.AddSmallButton(Lang.Get("createacolony:recruit-hire"), () => Hire(name), ElementBounds.Fixed(Width - 130, y + 7, 120, 28));
            }
            y += RecruitRowHeight;
        }
    }

    bool Hire(string name)
    {
        network.SendAction(new ColonyActionPacket
            { ColonyId = snapshot.ColonyId, Action = ColonyAction.HireRecruit, BuildingId = snapshot.Building.BuildingId, Text = name });
        return true;
    }

    bool SetKeep(ColonyAction action, string key, int keep)
    {
        network.SendAction(new ColonyActionPacket
            { ColonyId = snapshot.ColonyId, Action = action, BuildingId = snapshot.Building.BuildingId, Text = key + "|" + keep });
        return true;
    }

    /// <summary>An on/off setting under its section heading: a switch for officers, On or Off for everyone else. Returns the y below it.</summary>
    double AddSetting(GuiComposer composer, string key, ColonyAction action, bool on, double y)
    {
        string state = Lang.Get(on ? "createacolony:on" : "createacolony:off");
        if (ViewerRole >= ColonyRole.Officer)
        {
            int id = snapshot.Building.BuildingId;
            composer
                .AddSwitch(value => network.SendAction(new ColonyActionPacket
                    { ColonyId = snapshot.ColonyId, Action = action, BuildingId = id, Text = value ? "1" : "0" }),
                    ElementBounds.Fixed(0, y, 30, 30), key)
                .AddStaticText(state, GuiKit.Body(), ElementBounds.Fixed(40, y + 5, Width - 40, 25));
        }
        else
        {
            composer.AddStaticText(state, GuiKit.Body(), ElementBounds.Fixed(0, y + 5, Width, 25));
        }
        return y + 36;
    }

    bool SetRadius(int radius)
    {
        if (radius == snapshot.WorkRadius) return true;
        network.SendAction(new ColonyActionPacket
            { ColonyId = snapshot.ColonyId, Action = ColonyAction.SetWorkRadius, BuildingId = snapshot.Building.BuildingId, Text = radius.ToString() });
        return true;
    }

    /// <summary>The "any wood or stone" setting: a switch for officers, a line of text for everyone else. Returns the y below it.</summary>
    double AddAnyTypeSwitch(GuiComposer composer, double y)
    {
        if (ViewerRole >= ColonyRole.Officer)
        {
            composer
                .AddSwitch(on => network.SendAction(new ColonyActionPacket
                    { ColonyId = snapshot.ColonyId, Action = ColonyAction.SetAnyWoodOrStone, Text = on ? "1" : "0" }),
                    ElementBounds.Fixed(0, y, 30, 30), "anytype")
                .AddStaticText(Lang.Get("createacolony:any-type"), GuiKit.Body(), ElementBounds.Fixed(40, y + 5, Width - 40, 25));
            y += 34;
        }
        else
        {
            y = composer.Text(Lang.Get(snapshot.AnyWoodOrStone ? "createacolony:any-type-on" : "createacolony:any-type-off"), GuiKit.Body(), 0, y + 5, Width, 4);
        }
        return composer.Text(Lang.Get("createacolony:any-type-hint"), GuiKit.Muted(), ViewerRole >= ColonyRole.Officer ? 40 : 0, y, Width - 40, 12);
    }

    /// <summary>Build/upgrade, cancel and turn buttons for one building. Returns the y below them.</summary>
    double AddOrderButtons(GuiComposer composer, BuildingView b, double y)
    {
        if (ViewerRole < ColonyRole.Officer) return y;
        int id = b.BuildingId;
        if (b.TargetLevel > 0)
        {
            composer.AddSmallButton(Lang.Get("createacolony:build-cancel"), () => Send(ColonyAction.CancelBuild, id), ElementBounds.Fixed(0, y, 160, 28));
            return y + 34;
        }
        if (b.Level >= b.MaxLevel) return y;

        string label = Lang.Get(b.Level == 0 ? "createacolony:build-start" : "createacolony:build-upgrade", b.Level + 1);
        composer.AddSmallButton(label, () => Send(ColonyAction.Build, id), ElementBounds.Fixed(0, y, 160, 28));
        if (b.Level == 0)
        {
            composer.AddSmallButton(Lang.Get("createacolony:build-rotate", b.Rotation), () => Send(ColonyAction.Rotate, id), ElementBounds.Fixed(170, y, 200, 28));
        }
        return y + 34;
    }

    void ComposeOrders(GuiComposer composer)
    {
        var orders = snapshot.Orders;
        if (orders.Count == 0)
        {
            composer.Text(Lang.Get("createacolony:hut-no-orders"), GuiKit.Body(), 0, 30, Width);
            return;
        }

        int pages = (orders.Count + OrdersPerPage - 1) / OrdersPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        int shown = Math.Min(OrdersPerPage, orders.Count - page * OrdersPerPage);
        composer.AddInset(ElementBounds.Fixed(0, 30, Width, shown * OrderRowHeight + 8), 3, 0.8f);
        double y = 34;
        foreach (var o in orders.Skip(page * OrdersPerPage).Take(OrdersPerPage))
        {
            string title = Lang.Get("createacolony:building-" + o.Type) + " " + Lang.Get("createacolony:order-level", o.TargetLevel);
            composer
                .AddStaticText(title, GuiKit.Body(), ElementBounds.Fixed(10, y + 6, Width - 150, 25))
                .AddStaticText(StatusText(o), GuiKit.Detail().WithColor(GuiStyle.ActiveButtonTextColor), ElementBounds.Fixed(10, y + 30, Width - 150, 22));
            if (ViewerRole >= ColonyRole.Officer)
            {
                int id = o.BuildingId;
                composer.AddSmallButton(Lang.Get("createacolony:build-cancel"), () => Send(ColonyAction.CancelBuild, id), ElementBounds.Fixed(Width - 130, y + 14, 120, 28));
            }
            y += OrderRowHeight;
        }
        AddPager(composer, pages, y + 12);
    }

    void ComposeItems(GuiComposer composer)
    {
        if (snapshot.BuilderName.Length == 0)
        {
            composer.Text(Lang.Get("createacolony:hut-no-builder"), GuiKit.Body().WithColor(GuiKit.WarnColor), 0, 30, Width);
            return;
        }
        if (snapshot.ResourcesFor.Length == 0)
        {
            composer.Text(Lang.Get("createacolony:items-nothing", snapshot.BuilderName), GuiKit.Body(), 0, 30, Width);
            return;
        }

        composer.AddStaticText(Lang.Get("createacolony:items-for", snapshot.ResourcesFor), GuiKit.Heading(), ElementBounds.Fixed(0, 30, Width, 25));

        // Missing items first, so the list leads with what the builder is waiting on.
        var rows = snapshot.Resources.OrderBy(r => r.Have >= r.Needed).ThenByDescending(r => r.IsTool).ToList();
        if (rows.Count == 0)
        {
            composer.Text(Lang.Get("createacolony:items-none"), GuiKit.Body(), 0, 62, Width);
            return;
        }

        int perPage = ItemRowsPerPage * ItemColumns;
        int pages = (rows.Count + perPage - 1) / perPage;
        page = Math.Clamp(page, 0, pages - 1);
        double columnWidth = (Width - 12) / ItemColumns;
        int onPage = Math.Min(perPage, rows.Count - page * perPage);
        composer.AddInset(ElementBounds.Fixed(0, 60, Width, (onPage + ItemColumns - 1) / ItemColumns * ItemRowHeight + 8), 3, 0.8f);
        int i = 0;
        foreach (var r in rows.Skip(page * perPage).Take(perPage))
        {
            var stack = new ItemStack(r.Stack);
            if (!stack.ResolveBlockOrItem(capi.World)) continue;
            double x = 6 + i % ItemColumns * columnWidth;
            double y = 64 + i / ItemColumns * ItemRowHeight;
            composer.AddRichtext(ItemRow(stack, r), ElementBounds.Fixed(x, y, columnWidth - 10, ItemRowHeight));
            i++;
        }
        AddPager(composer, pages, 60 + ItemRowsPerPage * ItemRowHeight + 20);
    }

    RichTextComponentBase[] ItemRow(ItemStack stack, ResourceView r)
    {
        bool done = r.Have >= r.Needed;
        // Green when the builder has enough, orange when it is still waiting.
        var countFont = CairoFont.WhiteDetailText().WithColor(done ? GuiKit.GoodColor : GuiKit.WarnColor);
        string name = r.IsTool ? Lang.Get("createacolony:items-tool", Lang.Get("createacolony:tool-" + (stack.Collectible.Tool?.ToString().ToLowerInvariant() ?? "")))
            : r.AnyOf.Length > 0 ? Lang.Get("createacolony:items-any-" + r.AnyOf, stack.GetName()) : stack.GetName();
        return new RichTextComponentBase[]
        {
            new ItemstackTextComponent(capi, stack, 40, 10),
            new RichTextComponent(capi, name + "\n", CairoFont.WhiteSmallText()) { VerticalAlign = EnumVerticalAlign.Top },
            new RichTextComponent(capi, Lang.Get("createacolony:items-count", Math.Min(r.Have, r.Needed), r.Needed), countFont)
        };
    }

    void AddPager(GuiComposer composer, int pages, double y) => composer.Pager(page, pages, y, Width, p => { page = p; Compose(); });

    static string LevelText(BuildingView b) => b.MaxLevel == 0
        ? Lang.Get("createacolony:building-no-blueprints", b.Level)
        : Lang.Get("createacolony:building-level", b.Level, b.MaxLevel);

    static string StatusText(BuildingView b) =>
        Lang.Get("createacolony:build-status", b.TargetLevel, b.ProgressPercent, Lang.Get("createacolony:buildstatus-" + b.Status));

    /// <summary>The server opens the storage window; this one closes so they do not overlap.</summary>
    bool OpenStorage()
    {
        int id = snapshot.Building.BuildingId;
        TryClose();
        network.SendAction(new ColonyActionPacket { ColonyId = snapshot.ColonyId, Action = ColonyAction.OpenStorage, BuildingId = id });
        return true;
    }

    bool Send(ColonyAction action, int buildingId)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = snapshot.ColonyId, Action = action, BuildingId = buildingId });
        return true;
    }

    public override void OnGuiClosed()
    {
        if (!Replaced) network.SendAction(new ColonyActionPacket { ColonyId = snapshot.ColonyId, Action = ColonyAction.CloseWindow });
        base.OnGuiClosed();
    }
}
