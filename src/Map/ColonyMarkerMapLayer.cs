using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CreateAColony.Colonies;
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
    /// <summary>The More Map Icons icon, used when the player's game has it.</summary>
    [ProtoMember(5)] public string Icon = "";
    /// <summary>The game's own icon for players without More Map Icons.</summary>
    [ProtoMember(6)] public string Fallback = "circle";
    [ProtoMember(7)] public int Color;
    [ProtoMember(8)] public string ColonyName = "";
}

/// <summary>
/// The "Colony markers" layer of the world map: what the colony's Explorers and Gatherers found, and where
/// settlers fell (ColonyManager.Markers.cs). It draws them like waypoints, with the waypoint icons the player's
/// game has: More Map Icons' when that mod is installed, the game's own otherwise. They are the colony's, so
/// they cannot be edited from the map; a hut's window can take its markers away.
/// </summary>
public class ColonyMarkerMapLayer : MarkerMapLayer
{
    public const string Code = "colonymarkers";

    /// <summary>
    /// Colours More Map Icons suggests names for (Olivine, Emerald, Brown Coal, Peat) that the game's own colour
    /// list leaves out, so the Add waypoint window offers them too.
    /// </summary>
    static readonly string[] ExtraWaypointColors = { "#03D941", "#885530" };

    readonly List<ColonyMarkerView> markers = new();
    readonly Matrixf mvMat = new();
    Vec2f viewPosRef = new();
    int hovered = -1;

    public ColonyMarkerMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink) { }

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
            foreach (var marker in colony.Markers)
            {
                if (ColonyManager.KindOf(marker.Kind) is not { } kind) continue;
                views.Add(new ColonyMarkerView
                {
                    X = marker.X, Y = marker.Y, Z = marker.Z,
                    Title = marker.Title,
                    // The game names icons by their file name, which asset paths keep in lower case.
                    Icon = kind.Icon.ToLowerInvariant(),
                    Fallback = kind.Fallback.ToLowerInvariant(),
                    Color = ColorUtil.Hex2Int(kind.Color),
                    ColonyName = colony.Name
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
        // The game's waypoint layer keeps the colour list the Add waypoint window offers.
        if (WaypointLayer() is { } wpLayer)
        {
            foreach (string hex in ExtraWaypointColors)
            {
                int color = ColorUtil.Hex2Int(hex);
                if (!wpLayer.WaypointColors.Contains(color)) wpLayer.WaypointColors.Add(color);
            }
        }
    }

    WaypointMapLayer? WaypointLayer() =>
        api.ModLoader.GetModSystem<WorldMapManager>()?.MapLayers.OfType<WaypointMapLayer>().FirstOrDefault();

    public override void Render(GuiElementMap mapElem, float dt)
    {
        if (!Active || markers.Count == 0) return;
        // The icons are the ones the game's waypoint layer loaded, More Map Icons' among them when it is installed.
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
            float scaleX = (tex.Width + hover) * 0.5f * WaypointMapComponent.IconScale;
            float scaleY = (tex.Height + hover) * 0.5f * WaypointMapComponent.IconScale;
            prog.BindTexture2D("tex2d", tex.TextureId, 0);
            mvMat.Set(capi.Render.CurrentModelviewMatrix).Translate(x, y, 60).Scale(scaleX, scaleY, 0);

            // A shadow, then the icon, as the game draws waypoints.
            float uiscale = RuntimeEnv.GUIScale;
            var shadow = mvMat.Clone().Scale(1.1f, 1.1f, 1.1f).Translate(1.25 * uiscale / scaleX, 1.25 * uiscale / scaleY, 0);
            prog.Uniform("rgbaIn", new Vec4f(0, 0, 0, 0.6f));
            prog.UniformMatrix("modelViewMatrix", shadow.Values);
            capi.Render.RenderMesh(quad);

            var color = new Vec4f();
            ColorUtil.ToRGBAVec4f(marker.Color, ref color);
            prog.Uniform("rgbaIn", color);
            prog.UniformMatrix("modelViewMatrix", mvMat.Values);
            capi.Render.RenderMesh(quad);
        }
    }

    public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
    {
        hovered = -1;
        if (!Active) return;
        double size = RuntimeEnv.GUIScale * 8;
        for (int i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            var pos = new Vec2f();
            mapElem.TranslateWorldPosToViewPos(new Vec3d(marker.X, marker.Y, marker.Z), ref pos);
            double dx = args.X - (pos.X + mapElem.Bounds.renderX), dy = args.Y - (pos.Y + mapElem.Bounds.renderY);
            if (Math.Abs(dx) >= size || Math.Abs(dy) >= size) continue;
            hovered = i;
            hoverText.AppendLine(marker.Title);
            hoverText.AppendLine(Lang.Get("createacolony:marker-hover", marker.ColonyName));
            break;
        }
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
