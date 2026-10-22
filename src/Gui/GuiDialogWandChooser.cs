using System.Linq;
using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>The Builder's Wand building list: pick what to build at the spot that was right-clicked, one tab per kind of building.</summary>
public class GuiDialogWandChooser : GuiDialog
{
    const double Width = 320;
    const double TabWidth = 170;
    const double TabsHeight = 8 * 30 + 10;

    /// <summary>The tab shown last, so the list opens where the player left it.</summary>
    static int currentTab;

    readonly ColonyNetwork network;
    readonly WandChooserSnapshot snapshot;

    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogWandChooser(ICoreClientAPI capi, ColonyNetwork network, WandChooserSnapshot snapshot) : base(capi)
    {
        this.network = network;
        this.snapshot = snapshot;
        if (currentTab < 0 || currentTab >= BuildingCategories.All.Length) currentTab = 0;
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

        var composer = capi.Gui.CreateCompo("createacolony-wandchooser", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("createacolony:wand-choose-title"), () => TryClose())
            .AddVerticalTabs(tabs, ElementBounds.Fixed(-TabWidth, 35, TabWidth, TabsHeight), (id, tab) => { currentTab = id; Compose(); }, "tabs")
            .BeginChildElements(bgBounds);

        var category = BuildingCategories.All[currentTab];
        double y = 30;
        composer.AddStaticText(Lang.Get("createacolony:wand-tab-" + category.Code), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, y, Width, 25));
        y += 28;
        composer.AddStaticText(Lang.Get("createacolony:wand-choose-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 40));
        y += 44;

        int shown = 0;
        foreach (var type in category.Types)
        {
            int i = snapshot.Types.IndexOf(type);
            if (i < 0) continue;
            string label = Lang.Get("createacolony:wand-choose-entry", Lang.Get("createacolony:building-" + type), snapshot.Have[i]);
            composer.AddSmallButton(label, () => Choose(type), ElementBounds.Fixed(0, y, Width, 28));
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
    }

    bool Choose(string type)
    {
        network.SendAction(new ColonyActionPacket { Action = ColonyAction.WandChoose, Text = type });
        TryClose();
        return true;
    }
}
