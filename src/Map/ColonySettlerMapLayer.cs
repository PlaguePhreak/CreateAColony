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

/// <summary>One settler as the map layer gets it.</summary>
[ProtoContract]
public class SettlerDotView
{
    [ProtoMember(1)] public double X;
    [ProtoMember(2)] public double Z;
    [ProtoMember(3)] public string Name = "";
    [ProtoMember(4)] public string Job = "";
    /// <summary>0 settler, 1 wanderer, 2 knocked out; coloured as on the Town map.</summary>
    [ProtoMember(5)] public int Kind;
    [ProtoMember(6)] public string ColonyName = "";
}

/// <summary>
/// The "Colony settlers" layer of the world map: every settler of the player's colonies as a dot, coloured as
/// on the Town map (green settlers, yellow wanderers, red knocked out). Pointing at one names it. Settlers far
/// away are not on the player's game, so the server sends where they are every few seconds.
/// </summary>
public class ColonySettlerMapLayer : MarkerMapLayer
{
    public const string Code = "colonysettlers";
    const int SendEveryMs = 3000;

    readonly List<SettlerDotView> dots = new();
    readonly Matrixf mvMat = new();
    Vec2f viewPos = new();
    int hovered = -1;

    public ColonySettlerMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
    {
        if (api is ICoreServerAPI sapi) sapi.Event.RegisterGameTickListener(_ => SendAll(sapi), SendEveryMs, 1000);
    }

    public override string Title => "Colony settlers";
    public override string LayerGroupCode => Code;
    public override EnumMapAppSide DataSide => EnumMapAppSide.Server;
    public override bool RequireChunkLoaded => false;

    // ---- Server ----

    public override void OnViewChangedServer(IServerPlayer fromPlayer, int x1, int z1, int x2, int z2) => Send(fromPlayer);

    [Obsolete("Kept because the base layer still has it")]
    public override void OnViewChangedServer(IServerPlayer fromPlayer, List<FastVec2i> nowVisible, List<FastVec2i> nowHidden) => Send(fromPlayer);

    void SendAll(ICoreServerAPI sapi)
    {
        foreach (var player in sapi.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            if (player.ConnectionState == EnumClientState.Playing) Send(player);
        }
    }

    /// <summary>Sends the player where the settlers of every colony they belong to are.</summary>
    void Send(IServerPlayer player)
    {
        var manager = api.ModLoader.GetModSystem<ColonyManager>();
        if (manager == null) return;
        var colonies = manager.All.Where(c => c.RoleOf(player.PlayerUID) >= ColonyRole.Member).ToList();
        if (colonies.Count == 0) return;
        var views = new List<SettlerDotView>();
        foreach (var colony in colonies)
        {
            foreach (var c in colony.Citizens)
            {
                if (!c.Alive) continue;
                Vec3d? at = api.World.GetEntityById(c.EntityId) is { Alive: true } entity ? entity.Pos.XYZ : c.LastSeenPos;
                if (at == null || (at.X == 0 && at.Z == 0)) continue;
                views.Add(new SettlerDotView
                {
                    X = at.X, Z = at.Z, Name = c.Name,
                    Job = c.IsChild ? "child" : c.JobCode ?? "",
                    Kind = c.Visitor ? 1 : c.IsDowned ? 2 : 0,
                    ColonyName = colony.Name
                });
            }
        }
        mapSink.SendMapDataToClient(this, player, SerializerUtil.Serialize(views));
    }

    // ---- Client ----

    public override void OnDataFromServer(byte[] data)
    {
        dots.Clear();
        dots.AddRange(SerializerUtil.Deserialize<List<SettlerDotView>>(data) ?? new List<SettlerDotView>());
        hovered = -1;
    }

    static readonly Vec4f[] Colors =
    {
        new(0.4f, 0.95f, 0.45f, 1), // settler
        new(1f, 0.9f, 0.3f, 1),     // wanderer
        new(0.9f, 0.3f, 0.3f, 1)    // knocked out
    };

    WaypointMapLayer? WaypointLayer() =>
        api.ModLoader.GetModSystem<WorldMapManager>()?.MapLayers.OfType<WaypointMapLayer>().FirstOrDefault();

    public override void Render(GuiElementMap mapElem, float dt)
    {
        if (!Active || dots.Count == 0) return;
        // The game's own round waypoint icon, tinted.
        if (WaypointLayer() is not { texturesByIcon: { } textures, quadModel: { } quad } || !textures.TryGetValue("circle", out var tex)) return;
        var capi = (ICoreClientAPI)api;
        var prog = capi.Render.GetEngineShader(EnumShaderProgram.Gui);
        prog.Uniform("extraGlow", 0);
        prog.Uniform("applyColor", 0);
        prog.Uniform("noTexture", 0f);
        prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
        prog.BindTexture2D("tex2d", tex.TextureId, 0);
        float uiscale = RuntimeEnv.GUIScale;

        for (int i = 0; i < dots.Count; i++)
        {
            var dot = dots[i];
            mapElem.TranslateWorldPosToViewPos(new Vec3d(dot.X, 0, dot.Z), ref viewPos);
            if (viewPos.X < -10 || viewPos.Y < -10 || viewPos.X > mapElem.Bounds.OuterWidth + 10 || viewPos.Y > mapElem.Bounds.OuterHeight + 10) continue;
            float x = (float)(mapElem.Bounds.renderX + viewPos.X);
            float y = (float)(mapElem.Bounds.renderY + viewPos.Y);
            // Smaller than a waypoint, a little bigger under the pointer.
            float size = (hovered == i ? 9 : 6) * uiscale;
            mvMat.Set(capi.Render.CurrentModelviewMatrix).Translate(x, y, 61).Scale(size, size, 0);

            var shadow = mvMat.Clone().Scale(1.3f, 1.3f, 1.3f);
            prog.Uniform("rgbaIn", new Vec4f(0, 0, 0, 0.8f));
            prog.UniformMatrix("modelViewMatrix", shadow.Values);
            capi.Render.RenderMesh(quad);

            prog.Uniform("rgbaIn", Colors[Math.Clamp(dot.Kind, 0, Colors.Length - 1)]);
            prog.UniformMatrix("modelViewMatrix", mvMat.Values);
            capi.Render.RenderMesh(quad);
        }
    }

    public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
    {
        hovered = -1;
        if (!Active) return;
        double reach = RuntimeEnv.GUIScale * 7;
        double best = double.MaxValue;
        for (int i = 0; i < dots.Count; i++)
        {
            var pos = new Vec2f();
            mapElem.TranslateWorldPosToViewPos(new Vec3d(dots[i].X, 0, dots[i].Z), ref pos);
            double dx = args.X - (pos.X + mapElem.Bounds.renderX), dy = args.Y - (pos.Y + mapElem.Bounds.renderY);
            double d = dx * dx + dy * dy;
            if (d < reach * reach && d < best)
            {
                best = d;
                hovered = i;
            }
        }
        if (hovered < 0) return;
        var dot = dots[hovered];
        hoverText.AppendLine(dot.Name);
        string job = dot.Job == "child" ? Lang.Get("createacolony:citizens-child") : Lang.Get("createacolony:job-" + (dot.Job.Length == 0 ? "none" : dot.Job));
        hoverText.AppendLine(Lang.Get("createacolony:settler-dot-hover", job, dot.ColonyName));
    }
}

/// <summary>Registers the Colony settlers map layer, listed with the colony's other layers after the game's Owned creatures.</summary>
public class ColonySettlerMapSystem : ModSystem
{
    public override double ExecuteOrder() => 1.1;

    public override void Start(ICoreAPI api)
    {
        api.ModLoader.GetModSystem<WorldMapManager>()?.RegisterMapLayer<ColonySettlerMapLayer>(ColonySettlerMapLayer.Code, 2.15);
    }
}
