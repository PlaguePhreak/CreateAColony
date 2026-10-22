using System;
using System.Linq;
using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// The Town Hall window: an overview tab, a settlers tab with a job picker per settler, and a
/// members tab where the owner sets roles. Every change goes to the server, which answers with a
/// fresh snapshot; the window never changes colony data on its own.
/// </summary>
public class GuiDialogTownHall : GuiDialog
{
    const int RowsPerPage = 8;
    const double RowHeight = 34;
    const double Width = 560;

    readonly ColonyNetwork network;
    ColonySnapshot snapshot;
    int currentTab;
    int page;
    string pendingName;

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
        if (fresh.Name != snapshot.Name) pendingName = fresh.Name;
        snapshot = fresh;
        // Timed progress refreshes would close an open dropdown on the other tabs.
        if (fresh.Periodic && currentTab != 0) return;
        Compose();
    }

    ColonyRole ViewerRole => (ColonyRole)snapshot.ViewerRole;

    void Compose()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        var tabs = new[]
        {
            new GuiTab { Name = Lang.Get("createacolony:tab-overview"), DataInt = 0, Active = currentTab == 0 },
            new GuiTab { Name = Lang.Get("createacolony:tab-settlers"), DataInt = 1, Active = currentTab == 1 },
            new GuiTab { Name = Lang.Get("createacolony:tab-members"), DataInt = 2, Active = currentTab == 2 },
        };

        var composer = capi.Gui.CreateCompo("createacolony-townhall", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(snapshot.Name, () => TryClose())
            .AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 3 * 30), (id, tab) => { currentTab = id; page = 0; Compose(); }, "tabs")
            .BeginChildElements(bgBounds);

        switch (currentTab)
        {
            case 0: ComposeOverview(composer); break;
            case 1: ComposeSettlers(composer); break;
            case 2: ComposeMembers(composer); break;
        }

        SingleComposer = composer.EndChildElements().Compose();
        SingleComposer.GetTextInput("colonyname")?.SetValue(pendingName);
        SingleComposer.GetSwitch("permadeath")?.SetValue(snapshot.Permadeath);
    }

    void ComposeOverview(GuiComposer composer)
    {
        int alive = snapshot.Citizens.Count(c => c.State != "dead" && !c.Visitor);
        int hungry = snapshot.Citizens.Count(c => c.State == "hungry");
        double y = 30;

        // The colony's numbers in two columns on a sunken panel.
        const double pad = 10;
        double column = (Width - 3 * pad) / 2;
        composer.AddInset(ElementBounds.Fixed(0, y, Width, 3 * 24 + 2 * pad), 3, 0.8f);
        double left = y + pad, right = y + pad;
        left = composer.Row(Lang.Get("createacolony:stat-level"), snapshot.Level.ToString(), pad, left, column);
        left = composer.Row(Lang.Get("createacolony:stat-settlers"), alive.ToString(), pad, left, column);
        left = composer.Row(Lang.Get("createacolony:stat-hungry"), hungry.ToString(), pad, left, column, hungry > 0 ? GuiKit.WarnColor : null);
        right = composer.Row(Lang.Get("createacolony:stat-beds"), snapshot.BedCount.ToString(), 2 * pad + column, right, column);
        right = composer.Row(Lang.Get("createacolony:stat-members"), snapshot.Members.Count.ToString(), 2 * pad + column, right, column);
        composer.Row(Lang.Get("createacolony:stat-supplies"), snapshot.SupplyDeliveriesLeft.ToString(), 2 * pad + column, right, column);
        y += 3 * 24 + 2 * pad + 10;

        y = composer.Text(snapshot.HasWarehouse
            ? Lang.Get("createacolony:overview-warehouse", snapshot.WarehouseChests, snapshot.DropOffs, snapshot.OpenRequests)
            : Lang.Get("createacolony:overview-no-warehouse"), GuiKit.Detail(), 0, y, Width, 2);
        y = composer.Text(snapshot.WandererDays <= 0 ? Lang.Get("createacolony:overview-no-wanderers")
            : Lang.Get(snapshot.WanderersNeedFreeBed ? "createacolony:overview-wanderers-bed" : "createacolony:overview-wanderers",
                snapshot.WandererDays.ToString("0.#"), snapshot.FreeBeds), GuiKit.Detail(), 0, y, Width, 2);
        int visitors = snapshot.Citizens.Count(c => c.Visitor);
        if (visitors > 0) y = composer.Text(Lang.Get("createacolony:overview-visitors", visitors), GuiKit.Body().WithColor(GuiKit.WarnColor), 0, y, Width, 2);
        y += 12;

        if (ViewerRole >= ColonyRole.Officer)
        {
            y = composer.Section(Lang.Get("createacolony:colony-name"), y, Width);
            composer
                .AddTextInput(ElementBounds.Fixed(0, y, Width - 130, 30), text => pendingName = text, CairoFont.WhiteSmallText(), "colonyname")
                .AddSmallButton(Lang.Get("createacolony:rename"), OnRename, ElementBounds.Fixed(Width - 120, y + 1, 120, 28));
            y += 44;
        }
        else
        {
            y = composer.Text(Lang.Get("createacolony:visitor-readonly"), GuiKit.Muted(), 0, y, Width, 14);
        }
        y = ComposeTownHallBuilding(composer, y);
        ComposePermadeath(composer, y);
    }

    /// <summary>The colony's Permadeath setting: a switch for the owner, a line of text for everyone else.</summary>
    void ComposePermadeath(GuiComposer composer, double y)
    {
        y = composer.Section(Lang.Get("createacolony:permadeath"), y, Width);
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
        composer.Text(Lang.Get(snapshot.Permadeath ? "createacolony:permadeath-on-hint" : "createacolony:permadeath-off-hint"), GuiKit.Muted(), 0, y, Width);
    }

    /// <summary>The Town Hall's own level and upgrade button. Other buildings are ordered from the Builder's Hut. Returns the y below it.</summary>
    double ComposeTownHallBuilding(GuiComposer composer, double y)
    {
        var b = snapshot.Buildings.FirstOrDefault();
        if (b == null) return y;

        y = composer.Section(Lang.Get("createacolony:building-townhall"), y, Width);
        string level = b.MaxLevel == 0
            ? Lang.Get("createacolony:building-no-blueprints", b.Level)
            : Lang.Get("createacolony:building-level", b.Level, b.MaxLevel);
        composer.AddStaticText(level, GuiKit.Body(), ElementBounds.Fixed(0, y + 5, Width - 320, 25));

        if (ViewerRole >= ColonyRole.Officer)
        {
            int id = b.BuildingId;
            if (b.TargetLevel > 0)
            {
                composer.AddSmallButton(Lang.Get("createacolony:build-cancel"), () => Send(ColonyAction.CancelBuild, id), ElementBounds.Fixed(Width - 120, y, 120, 28));
            }
            else if (b.Level < b.MaxLevel)
            {
                string label = Lang.Get(b.Level == 0 ? "createacolony:build-start" : "createacolony:build-upgrade", b.Level + 1);
                composer.AddSmallButton(label, () => Send(ColonyAction.Build, id), ElementBounds.Fixed(Width - 120, y, 120, 28));
            }
            if (b.Level == 0 && b.TargetLevel == 0)
            {
                // Like a hut, the Town Hall can be turned until its first level is built.
                composer.AddSmallButton(Lang.Get("createacolony:build-rotate", b.Rotation), () => Send(ColonyAction.Rotate, id), ElementBounds.Fixed(Width - 310, y, 180, 28));
            }
        }
        y += 34;
        if (b.TargetLevel > 0)
        {
            string status = Lang.Get("createacolony:build-status", b.TargetLevel, b.ProgressPercent, Lang.Get("createacolony:buildstatus-" + b.Status));
            y = composer.Text(status, GuiKit.Detail().WithColor(GuiStyle.ActiveButtonTextColor), 0, y, Width, 4);
        }
        return composer.Text(Lang.Get("createacolony:townhall-builder-hint"), GuiKit.Muted(), 0, y, Width, 14);
    }

    void ComposeSettlers(GuiComposer composer)
    {
        // Wanderers asking to join come first, so they are not missed.
        var citizens = snapshot.Citizens.OrderByDescending(c => c.Visitor).ThenBy(c => c.State == "dead").ThenBy(c => c.Name).ToList();
        if (citizens.Count == 0)
        {
            composer.Text(Lang.Get("createacolony:no-citizens"), GuiKit.Body(), 0, 30, Width);
            return;
        }

        composer
            .AddStaticText(Lang.Get("createacolony:col-name"), GuiKit.Muted(), ElementBounds.Fixed(10, 30, 150, 20))
            .AddStaticText(Lang.Get("createacolony:col-status"), GuiKit.Muted(), ElementBounds.Fixed(170, 30, 190, 20))
            .AddStaticText(Lang.Get("createacolony:col-job"), GuiKit.Muted(), ElementBounds.Fixed(370, 30, 180, 20));

        string[] jobCodes = Jobs.All;
        string[] jobNames = jobCodes.Select(JobName).ToArray();
        bool canAssign = ViewerRole >= ColonyRole.Officer;

        int pages = (citizens.Count + RowsPerPage - 1) / RowsPerPage;
        page = Math.Clamp(page, 0, pages - 1);
        int shown = Math.Min(RowsPerPage, citizens.Count - page * RowsPerPage);
        composer.AddInset(ElementBounds.Fixed(0, 52, Width, shown * RowHeight + 6), 3, 0.8f);
        double y = 55;
        foreach (var c in citizens.Skip(page * RowsPerPage).Take(RowsPerPage))
        {
            if (c.Visitor)
            {
                ComposeVisitor(composer, c, y, canAssign);
                y += RowHeight;
                continue;
            }
            string status = Lang.Get("createacolony:settler-status", Lang.Get("createacolony:state-" + c.State), c.FoodPercent,
                Lang.Get(c.HasBed ? "createacolony:has-bed" : "createacolony:no-bed"));
            composer
                .AddStaticText(c.Name, c.State == "dead" ? GuiKit.Body().WithColor(GuiKit.MutedColor) : GuiKit.Body(), ElementBounds.Fixed(10, y + 6, 155, 25))
                .AddStaticText(status, c.State == "hungry" ? GuiKit.Detail().WithColor(GuiKit.WarnColor) : GuiKit.Detail(), ElementBounds.Fixed(170, y + 8, 195, 25));

            if (canAssign && c.State != "dead")
            {
                int citizenId = c.CitizenId;
                int selected = Math.Max(0, Array.IndexOf(jobCodes, c.Job));
                composer.AddDropDown(jobCodes, jobNames, selected, (code, on) => OnSetJob(citizenId, code),
                    ElementBounds.Fixed(370, y + 2, 180, 28), "job-" + citizenId);
            }
            else
            {
                composer.AddStaticText(JobName(c.Job), GuiKit.Body(), ElementBounds.Fixed(370, y + 6, 180, 25));
            }
            y += RowHeight;
        }

        composer.Pager(page, pages, y + 14, Width, p => { page = p; Compose(); });
    }

    /// <summary>A wanderer's row: who, how long it still waits, and Welcome / Turn away buttons for officers.</summary>
    void ComposeVisitor(GuiComposer composer, CitizenView c, double y, bool canDecide)
    {
        var visitorFont = CairoFont.WhiteSmallText().WithColor(new[] { 1.0, 0.85, 0.4, 1.0 });
        composer
            .AddStaticText(c.Name, visitorFont, ElementBounds.Fixed(10, y + 6, 155, 25))
            .AddStaticText(Lang.Get("createacolony:visitor-status", c.VisitorHoursLeft), GuiKit.Detail(), ElementBounds.Fixed(170, y + 8, 195, 25));
        if (!canDecide) return;
        int citizenId = c.CitizenId;
        composer
            .AddSmallButton(Lang.Get("createacolony:visitor-welcome"), () => SendCitizen(ColonyAction.WelcomeVisitor, citizenId), ElementBounds.Fixed(370, y + 2, 85, 28))
            .AddSmallButton(Lang.Get("createacolony:visitor-turn-away"), () => SendCitizen(ColonyAction.TurnAwayVisitor, citizenId), ElementBounds.Fixed(460, y + 2, 90, 28));
    }

    bool SendCitizen(ColonyAction action, int citizenId)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = action, CitizenId = citizenId });
        return true;
    }

    void ComposeMembers(GuiComposer composer)
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

        composer.Text(Lang.Get("createacolony:invite-hint"), GuiKit.Muted(), 0, y + 12, Width);
    }

    bool Send(ColonyAction action, int buildingId)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = action, BuildingId = buildingId });
        return true;
    }

    static string JobName(string code) => Lang.Get("createacolony:job-" + (code.Length == 0 ? "none" : code));

    static string RoleName(ColonyRole role) => Lang.Get("createacolony:role-" + role.ToString().ToLowerInvariant());

    bool OnRename()
    {
        network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = ColonyAction.Rename, Text = pendingName });
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
}
