using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// The placement window shown beside a building's outline: turn it, raise or lower it, order it
/// built here, or take the hut back. It sits at the side of the screen so the outline stays visible.
/// </summary>
public class GuiDialogPlacement : GuiDialog
{
    const double Width = 320;

    readonly ColonyNetwork network;
    PlacementSnapshot snapshot;

    public int BuildingId => snapshot.BuildingId;

    /// <summary>Set when another building's window takes this one's place, so closing it doesn't clear the new outline.</summary>
    public bool Replaced;

    /// <summary>Set after Build here or Not here, which clear the outline on the server themselves.</summary>
    bool done;

    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogPlacement(ICoreClientAPI capi, ColonyNetwork network, PlacementSnapshot snapshot) : base(capi)
    {
        this.network = network;
        this.snapshot = snapshot;
        Compose();
    }

    public void Update(PlacementSnapshot fresh)
    {
        snapshot = fresh;
        Compose();
    }

    void Compose()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        string name = Lang.Get("createacolony:building-" + snapshot.Type);
        var composer = capi.Gui.CreateCompo("createacolony-placement", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("createacolony:placement-title", name), () => TryClose())
            .BeginChildElements(bgBounds);

        double y = 30;
        string shown = snapshot.ShownLevel == 0
            ? Lang.Get("createacolony:placement-no-blueprint")
            : Lang.Get("createacolony:placement-shown", snapshot.ShownLevel, snapshot.Blocked, snapshot.Blocks);
        composer.AddStaticText(shown, CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 60));
        y += 64;
        string offset = snapshot.YOffset == 0 ? "0" : snapshot.YOffset > 0 ? "+" + snapshot.YOffset : snapshot.YOffset.ToString();
        composer.AddStaticText(Lang.Get("createacolony:placement-state", snapshot.Rotation, offset), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, Width, 25));
        y += 32;

        if (snapshot.CanAdjust)
        {
            composer
                .AddSmallButton(Lang.Get("createacolony:placement-turn-left"), () => Send(ColonyAction.PlacementTurn, "-1"), ElementBounds.Fixed(0, y, 150, 28))
                .AddSmallButton(Lang.Get("createacolony:placement-turn-right"), () => Send(ColonyAction.PlacementTurn, "1"), ElementBounds.Fixed(160, y, 150, 28));
            y += 34;
            composer
                .AddSmallButton(Lang.Get("createacolony:placement-lower"), () => Send(ColonyAction.PlacementHeight, "-1"), ElementBounds.Fixed(0, y, 150, 28))
                .AddSmallButton(Lang.Get("createacolony:placement-raise"), () => Send(ColonyAction.PlacementHeight, "1"), ElementBounds.Fixed(160, y, 150, 28));
            y += 44;
        }
        else
        {
            composer.AddStaticText(Lang.Get("createacolony:placement-fixed"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 40));
            y += 44;
        }

        if (snapshot.CanOrder)
        {
            composer.AddSmallButton(Lang.Get("createacolony:placement-confirm"), () => Finish(ColonyAction.PlacementConfirm), ElementBounds.Fixed(0, y, 150, 28));
        }
        if (snapshot.CanAdjust && snapshot.Type is not ("townhall" or "warehouse"))
        {
            composer.AddSmallButton(Lang.Get("createacolony:placement-cancel"), () => Finish(ColonyAction.PlacementCancel), ElementBounds.Fixed(160, y, 150, 28));
        }
        y += 36;
        composer.AddStaticText(Lang.Get("createacolony:placement-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 60));

        SingleComposer = composer.EndChildElements().Compose();
    }

    bool Send(ColonyAction action, string text)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = snapshot.ColonyId, Action = action, BuildingId = snapshot.BuildingId, Text = text });
        return true;
    }

    bool Finish(ColonyAction action)
    {
        Send(action, "");
        done = true;
        TryClose();
        return true;
    }

    public override void OnGuiClosed()
    {
        if (!Replaced && !done) Send(ColonyAction.PlacementClose, "");
        base.OnGuiClosed();
    }
}
