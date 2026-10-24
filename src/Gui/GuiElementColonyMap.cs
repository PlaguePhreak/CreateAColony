using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using CreateAColony.Colonies;
using CreateAColony.Map;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Gui;

/// <summary>Where the Town map is looking and how close, kept by the window so it stays put when the window redraws.</summary>
public class ColonyMapView
{
    /// <summary>1 fits the colony in the box; 2 shows half as much land, twice as big; below 1 shows the land around it.</summary>
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
/// The Town map: the ground as the world map draws it, for all land near the colony that the player or a
/// settler has loaded (see ColonyLandMap), with the colony's land tinted and outlined in gold, each building's
/// ground (Town Hall gold, huts blue, buildings being built orange), the settlers as dots and the player.
/// North is up. The mouse wheel zooms around the pointer, from the whole area down to a few blocks, dragging
/// with the left button moves the map, and pointing at a building names it. The ground is drawn from
/// textures of 8 by 8 chunks; the rest is drawn into one texture redrawn only when something changes.
/// </summary>
public class GuiElementColonyMap : GuiElement
{
    /// <summary>Most screen pixels one block may take when zoomed right in.</summary>
    const double MaxPixelsPerBlock = 16;

    readonly ColonyLandMap? landMap;
    readonly string colonyId;
    readonly (int MinX, int MinZ, int MaxX, int MaxZ) box;
    readonly ColonyMapView view;
    ColonySnapshot snapshot;
    Vec3d? player;
    // The colony's land as non-overlapping cells (to tint) and the edge of it (to outline), in blocks.
    List<(double X1, double Z1, double X2, double Z2)> claimCells = new();
    List<(double X1, double Z1, double X2, double Z2)> claimEdges = new();
    // Buildings, dots and labels; the ground is drawn under it from ColonyLandMap's textures.
    LoadedTexture texture;
    bool dirty = true;
    bool hadTiles;
    bool dragging;
    int prevMouseX, prevMouseY;
    MapBuildingView? hovered;

    /// <summary>box is the colony's land and buildings in blocks (inclusive), which Fit shows.</summary>
    public GuiElementColonyMap(ICoreClientAPI capi, ElementBounds bounds, ColonySnapshot snapshot, (int MinX, int MinZ, int MaxX, int MaxZ) box, Vec3d? player, ColonyMapView view) : base(capi, bounds)
    {
        landMap = capi.ModLoader.GetModSystem<ColonyLandMap>();
        colonyId = snapshot.ColonyId;
        this.snapshot = snapshot;
        this.box = box;
        this.player = player;
        this.view = view;
        texture = new LoadedTexture(capi);
        MakeClaimShape(snapshot.ClaimRects);
    }

    /// <summary>New settler positions and claim from the server, without rebuilding the window (which would drop a drag).</summary>
    public void SetSnapshot(ColonySnapshot fresh, Vec3d? player)
    {
        if (!fresh.ClaimRects.SequenceEqual(snapshot.ClaimRects)) MakeClaimShape(fresh.ClaimRects);
        var was = hovered;
        snapshot = fresh;
        this.player = player;
        hovered = was == null ? null : fresh.MapBuildings.FirstOrDefault(b => b.X1 == was.X1 && b.Z1 == was.Z1 && b.Type == was.Type);
        dirty = true;
    }

    /// <summary>Zooms by a factor around the middle of the map (the + and − buttons).</summary>
    public void ZoomBy(double factor) => ZoomAt(factor, Bounds.InnerWidth / 2, Bounds.InnerHeight / 2);

    /// <summary>Shows the whole colony again.</summary>
    public void Fit()
    {
        view.Reset();
        dirty = true;
    }

    /// <summary>Moves the map so the player is in the middle.</summary>
    public void CenterOnPlayer()
    {
        if (player == null) return;
        view.CenterX = player.X;
        view.CenterZ = player.Z;
        dirty = true;
    }

    /// <summary>
    /// Cuts the claim rectangles (minX, minZ, maxX, maxZ, inclusive) along every edge any of them has, so the
    /// land is a grid of cells that are either claimed or not: claimed cells are tinted once each, and the outline
    /// is every side of a claimed cell that touches an unclaimed one.
    /// </summary>
    void MakeClaimShape(List<int> rects)
    {
        claimCells = new();
        claimEdges = new();
        var all = new List<(int X1, int Z1, int X2, int Z2)>();
        for (int r = 0; r + 3 < rects.Count; r += 4) all.Add((rects[r], rects[r + 1], rects[r + 2] + 1, rects[r + 3] + 1));
        if (all.Count == 0) return;
        var xs = all.SelectMany(a => new[] { a.X1, a.X2 }).Distinct().OrderBy(v => v).ToList();
        var zs = all.SelectMany(a => new[] { a.Z1, a.Z2 }).Distinct().OrderBy(v => v).ToList();
        int w = xs.Count - 1, h = zs.Count - 1;
        var claimed = new bool[w, h];
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                double mx = (xs[i] + xs[i + 1]) / 2.0, mz = (zs[j] + zs[j + 1]) / 2.0;
                claimed[i, j] = all.Any(a => mx >= a.X1 && mx < a.X2 && mz >= a.Z1 && mz < a.Z2);
            }
        }
        bool In(int i, int j) => i >= 0 && j >= 0 && i < w && j < h && claimed[i, j];
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                if (!claimed[i, j]) continue;
                double x0 = xs[i], x1 = xs[i + 1], z0 = zs[j], z1 = zs[j + 1];
                claimCells.Add((x0, z0, x1, z1));
                if (!In(i, j - 1)) claimEdges.Add((x0, z0, x1, z0));
                if (!In(i, j + 1)) claimEdges.Add((x0, z1, x1, z1));
                if (!In(i - 1, j)) claimEdges.Add((x0, z0, x0, z1));
                if (!In(i + 1, j)) claimEdges.Add((x1, z0, x1, z1));
            }
        }
    }

    // ---- Where things are on screen -------------------------------------------------------

    /// <summary>Screen pixels per block that fit the colony in the box.</summary>
    double FitScale => Math.Min(Bounds.InnerWidth / (box.MaxX - box.MinX + 1.0), Bounds.InnerHeight / (box.MaxZ - box.MinZ + 1.0));

    double PixelsPerBlock => FitScale * view.Zoom;

    /// <summary>The land the map can show, in blocks: what the server allows, else a guess until it answers.</summary>
    (double MinX, double MinZ, double MaxX, double MaxZ) Area()
    {
        int chunk = GlobalConstants.ChunkSize;
        if (landMap?.RangeOf(colonyId) is { } r) return (r.MinX * chunk, r.MinZ * chunk, (r.MaxX + 1) * chunk, (r.MaxZ + 1) * chunk);
        return (box.MinX - 1024, box.MinZ - 1024, box.MaxX + 1024, box.MaxZ + 1024);
    }

    /// <summary>Zoomed out, the whole area fits; zoomed in, a block is at most MaxPixelsPerBlock across.</summary>
    (double Min, double Max) ZoomLimits()
    {
        var a = Area();
        double fit = FitScale;
        double whole = Math.Min(Bounds.InnerWidth / (a.MaxX - a.MinX), Bounds.InnerHeight / (a.MaxZ - a.MinZ));
        return (Math.Min(1, whole / fit), Math.Max(1, MaxPixelsPerBlock / fit));
    }

    /// <summary>Keeps the zoom in its limits and the middle of the map inside the area.</summary>
    void Clamp()
    {
        var (min, max) = ZoomLimits();
        view.Zoom = Math.Clamp(double.IsFinite(view.Zoom) ? view.Zoom : 1, min, max);
        var a = Area();
        if (double.IsNaN(view.CenterX) || double.IsNaN(view.CenterZ))
        {
            view.CenterX = (box.MinX + box.MaxX + 1) / 2.0;
            view.CenterZ = (box.MinZ + box.MaxZ + 1) / 2.0;
        }
        view.CenterX = Math.Clamp(view.CenterX, a.MinX, a.MaxX);
        view.CenterZ = Math.Clamp(view.CenterZ, a.MinZ, a.MaxZ);
    }

    /// <summary>A block position on screen, in the element's own pixels.</summary>
    (double X, double Y) WorldToScreen(double x, double z)
    {
        double scale = PixelsPerBlock;
        return (Bounds.InnerWidth / 2 + (x - view.CenterX) * scale, Bounds.InnerHeight / 2 + (z - view.CenterZ) * scale);
    }

    /// <summary>Zooms by a factor keeping the land under (mx, my), in the element's own pixels, where it is.</summary>
    void ZoomAt(double factor, double mx, double my)
    {
        Clamp();
        double scale = PixelsPerBlock;
        double wx = view.CenterX + (mx - Bounds.InnerWidth / 2) / scale, wz = view.CenterZ + (my - Bounds.InnerHeight / 2) / scale;
        var (min, max) = ZoomLimits();
        view.Zoom = Math.Clamp(view.Zoom * factor, min, max);
        double newScale = PixelsPerBlock;
        view.CenterX = wx - (mx - Bounds.InnerWidth / 2) / newScale;
        view.CenterZ = wz - (my - Bounds.InnerHeight / 2) / newScale;
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
            double scale = PixelsPerBlock;
            view.CenterX -= (args.X - prevMouseX) / scale;
            view.CenterZ -= (args.Y - prevMouseY) / scale;
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
        foreach (var b in snapshot.MapBuildings.OrderBy(b => b.Type == BuildingTypes.TownHall))
        {
            var (bx, by, bw, bh) = Rect(b, scaled(6));
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
        landMap?.Poll(colonyId);
        bool hasTiles = landMap?.HasAnyTiles == true;
        if (hasTiles != hadTiles)
        {
            hadTiles = hasTiles;
            dirty = true;
        }
        Clamp();
        api.Render.PushScissor(Bounds);
        RenderGround();
        if (dirty) Redraw();
        if (texture.TextureId != 0)
        {
            api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId, Bounds.renderX, Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight);
        }
        api.Render.PopScissor();
    }

    /// <summary>The ground: each texture of 8 by 8 chunks that is on screen. Land nobody has loaded stays dark.</summary>
    void RenderGround()
    {
        if (landMap == null) return;
        int size = ColonyLandMap.RegionSize;
        double scale = PixelsPerBlock, halfW = Bounds.InnerWidth / 2 / scale, halfH = Bounds.InnerHeight / 2 / scale;
        int rx0 = ColonyLandMap.FloorDiv((int)Math.Floor(view.CenterX - halfW), size), rx1 = ColonyLandMap.FloorDiv((int)Math.Floor(view.CenterX + halfW), size);
        int rz0 = ColonyLandMap.FloorDiv((int)Math.Floor(view.CenterZ - halfH), size), rz1 = ColonyLandMap.FloorDiv((int)Math.Floor(view.CenterZ + halfH), size);
        // Remaking a texture takes a moment, so only a few are remade each frame.
        int budget = 3;
        for (int rz = rz0; rz <= rz1; rz++)
        {
            for (int rx = rx0; rx <= rx1; rx++)
            {
                int id = landMap.RegionTexture(rx, rz, ref budget);
                if (id == 0) continue;
                var (x0, y0) = WorldToScreen(rx * size, rz * size);
                var (x1, y1) = WorldToScreen((rx + 1) * size, (rz + 1) * size);
                // Whole pixels, so neighbouring textures meet without a gap.
                x0 = Math.Floor(x0); y0 = Math.Floor(y0); x1 = Math.Floor(x1); y1 = Math.Floor(y1);
                api.Render.Render2DTexture(id, (float)(Bounds.renderX + x0), (float)(Bounds.renderY + y0), (float)(x1 - x0), (float)(y1 - y0));
            }
        }
    }

    void Redraw()
    {
        dirty = false;
        int width = Math.Max(1, (int)Math.Ceiling(Bounds.InnerWidth)), height = Math.Max(1, (int)Math.Ceiling(Bounds.InnerHeight));
        using var surface = new ImageSurface(Format.Argb32, width, height);
        using (var ctx = new Context(surface))
        {
            Draw(ctx, width, height);
        }
        api.Gui.LoadOrUpdateCairoTexture(surface, false, ref texture);
    }

    void Draw(Context ctx, int width, int height)
    {
        double scale = PixelsPerBlock;

        // The colony's land: a light gold tint, and a gold line round the edge.
        ctx.SetSourceRGBA(1, 0.82, 0.35, 0.18);
        foreach (var c in claimCells)
        {
            var (x0, y0) = WorldToScreen(c.X1, c.Z1);
            var (x1, y1) = WorldToScreen(c.X2, c.Z2);
            ctx.Rectangle(x0, y0, x1 - x0, y1 - y0);
        }
        ctx.Fill();
        ctx.SetSourceRGBA(1, 0.82, 0.25, 1);
        ctx.LineWidth = Math.Max(1.5, scaled(2));
        foreach (var e in claimEdges)
        {
            var (x0, y0) = WorldToScreen(e.X1, e.Z1);
            var (x1, y1) = WorldToScreen(e.X2, e.Z2);
            ctx.MoveTo(x0, y0);
            ctx.LineTo(x1, y1);
        }
        ctx.Stroke();

        // Buildings: the ground each one stands on.
        double minSize = scaled(5);
        foreach (var b in snapshot.MapBuildings.OrderBy(b => b.Type == BuildingTypes.TownHall))
        {
            var (x, y, bw, bh) = Rect(b, minSize);
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

        // Settlers, then the player on top. Dots grow a little when zoomed in, and shrink a little zoomed out.
        double dotScale = Math.Clamp(1 + (view.Zoom - 1) / 6, 0.7, 2);
        foreach (var c in snapshot.Citizens.Where(c => c.State != "dead" && (c.X != 0 || c.Z != 0)))
        {
            double[] color = c.Visitor ? new[] { 1.0, 0.9, 0.3 } : c.State == "downed" ? new[] { 0.9, 0.3, 0.3 } : new[] { 0.4, 0.95, 0.45 };
            Dot(ctx, WorldToScreen(c.X + 0.5, c.Z + 0.5), scaled(3) * dotScale, color);
        }
        if (player != null) Dot(ctx, WorldToScreen(player.X, player.Z), scaled(4) * Math.Max(1, dotScale), new[] { 1.0, 1.0, 1.0 });

        // North arrow, how far zoomed in, and a note while the ground is on its way.
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(scaled(14));
        Label(ctx, "N ↑", scaled(6), scaled(6), false);
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(scaled(12));
        if (Math.Abs(view.Zoom - 1) > 0.01)
        {
            string zoom = Lang.Get("createacolony:townmap-zoom", view.Zoom.ToString(view.Zoom < 1 ? "0.##" : "0.#"));
            Label(ctx, zoom, width - scaled(14) - ctx.TextExtents(zoom).XAdvance, scaled(6), false);
        }
        if (!hadTiles)
        {
            string loading = Lang.Get("createacolony:townmap-loading");
            Label(ctx, loading, scaled(6), height - scaled(6) - ctx.FontExtents.Height - scaled(4), false);
        }

        // The name of the building under the pointer, just above it (or below when there is no room).
        if (hovered != null)
        {
            var (x, y, bw, bh) = Rect(hovered, minSize);
            string text = Lang.Get("createacolony:hut-name-level", hovered.Name, hovered.Level)
                + (hovered.UnderConstruction ? " · " + Lang.Get("createacolony:townmap-being-built") : "");
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

    /// <summary>A building's ground on screen, at least minSize across so small huts still show.</summary>
    (double X, double Y, double W, double H) Rect(MapBuildingView b, double minSize)
    {
        var (x1, y1) = WorldToScreen(b.X1, b.Z1);
        var (x2, y2) = WorldToScreen(b.X2 + 1, b.Z2 + 1);
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
    }
}
