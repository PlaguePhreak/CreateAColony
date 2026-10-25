using System.Linq;
using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// The Builder's Wand building list: pick what to build at the spot that was right-clicked. The tabs down
/// the side pick the kind of building. As in MineColonies, the player first picks an asset pack (a set of
/// blueprints in one look), which every building they place uses; the last tab, Change pack, picks another.
/// </summary>
public class GuiDialogWandChooser : GuiDialog
{
    const double Width = 420;
    const double ButtonWidth = 180;
    const double RowHeight = 36;
    const double TabWidth = 180;
    const double TabsHeight = 10 * 30;
    /// <summary>Content ends level with the last side tab (the content starts below the dialog padding).</summary>
    const double MinContentHeight = 35 + TabsHeight - 25;

    /// <summary>The tab shown last, so the list opens where the player left it.</summary>
    static int currentTab;
    /// <summary>The sub-tab shown last on each tab that has them (such as Craftsmanship).</summary>
    static readonly System.Collections.Generic.Dictionary<string, int> currentSub = new();
    /// <summary>The Change pack tab, after the building tabs.</summary>
    static int PackTab => BuildingCategories.All.Length;

    /// <summary>The sub-tab to mark once the window is composed, or -1 for none.</summary>
    int shownSub = -1;

    readonly ColonyNetwork network;
    readonly WandChooserSnapshot snapshot;

    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogWandChooser(ICoreClientAPI capi, ColonyNetwork network, WandChooserSnapshot snapshot) : base(capi)
    {
        this.network = network;
        this.snapshot = snapshot;
        if (currentTab < 0 || currentTab >= PackTab) currentTab = 0;
        // No pack yet: the list asks for one first.
        if (snapshot.Pack == "") currentTab = PackTab;
        Compose();
    }

    void Compose()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        var tabs = BuildingCategories.All.Select((c, i) => new GuiTab
        {
            Name = Lang.Get("createacolony:wand-tab-" + c.Code),
            DataInt = i,
            Active = i == currentTab
        }).Append(new GuiTab { Name = Lang.Get("createacolony:wand-tab-pack"), DataInt = PackTab, Active = currentTab == PackTab }).ToArray();

        var composer = capi.Gui.CreateCompo("createacolony-wandchooser", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("createacolony:wand-choose-title"), () => TryClose())
            .AddVerticalTabs(tabs, ElementBounds.Fixed(-TabWidth, 35, TabWidth, TabsHeight), (id, tab) => PickTab(id), "tabs")
            .BeginChildElements(bgBounds);

        shownSub = -1;
        double y = currentTab == PackTab ? ComposePacks(composer) : ComposeBuildings(composer);

        // Keep the window as tall as the tabs beside it, and no taller.
        if (y < MinContentHeight) composer.AddStaticText("", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, MinContentHeight - y));

        SingleComposer = composer.EndChildElements().Compose();
        if (shownSub >= 0) SingleComposer.GetHorizontalTabs("subtabs").SetValue(shownSub, false);
    }

    void PickTab(int id)
    {
        // Until a pack is picked there is nothing to list.
        currentTab = snapshot.Pack == "" ? PackTab : id;
        Compose();
    }

    /// <summary>The buildings on the current tab, in the player's pack. Returns the height used.</summary>
    double ComposeBuildings(GuiComposer composer)
    {
        string style = snapshot.Pack;
        var category = BuildingCategories.All[currentTab];
        double y = 30;
        if (category.Subs != null)
        {
            // Sub-tabs along the top, such as Wood working and Metallurgy on Craftsmanship.
            var group = category;
            currentSub.TryGetValue(group.Code, out int sub);
            if (sub < 0 || sub >= group.Subs.Length) sub = 0;
            var subTabs = group.Subs.Select((c, i) => new GuiTab { Name = Lang.Get("createacolony:wand-tab-" + c.Code), DataInt = i }).ToArray();
            var tabFont = CairoFont.WhiteDetailText().WithFontSize(13);
            composer.AddHorizontalTabs(subTabs, ElementBounds.Fixed(0, y, Width, 25), id => { currentSub[group.Code] = id; Compose(); }, tabFont,
                tabFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "subtabs");
            shownSub = sub;
            category = group.Subs[sub];
            y += 32;
        }
        composer.AddStaticText(Lang.Get("createacolony:wand-tab-heading", Lang.Get("createacolony:style-" + style), Lang.Get("createacolony:wand-tab-" + category.Code)),
            GuiKit.Heading(), ElementBounds.Fixed(0, y, Width, 25));
        y += 30;
        y = composer.Text(Lang.Get("createacolony:wand-choose-hint"), GuiKit.Muted(), 0, y, Width, 8);

        // The buildings, one row each, in a sunken box: name button on the left, blocks carried and blueprint state on the right.
        var types = category.Types.Where(t => snapshot.Types.Contains(t)).ToList();
        double listHeight = types.Count == 0 ? 52 : types.Count * RowHeight + 8;
        composer.AddInset(ElementBounds.Fixed(0, y, Width, listHeight), 3, 0.8f);
        if (types.Count == 0)
        {
            composer.Text(Lang.Get("createacolony:wand-tab-empty"), GuiKit.Muted(), 10, y + 9, Width - 20);
        }
        double row = y + 6;
        foreach (var type in types)
        {
            int have = snapshot.Have[snapshot.Types.IndexOf(type)];
            bool hasBlueprint = snapshot.WithBlueprints.Contains(type + "|" + style);
            composer.AddSmallButton(Lang.Get("createacolony:building-" + type), () => Choose(type, style), ElementBounds.Fixed(6, row + 3, ButtonWidth, 28));
            double info = 6 + ButtonWidth + 12;
            composer.AddStaticText(Lang.Get("createacolony:wand-row-have", have),
                GuiKit.Detail().WithColor(have > 0 ? GuiKit.GoodColor : GuiStyle.DialogDefaultTextColor), ElementBounds.Fixed(info, row + 1, Width - info - 6, 17));
            string? variants = snapshot.VariantCounts.FirstOrDefault(v => v.StartsWith(type + "|" + style + "|"));
            string blueprintText = !hasBlueprint ? Lang.Get("createacolony:wand-row-no-blueprint")
                : variants != null ? Lang.Get("createacolony:wand-row-variants", variants.Split('|')[2])
                : Lang.Get("createacolony:wand-row-blueprint");
            composer.AddStaticText(blueprintText,
                hasBlueprint ? GuiKit.Muted() : GuiKit.Detail().WithColor(GuiKit.WarnColor), ElementBounds.Fixed(info, row + 18, Width - info - 6, 17));
            row += RowHeight;
        }
        y += listHeight + 8;

        string noteKey = "createacolony:wand-tab-note-" + category.Code;
        if (Lang.HasTranslation(noteKey, false, false)) y = composer.Text(Lang.Get(noteKey), GuiKit.Muted(), 0, y, Width, 8);
        return y;
    }

    /// <summary>The asset packs to pick from, the one in use marked. Returns the height used.</summary>
    double ComposePacks(GuiComposer composer)
    {
        double y = 30;
        composer.AddStaticText(Lang.Get("createacolony:wand-pack-heading"), GuiKit.Heading(), ElementBounds.Fixed(0, y, Width, 25));
        y += 30;
        y = composer.Text(Lang.Get(snapshot.Pack == "" ? "createacolony:wand-pack-first" : "createacolony:wand-pack-hint"), GuiKit.Muted(), 0, y, Width, 8);

        // Each pack: its button and how many buildings it has, then its description below.
        var packs = BlueprintStyles.All;
        double textWidth = Width - 24;
        var heights = packs.Select(p => 40 + GuiKit.TextHeight(capi, GuiKit.Muted(), Lang.Get("createacolony:pack-desc-" + p), textWidth) + 8).ToArray();
        double listHeight = heights.Sum() + 8;
        composer.AddInset(ElementBounds.Fixed(0, y, Width, listHeight), 3, 0.8f);
        double row = y + 6;
        int buildings = snapshot.Types.Count;
        for (int i = 0; i < packs.Length; i++)
        {
            string pack = packs[i];
            int with = snapshot.Types.Count(t => snapshot.WithBlueprints.Contains(t + "|" + pack));
            bool inUse = pack == snapshot.Pack;
            composer.AddSmallButton(Lang.Get("createacolony:style-" + pack), () => ChoosePack(pack), ElementBounds.Fixed(6, row + 3, ButtonWidth, 28));
            double info = 6 + ButtonWidth + 12;
            composer.AddStaticText(Lang.Get(inUse ? "createacolony:wand-pack-in-use" : "createacolony:wand-pack-buildings", with, buildings),
                GuiKit.Detail().WithColor(inUse ? GuiKit.GoodColor : GuiStyle.DialogDefaultTextColor), ElementBounds.Fixed(info, row + 8, Width - info - 6, 17));
            composer.Text(Lang.Get("createacolony:pack-desc-" + pack), GuiKit.Muted(), 12, row + 38, textWidth);
            row += heights[i];
        }
        return y + listHeight + 8;
    }

    bool ChoosePack(string pack)
    {
        currentTab = 0;
        network.SendAction(new ColonyActionPacket { Action = ColonyAction.WandPack, Text = pack });
        return true;
    }

    bool Choose(string type, string style)
    {
        network.SendAction(new ColonyActionPacket { Action = ColonyAction.WandChoose, Text = type + "|" + style });
        TryClose();
        return true;
    }
}
