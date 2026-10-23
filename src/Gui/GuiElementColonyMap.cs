using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Cairo;
using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Gui;

/// <summary>
/// A picture of the ground seen from above, sampled from the chunks the client has loaded: one pixel per
/// Step blocks, coloured like the vanilla world map and shaded by slope. Land that is not loaded is dark.
/// Made once and kept, as MineColonies' town map is, until the player asks for a fresh one.
/// </summary>
public class ColonyMapTerrain
{
    static readonly int Unknown = Argb(38, 38, 42);

    public int MinX, MinZ, Width, Height, Step;
    public int[] Pixels = Array.Empty<int>();

    /// <summary>Samples the ground between the two corners (inclusive), at most maxSide pixels a side.</summary>
    public static ColonyMapTerrain Build(ICoreClientAPI capi, int minX, int minZ, int maxX, int maxZ, int maxSide)
    {
        int step = Math.Max(1, (int)Math.Ceiling(Math.Max(maxX - minX + 1, maxZ - minZ + 1) / (double)maxSide));
        var map = new ColonyMapTerrain
        {
            MinX = minX, MinZ = minZ, Step = step,
            Width = (maxX - minX) / step + 1, Height = (maxZ - minZ) / step + 1
        };
        map.Pixels = new int[map.Width * map.Height];
        var heights = new int[map.Width * map.Height];
        var ba = capi.World.BlockAccessor;
        var pos = new BlockPos(0);
        const int chunkSize = GlobalConstants.ChunkSize;

        for (int pz = 0; pz < map.Height; pz++)
        {
            for (int px = 0; px < map.Width; px++)
            {
                int i = pz * map.Width + px;
                int x = minX + px * step + step / 2, z = minZ + pz * step + step / 2;
                var mapChunk = ba.GetMapChunk(x / chunkSize, z / chunkSize);
                if (mapChunk == null)
                {
                    map.Pixels[i] = Unknown;
                    heights[i] = -1;
                    continue;
                }
                int y = mapChunk.RainHeightMap[GameMath.Mod(z, chunkSize) * chunkSize + GameMath.Mod(x, chunkSize)];
                if (ba.GetChunk(x / chunkSize, y / chunkSize, z / chunkSize) == null)
                {
                    map.Pixels[i] = Unknown;
                    heights[i] = -1;
                    continue;
                }
                heights[i] = y;
                pos.Set(x, y, z);
                try
                {
                    var block = ba.GetBlock(pos, BlockLayersAccess.FluidOrSolid);
                    if (block.BlockMaterial == EnumBlockMaterial.Snow && y > 0)
                    {
                        // Snow on top hides the ground, so look at what it lies on, as the world map does.
                        pos.Y--;
                        block = ba.GetBlock(pos, BlockLayersAccess.FluidOrSolid);
                    }
                    int color = block.Id == 0 ? Unknown : block.GetColor(capi, pos);
                    // Block colours keep red in the lowest byte; Cairo wants 0xAARRGGBB.
                    map.Pixels[i] = Argb(color & 0xff, (color >> 8) & 0xff, (color >> 16) & 0xff);
                }
                catch (Exception)
                {
                    map.Pixels[i] = Unknown;
                }
            }
        }

        // Slopes facing north-west lighter, the others darker, so hills show.
        for (int pz = 1; pz < map.Height; pz++)
        {
            for (int px = 1; px < map.Width; px++)
            {
                int i = pz * map.Width + px;
                int here = heights[i], there = heights[(pz - 1) * map.Width + px - 1];
                if (here < 0 || there < 0 || here == there) continue;
                float shade = here > there ? 1.12f : 0.86f;
                map.Pixels[i] = Shade(map.Pixels[i], shade);
            }
        }
        return map;
    }

    public bool Covers(int minX, int minZ, int maxX, int maxZ, int maxSide)
    {
        int step = Math.Max(1, (int)Math.Ceiling(Math.Max(maxX - minX + 1, maxZ - minZ + 1) / (double)maxSide));
        return MinX == minX && MinZ == minZ && Step == step && Width == (maxX - minX) / step + 1 && Height == (maxZ - minZ) / step + 1;
    }

    public static int Argb(int r, int g, int b) => unchecked((int)0xff000000) | r << 16 | g << 8 | b;

    public static int Shade(int argb, float f)
    {
        int r = Math.Min(255, (int)(((argb >> 16) & 0xff) * f));
        int g = Math.Min(255, (int)(((argb >> 8) & 0xff) * f));
        int b = Math.Min(255, (int)((argb & 0xff) * f));
        return Argb(r, g, b);
    }

    public static int Mix(int argb, int r, int g, int b, float amount)
    {
        int Blend(int from, int to) => (int)(from + (to - from) * amount);
        return Argb(Blend((argb >> 16) & 0xff, r), Blend((argb >> 8) & 0xff, g), Blend(argb & 0xff, b));
    }
}

/// <summary>Where the Town map is looking and how close, kept by the window so it stays put when the window redraws.</summary>
public class ColonyMapView
{
    /// <summary>1 shows the whole map; 2 shows half as much land, twice as big; and so on.</summary>
    public double Zoom = 1;
    /// <summary>The block the middle of the map shows; NaN for the middle of the colony.</summary>
    public double CenterX = double.NaN, CenterZ = double.NaN;

    public void Reset()
    {
        Zoom = 1;
        CenterX = CenterZ = double.NaN;
    }
}

/// <summary>
/// The Town map: the ground, the colony's land tinted with a gold outline, each building's ground
/// (Town Hall gold, huts blue, buildings being built orange), the settlers as dots and the player.
/// North is up. The mouse wheel zooms in and out around the pointer, dragging with the left button
/// moves the map, and pointing at a building names it. The picture is drawn into its own texture and
/// redrawn only when something changes, so zooming and dragging do not rebuild the whole window.
/// </summary>
public class GuiElementColonyMap : GuiElement
{
    public const double MinZoom = 1, MaxZoom = 10;

    readonly ColonyMapTerrain terrain;
    readonly ColonyMapView view;
    ColonySnapshot snapshot;
    Vec3d? player;
    bool[] mask;
    // The ground with the colony's land tinted, one pixel per map pixel; scaled up when drawn.
    ImageSurface? ground;
    LoadedTexture texture;
    bool dirty = true;
    bool dragging;
    int prevMouseX, prevMouseY;
    MapBuildingView? hovered;

    public GuiElementColonyMap(ICoreClientAPI capi, ElementBounds bounds, ColonyMapTerrain terrain, ColonySnapshot snapshot, Vec3d? player, ColonyMapView view) : base(capi, bounds)
    {
        this.terrain = terrain;
        this.snapshot = snapshot;
        this.player = player;
        this.view = view;
        texture = new LoadedTexture(capi);
        mask = ClaimMask(terrain, snapshot);
    }

    /// <summary>New settler positions and claim from the server, without rebuilding the window (which would drop a drag).</summary>
    public void SetSnapshot(ColonySnapshot fresh, Vec3d? player)
    {
        if (!fresh.ClaimRects.SequenceEqual(snapshot.ClaimRects))
        {
            mask = ClaimMask(terrain, fresh);
            ground?.Dispose();
            ground = null;
        }
        var was = hovered;
        snapshot = fresh;
        this.player = player;
        hovered = was == null ? null : fresh.MapBuildings.FirstOrDefault(b => b.X1 == was.X1 && b.Z1 == was.Z1 && b.Type == was.Type);
        dirty = true;
    }

    /// <summary>Zooms by a factor around the middle of the map (the + and − buttons).</summary>
    public void ZoomBy(double factor)
    {
        ZoomAt(factor, Bounds.InnerWidth / 2, Bounds.InnerHeight / 2);
    }

    /// <summary>Shows the whole colony again.</summary>
    public void Fit()
    {
        view.Reset();
        dirty = true;
    }

    /// <summary>Which map pixels lie on the colony's land.</summary>
    public static bool[] ClaimMask(ColonyMapTerrain terrain, ColonySnapshot snapshot)
    {
        var mask = new bool[terrain.Width * terrain.Height];
        var rects = snapshot.ClaimRects;
        for (int r = 0; r + 3 < rects.Count; r += 4)
        {
            for (int pz = 0; pz < terrain.Height; pz++)
            {
                int z = terrain.MinZ + pz * terrain.Step + terrain.Step / 2;
                if (z < rects[r + 1] || z > rects[r + 3]) continue;
                for (int px = 0; px < terrain.Width; px++)
                {
                    int x = terrain.MinX + px * terrain.Step + terrain.Step / 2;
                    if (x >= rects[r] && x <= rects[r + 2]) mask[pz * terrain.Width + px] = true;
                }
            }
        }
        return mask;
    }

    // ---- Where things are on screen -------------------------------------------------------

    /// <summary>Screen size of one map pixel at the current zoom; at zoom 1 the whole map fits the box.</summary>
    double Cell => Math.Min(Bounds.InnerWidth / terrain.Width, Bounds.InnerHeight / terrain.Height) * view.Zoom;

    /// <summary>Where the map's top left corner is drawn, inside the element, at the current zoom and position.</summary>
    (double Left, double Top) Origin()
    {
        double cell = Cell;
        double cx = (view.CenterX - terrain.MinX) / terrain.Step, cz = (view.CenterZ - terrain.MinZ) / terrain.Step;
        return (Bounds.InnerWidth / 2 - cx * cell, Bounds.InnerHeight / 2 - cz * cell);
    }

    /// <summary>
    /// Keeps the zoom between MinZoom and MaxZoom and the map on screen: an edge of the map never comes past
    /// the edge of the box, and a map smaller than the box stays in its middle.
    /// </summary>
    void Clamp()
    {
        view.Zoom = Math.Clamp(double.IsFinite(view.Zoom) ? view.Zoom : 1, MinZoom, MaxZoom);
        double cell = Cell;
        double Axis(double center, int origin, int pixels, double box)
        {
            double mid = pixels / 2.0;
            double c = double.IsNaN(center) ? mid : (center - origin) / terrain.Step;
            double half = box / 2 / cell;
            c = pixels * cell <= box ? mid : Math.Clamp(c, half, pixels - half);
            return origin + c * terrain.Step;
        }
        view.CenterX = Axis(view.CenterX, terrain.MinX, terrain.Width, Bounds.InnerWidth);
        view.CenterZ = Axis(view.CenterZ, terrain.MinZ, terrain.Height, Bounds.InnerHeight);
    }

    /// <summary>Zooms by a factor keeping the land under (mx, my), in the element's own pixels, where it is.</summary>
    void ZoomAt(double factor, double mx, double my)
    {
        Clamp();
        var (left, top) = Origin();
        double cell = Cell;
        double px = (mx - left) / cell, pz = (my - top) / cell;
        view.Zoom = Math.Clamp(view.Zoom * factor, MinZoom, MaxZoom);
        double newCell = Cell;
        view.CenterX = terrain.MinX + (px - (mx - Bounds.InnerWidth / 2) / newCell) * terrain.Step;
        view.CenterZ = terrain.MinZ + (pz - (my - Bounds.InnerHeight / 2) / newCell) * terrain.Step;
        Clamp();
        dirty = true;
    }

    // ---- Mouse ----------------------------------------------------------------------------

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseDownOnElement(api, args);
        if (args.Button != EnumMouseButton.Left) return;
        dragging = true;
        prevMouseX = args.X;
        prevMouseY = args.Y;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseUp(api, args);
        dragging = false;
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        // A button let go outside the window never reaches OnMouseUp.
        if (dragging && !api.Input.MouseButton.Left) dragging = false;
        if (dragging)
        {
            double cell = Cell;
            view.CenterX -= (args.X - prevMouseX) / cell * terrain.Step;
            view.CenterZ -= (args.Y - prevMouseY) / cell * terrain.Step;
            prevMouseX = args.X;
            prevMouseY = args.Y;
            Clamp();
            dirty = true;
        }

        var under = Bounds.PointInside(args.X, args.Y) ? BuildingAt(args.X - Bounds.absX, args.Y - Bounds.absY) : null;
        if (under != hovered)
        {
            hovered = under;
            dirty = true;
        }
    }

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (!Bounds.PointInside(api.Input.MouseX, api.Input.MouseY)) return;
        ZoomAt(args.delta > 0 ? 1.25 : 0.8, api.Input.MouseX - Bounds.absX, api.Input.MouseY - Bounds.absY);
        args.SetHandled(true);
    }

    /// <summary>The building drawn under a point in the element's own pixels; huts before the Town Hall so a hut on its ground wins.</summary>
    MapBuildingView? BuildingAt(double x, double y)
    {
        Clamp();
        var (left, top) = Origin();
        double cell = Cell;
        foreach (var b in snapshot.MapBuildings.OrderBy(b => b.Type == BuildingTypes.TownHall))
        {
            var (bx, by, bw, bh) = Rect(terrain, b, cell, left, top, scaled(6));
            if (x >= bx && x <= bx + bw && y >= by && y <= by + bh) return b;
        }
        return null;
    }

    // ---- Drawing --------------------------------------------------------------------------

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        dirty = true;
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (dirty) Redraw();
        if (texture.TextureId == 0) return;
        api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId, Bounds.renderX, Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight);
    }

    void Redraw()
    {
        dirty = false;
        int width = Math.Max(1, (int)Math.Ceiling(Bounds.InnerWidth)), height = Math.Max(1, (int)Math.Ceiling(Bounds.InnerHeight));
        using var surface = new ImageSurface(Format.Argb32, width, height);
        using (var ctx = new Context(surface))
        {
            Clamp();
            var (left, top) = Origin();
            Draw(ctx, Cell, left, top, width, height);
        }
        api.Gui.LoadOrUpdateCairoTexture(surface, false, ref texture);
    }

    /// <summary>The ground, with the colony's land tinted gold, as one small picture to scale up.</summary>
    ImageSurface Ground()
    {
        int w = terrain.Width, h = terrain.Height;
        var pixels = (int[])terrain.Pixels.Clone();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (mask[i]) pixels[i] = ColonyMapTerrain.Mix(pixels[i], 255, 210, 90, 0.18f);
        }
        var image = new ImageSurface(Format.Argb32, w, h);
        image.Flush();
        int stride = image.Stride / 4;
        var rowBuffer = new int[stride];
        for (int row = 0; row < h; row++)
        {
            Array.Copy(pixels, row * w, rowBuffer, 0, w);
            Marshal.Copy(rowBuffer, 0, image.DataPtr + row * image.Stride, stride);
        }
        image.MarkDirty();
        return image;
    }

    void Draw(Context ctx, double cell, double left, double top, int width, int height)
    {
        int w = terrain.Width, h = terrain.Height;

        // Behind the map, where it does not reach.
        ctx.SetSourceRGBA(0.15, 0.15, 0.16, 1);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();

        ground ??= Ground();
        ctx.Save();
        ctx.Translate(left, top);
        ctx.Scale(cell, cell);
        using (var pattern = new SurfacePattern(ground) { Filter = Filter.Nearest })
        {
            ctx.SetSource(pattern);
            ctx.Rectangle(0, 0, w, h);
            ctx.Fill();
        }
        ctx.Restore();

        // Only the map pixels on screen need their edges drawn.
        int px0 = Math.Max(0, (int)Math.Floor(-left / cell) - 1), px1 = Math.Min(w - 1, (int)Math.Ceiling((width - left) / cell) + 1);
        int pz0 = Math.Max(0, (int)Math.Floor(-top / cell) - 1), pz1 = Math.Min(h - 1, (int)Math.Ceiling((height - top) / cell) + 1);

        // The edge of the colony's land: every side of a claimed pixel that touches unclaimed land.
        bool Claimed(int px, int pz) => px >= 0 && pz >= 0 && px < w && pz < h && mask[pz * w + px];
        ctx.SetSourceRGBA(1, 0.82, 0.25, 1);
        ctx.LineWidth = Math.Max(1.5, scaled(2));
        for (int pz = pz0; pz <= pz1; pz++)
        {
            for (int px = px0; px <= px1; px++)
            {
                if (!mask[pz * w + px]) continue;
                double x0 = left + px * cell, y0 = top + pz * cell, x1 = x0 + cell, y1 = y0 + cell;
                if (!Claimed(px, pz - 1)) { ctx.MoveTo(x0, y0); ctx.LineTo(x1, y0); }
                if (!Claimed(px, pz + 1)) { ctx.MoveTo(x0, y1); ctx.LineTo(x1, y1); }
                if (!Claimed(px - 1, pz)) { ctx.MoveTo(x0, y0); ctx.LineTo(x0, y1); }
                if (!Claimed(px + 1, pz)) { ctx.MoveTo(x1, y0); ctx.LineTo(x1, y1); }
            }
        }
        ctx.Stroke();

        // Buildings: the ground each one stands on.
        double minSize = scaled(5);
        foreach (var b in snapshot.MapBuildings.OrderBy(b => b.Type == BuildingTypes.TownHall))
        {
            var (x, y, bw, bh) = Rect(terrain, b, cell, left, top, minSize);
            double[] color = b.Type == BuildingTypes.TownHall ? new[] { 1.0, 0.8, 0.2 }
                : b.UnderConstruction || b.Level == 0 ? new[] { 1.0, 0.55, 0.2 }
                : new[] { 0.45, 0.75, 1.0 };
            ctx.SetSourceRGBA(color[0], color[1], color[2], b == hovered ? 0.6 : 0.35);
            ctx.Rectangle(x, y, bw, bh);
            ctx.FillPreserve();
            ctx.SetSourceRGBA(color[0], color[1], color[2], 1);
            ctx.LineWidth = Math.Max(1, scaled(b == hovered ? 2.5 : 1.5));
            if (b.UnderConstruction || b.Level == 0) ctx.SetDash(new[] { scaled(3), scaled(2) }, 0);
            ctx.Stroke();
            ctx.SetDash(Array.Empty<double>(), 0);
        }

        // Settlers, then the player on top. Dots grow a little when zoomed in.
        double dotScale = Math.Min(2, 1 + (view.Zoom - 1) / 6);
        foreach (var c in snapshot.Citizens.Where(c => c.State != "dead" && (c.X != 0 || c.Z != 0)))
        {
            double[] color = c.Visitor ? new[] { 1.0, 0.9, 0.3 } : c.State == "downed" ? new[] { 0.9, 0.3, 0.3 } : new[] { 0.4, 0.95, 0.45 };
            Dot(ctx, WorldToScreen(terrain, c.X + 0.5, c.Z + 0.5, cell, left, top), scaled(3) * dotScale, color);
        }
        if (player != null) Dot(ctx, WorldToScreen(terrain, player.X, player.Z, cell, left, top), scaled(4) * dotScale, new[] { 1.0, 1.0, 1.0 });

        // North arrow, and how far zoomed in.
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(scaled(14));
        Label(ctx, "N ↑", scaled(6), scaled(6), false);
        if (view.Zoom > MinZoom + 0.01)
        {
            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
            ctx.SetFontSize(scaled(12));
            string zoom = Lang.Get("createacolony:townmap-zoom", view.Zoom.ToString("0.#"));
            Label(ctx, zoom, width - scaled(14) - ctx.TextExtents(zoom).XAdvance, scaled(6), false);
        }

        // The name of the building under the pointer, just above it (or below when there is no room).
        if (hovered != null)
        {
            var (x, y, bw, bh) = Rect(terrain, hovered, cell, left, top, minSize);
            string text = Lang.Get("createacolony:hut-name-level", hovered.Name, hovered.Level)
                + (hovered.UnderConstruction ? " · " + Lang.Get("createacolony:townmap-being-built") : "");
            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
            ctx.SetFontSize(scaled(13));
            double boxWidth = ctx.TextExtents(text).XAdvance + scaled(8), boxHeight = ctx.FontExtents.Height + scaled(4);
            double lx = Math.Clamp(x + bw / 2 - boxWidth / 2, scaled(2), Math.Max(scaled(2), width - boxWidth - scaled(2)));
            double ly = y - boxHeight - scaled(4);
            if (ly < scaled(2)) ly = Math.Min(height - boxHeight - scaled(2), y + bh + scaled(4));
            Label(ctx, text, lx, ly, true);
        }
    }

    /// <summary>Text on a dark box so it reads on any ground; (x, y) is the box's top left corner.</summary>
    void Label(Context ctx, string text, double x, double y, bool framed)
    {
        double boxWidth = ctx.TextExtents(text).XAdvance + scaled(8), boxHeight = ctx.FontExtents.Height + scaled(4);
        ctx.SetSourceRGBA(0, 0, 0, framed ? 0.75 : 0.45);
        ctx.Rectangle(x, y, boxWidth, boxHeight);
        ctx.Fill();
        if (framed)
        {
            ctx.SetSourceRGBA(1, 1, 1, 0.5);
            ctx.LineWidth = 1;
            ctx.Rectangle(x + 0.5, y + 0.5, boxWidth - 1, boxHeight - 1);
            ctx.Stroke();
        }
        ctx.SetSourceRGBA(1, 1, 1, 0.95);
        ctx.MoveTo(x + scaled(4), y + scaled(2) + ctx.FontExtents.Ascent);
        ctx.ShowText(text);
    }

    static (double X, double Y) WorldToScreen(ColonyMapTerrain t, double x, double z, double cell, double left, double top) =>
        (left + (x - t.MinX) / t.Step * cell, top + (z - t.MinZ) / t.Step * cell);

    /// <summary>A building's ground on screen, at least minSize across so small huts still show.</summary>
    public static (double X, double Y, double W, double H) Rect(ColonyMapTerrain t, MapBuildingView b, double cell, double left, double top, double minSize)
    {
        var (x1, y1) = WorldToScreen(t, b.X1, b.Z1, cell, left, top);
        var (x2, y2) = WorldToScreen(t, b.X2 + 1, b.Z2 + 1, cell, left, top);
        double bw = Math.Max(minSize, x2 - x1), bh = Math.Max(minSize, y2 - y1);
        return ((x1 + x2 - bw) / 2, (y1 + y2 - bh) / 2, bw, bh);
    }

    static void Dot(Context ctx, (double X, double Y) at, double radius, double[] color)
    {
        ctx.Arc(at.X, at.Y, radius, 0, 2 * Math.PI);
        ctx.SetSourceRGBA(color[0], color[1], color[2], 1);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0, 0, 0, 0.8);
        ctx.LineWidth = 1;
        ctx.Stroke();
    }

    public override void Dispose()
    {
        base.Dispose();
        texture.Dispose();
        ground?.Dispose();
        ground = null;
    }
}
