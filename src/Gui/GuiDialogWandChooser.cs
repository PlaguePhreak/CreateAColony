using System.Linq;
using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// The Builder's Wand building list: pick what to build at the spot that was right-clicked. The tabs along
/// the top pick the blueprint style, the tabs down the side the kind of building.
/// </summary>
public class GuiDialogWandChooser : GuiDialog
{
    const double Width = 380;
    const double ButtonWidth = 180;
    const double RowHeight = 36;
    const double TabWidth = 180;
    const double TabsHeight = 8 * 30;
    /// <summary>Content ends level with the last side tab (the content starts below the dialog padding).</summary>
    const double MinContentHeight = 35 + TabsHeight - 25;

    /// <summary>The tabs shown last, so the list opens where the player left it.</summary>
    static int currentTab;
    static int currentStyle;

    readonly ColonyNetwork network;
    readonly WandChooserSnapshot snapshot;

    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogWandChooser(ICoreClientAPI capi, ColonyNetwork network, WandChooserSnapshot snapshot) : base(capi)
    {
        this.network = network;
        this.snapshot = snapshot;
        if (currentTab < 0 || currentTab >= BuildingCategories.All.Length) currentTab = 0;
        int planned = System.Array.IndexOf(BlueprintStyles.All, snapshot.Style);
        if (planned >= 0) currentStyle = planned;
        if (currentStyle < 0 || currentStyle >= BlueprintStyles.All.Length) currentStyle = 0;
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
        }).ToArray();

        var styleTabs = BlueprintStyles.All.Select((s, i) => new GuiTab { Name = Lang.Get("createacolony:style-" + s), DataInt = i }).ToArray();
        var styleFont = CairoFont.WhiteSmallText();

        var composer = capi.Gui.CreateCompo("createacolony-wandchooser", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("createacolony:wand-choose-title"), () => TryClose())
            .AddHorizontalTabs(styleTabs, ElementBounds.Fixed(0, -26, Width, 27), id => { currentStyle = id; Compose(); },
                styleFont, styleFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "styles")
            .AddVerticalTabs(tabs, ElementBounds.Fixed(-TabWidth, 35, TabWidth, TabsHeight), (id, tab) => { currentTab = id; Compose(); }, "tabs")
            .BeginChildElements(bgBounds);

        string style = BlueprintStyles.All[currentStyle];
        var category = BuildingCategories.All[currentTab];
        double y = 30;
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
            composer.AddStaticText(Lang.Get(hasBlueprint ? "createacolony:wand-row-blueprint" : "createacolony:wand-row-no-blueprint"),
                hasBlueprint ? GuiKit.Muted() : GuiKit.Detail().WithColor(GuiKit.WarnColor), ElementBounds.Fixed(info, row + 18, Width - info - 6, 17));
            row += RowHeight;
        }
        y += listHeight + 8;

        foreach (var noteKey in new[] { "createacolony:wand-style-note-" + style, "createacolony:wand-tab-note-" + category.Code })
        {
            if (!Lang.HasTranslation(noteKey, false, false)) continue;
            y = composer.Text(Lang.Get(noteKey), GuiKit.Muted(), 0, y, Width, 8);
        }

        // Keep the window as tall as the tabs beside it, and no taller.
        if (y < MinContentHeight) composer.AddStaticText("", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, MinContentHeight - y));

        SingleComposer = composer.EndChildElements().Compose();
        SingleComposer.GetHorizontalTabs("styles").activeElement = currentStyle;
    }

    bool Choose(string type, string style)
    {
        network.SendAction(new ColonyActionPacket { Action = ColonyAction.WandChoose, Text = type + "|" + style });
        TryClose();
        return true;
    }
}
