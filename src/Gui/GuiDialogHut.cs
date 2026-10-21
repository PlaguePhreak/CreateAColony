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

        if (!snapshot.IsBuilderHut) currentTab = 0;

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

        composer.BeginChildElements(bgBounds);
        switch (currentTab)
        {
            case 0: ComposeBuilding(composer); break;
            case 1: ComposeOrders(composer); break;
            case 2: ComposeItems(composer); break;
        }
        SingleComposer = composer.EndChildElements().Compose();
        SingleComposer.GetSwitch("anytype")?.SetValue(snapshot.AnyWoodOrStone);
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
            double height = b.Type is BuildingTypes.Forester or BuildingTypes.Farmer ? 150 : 50;
            composer.AddRichtext(snapshot.Info, CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, height));
            y += height + 5;
        }

        y = AddOrderButtons(composer, b, y) + 10;
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
