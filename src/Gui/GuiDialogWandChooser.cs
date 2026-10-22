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
    const double Width = 320;
    const double TabWidth = 170;
    const double TabsHeight = 8 * 30 + 10;

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
        var styleFont = CairoFont.WhiteDetailText();

        var composer = capi.Gui.CreateCompo("createacolony-wandchooser", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("createacolony:wand-choose-title"), () => TryClose())
            .AddHorizontalTabs(styleTabs, ElementBounds.Fixed(0, -24, Width, 25), id => { currentStyle = id; Compose(); },
                styleFont, styleFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "styles")
            .AddVerticalTabs(tabs, ElementBounds.Fixed(-TabWidth, 35, TabWidth, TabsHeight), (id, tab) => { currentTab = id; Compose(); }, "tabs")
            .BeginChildElements(bgBounds);

        string style = BlueprintStyles.All[currentStyle];
        var category = BuildingCategories.All[currentTab];
        double y = 30;
        composer.AddStaticText(Lang.Get("createacolony:wand-tab-heading", Lang.Get("createacolony:style-" + style), Lang.Get("createacolony:wand-tab-" + category.Code)),
            CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, y, Width, 25));
        y += 28;
        composer.AddStaticText(Lang.Get("createacolony:wand-choose-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 40));
        y += 44;

        int shown = 0;
        foreach (var type in category.Types)
        {
            int i = snapshot.Types.IndexOf(type);
            if (i < 0) continue;
            string label = Lang.Get(snapshot.WithBlueprints.Contains(type + "|" + style) ? "createacolony:wand-choose-entry" : "createacolony:wand-choose-entry-none",
                Lang.Get("createacolony:building-" + type), snapshot.Have[i]);
            composer.AddSmallButton(label, () => Choose(type, style), ElementBounds.Fixed(0, y, Width, 28));
            y += 34;
            shown++;
        }
        if (shown == 0)
        {
            composer.AddStaticText(Lang.Get("createacolony:wand-tab-empty"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 40));
            y += 44;
        }

        string noteKey = "createacolony:wand-tab-note-" + category.Code;
        if (Lang.HasTranslation(noteKey, false, false))
        {
            composer.AddStaticText(Lang.Get(noteKey), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y + 6, Width, 60));
            y += 66;
        }

        // Keep the window at least as tall as the tabs beside it.
        if (y < 35 + TabsHeight) composer.AddStaticText("", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 35 + TabsHeight - y));

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
