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
            .AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, 160), (id, tab) => { currentTab = id; page = 0; Compose(); }, "tabs")
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
        string stats = Lang.Get("createacolony:overview-stats",
            snapshot.Level, alive, snapshot.BedCount, hungry, snapshot.Members.Count, snapshot.SupplyDeliveriesLeft);
        stats += "<br>" + (snapshot.HasWarehouse
            ? Lang.Get("createacolony:overview-warehouse", snapshot.WarehouseChests, snapshot.DropOffs, snapshot.OpenRequests)
            : Lang.Get("createacolony:overview-no-warehouse"));
        stats += "<br>" + (snapshot.WandererDays <= 0 ? Lang.Get("createacolony:overview-no-wanderers")
            : Lang.Get(snapshot.WanderersNeedFreeBed ? "createacolony:overview-wanderers-bed" : "createacolony:overview-wanderers",
                snapshot.WandererDays.ToString("0.#"), snapshot.FreeBeds));
        int visitors = snapshot.Citizens.Count(c => c.Visitor);
        if (visitors > 0) stats += "<br>" + Lang.Get("createacolony:overview-visitors", visitors);

        composer.AddRichtext(stats, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, Width, 200));

        if (ViewerRole >= ColonyRole.Officer)
        {
            composer
                .AddStaticText(Lang.Get("createacolony:colony-name"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 245, 120, 25))
                .AddTextInput(ElementBounds.Fixed(120, 240, 280, 30), text => pendingName = text, CairoFont.WhiteSmallText(), "colonyname")
                .AddSmallButton(Lang.Get("createacolony:rename"), OnRename, ElementBounds.Fixed(410, 240, 120, 30));
        }
        else
        {
            composer.AddStaticText(Lang.Get("createacolony:visitor-readonly"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 245, Width, 25));
        }
        ComposeTownHallBuilding(composer, 290);
        ComposePermadeath(composer, 405);
    }

    /// <summary>The colony's Permadeath setting: a switch for the owner, a line of text for everyone else.</summary>
    void ComposePermadeath(GuiComposer composer, double y)
    {
        if (ViewerRole == ColonyRole.Owner)
        {
            composer
                .AddSwitch(on => network.SendAction(new ColonyActionPacket { ColonyId = ColonyId, Action = ColonyAction.SetPermadeath, Text = on ? "1" : "0" }),
                    ElementBounds.Fixed(0, y, 30, 30), "permadeath")
                .AddStaticText(Lang.Get("createacolony:permadeath"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(40, y + 5, Width - 40, 25));
        }
        else
        {
            composer.AddStaticText(Lang.Get("createacolony:permadeath") + ": " + Lang.Get(snapshot.Permadeath ? "createacolony:on" : "createacolony:off"),
                CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 5, Width, 25));
        }
        composer.AddStaticText(Lang.Get(snapshot.Permadeath ? "createacolony:permadeath-on-hint" : "createacolony:permadeath-off-hint"),
            CairoFont.WhiteDetailText(), ElementBounds.Fixed(40, y + 34, Width - 40, 40));
    }

    /// <summary>The Town Hall's own level and upgrade button. Other buildings are ordered from the Builder's Hut.</summary>
    void ComposeTownHallBuilding(GuiComposer composer, double y)
    {
        var b = snapshot.Buildings.FirstOrDefault();
        if (b == null) return;

        string level = b.MaxLevel == 0
            ? Lang.Get("createacolony:building-no-blueprints", b.Level)
            : Lang.Get("createacolony:building-level", b.Level, b.MaxLevel);
        composer.AddStaticText(Lang.Get("createacolony:building-townhall") + ": " + level, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 6, 390, 25));

        if (ViewerRole >= ColonyRole.Officer)
        {
            int id = b.BuildingId;
            if (b.TargetLevel > 0)
            {
                composer.AddSmallButton(Lang.Get("createacolony:build-cancel"), () => Send(ColonyAction.CancelBuild, id), ElementBounds.Fixed(410, y, 120, 28));
            }
            else if (b.Level < b.MaxLevel)
            {
                string label = Lang.Get(b.Level == 0 ? "createacolony:build-start" : "createacolony:build-upgrade", b.Level + 1);
                composer.AddSmallButton(label, () => Send(ColonyAction.Build, id), ElementBounds.Fixed(410, y, 120, 28));
            }
            if (b.Level == 0 && b.TargetLevel == 0)
            {
                // Like a hut, the Town Hall can be turned until its first level is built.
                composer.AddSmallButton(Lang.Get("createacolony:build-rotate", b.Rotation), () => Send(ColonyAction.Rotate, id), ElementBounds.Fixed(0, y + 32, 230, 28));
            }
        }
        if (b.TargetLevel > 0)
        {
            string status = Lang.Get("createacolony:build-status", b.TargetLevel, b.ProgressPercent, Lang.Get("createacolony:buildstatus-" + b.Status));
            composer.AddStaticText(status, CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y + 34, Width, 25));
        }
        composer.AddStaticText(Lang.Get("createacolony:townhall-builder-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y + 64, Width, 40));
    }

    void ComposeSettlers(GuiComposer composer)
    {
        // Wanderers asking to join come first, so they are not missed.
        var citizens = snapshot.Citizens.OrderByDescending(c => c.Visitor).ThenBy(c => c.State == "dead").ThenBy(c => c.Name).ToList();
        if (citizens.Count == 0)
        {
            composer.AddStaticText(Lang.Get("createacolony:no-citizens"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, Width, 25));
            return;
        }

        composer
            .AddStaticText(Lang.Get("createacolony:col-name"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 30, 160, 20))
            .AddStaticText(Lang.Get("createacolony:col-status"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(160, 30, 200, 20))
            .AddStaticText(Lang.Get("createacolony:col-job"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(370, 30, 180, 20));

        string[] jobCodes = Jobs.All;
        string[] jobNames = jobCodes.Select(JobName).ToArray();
        bool canAssign = ViewerRole >= ColonyRole.Officer;

        int pages = (citizens.Count + RowsPerPage - 1) / RowsPerPage;
        page = Math.Clamp(page, 0, pages - 1);
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
                .AddStaticText(c.Name, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 6, 160, 25))
                .AddStaticText(status, CairoFont.WhiteDetailText(), ElementBounds.Fixed(160, y + 8, 200, 25));

            if (canAssign && c.State != "dead")
            {
                int citizenId = c.CitizenId;
                int selected = Math.Max(0, Array.IndexOf(jobCodes, c.Job));
                composer.AddDropDown(jobCodes, jobNames, selected, (code, on) => OnSetJob(citizenId, code),
                    ElementBounds.Fixed(370, y, 180, 28), "job-" + citizenId);
            }
            else
            {
                composer.AddStaticText(JobName(c.Job), CairoFont.WhiteSmallText(), ElementBounds.Fixed(370, y + 6, 180, 25));
            }
            y += RowHeight;
        }

        if (pages > 1)
        {
            y += 10;
            composer
                .AddSmallButton(Lang.Get("createacolony:prev"), () => { page--; Compose(); return true; }, ElementBounds.Fixed(0, y, 80, 28))
                .AddStaticText(Lang.Get("createacolony:page", page + 1, pages), CairoFont.WhiteDetailText(), ElementBounds.Fixed(100, y + 6, 120, 25))
                .AddSmallButton(Lang.Get("createacolony:next"), () => { page++; Compose(); return true; }, ElementBounds.Fixed(230, y, 80, 28));
        }
    }

    /// <summary>A wanderer's row: who, how long it still waits, and Welcome / Turn away buttons for officers.</summary>
    void ComposeVisitor(GuiComposer composer, CitizenView c, double y, bool canDecide)
    {
        var visitorFont = CairoFont.WhiteSmallText().WithColor(new[] { 1.0, 0.85, 0.4, 1.0 });
        composer
            .AddStaticText(c.Name, visitorFont, ElementBounds.Fixed(0, y + 6, 160, 25))
            .AddStaticText(Lang.Get("createacolony:visitor-status", c.VisitorHoursLeft), CairoFont.WhiteDetailText(), ElementBounds.Fixed(160, y + 8, 200, 25));
        if (!canDecide) return;
        int citizenId = c.CitizenId;
        composer
            .AddSmallButton(Lang.Get("createacolony:visitor-welcome"), () => SendCitizen(ColonyAction.WelcomeVisitor, citizenId), ElementBounds.Fixed(370, y, 85, 28))
            .AddSmallButton(Lang.Get("createacolony:visitor-turn-away"), () => SendCitizen(ColonyAction.TurnAwayVisitor, citizenId), ElementBounds.Fixed(460, y, 90, 28));
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
        foreach (var m in snapshot.Members)
        {
            var role = (ColonyRole)m.Role;
            composer.AddStaticText(m.Name, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 6, 220, 25));

            if (isOwner && role != ColonyRole.Owner)
            {
                string uid = m.PlayerUid;
                int selected = role == ColonyRole.Officer ? 1 : 0;
                composer
                    .AddDropDown(roleCodes, roleNames, selected, (code, on) => OnSetRole(uid, int.Parse(code)),
                        ElementBounds.Fixed(230, y, 160, 28), "role-" + uid)
                    .AddSmallButton(Lang.Get("createacolony:remove"), () => OnRemove(uid), ElementBounds.Fixed(410, y, 120, 28));
            }
            else
            {
                composer.AddStaticText(RoleName(role), CairoFont.WhiteSmallText(), ElementBounds.Fixed(230, y + 6, 160, 25));
            }
            y += RowHeight;
        }

        composer.AddStaticText(Lang.Get("createacolony:invite-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y + 10, Width, 40));
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
