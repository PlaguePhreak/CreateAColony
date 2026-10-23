using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>A small "Quests" button shown at the top of the screen while the player's inventory is open.</summary>
public class GuiDialogQuestButton : GuiDialog
{
    readonly ColonyNetwork network;

    public override string? ToggleKeyCombinationCode => null;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => false;
    public override double DrawOrder => 0.2;

    public GuiDialogQuestButton(ICoreClientAPI capi, ColonyNetwork network) : base(capi)
    {
        this.network = network;
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterTop).WithFixedAlignmentOffset(0, 12);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(6);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        SingleComposer = capi.Gui.CreateCompo("createacolony-questbutton", dialogBounds)
            .AddShadedDialogBG(bgBounds, false)
            .BeginChildElements(bgBounds)
                .AddSmallButton(Lang.Get("createacolony:questbook-open"), () => { network.RequestQuestBook(); return true; }, ElementBounds.Fixed(0, 0, 130, 28))
                .AddHoverText(Lang.Get("createacolony:questbook-open-hint"), GuiKit.Detail(), 220, ElementBounds.Fixed(0, 0, 130, 28))
            .EndChildElements()
            .Compose();
    }
}
