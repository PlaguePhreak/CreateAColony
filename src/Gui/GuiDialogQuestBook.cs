using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Network;
using CreateAColony.Quests;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// The quest book: groups down the left as tabs, a chapter chooser along the top, the chapter's map in
/// the middle and the chosen quest's card underneath with its objectives, rewards and buttons. All the
/// text and layout comes from the quest files; the server only says where each quest stands.
/// </summary>
public class GuiDialogQuestBook : GuiDialog
{
    const double Width = 620;
    const double MapHeight = 290;
    const double CardHeight = 230;

    readonly ColonyNetwork network;
    readonly QuestLibrary library;
    QuestBookSnapshot snapshot;
    Dictionary<string, QuestStateView> states = new();
    string lastSignature = "";

    int groupIndex;
    // The chapter chosen in each group, the quest chosen in each chapter, and where each map is scrolled to.
    readonly Dictionary<string, string> chapterOf = new();
    readonly Dictionary<string, string?> selectedOf = new();
    readonly Dictionary<string, QuestMapView> views = new();
    // Item icons looked up once per quest; null when nothing matched.
    readonly Dictionary<string, ItemStack?> icons = new();
    GuiElementQuestMap? mapElement;

    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogQuestBook(ICoreClientAPI capi, ColonyNetwork network, QuestBookSnapshot snapshot) : base(capi)
    {
        this.network = network;
        this.snapshot = snapshot;
        library = network.ClientQuests;
        Apply(snapshot);
        Compose();
    }

    public void Update(QuestBookSnapshot fresh)
    {
        string before = lastSignature;
        Apply(fresh);
        // Timed refreshes only redraw when something moved, so an open chapter list stays open.
        if (fresh.Periodic && before == lastSignature) return;
        Compose();
    }

    void Apply(QuestBookSnapshot fresh)
    {
        snapshot = fresh;
        states = fresh.Quests.ToDictionary(s => s.Code, s => s);
        lastSignature = string.Join("|", fresh.Quests.Select(s => s.Code + s.Status + (s.Tracked ? "t" : "") + string.Join(",", s.Progress))) + fresh.ColonyId;
    }

    // ---- What is chosen -------------------------------------------------------------------

    QuestGroupDef? Group => library.Groups.Count == 0 ? null : library.Groups[Math.Clamp(groupIndex, 0, library.Groups.Count - 1)];

    QuestChapterDef? Chapter
    {
        get
        {
            var group = Group;
            if (group == null || group.Chapters.Count == 0) return null;
            return group.Chapters.FirstOrDefault(c => c.Code == chapterOf.GetValueOrDefault(group.Code)) ?? group.Chapters[0];
        }
    }

    QuestDef? Selected
    {
        get
        {
            var chapter = Chapter;
            if (chapter == null) return null;
            string? code = selectedOf.GetValueOrDefault(chapter.Code);
            return code == null ? null : chapter.Quests.FirstOrDefault(q => q.Code == code);
        }
    }

    public QuestStatus StatusOf(QuestDef q) => states.TryGetValue(q.Code, out var s) ? (QuestStatus)s.Status : QuestStatus.Locked;

    (int Done, int Total) ChapterProgress(QuestChapterDef chapter)
    {
        int total = chapter.Quests.Count(q => !q.Secret || StatusOf(q) != QuestStatus.Locked);
        int done = chapter.Quests.Count(q => StatusOf(q) == QuestStatus.Claimed);
        return (done, total);
    }

    // ---- Layout ---------------------------------------------------------------------------

    void Compose()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        var tabs = library.Groups.Select((g, i) => new GuiTab { Name = Lang.Get("createacolony:questgroup-" + g.Code), DataInt = i, Active = i == groupIndex }).ToArray();
        string title = Lang.Get("createacolony:questbook-title");
        if (snapshot.ColonyName.Length > 0) title += " · " + snapshot.ColonyName;

        mapElement = null;
        var composer = capi.Gui.CreateCompo("createacolony-questbook", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(title, () => TryClose());
        if (tabs.Length > 0)
        {
            composer.AddVerticalTabs(tabs, ElementBounds.Fixed(-160, 35, 160, tabs.Length * 30), (_, tab) => { groupIndex = tab.DataInt; Compose(); }, "tabs");
        }
        composer.BeginChildElements(bgBounds);

        double y = 30;
        var group = Group;
        var chapter = Chapter;
        if (group == null)
        {
            composer.Text(Lang.Get("createacolony:quests-none-loaded"), GuiKit.Muted(), 0, y, Width);
            SingleComposer = composer.EndChildElements().Compose();
            return;
        }

        // The chapter chooser, with how many quests of each are done.
        var chapters = group.Chapters;
        if (chapters.Count > 0)
        {
            string[] values = chapters.Select(c => c.Code).ToArray();
            string[] names = chapters.Select(c =>
            {
                var (done, total) = ChapterProgress(c);
                return Lang.Get("createacolony:chapter-" + c.Code) + "   " + done + " / " + total;
            }).ToArray();
            int index = chapter == null ? 0 : Math.Max(0, chapters.IndexOf(chapter));
            composer
                .AddStaticText(Lang.Get("createacolony:questbook-chapter"), GuiKit.Body(), ElementBounds.Fixed(0, y + 4, 90, 26))
                .AddDropDown(values, names, index, (code, selected) =>
                {
                    if (!selected) return;
                    chapterOf[group.Code] = code;
                    Compose();
                }, ElementBounds.Fixed(90, y, 300, 28), "chapter");
            string scope = Lang.Get(chapter?.PerColony == true ? "createacolony:questbook-scope-colony" : "createacolony:questbook-scope-player");
            composer.AddStaticText(scope, GuiKit.Muted().WithOrientation(EnumTextOrientation.Right), ElementBounds.Fixed(Width - 220, y + 4, 220, 26));
        }
        y += 38;

        // The chapter's map.
        var mapBounds = ElementBounds.Fixed(0, y, Width, MapHeight);
        composer.AddInset(mapBounds.FlatCopy(), 3, 0.8f);
        var quests = chapter?.Quests ?? new List<QuestDef>();
        string chapterCode = chapter?.Code ?? "";
        if (!views.TryGetValue(chapterCode, out var view)) views[chapterCode] = view = new QuestMapView();
        mapElement = new GuiElementQuestMap(capi, mapBounds, quests, states, IconsFor(quests), view, selectedOf.GetValueOrDefault(chapterCode), code =>
        {
            selectedOf[chapterCode] = code;
            Compose();
        });
        mapElement.CentreOnQuests(chapterCode);
        composer.AddInteractiveElement(mapElement, "questmap");
        y += MapHeight + 8;

        // The chosen quest.
        composer.AddInset(ElementBounds.Fixed(0, y, Width, CardHeight), 3, 0.8f);
        var quest = Selected;
        if (quest == null)
        {
            string hint = quests.Count == 0 ? Lang.Get("createacolony:quests-chapter-empty") : Lang.Get("createacolony:questbook-pick");
            composer.Text(hint, GuiKit.Muted(), 10, y + 10, Width - 20);
        }
        else
        {
            ComposeCard(composer, quest, y);
        }

        SingleComposer = composer.EndChildElements().Compose();
    }

    /// <summary>The quest's title, where it stands, its story, its objectives with progress, its rewards and its buttons.</summary>
    void ComposeCard(GuiComposer composer, QuestDef quest, double top)
    {
        const double pad = 10;
        double x = pad, width = Width - 2 * pad, y = top + pad;
        var status = StatusOf(quest);
        var state = states.GetValueOrDefault(quest.Code);

        composer.AddStaticText(Lang.Get(QuestLibrary.TitleKey(quest)), GuiKit.Heading(), ElementBounds.Fixed(x, y, width - 160, 26));
        var statusFont = GuiKit.Body().WithOrientation(EnumTextOrientation.Right).WithColor(StatusColor(status));
        composer.AddStaticText(Lang.Get("createacolony:queststatus-" + status.ToString().ToLowerInvariant()), statusFont, ElementBounds.Fixed(x + width - 160, y + 2, 160, 26));
        y += 30;

        // Two columns: the story and objectives on the left, the rewards and buttons on the right.
        double leftWidth = width * 0.62, rightX = x + leftWidth + pad, rightWidth = width - leftWidth - pad;
        double left = y, right = y;

        string desc = Lang.Get(QuestLibrary.TitleKey(quest) + "-desc");
        if (desc.EndsWith("-desc")) desc = "";
        if (desc.Length > 0)
        {
            double descHeight = Math.Min(GuiKit.TextHeight(capi, GuiKit.Detail(), desc, leftWidth), 70);
            composer.AddStaticText(desc, GuiKit.Detail(), ElementBounds.Fixed(x, left, leftWidth, descHeight));
            left += descHeight + 6;
        }
        for (int i = 0; i < quest.Objectives.Count; i++)
        {
            var o = quest.Objectives[i];
            int progress = state != null && i < state.Progress.Count ? state.Progress[i] : 0;
            int needed = state != null && i < state.Needed.Count ? state.Needed[i] : Math.Max(1, o.Count);
            bool done = status == QuestStatus.Claimed || progress >= needed;
            string mark = done ? "✓ " : "• ";
            string line = mark + ObjectiveText(capi, o);
            if (needed > 1 || o.Type is ObjectiveTypes.Obtain or ObjectiveTypes.Deliver or ObjectiveTypes.Stock) line += "  (" + progress + " / " + needed + ")";
            var font = done ? GuiKit.Detail().WithColor(GuiKit.GoodColor) : GuiKit.Detail();
            composer.AddStaticText(line, font, ElementBounds.Fixed(x, left, leftWidth, 20));
            left += 20;
        }

        if (quest.Rewards.Count > 0)
        {
            composer.AddStaticText(Lang.Get("createacolony:questbook-rewards"), GuiKit.Body(), ElementBounds.Fixed(rightX, right, rightWidth, 22));
            right += 24;
            var parts = new List<RichTextComponentBase>();
            foreach (var reward in quest.Rewards)
            {
                var stack = QuestItems.Sample(capi.World, reward.Code);
                if (stack == null) continue;
                stack = stack.Clone();
                stack.StackSize = Math.Max(1, reward.Count);
                parts.Add(new ItemstackTextComponent(capi, stack, 40, 4, EnumFloat.Inline));
            }
            if (parts.Count > 0)
            {
                composer.AddRichtext(parts.ToArray(), ElementBounds.Fixed(rightX, right, rightWidth, 44));
                right += 48;
            }
        }

        // Buttons along the bottom right.
        double by = top + CardHeight - pad - 28;
        double bx = x + width;
        void Button(string key, Action click)
        {
            bx -= 100;
            composer.AddSmallButton(Lang.Get("createacolony:" + key), () => { click(); return true; }, ElementBounds.Fixed(bx, by, 96, 28));
            bx -= 6;
        }
        switch (status)
        {
            case QuestStatus.Available when quest.Accept:
                Button("quest-accept", () => network.SendQuestAction(quest.Code, QuestAction.Accept));
                break;
            case QuestStatus.Complete:
                Button("quest-claim", () => network.SendQuestAction(quest.Code, QuestAction.Claim));
                break;
            case QuestStatus.Active when quest.Accept:
                Button("quest-drop", () => network.SendQuestAction(quest.Code, QuestAction.Drop));
                break;
        }
        if (status is QuestStatus.Available or QuestStatus.Active or QuestStatus.Complete)
        {
            bool tracked = state?.Tracked == true;
            Button(tracked ? "quest-untrack" : "quest-track", () => network.SendQuestAction(quest.Code, QuestAction.Track));
        }
        if (status == QuestStatus.Locked)
        {
            var needs = quest.Requires.Select(r => library.Get(r)).Where(r => r != null && StatusOf(r!) != QuestStatus.Claimed).Select(r => Lang.Get(QuestLibrary.TitleKey(r!))).ToList();
            string why = quest.PerColony && snapshot.ColonyId.Length == 0
                ? Lang.Get("createacolony:questbook-needs-colony")
                : needs.Count > 0 ? Lang.Get("createacolony:questbook-needs", string.Join(", ", needs)) : "";
            if (why.Length > 0) composer.Text(why, GuiKit.Muted(), x, by - 24, width - 220);
        }
        else if (status == QuestStatus.Available && quest.Accept)
        {
            composer.Text(Lang.Get("createacolony:questbook-accept-hint"), GuiKit.Muted(), x, by - 24, width - 220);
        }
    }

    static double[] StatusColor(QuestStatus status) => status switch
    {
        QuestStatus.Locked => GuiKit.MutedColor,
        QuestStatus.Active => new[] { 0.5, 0.72, 1.0, 1.0 },
        QuestStatus.Complete => new[] { 1.0, 0.82, 0.25, 1.0 },
        QuestStatus.Claimed => GuiKit.GoodColor,
        _ => new[] { 1.0, 1.0, 1.0, 1.0 }
    };

    // ---- Words and pictures -----------------------------------------------------------------

    /// <summary>One objective in words, from its type: "Have 8 × Flint", "Build a Warehouse", "Have 3 settlers"...</summary>
    public static string ObjectiveText(ICoreClientAPI capi, QuestObjectiveDef o)
    {
        if (o.Text.Length > 0) return Lang.Get("createacolony:" + o.Text);
        int count = Math.Max(1, o.Count);
        switch (o.Type)
        {
            case ObjectiveTypes.Obtain:
            case ObjectiveTypes.Deliver:
            case ObjectiveTypes.Stock:
                return Lang.Get("createacolony:objective-" + o.Type, count, ItemName(capi, o.Code));
            case ObjectiveTypes.Building:
                return o.Level > 1
                    ? Lang.Get("createacolony:objective-building-level", Lang.Get("createacolony:building-" + o.Code), o.Level)
                    : Lang.Get("createacolony:objective-building", Lang.Get("createacolony:building-" + o.Code));
            case ObjectiveTypes.Buildings:
                return o.Code.Length > 0
                    ? Lang.Get("createacolony:objective-buildings-of", count, Lang.Get("createacolony:building-" + o.Code))
                    : Lang.Get("createacolony:objective-buildings", count);
            case ObjectiveTypes.Job:
                return Lang.Get("createacolony:objective-job", count, Lang.Get("createacolony:job-" + o.Code));
            case ObjectiveTypes.Stat:
                return Lang.Get("createacolony:objective-stat", count, GuiDialogHut.StatLabel(o.Code));
            case ObjectiveTypes.Event:
                return Lang.Get("createacolony:objective-event", count, Lang.Get("createacolony:" + o.Code));
            default:
                return Lang.Get("createacolony:objective-" + o.Type, count);
        }
    }

    /// <summary>A readable name for an item code or wildcard, from the first matching item.</summary>
    static string ItemName(ICoreClientAPI capi, string code)
    {
        var stack = QuestItems.Sample(capi.World, code);
        if (stack != null) return stack.GetName();
        // Fall back to the code's last part, with the wildcards taken out.
        string tail = code.Contains(':') ? code[(code.IndexOf(':') + 1)..] : code;
        return tail.Replace("*", "").Replace("-", " ").Trim();
    }

    Dictionary<string, ItemStack?> IconsFor(List<QuestDef> quests)
    {
        foreach (var q in quests)
        {
            if (icons.ContainsKey(q.Code)) continue;
            ItemStack? stack = q.Icon.Length > 0 ? QuestItems.Sample(capi.World, q.Icon) : null;
            if (stack == null)
            {
                var first = q.Objectives.FirstOrDefault(o => o.Type is ObjectiveTypes.Obtain or ObjectiveTypes.Deliver or ObjectiveTypes.Stock);
                if (first != null) stack = QuestItems.Sample(capi.World, first.Code);
            }
            stack ??= QuestItems.Sample(capi.World, "game:paper-parchment");
            icons[q.Code] = stack;
        }
        return icons;
    }

    public override void OnGuiClosed()
    {
        network.SendQuestAction("", QuestAction.CloseWindow);
        base.OnGuiClosed();
    }
}
