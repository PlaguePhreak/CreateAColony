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
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 120), (id, tab) => { currentTab = id; page = 0; Compose(); }, "tabs");
        }
        else if (snapshot.IsGatherHut)
        {
            var tabs = new[]
            {
                new GuiTab { Name = Lang.Get("createacolony:tab-building"), DataInt = 0, Active = currentTab == 0 },
                new GuiTab { Name = Lang.Get("createacolony:tab-workarea"), DataInt = WorkAreaTab, Active = currentTab == WorkAreaTab },
            };
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 80), (id, tab) => { currentTab = tab.DataInt; Compose(); }, "tabs");
        }
        else if (snapshot.IsPotterHut)
        {
            var tabs = new[]
            {
                new GuiTab { Name = Lang.Get("createacolony:tab-building"), DataInt = 0, Active = currentTab == 0 },
                new GuiTab { Name = Lang.Get("createacolony:tab-pottery"), DataInt = PotteryTab, Active = currentTab == PotteryTab },
            };
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 80), (id, tab) => { currentTab = tab.DataInt; page = 0; Compose(); }, "tabs");
        }
        else if (snapshot.IsCookHut)
        {
            var tabs = new[]
            {
                new GuiTab { Name = Lang.Get("createacolony:tab-building"), DataInt = 0, Active = currentTab == 0 },
                new GuiTab { Name = Lang.Get("createacolony:tab-kitchen"), DataInt = KitchenTab, Active = currentTab == KitchenTab },
            };
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 80), (id, tab) => { currentTab = tab.DataInt; page = 0; Compose(); }, "tabs");
        }

        else if (snapshot.IsTavern)
        {
            var tabs = new[]
            {
                new GuiTab { Name = Lang.Get("createacolony:tab-building"), DataInt = 0, Active = currentTab == 0 },
                new GuiTab { Name = Lang.Get("createacolony:tab-recruits"), DataInt = RecruitsTab, Active = currentTab == RecruitsTab },
            };
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 80), (id, tab) => { currentTab = tab.DataInt; Compose(); }, "tabs");
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
        composer.AddStaticText(LevelText(b), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, Width, 25));
        y += 30;

        if (b.TargetLevel > 0)
        {
            composer.AddStaticText(StatusText(b), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 25));
            y += 30;
        }
        if (snapshot.Info.Length > 0)
        {
            // A worker's hut shows who works there, what they are doing and how the job works.
            double height = b.Type is BuildingTypes.Forester or BuildingTypes.Farmer or BuildingTypes.Potter ? 150
                : b.Type == BuildingTypes.Tavern ? 80 : 50;
            composer.AddRichtext(snapshot.Info, CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, height));
            y += height + 5;
        }

        y = AddOrderButtons(composer, b, y);
        if (snapshot.HasStorage && ViewerRole >= ColonyRole.Member)
        {
            composer.AddSmallButton(Lang.Get("createacolony:hut-open-storage"), OpenStorage, ElementBounds.Fixed(0, y, 160, 28));
            y += 34;
        }
        y += 10;
        if (snapshot.IsBuilderHut)
        {
            string builder = snapshot.BuilderName.Length > 0
                ? Lang.Get("createacolony:hut-builder-name", snapshot.BuilderName)
                : Lang.Get("createacolony:hut-no-builder");
            composer.AddStaticText(builder, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, Width, 25));
            y += 30;
            y = AddAnyTypeSwitch(composer, y);
            composer.AddStaticText(Lang.Get("createacolony:build-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 40));
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

        composer.AddStaticText(Lang.Get("createacolony:workarea-range", job), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, Width, 25));
        y += 30;
        string range = Lang.Get("createacolony:workarea-blocks", snapshot.WorkRadius);
        if (officer)
        {
            var choices = snapshot.RadiusChoices;
            int at = Math.Max(0, choices.IndexOf(snapshot.WorkRadius));
            composer
                .AddSmallButton("<", () => SetRadius(choices[Math.Max(0, at - 1)]), ElementBounds.Fixed(0, y, 40, 28))
                .AddStaticText(range, CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(45, y + 4, 130, 25))
                .AddSmallButton(">", () => SetRadius(choices[Math.Min(choices.Count - 1, at + 1)]), ElementBounds.Fixed(180, y, 40, 28));
        }
        else
        {
            composer.AddStaticText(range, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 4, Width, 25));
        }
        y += 36;
        composer.AddStaticText(Lang.Get(farmer ? "createacolony:workarea-range-hint-farmer" : "createacolony:workarea-range-hint-forester"),
            CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 60));
        y += 66;

        y = AddSetting(composer, "escort", ColonyAction.SetEscort, snapshot.Escort, Lang.Get("createacolony:workarea-escort"), y);
        string guard = !snapshot.Escort ? Lang.Get("createacolony:workarea-escort-hint")
            : snapshot.GuardName.Length > 0 ? Lang.Get("createacolony:workarea-escort-by", snapshot.GuardName)
            : Lang.Get("createacolony:workarea-escort-none", snapshot.GuardCount);
        composer.AddStaticText(guard, CairoFont.WhiteDetailText(), ElementBounds.Fixed(40, y, Width - 40, 45));
        y += 50;

        if (farmer)
        {
            y = AddSetting(composer, "berries", ColonyAction.SetCollectBerries, snapshot.CollectBerries, Lang.Get("createacolony:workarea-berries"), y);
            composer.AddStaticText(Lang.Get("createacolony:workarea-berries-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(40, y, Width - 40, 45));
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
        composer.AddStaticText(Lang.Get("createacolony:" + prefix + "-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 30, Width, 40));
        var rows = list.OrderByDescending(p => p.Keep > 0).ThenBy(p => p.Key).ToList();
        if (rows.Count == 0)
        {
            composer.AddStaticText(Lang.Get("createacolony:" + prefix + "-none"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 75, Width, 25));
            return;
        }
        int pages = (rows.Count + PotteryRowsPerPage - 1) / PotteryRowsPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        double y = 75;
        foreach (var row in rows.Skip(page * PotteryRowsPerPage).Take(PotteryRowsPerPage))
        {
            var stack = new ItemStack(row.Stack);
            if (!stack.ResolveBlockOrItem(capi.World)) continue;
            var countFont = CairoFont.WhiteDetailText().WithColor(row.Keep == 0 ? new[] { 0.7, 0.7, 0.7, 1.0 }
                : row.Have >= row.Keep ? new[] { 0.55, 0.85, 0.45, 1.0 } : new[] { 1.0, 0.65, 0.3, 1.0 });
            composer.AddRichtext(new RichTextComponentBase[]
            {
                new ItemstackTextComponent(capi, stack, 38, 10),
                new RichTextComponent(capi, stack.GetName() + "\n", CairoFont.WhiteSmallText()) { VerticalAlign = EnumVerticalAlign.Top },
                new RichTextComponent(capi, Lang.Get("createacolony:" + prefix + "-have", row.Have, row.Clay), countFont)
            }, ElementBounds.Fixed(0, y, 330, PotteryRowHeight));

            string keep = Lang.Get("createacolony:" + prefix + "-keep", row.Keep);
            if (officer)
            {
                var choices = snapshot.KeepChoices.Union(new[] { row.Keep }).OrderBy(c => c).ToList();
                int at = choices.IndexOf(row.Keep);
                string key = row.Key;
                composer
                    .AddSmallButton("<", () => SetKeep(action, key, choices[Math.Max(0, at - 1)]), ElementBounds.Fixed(340, y + 6, 36, 28))
                    .AddStaticText(keep, CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(380, y + 10, 120, 25))
                    .AddSmallButton(">", () => SetKeep(action, key, choices[Math.Min(choices.Count - 1, at + 1)]), ElementBounds.Fixed(504, y + 6, 36, 28));
            }
            else
            {
                composer.AddStaticText(keep, CairoFont.WhiteSmallText(), ElementBounds.Fixed(380, y + 10, 160, 25));
            }
            y += PotteryRowHeight;
        }
        AddPager(composer, pages, y + 6);
    }

    /// <summary>
    /// The Recruits tab of a Tavern: today's people looking for work, what each asks in rusty gears,
    /// and a Hire button. The viewer pays, from what they carry and then from the Tavern's storage.
    /// </summary>
    void ComposeRecruits(GuiComposer composer)
    {
        if (snapshot.Building.Level < 1)
        {
            composer.AddStaticText(Lang.Get("createacolony:tavern-closed"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, Width, 50));
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
        header.Add(new RichTextComponent(capi, have, CairoFont.WhiteSmallText()) { VerticalAlign = EnumVerticalAlign.Middle });
        composer.AddRichtext(header.ToArray(), ElementBounds.Fixed(0, 30, Width, 36));
        composer.AddStaticText(Lang.Get("createacolony:recruits-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 70, Width, 40));

        if (snapshot.Recruits.Count == 0)
        {
            composer.AddStaticText(Lang.Get("createacolony:recruits-none"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 115, Width, 25));
            return;
        }

        bool canHire = ViewerRole >= ColonyRole.Member;
        double y = 115;
        foreach (var r in snapshot.Recruits)
        {
            bool afford = snapshot.Creative || snapshot.Gears >= r.Cost;
            var costFont = CairoFont.WhiteDetailText().WithColor(afford ? new[] { 0.55, 0.85, 0.45, 1.0 } : new[] { 1.0, 0.65, 0.3, 1.0 });
            composer.AddRichtext(new RichTextComponentBase[]
            {
                new RichTextComponent(capi, r.Name + "\n", CairoFont.WhiteSmallText()),
                new RichTextComponent(capi, Lang.Get("createacolony:recruit-line", Lang.Get("createacolony:sex-" + r.Sex), r.Cost), costFont)
            }, ElementBounds.Fixed(0, y, 400, RecruitRowHeight));
            if (canHire)
            {
                string name = r.Name;
                composer.AddSmallButton(Lang.Get("createacolony:recruit-hire"), () => Hire(name), ElementBounds.Fixed(420, y + 6, 120, 28));
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

    /// <summary>An on/off setting: a switch for officers, a line of text for everyone else. Returns the y below it.</summary>
    double AddSetting(GuiComposer composer, string key, ColonyAction action, bool on, string label, double y)
    {
        if (ViewerRole >= ColonyRole.Officer)
        {
            int id = snapshot.Building.BuildingId;
            composer
                .AddSwitch(value => network.SendAction(new ColonyActionPacket
                    { ColonyId = snapshot.ColonyId, Action = action, BuildingId = id, Text = value ? "1" : "0" }),
                    ElementBounds.Fixed(0, y, 30, 30), key)
                .AddStaticText(label, CairoFont.WhiteSmallText(), ElementBounds.Fixed(40, y + 5, Width - 40, 25));
        }
        else
        {
            composer.AddStaticText(label + ": " + Lang.Get(on ? "createacolony:on" : "createacolony:off"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 5, Width, 25));
        }
        return y + 34;
    }

    bool SetRadius(int radius)
    {
        if (radius == snapshot.WorkRadius) return true;
        network.SendAction(new ColonyActionPacket
            { ColonyId = snapshot.ColonyId, Action = ColonyAction.SetWorkRadius, BuildingId = snapshot.Building.BuildingId, Text = radius.ToString() });
        return true;
    }

    /// <summary>The "any wood or stone" setting: a switch for officers, a line of text for everyone else.</summary>
    double AddAnyTypeSwitch(GuiComposer composer, double y)
    {
        if (ViewerRole >= ColonyRole.Officer)
        {
            composer
                .AddSwitch(on => network.SendAction(new ColonyActionPacket
                    { ColonyId = snapshot.ColonyId, Action = ColonyAction.SetAnyWoodOrStone, Text = on ? "1" : "0" }),
                    ElementBounds.Fixed(0, y, 30, 30), "anytype")
                .AddStaticText(Lang.Get("createacolony:any-type"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(40, y + 5, Width - 40, 25));
        }
        else
        {
            composer.AddStaticText(Lang.Get(snapshot.AnyWoodOrStone ? "createacolony:any-type-on" : "createacolony:any-type-off"),
                CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 5, Width, 25));
        }
        composer.AddStaticText(Lang.Get("createacolony:any-type-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(40, y + 32, Width - 40, 40));
        return y + 76;
    }

    /// <summary>Build/upgrade, cancel and turn buttons for one building. Returns the y below them.</summary>
    double AddOrderButtons(GuiComposer composer, BuildingView b, double y)
    {
        if (ViewerRole < ColonyRole.Officer) return y;
        int id = b.BuildingId;
        if (b.TargetLevel > 0)
        {
            composer.AddSmallButton(Lang.Get("createacolony:build-cancel"), () => Send(ColonyAction.CancelBuild, id), ElementBounds.Fixed(0, y, 140, 28));
            return y + 34;
        }
        if (b.Level >= b.MaxLevel) return y;

        string label = Lang.Get(b.Level == 0 ? "createacolony:build-start" : "createacolony:build-upgrade", b.Level + 1);
        composer.AddSmallButton(label, () => Send(ColonyAction.Build, id), ElementBounds.Fixed(0, y, 160, 28));
        if (b.Level == 0)
        {
            composer.AddSmallButton(Lang.Get("createacolony:build-rotate", b.Rotation), () => Send(ColonyAction.Rotate, id), ElementBounds.Fixed(170, y, 230, 28));
        }
        return y + 34;
    }

    void ComposeOrders(GuiComposer composer)
    {
        var orders = snapshot.Orders;
        if (orders.Count == 0)
        {
            composer.AddStaticText(Lang.Get("createacolony:hut-no-orders"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, Width, 50));
            return;
        }

        int pages = (orders.Count + OrdersPerPage - 1) / OrdersPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        double y = 30;
        foreach (var o in orders.Skip(page * OrdersPerPage).Take(OrdersPerPage))
        {
            string title = Lang.Get("createacolony:building-" + o.Type) + " " + Lang.Get("createacolony:order-level", o.TargetLevel);
            composer
                .AddStaticText(title, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 4, 400, 25))
                .AddStaticText(StatusText(o), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y + 30, 400, 25));
            if (ViewerRole >= ColonyRole.Officer)
            {
                int id = o.BuildingId;
                composer.AddSmallButton(Lang.Get("createacolony:build-cancel"), () => Send(ColonyAction.CancelBuild, id), ElementBounds.Fixed(420, y, 120, 28));
            }
            y += OrderRowHeight;
        }
        AddPager(composer, pages, y);
    }

    void ComposeItems(GuiComposer composer)
    {
        if (snapshot.BuilderName.Length == 0)
        {
            composer.AddStaticText(Lang.Get("createacolony:hut-no-builder"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, Width, 50));
            return;
        }
        if (snapshot.ResourcesFor.Length == 0)
        {
            composer.AddStaticText(Lang.Get("createacolony:items-nothing", snapshot.BuilderName), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, Width, 50));
            return;
        }

        composer.AddStaticText(Lang.Get("createacolony:items-for", snapshot.ResourcesFor), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, Width, 25));

        // Missing items first, so the list leads with what the builder is waiting on.
        var rows = snapshot.Resources.OrderBy(r => r.Have >= r.Needed).ThenByDescending(r => r.IsTool).ToList();
        if (rows.Count == 0)
        {
            composer.AddStaticText(Lang.Get("createacolony:items-none"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 60, Width, 25));
            return;
        }

        int perPage = ItemRowsPerPage * ItemColumns;
        int pages = (rows.Count + perPage - 1) / perPage;
        page = Math.Clamp(page, 0, pages - 1);
        double columnWidth = Width / ItemColumns;
        int i = 0;
        foreach (var r in rows.Skip(page * perPage).Take(perPage))
        {
            var stack = new ItemStack(r.Stack);
            if (!stack.ResolveBlockOrItem(capi.World)) continue;
            double x = i % ItemColumns * columnWidth;
            double y = 60 + i / ItemColumns * ItemRowHeight;
            composer.AddRichtext(ItemRow(stack, r), ElementBounds.Fixed(x, y, columnWidth - 10, ItemRowHeight));
            i++;
        }
        AddPager(composer, pages, 60 + ItemRowsPerPage * ItemRowHeight + 6);
    }

    RichTextComponentBase[] ItemRow(ItemStack stack, ResourceView r)
    {
        bool done = r.Have >= r.Needed;
        // Green when the builder has enough, orange when it is still waiting.
        var countFont = CairoFont.WhiteDetailText().WithColor(done ? new[] { 0.55, 0.85, 0.45, 1.0 } : new[] { 1.0, 0.65, 0.3, 1.0 });
        string name = r.IsTool ? Lang.Get("createacolony:items-tool", Lang.Get("createacolony:tool-" + (stack.Collectible.Tool?.ToString().ToLowerInvariant() ?? "")))
            : r.AnyOf.Length > 0 ? Lang.Get("createacolony:items-any-" + r.AnyOf, stack.GetName()) : stack.GetName();
        return new RichTextComponentBase[]
        {
            new ItemstackTextComponent(capi, stack, 40, 10),
            new RichTextComponent(capi, name + "\n", CairoFont.WhiteSmallText()) { VerticalAlign = EnumVerticalAlign.Top },
            new RichTextComponent(capi, Lang.Get("createacolony:items-count", Math.Min(r.Have, r.Needed), r.Needed), countFont)
        };
    }

    void AddPager(GuiComposer composer, int pages, double y)
    {
        if (pages <= 1) return;
        composer
            .AddSmallButton(Lang.Get("createacolony:prev"), () => { page--; Compose(); return true; }, ElementBounds.Fixed(0, y, 80, 28))
            .AddStaticText(Lang.Get("createacolony:page", page + 1, pages), CairoFont.WhiteDetailText(), ElementBounds.Fixed(100, y + 6, 120, 25))
            .AddSmallButton(Lang.Get("createacolony:next"), () => { page++; Compose(); return true; }, ElementBounds.Fixed(230, y, 80, 28));
    }

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
