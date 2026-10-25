using CreateAColony.Map;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// Opens on a right-click on a Colony markers map marker: Remove takes that one marker off the map, and the
/// settlers leave that spot alone after (ColonyManager.RemoveMarker).
/// </summary>
public class GuiDialogRemoveMarker : GuiDialog
{
    const double Width = 300;

    readonly ColonyNetwork network;
    readonly ColonyMarkerView marker;

    public override string? ToggleKeyCombinationCode => null;

    // Drawn above the open world map, and keeping the mouse while open, as the game's Edit waypoint window does.
    public override double DrawOrder => 0.2;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    public override bool CaptureAllInputs() => IsOpened();

    public override void OnMouseDown(MouseEvent args)
    {
        base.OnMouseDown(args);
        args.Handled = true;
    }

    public override void OnMouseUp(MouseEvent args)
    {
        base.OnMouseUp(args);
        args.Handled = true;
    }

    public override void OnMouseMove(MouseEvent args)
    {
        base.OnMouseMove(args);
        args.Handled = true;
    }

    public GuiDialogRemoveMarker(ICoreClientAPI capi, ColonyNetwork network, ColonyMarkerView marker) : base(capi)
    {
        this.network = network;
        this.marker = marker;
        Compose();
    }

    void Compose()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        var composer = capi.Gui.CreateCompo("createacolony-removemarker", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(marker.Title, () => TryClose())
            .BeginChildElements(bgBounds);

        string hint = Lang.Get(marker.FromHut ? "createacolony:marker-remove-hint" : "createacolony:marker-remove-hint-grave");
        double y = composer.Text(hint, GuiKit.Detail(), 0, 30, Width, 10);
        composer
            .AddSmallButton(Lang.Get("createacolony:cancel"), () => TryClose(), ElementBounds.Fixed(0, y, 120, 28))
            .AddSmallButton(Lang.Get("createacolony:marker-remove"), Remove, ElementBounds.Fixed(Width - 120, y, 120, 28));

        SingleComposer = composer.EndChildElements().Compose();
    }

    bool Remove()
    {
        network.SendAction(new ColonyActionPacket { ColonyId = marker.ColonyId, Action = ColonyAction.RemoveMarker, Text = marker.MarkerId.ToString() });
        TryClose();
        return true;
    }
}
