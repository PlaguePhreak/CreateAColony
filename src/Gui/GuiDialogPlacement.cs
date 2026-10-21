using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace CreateAColony.Gui;

/// <summary>
/// The placement window shown beside a building's outline: turn it, nudge it forward, back, left,
/// right, up or down, order it built here, or take the hut back. Esc closes the window but leaves the
/// outline up, so the player can walk around it; right-clicking with the Builder's Wand brings the
/// window back.
/// </summary>
public class GuiDialogPlacement : GuiDialog
{
    const double Width = 320;

    readonly ColonyNetwork network;
    PlacementSnapshot snapshot;

    public int BuildingId => snapshot.BuildingId;

    /// <summary>Set when another building's window takes this one's place.</summary>
    public bool Replaced;

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
        composer.AddStaticText(Lang.Get("createacolony:placement-state", snapshot.Rotation, Signed(snapshot.YOffset), Signed(snapshot.XOffset), Signed(snapshot.ZOffset)),
            CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, Width, 25));
        y += 32;

        if (snapshot.CanAdjust)
        {
            AddPair(composer, "turn-left", ColonyAction.PlacementTurn, "-1", "turn-right", ColonyAction.PlacementTurn, "1", y);
            y += 34;
            AddPair(composer, "forward", ColonyAction.PlacementNudge, "forward", "back", ColonyAction.PlacementNudge, "back", y);
            y += 34;
            AddPair(composer, "left", ColonyAction.PlacementNudge, "left", "right", ColonyAction.PlacementNudge, "right", y);
            y += 34;
            AddPair(composer, "lower", ColonyAction.PlacementHeight, "-1", "raise", ColonyAction.PlacementHeight, "1", y);
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
        y += 34;
        composer.AddSmallButton(Lang.Get("createacolony:placement-hide"), () => Finish(ColonyAction.PlacementClose), ElementBounds.Fixed(0, y, 150, 28));
        y += 36;
        composer.AddStaticText(Lang.Get("createacolony:placement-hint"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, Width, 80));

        SingleComposer = composer.EndChildElements().Compose();
    }

    static string Signed(int n) => n > 0 ? "+" + n : n.ToString();

    void AddPair(GuiComposer composer, string leftKey, ColonyAction leftAction, string leftText, string rightKey, ColonyAction rightAction, string rightText, double y)
    {
        composer
            .AddSmallButton(Lang.Get("createacolony:placement-" + leftKey), () => Send(leftAction, leftText), ElementBounds.Fixed(0, y, 150, 28))
            .AddSmallButton(Lang.Get("createacolony:placement-" + rightKey), () => Send(rightAction, rightText), ElementBounds.Fixed(160, y, 150, 28));
    }

    bool Send(ColonyAction action, string text)
    {
        network.SendAction(new ColonyActionPacket { ColonyId = snapshot.ColonyId, Action = action, BuildingId = snapshot.BuildingId, Text = text });
        return true;
    }

    bool Finish(ColonyAction action)
    {
        Send(action, "");
        network.OutlineCleared();
        TryClose();
        return true;
    }
}
