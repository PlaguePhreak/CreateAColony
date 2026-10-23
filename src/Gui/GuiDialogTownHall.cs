using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using CreateAColony.Network;
using CreateAColony.Quests;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Gui;

/// <summary>
/// The Town Hall window, laid out like MineColonies' with tabs down the left: Actions (name, level,
/// upgrade, land), Town map, Information (event log and work orders), Citizens, Statistics, Permissions and
/// Settings. Every change goes to the server, which answers with a fresh snapshot; the window never
/// changes colony data on its own.
/// </summary>
public class GuiDialogTownHall : GuiDialog
{
    const int RowsPerPage = 8;
    const double RowHeight = 34;
    const double Width = 560;
    const int EventsPerPage = 10;
    const double EventRowHeight = 26;
    const int OrdersPerPage = 5;
    const double OrderRowHeight = 62;

    const int ActionsTab = 0;
    const int InfoTab = 1;
    const int CitizensTab = 2;
    const int StatsTab = 3;
    const int PermissionsTab = 4;
    const int SettingsTab = 5;
    const int MapTab = 6;
    const int BannersTab = 7;
    const int ProspectingTab = 8;
    const int OreRowsPerPage = 7;
    const double OreRowHeight = 44;
    const int QuestsTab = 9;
    const double MapHeight = 380;
    // The map is at most this many pixels a side; bigger colonies get one pixel per few blocks.
    const int MapMaxSide = 320;
    const int MapMargin = 16;
    // Opened with the ? button on the Actions tab, with a Back button.
    const int HelpView = 20;

    static readonly (int Id, string Lang)[] Tabs =
    {
        (ProspectingTab, "tab-prospecting"), (ActionsTab, "tab-actions"), (QuestsTab, "tab-quests"), (MapTab, "tab-townmap"), (BannersTab, "tab-banners"), (InfoTab, "tab-information"), (CitizensTab, "tab-citizens"),
        (StatsTab, "tab-statistics"), (PermissionsTab, "tab-permissions"), (SettingsTab, "tab-settings")
    };

    readonly ColonyNetwork network;
    ColonySnapshot snapshot;
    int currentTab;
    int page;
    bool renaming;
    string pendingName;
    // Which page of the Information tab (0 events, 1 work orders) and of the Statistics tab (0 colony, 1 work done).
    int infoPage;
    int statsPage;
    int statPeriod = 1;
    // Which page of the Citizens tab: 0 the living (and wanderers), 1 the dead.
    int citizensPage;
    // The Town map's ground, kept until the player asks for a fresh one or the colony's land changes.
    ColonyMapTerrain? terrain;
    // Where the Town map is looking and how far zoomed in, kept while the window is open.
    readonly ColonyMapView mapView = new();
    GuiElementColonyMap? mapElement;

    public string ColonyId => snapshot.ColonyId;

    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogTownHall(ICoreClientAPI capi, ColonyNetwork network, ColonySnapshot snapshot) : base(capi)
    {
        this.network = network;
        this.snapshot = snapshot;
        pendingName = snapshot.Name;
        Compose();
    }

    public void Update(ColonySnapshot fresh)
    {
        // Keep a half-typed name unless the name really changed.
        if (fresh.Name != snapshot.Name && !renaming) pendingName = fresh.Name;
        snapshot = fresh;
        // On the Town map, timed refreshes only move the dots, so a zoom or a drag in progress is kept.
        if (fresh.Periodic && currentTab == MapTab && mapElement != null && terrain != null
            && MapBox(fresh) is { } box && terrain.Covers(box.MinX, box.MinZ, box.MaxX, box.MaxZ, MapMaxSide))
        {
            mapElement.SetSnapshot(fresh, capi.World.Player?.Entity?.Pos.XYZ);
            return;
        }
        // Timed progress refreshes would close an open dropdown or take the focus from the name box.
        if (fresh.Periodic && (renaming || currentTab is CitizensTab or PermissionsTab)) return;
        Compose();
    }

    ColonyRole ViewerRole => (ColonyRole)snapshot.ViewerRole;
    bool Officer => ViewerRole >= ColonyRole.Officer;

    void Compose()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        int activeTab = currentTab == HelpView ? ActionsTab : currentTab;
        var tabs = Tabs.Select(t => new GuiTab { Name = Lang.Get("createacolony:" + t.Lang), DataInt = t.Id, Active = t.Id == activeTab }).ToArray();

        mapElement = null;
        var composer = capi.Gui.CreateCompo("createacolony-townhall", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(snapshot.Name, () => TryClose())
            .AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, tabs.Length * 30), (id, tab) => { currentTab = tab.DataInt; page = 0; renaming = false; Compose(); }, "tabs")
            .BeginChildElements(bgBounds);

        switch (currentTab)
        {
            case ActionsTab: ComposeActions(composer); break;
            case MapTab: ComposeMap(composer); break;
            case ProspectingTab: ComposeProspecting(composer); break;
            case BannersTab: ComposeBanners(composer); break;
            case QuestsTab: ComposeQuests(composer); break;
            case InfoTab: ComposeInformation(composer); break;
            case CitizensTab: ComposeCitizens(composer); break;
            case StatsTab: ComposeStatistics(composer); break;
            case PermissionsTab: ComposePermissions(composer); break;
            case SettingsTab: ComposeSettings(composer); break;
            case HelpView: ComposeHelp(composer); break;
        }

        SingleComposer = composer.EndChildElements().Compose();
        SingleComposer.GetTextInput("colonyname")?.SetValue(pendingName);
        SingleComposer.GetSwitch("permadeath")?.SetValue(snapshot.Permadeath);
        SingleComposer.GetSwitch("banneronnew")?.SetValue(snapshot.BannerOnNew);
    }

    /// <summary>A row of page buttons along the top of a tab, the chosen one marked. Returns the y below them.</summary>
    double PageButtons(GuiComposer composer, string[] langKeys, int chosen, Action<int> choose, double y, double buttonWidth)
    {
        double x = 0;
        for (int i = 0; i < langKeys.Length; i++)
        {
            int index = i;
            string label = (i == chosen ? "• " : "") + Lang.Get("createacolony:" + langKeys[i]);
            composer.AddSmallButton(label, () => { choose(index); page = 0; Compose(); return true; }, ElementBounds.Fixed(x, y, buttonWidth, 28),
                i == chosen ? EnumButtonStyle.Normal : EnumButtonStyle.Small);
            x += buttonWidth + 8;
        }
        return y + 40;
    }

    /// <summary>A greyed "Coming later: ..." line for the MineColonies features this mod has no system for yet.</summary>
    double ComingLater(GuiComposer composer, string langKey, double y)
    {
        return composer.Text(Lang.Get("createacolony:townhall-coming-later", Lang.Get("createacolony:" + langKey)), GuiKit.Muted(), 0, y, Width, 6);
    }

    // ---- Prospecting tab ------------------------------------------------------------------

    /// <summary>
    /// The Prospecting tab: every ore the colony's prospectors found, strongest first, with the best
    /// reading of each and where it is from the Town Hall. The readings themselves are on the Colony
    /// prospecting layer of the world map.
    /// </summary>
    void ComposeProspecting(GuiComposer composer)
    {
        double y = composer.Text(Lang.Get("createacolony:townhall-prospecting-hint", snapshot.OreReadings, snapshot.ProspectorHuts), GuiKit.Muted(), 0, 30, Width, 10);
        if (snapshot.Ores.Count == 0)
        {
            composer.Text(Lang.Get(snapshot.ProspectorHuts == 0 ? "createacolony:townhall-prospecting-nohut" : "createacolony:ore-none"), GuiKit.Body(), 0, y, Width);
            return;
        }
        int pages = (snapshot.Ores.Count + OreRowsPerPage - 1) / OreRowsPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        int shown = Math.Min(OreRowsPerPage, snapshot.Ores.Count - page * OreRowsPerPage);
        composer.AddInset(ElementBounds.Fixed(0, y, Width, shown * OreRowHeight + 8), 3, 0.8f);
        y += 4;
        foreach (var ore in snapshot.Ores.Skip(page * OreRowsPerPage).Take(OreRowsPerPage))
        {
            string density = Lang.Get("propick-density-" + new[] { "verypoor", "poor", "decent", "high", "veryhigh", "ultrahigh" }[(int)GameMath.Clamp(ore.Best * 7.5, 0, 5)]);
            composer.AddStaticText(Lang.Get("ore-" + ore.Code), CairoFont.WhiteSmallText(), ElementBounds.Fixed(10, y + 4, Width - 20, 20));
            string detail = Lang.Get("createacolony:ore-row", density, ore.Where, ore.Sites)
                + (ore.MinedOut > 0 ? "  " + Lang.Get("createacolony:ore-row-minedout", ore.MinedOut) : "");
            composer.AddStaticText(detail, GuiKit.Detail().WithColor(ore.MinedOut >= ore.Sites ? GuiKit.MutedColor : GuiKit.GoodColor), ElementBounds.Fixed(10, y + 22, Width - 20, 20));
            y += OreRowHeight;
        }
        composer.Pager(page, pages, y + 14, Width, p => { page = p; Compose(); });
    }

    // ---- Actions tab ----------------------------------------------------------------------

    /// <summary>
    /// The Actions tab, like a hut's Main tab: the colony's name and Town Hall level with a pencil to
    /// rename, the colony's numbers, the Town Hall's build options, its land, and the ? guide.
    /// </summary>
    void ComposeActions(GuiComposer composer)
    {
        var hall = snapshot.Buildings.FirstOrDefault();
        double y = 30;

        // Header: name and level, and the pencil.
        double headerHeight = hall?.TargetLevel > 0 ? 84 : 60;
        composer.AddInset(ElementBounds.Fixed(0, y, Width, headerHeight), 3, 0.8f);
        if (renaming)
        {
            composer
                .AddTextInput(ElementBounds.Fixed(10, y + 8, Width - 230, 28), text => pendingName = text, GuiKit.Body(), "colonyname")
                .AddSmallButton(Lang.Get("createacolony:save"), OnRename, ElementBounds.Fixed(Width - 212, y + 8, 100, 28))
                .AddSmallButton(Lang.Get("createacolony:cancel"), () => { renaming = false; pendingName = snapshot.Name; Compose(); return true; }, ElementBounds.Fixed(Width - 106, y + 8, 96, 28));
        }
        else
        {
            composer.AddStaticText(snapshot.Name, GuiKit.Heading(), ElementBounds.Fixed(10, y + 8, Width - 60, 26));
            if (Officer)
            {
                composer.AddIconButton("paintbrush", _ => { renaming = true; pendingName = snapshot.Name; Compose(); }, ElementBounds.Fixed(Width - 42, y + 6, 30, 30), "rename");
                composer.AddHoverText(Lang.Get("createacolony:townhall-rename"), GuiKit.Detail(), 200, ElementBounds.Fixed(Width - 42, y + 6, 30, 30));
            }
        }
        if (hall != null)
        {
            composer
                .AddStaticText(Lang.Get("createacolony:building-townhall") + ": " + GuiDialogHut.LevelText(hall), GuiKit.Detail(), ElementBounds.Fixed(10, y + 36, Width / 2, 20))
                .AddStaticText(Lang.Get("createacolony:placement-style", Lang.Get("createacolony:style-" + BlueprintStyles.Normalize(hall.Style))),
                    GuiKit.Detail().WithOrientation(EnumTextOrientation.Right), ElementBounds.Fixed(Width / 2, y + 36, Width / 2 - 10, 20));
            if (hall.TargetLevel > 0)
            {
                composer.AddStaticText(GuiDialogHut.StatusText(hall), GuiKit.Detail().WithColor(GuiStyle.ActiveButtonTextColor), ElementBounds.Fixed(10, y + 58, Width - 20, 22));
            }
        }
        y += headerHeight + 8;

        // The colony's numbers in two columns on a sunken panel.
        int alive = snapshot.Citizens.Count(c => c.State != "dead" && !c.Visitor);
        int hungry = snapshot.Citizens.Count(c => c.State == "hungry");
        const double pad = 10;
        double column = (Width - 3 * pad) / 2;
        composer.AddInset(ElementBounds.Fixed(0, y, Width, 3 * 24 + 2 * pad), 3, 0.8f);
        double left = y + pad, right = y + pad;
        left = composer.Row(Lang.Get("createacolony:stat-level"), snapshot.Level.ToString(), pad, left, column);
        string settlers = snapshot.PopulationCap > 0 ? Lang.Get("createacolony:townhall-of", alive, snapshot.PopulationCap) : alive.ToString();
        left = composer.Row(Lang.Get("createacolony:stat-settlers"), settlers, pad, left, column, snapshot.PopulationCap > 0 && alive >= snapshot.PopulationCap ? GuiKit.WarnColor : null);
        composer.Row(Lang.Get("createacolony:stat-hungry"), hungry.ToString(), pad, left, column, hungry > 0 ? GuiKit.WarnColor : null);
        right = composer.Row(Lang.Get("createacolony:stat-beds"), snapshot.BedCount.ToString(), 2 * pad + column, right, column);
        right = composer.Row(Lang.Get("createacolony:stat-members"), snapshot.Members.Count.ToString(), 2 * pad + column, right, column);
        if (snapshot.AverageHappiness >= 0)
        {
            string mood = Lang.Get("createacolony:mood-value", snapshot.AverageHappiness, Lang.Get("createacolony:mood-" + MoodWord(snapshot.AverageHappiness)));
            composer.Row(Lang.Get("createacolony:stat-happiness"), mood, 2 * pad + column, right, column, snapshot.AverageHappiness < 30 ? GuiKit.WarnColor : null);
        }
        y += 3 * 24 + 2 * pad + 10;

        int visitors = snapshot.Citizens.Count(c => c.Visitor);
        if (visitors > 0) y = composer.Text(Lang.Get("createacolony:townhall-visitors", visitors), GuiKit.Body().WithColor(GuiKit.WarnColor), 0, y, Width, 8);

        // Build options for the Town Hall itself; other buildings are ordered from their own huts.
        if (hall != null && Officer && (hall.TargetLevel > 0 || hall.Level < hall.MaxLevel || hall.Level == 0))
        {
            y = composer.Section(Lang.Get("createacolony:hut-build-options"), y, Width);
            y = AddBuildButtons(composer, hall, y);
        }
        y = composer.Text(Lang.Get("createacolony:townhall-builder-hint"), GuiKit.Muted(), 0, y, Width, 10);

        // The colony's land, as MineColonies shows its protected area.
        y = composer.Section(Lang.Get("createacolony:townhall-land"), y, Width);
        y = composer.Text(Lang.Get("createacolony:overview-claim", snapshot.ClaimColumns.ToString("N0"), snapshot.ClaimSquares, snapshot.TownHallClaimSize),
            GuiKit.Detail(), 0, y, Width, 10);

        if (!Officer) y = composer.Text(Lang.Get("createacolony:visitor-readonly"), GuiKit.Muted(), 0, y, Width, 10);

        // Footer: the guide.
        composer.AddSmallButton("?", () => Open(HelpView), ElementBounds.Fixed(0, y, 40, 28));
        composer.AddHoverText(Lang.Get("createacolony:townhall-help-hover"), GuiKit.Detail(), 220, ElementBounds.Fixed(0, y, 40, 28));
        y += 38;
        ComingLater(composer, "townhall-later-actions", y);
    }

    /// <summary>Upgrade or Cancel, and Turn until the first level is built. Returns the y below them.</summary>
    double AddBuildButtons(GuiComposer composer, BuildingView b, double y)
    {
        int id = b.BuildingId;
        if (b.TargetLevel > 0)
        {
            composer.AddSmallButton(Lang.Get("createacolony:build-cancel"), () => Send(ColonyAction.CancelBuild, id), ElementBounds.Fixed(0, y, 160, 28));
            return y + 36;
        }
        double x = 0;
        if (b.Level < b.MaxLevel)
        {
            string label = Lang.Get(b.Level == 0 ? "createacolony:build-start" : "createacolony:build-upgrade", b.Level + 1);
            composer.AddSmallButton(label, () => Send(ColonyAction.Build, id), ElementBounds.Fixed(0, y, 160, 28));
            x = 170;
        }
        if (b.Level == 0)
        {
            // Like a hut, the Town Hall can be turned until its first level is built.
            composer.AddSmallButton(Lang.Get("createacolony:build-rotate", b.Rotation), () => Send(ColonyAction.Rotate, id), ElementBounds.Fixed(x, y, 190, 28));
        }
        return y + 36;
    }

    // ---- Quests tab -----------------------------------------------------------------------

    /// <summary>
    /// The colony's quests at a glance: how far each colony chapter is, the quests ready to claim, and
    /// a button that opens the quest book itself.
    /// </summary>
    void ComposeQuests(GuiComposer composer)
    {
        double y = 30;
        var library = network.ClientQuests;
        var states = snapshot.Quests.ToDictionary(s => s.Code, s => s);
        QuestStatus StatusOf(QuestDef q) => states.TryGetValue(q.Code, out var s) ? (QuestStatus)s.Status : QuestStatus.Locked;

        composer.AddSmallButton(Lang.Get("createacolony:questbook-open"), () => { network.RequestQuestBook(); return true; }, ElementBounds.Fixed(Width - 180, y - 2, 180, 28));
        y = composer.Text(Lang.Get("createacolony:townhall-quests-intro"), GuiKit.Detail(), 0, y + 4, Width - 190, 10);

        // Quests ready to claim, so officers see them without opening the book.
        var ready = library.All.Where(q => q.PerColony && StatusOf(q) == QuestStatus.Complete).Take(5).ToList();
        if (ready.Count > 0)
        {
            y = composer.Section(Lang.Get("createacolony:townhall-quests-ready"), y, Width);
            foreach (var q in ready)
            {
                y = composer.Text("• " + Lang.Get(QuestLibrary.TitleKey(q)) + " — " + Lang.Get("createacolony:chapter-" + q.Chapter.Code), GuiKit.Body().WithColor(new[] { 1.0, 0.82, 0.25, 1.0 }), 10, y, Width - 10, 2);
            }
            y += 8;
        }

        // Each colony chapter as a row: name, done / total, and a bar.
        foreach (var group in library.Groups)
        {
            var chapters = group.Chapters.Where(c => c.PerColony).ToList();
            if (chapters.Count == 0) continue;
            y = composer.Section(Lang.Get("createacolony:questgroup-" + group.Code), y, Width);
            foreach (var chapter in chapters)
            {
                int total = chapter.Quests.Count(q => !q.Secret || StatusOf(q) != QuestStatus.Locked);
                int done = chapter.Quests.Count(q => StatusOf(q) == QuestStatus.Claimed);
                var font = done > 0 && done == total ? GuiKit.Body().WithColor(GuiKit.GoodColor) : GuiKit.Body();
                composer
                    .AddStaticText(Lang.Get("createacolony:chapter-" + chapter.Code), font, ElementBounds.Fixed(10, y, Width * 0.5, 22))
                    .AddStaticText(total == 0 ? Lang.Get("createacolony:townhall-quests-later") : done + " / " + total,
                        GuiKit.Detail().WithOrientation(EnumTextOrientation.Right), ElementBounds.Fixed(Width * 0.5, y + 1, Width * 0.5 - 10, 22));
                y += 24;
            }
            y += 4;
        }
    }

    void ComposeHelp(GuiComposer composer)
    {
        composer
            .AddStaticText(Lang.Get("createacolony:help-title", Lang.Get("createacolony:building-townhall")), GuiKit.Heading(), ElementBounds.Fixed(0, 30, Width - 110, 26))
            .AddSmallButton(Lang.Get("createacolony:back"), () => Open(ActionsTab), ElementBounds.Fixed(Width - 100, 28, 100, 28));
        composer.Rich(Lang.Get("createacolony:guide-townhall"), GuiKit.Detail(), 0, 64, Width);
    }

    bool Open(int view)
    {
        currentTab = view;
        page = 0;
        renaming = false;
        Compose();
        return true;
    }

    // ---- Town map tab ---------------------------------------------------------------------

    /// <summary>The area the Town map shows: the colony's land and buildings plus a margin, or null if it has neither.</summary>
    static (int MinX, int MinZ, int MaxX, int MaxZ)? MapBox(ColonySnapshot snapshot)
    {
        var xs = new List<int>();
        var zs = new List<int>();
        for (int r = 0; r + 3 < snapshot.ClaimRects.Count; r += 4)
        {
            xs.Add(snapshot.ClaimRects[r]); xs.Add(snapshot.ClaimRects[r + 2]);
            zs.Add(snapshot.ClaimRects[r + 1]); zs.Add(snapshot.ClaimRects[r + 3]);
        }
        foreach (var b in snapshot.MapBuildings)
        {
            xs.Add(b.X1); xs.Add(b.X2);
            zs.Add(b.Z1); zs.Add(b.Z2);
        }
        if (xs.Count == 0) return null;
        return (xs.Min() - MapMargin, zs.Min() - MapMargin, xs.Max() + MapMargin, zs.Max() + MapMargin);
    }

    /// <summary>
    /// The Town map: the colony's land and buildings from above, with the settlers and the player.
    /// The mouse wheel or the + and − buttons zoom, dragging moves the map, Fit shows it all again, and
    /// pointing at a building names it. The ground is drawn from what this client has loaded.
    /// </summary>
    void ComposeMap(GuiComposer composer)
    {
        double y = 30;
        if (MapBox(snapshot) is not { } box)
        {
            composer.Text(Lang.Get("createacolony:townmap-empty"), GuiKit.Body(), 0, y, Width);
            return;
        }
        if (terrain == null || !terrain.Covers(box.MinX, box.MinZ, box.MaxX, box.MaxZ, MapMaxSide))
        {
            terrain = ColonyMapTerrain.Build(capi, box.MinX, box.MinZ, box.MaxX, box.MaxZ, MapMaxSide);
        }

        var mapBounds = ElementBounds.Fixed(0, y, Width, MapHeight);
        mapElement = new GuiElementColonyMap(capi, mapBounds, terrain, snapshot, capi.World.Player?.Entity?.Pos.XYZ, mapView);
        composer
            .AddInset(mapBounds.FlatCopy(), 3, 0.8f)
            .AddInteractiveElement(mapElement, "townmap");
        y += MapHeight + 8;

        // Zoom buttons on the left, Redraw on the right, the key between them.
        composer
            .AddSmallButton(Lang.Get("createacolony:townmap-zoomout"), () => { mapElement?.ZoomBy(1 / 1.5); return true; }, ElementBounds.Fixed(0, y, 34, 28))
            .AddSmallButton(Lang.Get("createacolony:townmap-zoomin"), () => { mapElement?.ZoomBy(1.5); return true; }, ElementBounds.Fixed(40, y, 34, 28))
            .AddSmallButton(Lang.Get("createacolony:townmap-fit"), () => { mapElement?.Fit(); return true; }, ElementBounds.Fixed(80, y, 50, 28))
            .AddSmallButton(Lang.Get("createacolony:townmap-redraw"), () => { terrain = null; Compose(); return true; }, ElementBounds.Fixed(Width - 150, y, 150, 28));
        y += 36;
        y = composer.Text(Lang.Get("createacolony:townmap-legend"), GuiKit.Detail(), 0, y, Width, 6);
        composer.Text(Lang.Get("createacolony:townmap-hint", terrain.Step), GuiKit.Muted(), 0, y, Width);
    }

    // ---- Banners tab ----------------------------------------------------------------------

    /// <summary>
    /// The colony banner (a Heraldry: Banners banner): set it from the player's hand, copy its design
    /// onto the banners of the Town Hall, the Guard Towers or every building, and put it on new buildings.
    /// </summary>
    void ComposeBanners(GuiComposer composer)
    {
        double y = 30;
        if (!snapshot.HeraldryInstalled)
        {
            y = composer.Text(Lang.Get("createacolony:banner-needs-mod"), GuiKit.Body().WithColor(GuiKit.WarnColor), 0, y, Width, 12);
        }

        y = composer.Section(Lang.Get("createacolony:banner-colony"), y, Width);
        ItemStack? banner = null;
        if (snapshot.BannerStack != null)
        {
            banner = new ItemStack(snapshot.BannerStack);
            if (!banner.ResolveBlockOrItem(capi.World)) banner = null;
        }
        composer.AddInset(ElementBounds.Fixed(0, y, Width, 76), 3, 0.8f);
        if (banner != null)
        {
            composer.AddRichtext(new RichTextComponentBase[]
            {
                new ItemstackTextComponent(capi, banner, 64, 10),
                new RichTextComponent(capi, banner.GetName(), GuiKit.Body()) { VerticalAlign = EnumVerticalAlign.Middle }
            }, ElementBounds.Fixed(6, y + 4, Width - 200, 70));
        }
        else
        {
            composer.AddStaticText(Lang.Get("createacolony:banner-none-yet"), GuiKit.Detail(), ElementBounds.Fixed(10, y + 28, Width - 200, 22));
        }
        if (Officer)
        {
            composer.AddSmallButton(Lang.Get("createacolony:banner-use-held"), () => SendText(ColonyAction.SetColonyBanner, ""), ElementBounds.Fixed(Width - 180, y + 24, 170, 28));
        }
        y += 84;
        y = composer.Text(Lang.Get("createacolony:banner-hint"), GuiKit.Muted(), 0, y, Width, 12);

        if (!Officer || banner == null) return;

        y = composer.Section(Lang.Get("createacolony:banner-replace"), y, Width);
        composer
            .AddSmallButton(Lang.Get("createacolony:banner-on-townhall"), () => SendText(ColonyAction.ReplaceBanners, "townhall"), ElementBounds.Fixed(0, y, 180, 28))
            .AddSmallButton(Lang.Get("createacolony:banner-on-towers"), () => SendText(ColonyAction.ReplaceBanners, "guardtower"), ElementBounds.Fixed(190, y, 180, 28))
            .AddSmallButton(Lang.Get("createacolony:banner-on-all"), () => SendText(ColonyAction.ReplaceBanners, "all"), ElementBounds.Fixed(380, y, 180, 28));
        y += 36;
        y = composer.Text(Lang.Get("createacolony:banner-replace-hint"), GuiKit.Muted(), 0, y, Width, 12);

        composer
            .AddSwitch(on => SendText(ColonyAction.SetBannerOnNew, on ? "1" : "0"), ElementBounds.Fixed(0, y, 30, 30), "banneronnew")
            .AddStaticText(Lang.Get("createacolony:banner-on-new"), GuiKit.Body(), ElementBounds.Fixed(40, y + 5, Width - 40, 25));
    }

    bool SendText(ColonyAction action, string text)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = action, Text = text });
        return true;
    }

    // ---- Information tab ------------------------------------------------------------------

    void ComposeInformation(GuiComposer composer)
    {
        double y = PageButtons(composer, new[] { "townhall-events", "tab-orders" }, infoPage, p => infoPage = p, 30, 150);
        if (infoPage == 0) ComposeEvents(composer, y);
        else ComposeWorkOrders(composer, y);
    }

    /// <summary>The event log, newest first: settlers joining, leaving, dying, and buildings placed and finished.</summary>
    void ComposeEvents(GuiComposer composer, double y)
    {
        var events = Enumerable.Reverse(snapshot.Events).ToList();
        if (events.Count == 0)
        {
            composer.Text(Lang.Get("createacolony:townhall-no-events"), GuiKit.Body(), 0, y, Width);
            return;
        }
        int pages = (events.Count + EventsPerPage - 1) / EventsPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        int shown = Math.Min(EventsPerPage, events.Count - page * EventsPerPage);
        composer.AddInset(ElementBounds.Fixed(0, y, Width, shown * EventRowHeight + 8), 3, 0.8f);
        y += 4;
        foreach (var e in events.Skip(page * EventsPerPage).Take(EventsPerPage))
        {
            composer
                .AddStaticText(When(e.TotalDays), GuiKit.Muted(), ElementBounds.Fixed(10, y + 5, 140, 20))
                .AddStaticText(EventText(e), GuiKit.Detail(), ElementBounds.Fixed(155, y + 4, Width - 165, 20));
            y += EventRowHeight;
        }
        composer.Pager(page, pages, y + 12, Width, p => { page = p; Compose(); });
    }

    /// <summary>A moment in in-game days as the game's own calendar shows it: "1 May, 14:00", or "1 May, Year 2" in another year.</summary>
    string When(double totalDays)
    {
        var calendar = capi.World.Calendar;
        int daysPerMonth = Math.Max(1, calendar.DaysPerMonth);
        int daysPerYear = Math.Max(1, calendar.DaysPerYear);
        int year = (int)(totalDays / daysPerYear);
        int dayOfYear = (int)(totalDays - (double)year * daysPerYear);
        int month = Math.Clamp(dayOfYear / daysPerMonth, 0, 11);
        int day = dayOfYear % daysPerMonth + 1;
        string monthName = Lang.Get("month-" + (EnumMonth)(month + 1));
        if (year != (int)(calendar.TotalDays / daysPerYear)) return Lang.Get("createacolony:townhall-event-date-year", day, monthName, year);
        double hours = (totalDays - Math.Floor(totalDays)) * calendar.HoursPerDay;
        int hour = (int)hours;
        int minute = (int)((hours - hour) * 60);
        return Lang.Get("createacolony:townhall-event-time", day, monthName, hour.ToString("00"), minute.ToString("00"));
    }

    static string EventText(EventView e)
    {
        var args = e.Args.Select(a => a.StartsWith('@') ? Lang.Get("createacolony:" + a[1..]) : a).Cast<object>().ToArray();
        return Lang.Get("createacolony:" + e.Key, args);
    }

    /// <summary>Every building waiting to be built or upgraded, in the order builders take them, with Cancel for officers.</summary>
    void ComposeWorkOrders(GuiComposer composer, double y)
    {
        var orders = snapshot.WorkOrders;
        if (orders.Count == 0)
        {
            y = composer.Text(Lang.Get("createacolony:hut-no-orders"), GuiKit.Body(), 0, y, Width, 10);
            ComingLater(composer, "townhall-later-orders", y);
            return;
        }
        int pages = (orders.Count + OrdersPerPage - 1) / OrdersPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        int shown = Math.Min(OrdersPerPage, orders.Count - page * OrdersPerPage);
        composer.AddInset(ElementBounds.Fixed(0, y, Width, shown * OrderRowHeight + 8), 3, 0.8f);
        y += 4;
        int number = page * OrdersPerPage;
        foreach (var o in orders.Skip(page * OrdersPerPage).Take(OrdersPerPage))
        {
            number++;
            string title = number + ". " + o.Name + " " + Lang.Get("createacolony:order-level", o.TargetLevel);
            composer
                .AddStaticText(title, GuiKit.Body(), ElementBounds.Fixed(10, y + 6, Width - 150, 25))
                .AddStaticText(GuiDialogHut.StatusText(o), GuiKit.Detail().WithColor(GuiStyle.ActiveButtonTextColor), ElementBounds.Fixed(10, y + 30, Width - 150, 22));
            if (Officer)
            {
                int id = o.BuildingId;
                composer.AddSmallButton(Lang.Get("createacolony:build-cancel"), () => Send(ColonyAction.CancelBuild, id), ElementBounds.Fixed(Width - 130, y + 14, 120, 28));
            }
            y += OrderRowHeight;
        }
        composer.Pager(page, pages, y + 12, Width, p => { page = p; Compose(); });
        y += pages > 1 ? 52 : 16;
        y = composer.Text(Lang.Get("createacolony:townhall-orders-hint"), GuiKit.Muted(), 0, y, Width, 6);
        ComingLater(composer, "townhall-later-orders", y);
    }

    // ---- Citizens tab ---------------------------------------------------------------------

    /// <summary>The Citizens tab: Current Citizens (the living and wanderers) and Dead Citizens pages.</summary>
    void ComposeCitizens(GuiComposer composer)
    {
        int living = snapshot.Citizens.Count(c => c.State != "dead" && !c.Visitor), dead = snapshot.Citizens.Count(c => c.State == "dead");
        var labels = new[] { "citizens-current", "citizens-dead" };
        double top = 30;
        double x = 0;
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            string label = (i == citizensPage ? "• " : "") + Lang.Get("createacolony:" + labels[i], i == 0 ? living : dead);
            composer.AddSmallButton(label, () => { citizensPage = index; page = 0; Compose(); return true; }, ElementBounds.Fixed(x, top, 200, 28),
                i == citizensPage ? EnumButtonStyle.Normal : EnumButtonStyle.Small);
            x += 208;
        }
        top += 40;
        if (citizensPage == 1) ComposeDeadCitizens(composer, top);
        else ComposeCurrentCitizens(composer, top);
    }

    /// <summary>The dead: name, the job they had, when they died, and whether they have been buried.</summary>
    void ComposeDeadCitizens(GuiComposer composer, double top)
    {
        var dead = snapshot.Citizens.Where(c => c.State == "dead").OrderByDescending(c => c.DiedTotalDays).ThenBy(c => c.Name).ToList();
        double y;
        if (dead.Count == 0)
        {
            y = composer.Text(Lang.Get("createacolony:citizens-no-dead"), GuiKit.Body(), 0, top, Width);
        }
        else
        {
            composer
                .AddStaticText(Lang.Get("createacolony:col-name"), GuiKit.Muted(), ElementBounds.Fixed(10, top, 150, 20))
                .AddStaticText(Lang.Get("createacolony:col-job"), GuiKit.Muted(), ElementBounds.Fixed(165, top, 130, 20))
                .AddStaticText(Lang.Get("createacolony:col-died"), GuiKit.Muted(), ElementBounds.Fixed(300, top, 130, 20))
                .AddStaticText(Lang.Get("createacolony:col-burial"), GuiKit.Muted(), ElementBounds.Fixed(435, top, 125, 20));

            int pages = (dead.Count + RowsPerPage - 1) / RowsPerPage;
            page = Math.Clamp(page, 0, pages - 1);
            int shown = Math.Min(RowsPerPage, dead.Count - page * RowsPerPage);
            composer.AddInset(ElementBounds.Fixed(0, top + 22, Width, shown * RowHeight + 6), 3, 0.8f);
            y = top + 25;
            foreach (var c in dead.Skip(page * RowsPerPage).Take(RowsPerPage))
            {
                string died = c.DiedTotalDays > 0 ? When(c.DiedTotalDays) : Lang.Get("createacolony:citizens-died-unknown");
                var burialFont = c.Buried ? GuiKit.Detail().WithColor(GuiKit.GoodColor) : GuiKit.Detail().WithColor(GuiKit.WarnColor);
                composer
                    .AddStaticText(c.Name, GuiKit.Body().WithColor(GuiKit.MutedColor), ElementBounds.Fixed(10, y + 6, 150, 25))
                    .AddStaticText(c.Child ? Lang.Get("createacolony:citizens-child") : JobName(c.Job), GuiKit.Detail(), ElementBounds.Fixed(165, y + 8, 130, 25))
                    .AddStaticText(died, GuiKit.Detail(), ElementBounds.Fixed(300, y + 8, 130, 25))
                    .AddStaticText(Lang.Get(c.Buried ? "createacolony:citizens-buried" : "createacolony:citizens-not-buried"), burialFont, ElementBounds.Fixed(435, y + 8, 125, 25));
                y += RowHeight;
            }
            composer.Pager(page, pages, y + 14, Width, p => { page = p; Compose(); });
            y += pages > 1 ? 54 : 16;
        }
        ComingLater(composer, "townhall-later-burial", y);
    }

    /// <summary>The living, with their state, food and bed, and (for officers) their job and Recall.</summary>
    void ComposeCurrentCitizens(GuiComposer composer, double top)
    {
        // Wanderers asking to join come first, so they are not missed.
        var citizens = snapshot.Citizens.Where(c => c.State != "dead").OrderByDescending(c => c.Visitor).ThenBy(c => c.Name).ToList();
        if (citizens.Count == 0)
        {
            double empty = composer.Text(Lang.Get("createacolony:no-citizens"), GuiKit.Body(), 0, top, Width);
            ComingLater(composer, "townhall-later-citizens", empty);
            return;
        }

        composer
            .AddStaticText(Lang.Get("createacolony:col-name"), GuiKit.Muted(), ElementBounds.Fixed(10, top, 150, 20))
            .AddStaticText(Lang.Get("createacolony:col-status"), GuiKit.Muted(), ElementBounds.Fixed(165, top, 170, 20))
            .AddStaticText(Lang.Get("createacolony:col-job"), GuiKit.Muted(), ElementBounds.Fixed(340, top, 150, 20));

        string[] jobCodes = Jobs.All;
        string[] jobNames = jobCodes.Select(JobName).ToArray();

        int pages = (citizens.Count + RowsPerPage - 1) / RowsPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        int shown = Math.Min(RowsPerPage, citizens.Count - page * RowsPerPage);
        composer.AddInset(ElementBounds.Fixed(0, top + 22, Width, shown * RowHeight + 6), 3, 0.8f);
        double y = top + 25;
        foreach (var c in citizens.Skip(page * RowsPerPage).Take(RowsPerPage))
        {
            if (c.Visitor)
            {
                ComposeVisitor(composer, c, y);
                y += RowHeight;
                continue;
            }
            string status = Lang.Get("createacolony:settler-status", Lang.Get("createacolony:state-" + c.State), c.FoodPercent,
                Lang.Get(c.HasBed ? "createacolony:has-bed" : "createacolony:no-bed"));
            composer
                .AddStaticText(c.Name, c.State == "dead" ? GuiKit.Body().WithColor(GuiKit.MutedColor) : GuiKit.Body(), ElementBounds.Fixed(10, y + 6, 150, 25))
                .AddStaticText(status, c.State == "hungry" ? GuiKit.Detail().WithColor(GuiKit.WarnColor) : GuiKit.Detail(), ElementBounds.Fixed(165, y + 8, 172, 25));
            if (c.TraitsText.Length > 0) composer.AddHoverText(Lang.Get("createacolony:traits-hover", c.TraitsText), GuiKit.Detail(), 260, ElementBounds.Fixed(10, y + 6, 150, 25));

            if (c.Child && c.State != "dead")
            {
                composer.AddStaticText(Lang.Get("createacolony:child-grows-up", c.GrowsUpHours), GuiKit.Detail(), ElementBounds.Fixed(340, y + 8, 145, 25));
                if (Officer && c.State != "downed")
                {
                    int childId = c.CitizenId;
                    composer.AddSmallButton(Lang.Get("createacolony:townhall-recall"), () => SendCitizen(ColonyAction.RecallCitizen, childId), ElementBounds.Fixed(490, y + 2, 70, 28));
                }
            }
            else if (Officer && c.State != "dead")
            {
                int citizenId = c.CitizenId;
                int selected = Math.Max(0, Array.IndexOf(jobCodes, c.Job));
                composer.AddDropDown(jobCodes, jobNames, selected, (code, on) => OnSetJob(citizenId, code),
                    ElementBounds.Fixed(340, y + 2, 145, 28), "job-" + citizenId);
                if (c.State != "downed")
                {
                    composer.AddSmallButton(Lang.Get("createacolony:townhall-recall"), () => SendCitizen(ColonyAction.RecallCitizen, citizenId), ElementBounds.Fixed(490, y + 2, 70, 28));
                }
            }
            else
            {
                composer.AddStaticText(JobName(c.Job), GuiKit.Body(), ElementBounds.Fixed(340, y + 6, 180, 25));
            }
            y += RowHeight;
        }

        composer.Pager(page, pages, y + 14, Width, p => { page = p; Compose(); });
        y += pages > 1 ? 54 : 16;
        if (Officer) y = composer.Text(Lang.Get("createacolony:townhall-recall-hint"), GuiKit.Muted(), 0, y, Width, 6);
        ComingLater(composer, "townhall-later-citizens", y);
    }

    /// <summary>A wanderer's row: who, how long it still waits, and Welcome / Turn away buttons for officers.</summary>
    void ComposeVisitor(GuiComposer composer, CitizenView c, double y)
    {
        var visitorFont = CairoFont.WhiteSmallText().WithColor(new[] { 1.0, 0.85, 0.4, 1.0 });
        composer
            .AddStaticText(c.Name, visitorFont, ElementBounds.Fixed(10, y + 6, 150, 25))
            .AddStaticText(Lang.Get("createacolony:visitor-status", c.VisitorHoursLeft), GuiKit.Detail(), ElementBounds.Fixed(165, y + 8, 172, 25));
        if (c.TraitsText.Length > 0) composer.AddHoverText(Lang.Get("createacolony:traits-hover", c.TraitsText), GuiKit.Detail(), 260, ElementBounds.Fixed(10, y + 6, 150, 25));
        if (!Officer) return;
        int citizenId = c.CitizenId;
        composer
            .AddSmallButton(Lang.Get("createacolony:visitor-welcome"), () => SendCitizen(ColonyAction.WelcomeVisitor, citizenId), ElementBounds.Fixed(340, y + 2, 105, 28))
            .AddSmallButton(Lang.Get("createacolony:visitor-turn-away"), () => SendCitizen(ColonyAction.TurnAwayVisitor, citizenId), ElementBounds.Fixed(450, y + 2, 110, 28));
    }

    bool SendCitizen(ColonyAction action, int citizenId)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = action, CitizenId = citizenId });
        return true;
    }

    // ---- Statistics tab -------------------------------------------------------------------

    void ComposeStatistics(GuiComposer composer)
    {
        double y = PageButtons(composer, new[] { "townhall-stats-colony", "townhall-stats-work" }, statsPage, p => statsPage = p, 30, 150);
        if (statsPage == 0) ComposeColonyStats(composer, y);
        else ComposeWorkStats(composer, y);
    }

    /// <summary>Settlers against beds, the colony's age and buildings, and every job's workers against its places.</summary>
    void ComposeColonyStats(GuiComposer composer, double y)
    {
        int alive = snapshot.Citizens.Count(c => c.State != "dead" && !c.Visitor);
        int dead = snapshot.Citizens.Count(c => c.State == "dead");
        int days = Math.Max(0, (int)(capi.World.Calendar.TotalDays - snapshot.FoundedTotalDays));

        int statRows = snapshot.PopulationCap > 0 ? 5 : 4;
        composer.AddInset(ElementBounds.Fixed(0, y, Width, statRows * 24 + 12), 3, 0.8f);
        double row = y + 6;
        row = composer.Row(Lang.Get("createacolony:townhall-stat-settlers"), Lang.Get("createacolony:townhall-of", alive, snapshot.BedCount + snapshot.FreeBeds), 10, row, Width - 20,
            snapshot.FreeBeds == 0 ? GuiKit.WarnColor : null);
        if (snapshot.PopulationCap > 0) row = composer.Row(Lang.Get("createacolony:townhall-stat-population"), Lang.Get("createacolony:townhall-of", alive, snapshot.PopulationCap), 10, row, Width - 20,
            alive >= snapshot.PopulationCap ? GuiKit.WarnColor : null);
        row = composer.Row(Lang.Get("createacolony:townhall-stat-lost"), dead.ToString(), 10, row, Width - 20);
        row = composer.Row(Lang.Get("createacolony:townhall-stat-buildings"), snapshot.BuildingCount.ToString(), 10, row, Width - 20);
        composer.Row(Lang.Get("createacolony:townhall-stat-age"), Lang.Get("createacolony:townhall-days", days), 10, row, Width - 20);
        y += statRows * 24 + 12 + 6;
        y = composer.Text(Lang.Get("createacolony:townhall-beds-hint"), GuiKit.Muted(), 0, y, Width, 10);

        y = composer.Section(Lang.Get("createacolony:townhall-jobs"), y, Width);
        var jobs = snapshot.Jobs.Where(j => j.Workers > 0 || j.Places > 0).OrderBy(j => j.Job.Length == 0).ToList();
        if (jobs.Count == 0)
        {
            composer.Text(Lang.Get("createacolony:no-citizens"), GuiKit.Detail(), 0, y, Width);
            return;
        }
        composer.AddInset(ElementBounds.Fixed(0, y, Width, jobs.Count * 24 + 12), 3, 0.8f);
        y += 6;
        foreach (var j in jobs)
        {
            string count = j.Places < 0 ? j.Workers.ToString() : Lang.Get("createacolony:townhall-of", j.Workers, j.Places);
            bool open = j.Places > j.Workers;
            y = composer.Row(JobName(j.Job), count, 10, y, Width - 20, open ? GuiKit.WarnColor : null);
        }
        composer.Text(Lang.Get("createacolony:townhall-jobs-hint"), GuiKit.Muted(), 0, y + 12, Width);
    }

    /// <summary>Every hut's Statistics added together, for a chosen time.</summary>
    void ComposeWorkStats(GuiComposer composer, double y)
    {
        var periods = GuiDialogHut.StatPeriods;
        y = PageButtons(composer, periods.Select(p => "stats-" + p).ToArray(), statPeriod, p => statPeriod = p, y, 104);
        var rows = snapshot.Stats.Where(s => s.Counts.Length > statPeriod && s.Counts[statPeriod] > 0).ToList();
        if (rows.Count == 0)
        {
            y = composer.Text(Lang.Get("createacolony:stats-none"), GuiKit.Body(), 0, y, Width, 10);
        }
        else
        {
            composer.AddInset(ElementBounds.Fixed(0, y, Width, rows.Count * 24 + 12), 3, 0.8f);
            y += 6;
            foreach (var row in rows.OrderBy(r => r.Key.StartsWith("kill:") ? 0 : 1).ThenByDescending(r => r.Counts[statPeriod]))
            {
                y = composer.Row(GuiDialogHut.StatLabel(row.Key), row.Counts[statPeriod].ToString(), 10, y, Width - 20);
            }
            y += 12;
        }
        ComingLater(composer, "townhall-later-stats", y);
    }

    // ---- Permissions tab ------------------------------------------------------------------

    void ComposePermissions(GuiComposer composer)
    {
        bool isOwner = ViewerRole == ColonyRole.Owner;
        string[] roleCodes = { ((int)ColonyRole.Member).ToString(), ((int)ColonyRole.Officer).ToString() };
        string[] roleNames = { Lang.Get("createacolony:role-member"), Lang.Get("createacolony:role-officer") };

        double y = 30;
        composer
            .AddStaticText(Lang.Get("createacolony:col-player"), GuiKit.Muted(), ElementBounds.Fixed(10, y, 210, 20))
            .AddStaticText(Lang.Get("createacolony:col-role"), GuiKit.Muted(), ElementBounds.Fixed(230, y, 160, 20));
        y += 22;
        composer.AddInset(ElementBounds.Fixed(0, y, Width, snapshot.Members.Count * RowHeight + 6), 3, 0.8f);
        y += 3;
        foreach (var m in snapshot.Members)
        {
            var role = (ColonyRole)m.Role;
            composer.AddStaticText(m.Name, GuiKit.Body(), ElementBounds.Fixed(10, y + 6, 210, 25));

            if (isOwner && role != ColonyRole.Owner)
            {
                string uid = m.PlayerUid;
                int selected = role == ColonyRole.Officer ? 1 : 0;
                composer
                    .AddDropDown(roleCodes, roleNames, selected, (code, on) => OnSetRole(uid, int.Parse(code)),
                        ElementBounds.Fixed(230, y + 2, 160, 28), "role-" + uid)
                    .AddSmallButton(Lang.Get("createacolony:remove"), () => OnRemove(uid), ElementBounds.Fixed(Width - 130, y + 2, 120, 28));
            }
            else
            {
                composer.AddStaticText(RoleName(role), GuiKit.Body(), ElementBounds.Fixed(230, y + 6, 160, 25));
            }
            y += RowHeight;
        }

        y = composer.Text(Lang.Get("createacolony:invite-hint"), GuiKit.Muted(), 0, y + 12, Width, 8);
        y = composer.Text(Lang.Get("createacolony:townhall-roles-hint"), GuiKit.Muted(), 0, y, Width, 8);
        ComingLater(composer, "townhall-later-permissions", y);
    }

    // ---- Settings tab ---------------------------------------------------------------------

    /// <summary>Permadeath (the owner's switch), and how wanderers and the warehouse are set up.</summary>
    void ComposeSettings(GuiComposer composer)
    {
        double y = composer.Section(Lang.Get("createacolony:permadeath"), 30, Width);
        if (ViewerRole == ColonyRole.Owner)
        {
            composer
                .AddSwitch(on => network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = ColonyAction.SetPermadeath, Text = on ? "1" : "0" }),
                    ElementBounds.Fixed(0, y, 30, 30), "permadeath")
                .AddStaticText(Lang.Get(snapshot.Permadeath ? "createacolony:on" : "createacolony:off"), GuiKit.Body(), ElementBounds.Fixed(40, y + 5, 100, 25));
        }
        else
        {
            composer.AddStaticText(Lang.Get(snapshot.Permadeath ? "createacolony:on" : "createacolony:off"), GuiKit.Body(), ElementBounds.Fixed(0, y + 5, Width, 25));
        }
        y += 36;
        y = composer.Text(Lang.Get(snapshot.Permadeath ? "createacolony:permadeath-on-hint" : "createacolony:permadeath-off-hint"), GuiKit.Muted(), 0, y, Width, 12);

        y = composer.Section(Lang.Get("createacolony:townhall-wanderers"), y, Width);
        y = composer.Text(snapshot.WandererDays <= 0 ? Lang.Get("createacolony:overview-no-wanderers")
            : Lang.Get(snapshot.WanderersNeedFreeBed ? "createacolony:overview-wanderers-bed" : "createacolony:overview-wanderers",
                snapshot.WandererDays.ToString("0.#"), snapshot.FreeBeds), GuiKit.Detail(), 0, y, Width, 4);
        y = composer.Text(Lang.Get("createacolony:townhall-wanderers-hint"), GuiKit.Muted(), 0, y, Width, 12);

        y = composer.Section(Lang.Get("createacolony:building-warehouse"), y, Width);
        y = composer.Text(snapshot.HasWarehouse
            ? Lang.Get("createacolony:overview-warehouse", snapshot.WarehouseChests, snapshot.DropOffs, snapshot.OpenRequests)
            : Lang.Get("createacolony:overview-no-warehouse"), GuiKit.Detail(), 0, y, Width, 12);

        ComingLater(composer, "townhall-later-settings", y);
    }

    // ---- Sending --------------------------------------------------------------------------

    bool Send(ColonyAction action, int buildingId)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = action, BuildingId = buildingId });
        return true;
    }

    static string JobName(string code) => Lang.Get("createacolony:job-" + (code.Length == 0 ? "none" : code));

    static string RoleName(ColonyRole role) => Lang.Get("createacolony:role-" + role.ToString().ToLowerInvariant());

    bool OnRename()
    {
        renaming = false;
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = ColonyAction.Rename, Text = pendingName });
        Compose();
        return true;
    }

    void OnSetJob(int citizenId, string job)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = ColonyAction.SetJob, CitizenId = citizenId, Text = job });
    }

    void OnSetRole(string uid, int role)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = ColonyAction.SetRole, PlayerUid = uid, Role = role });
    }

    bool OnRemove(string uid)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = ColonyAction.RemoveMember, PlayerUid = uid });
        return true;
    }

    public override void OnGuiClosed()
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = ColonyAction.CloseWindow });
        base.OnGuiClosed();
    }

    /// <summary>The mood word for a happiness, the same bands as ColonyManager.MoodOf.</summary>
    static string MoodWord(int h) => h >= 85 ? "joyful" : h >= 70 ? "happy" : h >= 45 ? "content" : h >= 30 ? "unhappy" : h >= 15 ? "miserable" : "broken";
}
