using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Map;

/// <summary>
/// Makes the icon grid (and the colours above it) in the game's Add waypoint and Edit waypoint windows wider. The game lays the icons out
/// 10 to a row, which with our 120 icons (and More Map Icons' too) runs off the bottom of the screen. This
/// picks enough columns that the grid is about twice as wide as it is tall, as far as the screen allows.
/// </summary>
public class WaypointIconGrid : ModSystem
{
    const string HarmonyId = "createacolony.waypointicons";
    /// <summary>One icon in the grid: 27 pixels and 5 between (GuiDialogAddWayPoint, AddElementListPicker).</summary>
    const int Cell = 32;

    static ICoreClientAPI? capi;
    Harmony? harmony;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        try
        {
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(GuiComposerHelpers), nameof(GuiComposerHelpers.AddIconListPicker))
                    ?? throw new MissingMethodException(nameof(GuiComposerHelpers), nameof(GuiComposerHelpers.AddIconListPicker)),
                prefix: new HarmonyMethod(typeof(WaypointIconGrid), nameof(Wider)));
            harmony.Patch(AccessTools.Method(typeof(GuiComposerHelpers), nameof(GuiComposerHelpers.AddColorListPicker))
                    ?? throw new MissingMethodException(nameof(GuiComposerHelpers), nameof(GuiComposerHelpers.AddColorListPicker)),
                prefix: new HarmonyMethod(typeof(WaypointIconGrid), nameof(WiderColors)));
        }
        catch (Exception e)
        {
            Mod.Logger.Warning("Could not widen the waypoint icon grid, it stays as the game has it: {0}", e.Message);
        }
    }

    /// <summary>The waypoint windows call their picker "iconPicker" (Add) and "iconpicker" (Edit).</summary>
    static void Wider(string[] icons, string key, ref int maxLineWidth)
    {
        if (capi == null || icons == null || !string.Equals(key, "iconpicker", StringComparison.OrdinalIgnoreCase)) return;
        // The game starts a new row once the icons so far are wider than maxLineWidth.
        maxLineWidth = Math.Max(maxLineWidth, Columns(icons.Length) * Cell - 1);
    }

    /// <summary>The colours above the icons ("colorPicker", "colorpicker") take the same width as the icons.</summary>
    static void WiderColors(string key, ref int maxLineWidth)
    {
        if (capi == null || !string.Equals(key, "colorpicker", StringComparison.OrdinalIgnoreCase)) return;
        int icons = capi.ModLoader.GetModSystem<Vintagestory.GameContent.WorldMapManager>()?.MapLayers
            .OfType<Vintagestory.GameContent.WaypointMapLayer>().FirstOrDefault()?.WaypointIcons.Count ?? 0;
        if (icons == 0) return;
        maxLineWidth = Math.Max(maxLineWidth, Columns(icons) * Cell - 1);
    }

    static int Columns(int icons)
    {
        double screenWidth = capi!.Render.FrameWidth / Math.Max(0.5, RuntimeEnv.GUIScale);
        int maxColumns = Math.Max(10, (int)((screenWidth - 120) / Cell));
        return Math.Clamp((int)Math.Ceiling(Math.Sqrt(icons * 2.0)), 10, maxColumns);
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        capi = null;
    }
}
