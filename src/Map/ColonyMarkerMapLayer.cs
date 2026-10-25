using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CreateAColony.Colonies;
using CreateAColony.Gui;
using CreateAColony.Network;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CreateAColony.Map;

/// <summary>One colony marker as the map layer gets it.</summary>
[ProtoContract]
public class ColonyMarkerView
{
    [ProtoMember(1)] public double X;
    [ProtoMember(2)] public double Y;
    [ProtoMember(3)] public double Z;
    [ProtoMember(4)] public string Title = "";
    /// <summary>The waypoint icon, one of Vintage Colonies' own (vc-..., some made as the game starts: MarkerSubjects).</summary>
    [ProtoMember(5)] public string Icon = "";
    /// <summary>The game's own icon, if the first is missing.</summary>
    [ProtoMember(6)] public string Fallback = "circle";
    [ProtoMember(7)] public int Color;
    [ProtoMember(8)] public string ColonyName = "";
    [ProtoMember(9)] public int MarkerId;
    [ProtoMember(10)] public string ColonyId = "";
    /// <summary>The player is an officer of the colony, so they may take the marker off the map.</summary>
    [ProtoMember(11)] public bool CanRemove;
    /// <summary>A hut's worker put it down, so removing it also stops them marking that spot again.</summary>
    [ProtoMember(12)] public bool FromHut;
}

/// <summary>
/// The "Colony markers" layer of the world map: what the colony's Explorers and Gatherers found, and where
/// settlers fell (ColonyManager.Markers.cs). It draws them like waypoints, with the waypoint icons the player's
/// game loaded: Vintage Colonies' own vc- icons, or the game's own as a fallback. They are the colony's, so
/// they cannot be edited from the map, but an officer can right-click one to take it off (GuiDialogRemoveMarker);
/// a hut's window can take all its markers away.
/// </summary>
public class ColonyMarkerMapLayer : MarkerMapLayer
{
    public const string Code = "colonymarkers";

    /// <summary>
    /// Colours the Add waypoint window offers on top of the game's: two More Map Icons suggests names for
    /// (Olivine, Emerald, Brown Coal, Peat) that the game's own list leaves out, and 36 more.
    /// </summary>
    static readonly string[] ExtraWaypointColors =
    {
        "#03D941", "#885530",
        // More colours: pale ones, then deep ones.
        "#FFCDD2", "#F8BBD0", "#E1BEE7", "#D1C4E9", "#C5CAE9", "#BBDEFB", "#B3E5FC", "#B2EBF2", "#B2DFDB",
        "#C8E6C9", "#DCEDC8", "#F0F4C3", "#FFF9C4", "#FFECB3", "#FFE0B2", "#FFCCBC", "#D7CCC8", "#CFD8DC",
        "#B71C1C", "#880E4F", "#4A148C", "#311B92", "#1A237E", "#0D47A1", "#01579B", "#006064", "#004D40",
        "#1B5E20", "#33691E", "#827717", "#F57F17", "#FF6F00", "#E65100", "#BF360C", "#3E2723", "#263238"
    };

    readonly List<ColonyMarkerView> markers = new();
    readonly Matrixf mvMat = new();
    Vec2f viewPosRef = new();
    int hovered = -1;

    public ColonyMarkerMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
    {
        // Every ore, crop, animal, trader and story location gets its waypoint icon once the game has loaded
        // them all, before the map is first opened.
        if (api is ICoreClientAPI capi) capi.Event.LevelFinalize += AddSubjectIcons;
    }

    bool subjectIconsAdded;

    void AddSubjectIcons()
    {
        if (subjectIconsAdded || WaypointLayer() is not { } wpLayer) return;
        subjectIconsAdded = true;
        AddColors(wpLayer);
        try
        {
            MarkerSubjects.RegisterIcons((ICoreClientAPI)api, wpLayer);
        }
        catch (Exception e)
        {
            api.Logger.Warning("[createacolony] Could not add the colony marker icons to the waypoint list: {0}", e);
        }
    }

    public override string Title => "Colony markers";
    public override string LayerGroupCode => Code;
    public override EnumMapAppSide DataSide => EnumMapAppSide.Server;
    public override bool RequireChunkLoaded => false;

    // ---- Server ----

    public override void OnViewChangedServer(IServerPlayer fromPlayer, int x1, int z1, int x2, int z2) => Resend(fromPlayer);

    [Obsolete("Kept because the base layer still has it")]
    public override void OnViewChangedServer(IServerPlayer fromPlayer, List<FastVec2i> nowVisible, List<FastVec2i> nowHidden) => Resend(fromPlayer);

    /// <summary>Sends the player the markers of every colony they belong to.</summary>
    public void Resend(IServerPlayer player)
    {
        var manager = api.ModLoader.GetModSystem<ColonyManager>();
        if (manager == null) return;
        var views = new List<ColonyMarkerView>();
        foreach (var colony in manager.All.Where(c => c.RoleOf(player.PlayerUID) >= ColonyRole.Member))
        {
            bool canRemove = colony.RoleOf(player.PlayerUID) >= ColonyRole.Officer;
            foreach (var marker in colony.Markers)
            {
                if (ColonyManager.KindOf(marker.Kind) is not { } kind) continue;
                views.Add(new ColonyMarkerView
                {
                    X = marker.X, Y = marker.Y, Z = marker.Z,
                    Title = marker.Title,
                    // The game names icons by their file name, which asset paths keep in lower case.
                    Icon = (marker.Icon.Length > 0 ? marker.Icon : kind.Icon).ToLowerInvariant(),
                    Fallback = kind.Fallback.ToLowerInvariant(),
                    // Solid black by default, so they stand out on the map (Aaron, 0.91.0); "" keeps each kind's colour.
                    Color = ColorUtil.Hex2Int(string.IsNullOrWhiteSpace(manager.Config.MapMarkerColor) ? kind.Color : manager.Config.MapMarkerColor),
                    ColonyName = colony.Name,
                    MarkerId = marker.MarkerId,
                    ColonyId = colony.ColonyId,
                    CanRemove = canRemove,
                    FromHut = marker.BuildingId != 0
                });
            }
        }
        mapSink.SendMapDataToClient(this, player, SerializerUtil.Serialize(views));
    }

    // ---- Client ----

    public override void OnDataFromServer(byte[] data)
    {
        markers.Clear();
        markers.AddRange(SerializerUtil.Deserialize<List<ColonyMarkerView>>(data) ?? new List<ColonyMarkerView>());
        hovered = -1;
    }

    public override void OnMapOpenedClient()
    {
        AddSubjectIcons();
        if (WaypointLayer() is { } wpLayer) AddColors(wpLayer);
    }

    /// <summary>The game's waypoint layer keeps the colour list the Add waypoint window offers.</summary>
    static void AddColors(WaypointMapLayer wpLayer)
    {
        foreach (string hex in ExtraWaypointColors)
        {
            int color = ColorUtil.Hex2Int(hex);
            if (!wpLayer.WaypointColors.Contains(color)) wpLayer.WaypointColors.Add(color);
        }
    }

    WaypointMapLayer? WaypointLayer() =>
        api.ModLoader.GetModSystem<WorldMapManager>()?.MapLayers.OfType<WaypointMapLayer>().FirstOrDefault();

    public override void Render(GuiElementMap mapElem, float dt)
    {
        if (!Active || markers.Count == 0) return;
        // The icons are the ones the game's waypoint layer loaded, ours among them.
        if (WaypointLayer() is not { texturesByIcon: { } textures, quadModel: { } quad }) return;
        var capi = (ICoreClientAPI)api;
        var prog = capi.Render.GetEngineShader(EnumShaderProgram.Gui);
        prog.Uniform("extraGlow", 0);
        prog.Uniform("applyColor", 0);
        prog.Uniform("noTexture", 0f);
        prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);

        for (int i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            mapElem.TranslateWorldPosToViewPos(new Vec3d(marker.X, marker.Y, marker.Z), ref viewPosRef);
            if (viewPosRef.X < -10 || viewPosRef.Y < -10 || viewPosRef.X > mapElem.Bounds.OuterWidth + 10 || viewPosRef.Y > mapElem.Bounds.OuterHeight + 10) continue;
            if (!textures.TryGetValue(marker.Icon, out var tex) && !textures.TryGetValue(marker.Fallback, out tex)) textures.TryGetValue("circle", out tex);
            if (tex == null) continue;

            float x = (float)(mapElem.Bounds.renderX + viewPosRef.X);
            float y = (float)(mapElem.Bounds.renderY + viewPosRef.Y);
            float hover = (hovered == i ? 6 : 0) - 1.5f * Math.Max(1, 1 / mapElem.ZoomLevel);
            float scaleX = (tex.Width + hover) * 0.6f * WaypointMapComponent.IconScale;
            float scaleY = (tex.Height + hover) * 0.6f * WaypointMapComponent.IconScale;
            prog.BindTexture2D("tex2d", tex.TextureId, 0);
            mvMat.Set(capi.Render.CurrentModelviewMatrix).Translate(x, y, 60).Scale(scaleX, scaleY, 0);

            // A light halo, then the icon fully opaque, so dark icons stand out on any ground.
            var color = new Vec4f();
            ColorUtil.ToRGBAVec4f(marker.Color, ref color);
            color.A = 1;
            bool dark = color.R + color.G + color.B < 1.5f;
            var halo = mvMat.Clone().Scale(1.3f, 1.3f, 1.3f);
            prog.Uniform("rgbaIn", dark ? new Vec4f(1, 1, 1, 0.9f) : new Vec4f(0, 0, 0, 0.8f));
            prog.UniformMatrix("modelViewMatrix", halo.Values);
            capi.Render.RenderMesh(quad);

            prog.Uniform("rgbaIn", color);
            prog.UniformMatrix("modelViewMatrix", mvMat.Values);
            capi.Render.RenderMesh(quad);
        }
    }

    GuiDialogRemoveMarker? removeDialog;

    /// <summary>Right-click on a marker: an officer gets a small window to take it off the map.</summary>
    public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem)
    {
        if (!Active || args.Button != EnumMouseButton.Right) return;
        int i = MarkerAt(args, mapElem);
        if (i < 0 || !markers[i].CanRemove) return;
        args.Handled = true;
        removeDialog?.TryClose();
        removeDialog?.Dispose();
        var capi = (ICoreClientAPI)api;
        removeDialog = new GuiDialogRemoveMarker(capi, capi.ModLoader.GetModSystem<ColonyNetwork>(), markers[i]);
        removeDialog.TryOpen();
    }

    /// <summary>The marker under the mouse, or -1.</summary>
    int MarkerAt(MouseEvent args, GuiElementMap mapElem)
    {
        double size = RuntimeEnv.GUIScale * 8;
        var pos = new Vec2f();
        for (int i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            mapElem.TranslateWorldPosToViewPos(new Vec3d(marker.X, marker.Y, marker.Z), ref pos);
            double dx = args.X - (pos.X + mapElem.Bounds.renderX), dy = args.Y - (pos.Y + mapElem.Bounds.renderY);
            if (Math.Abs(dx) < size && Math.Abs(dy) < size) return i;
        }
        return -1;
    }

    public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
    {
        hovered = -1;
        if (!Active) return;
        int i = MarkerAt(args, mapElem);
        if (i < 0) return;
        hovered = i;
        hoverText.AppendLine(markers[i].Title);
        hoverText.AppendLine(Lang.Get("createacolony:marker-hover", markers[i].ColonyName));
        if (markers[i].CanRemove) hoverText.AppendLine(Lang.Get("createacolony:marker-hover-remove"));
    }
}

/// <summary>
/// Registers the Colony markers map layer, listed after the game's Owned creatures (position 2) with
/// Colony prospecting just before it.
/// </summary>
public class ColonyMarkerMapSystem : ModSystem
{
    public override double ExecuteOrder() => 1.1;

    public override void Start(ICoreAPI api)
    {
        api.ModLoader.GetModSystem<WorldMapManager>()?.RegisterMapLayer<ColonyMarkerMapLayer>(ColonyMarkerMapLayer.Code, 2.2);
    }
}
