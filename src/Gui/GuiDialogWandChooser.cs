using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>The Builder's Wand building list: pick what to build at the spot that was right-clicked.</summary>
public class GuiDialogWandChooser : GuiDialog
{
    const double Width = 320;

    readonly ColonyNetwork network;
    readonly WandChooserSnapshot snapshot;

    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogWandChooser(ICoreClientAPI capi, ColonyNetwork network, WandChooserSnapshot snapshot) : base(capi)
    {
        this.network = network;
        this.snapshot = snapshot;
        Compose();
    }

    void Compose()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        var composer = capi.Gui.CreateCompo("createacolony-wandchooser", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("createacolony:wand-choose-title"), () => TryClose())
            .BeginChildElements(bgBounds);

        double y = 30;
        composer.AddStaticText(Lang.Get("createacolony:wand-choose-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 40));
        y += 44;
        for (int i = 0; i < snapshot.Types.Count; i++)
        {
            string type = snapshot.Types[i];
            string label = Lang.Get("createacolony:wand-choose-entry", Lang.Get("createacolony:building-" + type), snapshot.Have[i]);
            composer.AddSmallButton(label, () => Choose(type), ElementBounds.Fixed(0, y, Width, 28));
            y += 34;
        }

        SingleComposer = composer.EndChildElements().Compose();
    }

    bool Choose(string type)
    {
        network.SendAction(new ColonyActionPacket { Action = ColonyAction.WandChoose, Text = type });
        TryClose();
        return true;
    }
}
