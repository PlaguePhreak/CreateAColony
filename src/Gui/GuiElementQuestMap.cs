using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using CreateAColony.Network;
using CreateAColony.Quests;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Gui;

/// <summary>Where a chapter's map is scrolled to, kept by the window so it stays put when the window redraws.</summary>
public class QuestMapView
{
    public string Chapter = "";
    /// <summary>How far the map is pushed from its starting place, in unscaled pixels.</summary>
    public double PanX, PanY;
}

/// <summary>
/// A chapter's map, FTB Quests style: each quest an icon in a shape (round, square, star or hexagon),
/// joined by lines from the quests it needs, coloured by where it stands (grey locked, white open, blue
/// counting, gold ready to claim, green done). Dragging moves the map, clicking an icon picks the quest.
/// The shapes and lines are drawn into a texture; the item icons are drawn on top each frame.
/// </summary>
public class GuiElementQuestMap : GuiElement
{
    /// <summary>Grid cell size and the icon's circle, unscaled.</summary>
    public const double CellSize = 76, NodeRadius = 23, IconSize = 30;

    readonly List<QuestDef> quests;
    readonly Dictionary<string, QuestStateView> states;
    readonly Dictionary<string, ItemStack?> icons;
    readonly QuestMapView view;
    readonly Action<string?> onSelect;
    string? selected;
    QuestDef? hovered;
    LoadedTexture texture;
    bool dirty = true;
    bool dragging, moved;
    int prevMouseX, prevMouseY, downX, downY;

    public GuiElementQuestMap(ICoreClientAPI capi, ElementBounds bounds, List<QuestDef> quests, Dictionary<string, QuestStateView> states,
        Dictionary<string, ItemStack?> icons, QuestMapView view, string? selected, Action<string?> onSelect) : base(capi, bounds)
    {
        this.quests = quests;
        this.states = states;
        this.icons = icons;
        this.view = view;
        this.selected = selected;
        this.onSelect = onSelect;
        texture = new LoadedTexture(capi);
    }

    public QuestStatus StatusOf(QuestDef q) => states.TryGetValue(q.Code, out var s) ? (QuestStatus)s.Status : QuestStatus.Locked;

    /// <summary>Secret quests stay off the map until the quests they need are done.</summary>
    bool Shown(QuestDef q) => !q.Secret || StatusOf(q) != QuestStatus.Locked;

    /// <summary>A quest's centre inside the element, in screen pixels.</summary>
    (double X, double Y) Centre(QuestDef q)
    {
        double cell = scaled(CellSize);
        return (Bounds.InnerWidth / 2 + q.X * cell + scaled(view.PanX), Bounds.InnerHeight / 2 + q.Y * cell + scaled(view.PanY));
    }

    /// <summary>Starts a new chapter's map with its quests in the middle of the box.</summary>
    public void CentreOnQuests(string chapter)
    {
        if (view.Chapter == chapter) return;
        view.Chapter = chapter;
        view.PanX = view.PanY = 0;
        var shown = quests.Where(Shown).ToList();
        if (shown.Count == 0) return;
        view.PanX = -(shown.Min(q => q.X) + shown.Max(q => q.X)) / 2.0 * CellSize;
        view.PanY = -(shown.Min(q => q.Y) + shown.Max(q => q.Y)) / 2.0 * CellSize;
    }

    QuestDef? QuestAt(double x, double y)
    {
        double r = scaled(NodeRadius);
        foreach (var q in quests)
        {
            if (!Shown(q)) continue;
            var (cx, cy) = Centre(q);
            if (Math.Abs(x - cx) <= r && Math.Abs(y - cy) <= r) return q;
        }
        return null;
    }

    // ---- Mouse ----------------------------------------------------------------------------

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseDownOnElement(api, args);
        if (args.Button != EnumMouseButton.Left) return;
        dragging = true;
        moved = false;
        prevMouseX = downX = args.X;
        prevMouseY = downY = args.Y;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseUp(api, args);
        if (!dragging) return;
        dragging = false;
        if (moved || !Bounds.PointInside(args.X, args.Y)) return;
        var under = QuestAt(args.X - Bounds.absX, args.Y - Bounds.absY);
        selected = under?.Code;
        dirty = true;
        onSelect(selected);
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (dragging && !api.Input.MouseButton.Left) dragging = false;
        if (dragging)
        {
            if (Math.Abs(args.X - downX) > 4 || Math.Abs(args.Y - downY) > 4) moved = true;
            view.PanX += (args.X - prevMouseX) / scaled(1);
            view.PanY += (args.Y - prevMouseY) / scaled(1);
            prevMouseX = args.X;
            prevMouseY = args.Y;
            dirty = true;
        }
        var under = Bounds.PointInside(args.X, args.Y) ? QuestAt(args.X - Bounds.absX, args.Y - Bounds.absY) : null;
        if (under != hovered)
        {
            hovered = under;
            dirty = true;
        }
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
        if (texture.TextureId != 0) api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId, Bounds.renderX, Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight);

        // The item icons, clipped to the box.
        api.Render.PushScissor(Bounds);
        float size = (float)scaled(IconSize);
        double margin = scaled(NodeRadius);
        foreach (var q in quests)
        {
            if (!Shown(q) || !icons.TryGetValue(q.Code, out var stack) || stack == null) continue;
            var (cx, cy) = Centre(q);
            if (cx < -margin || cy < -margin || cx > Bounds.InnerWidth + margin || cy > Bounds.InnerHeight + margin) continue;
            int color = StatusOf(q) == QuestStatus.Locked ? ColorUtil.ColorFromRgba(110, 110, 110, 255) : ColorUtil.WhiteArgb;
            api.Render.RenderItemstackToGui(new DummySlot(stack), Bounds.renderX + cx, Bounds.renderY + cy, 100, size, color, true, false, false);
        }
        api.Render.PopScissor();
    }

    void Redraw()
    {
        dirty = false;
        int width = Math.Max(1, (int)Math.Ceiling(Bounds.InnerWidth)), height = Math.Max(1, (int)Math.Ceiling(Bounds.InnerHeight));
        using var surface = new ImageSurface(Format.Argb32, width, height);
        using (var ctx = new Context(surface)) Draw(ctx, width, height);
        api.Gui.LoadOrUpdateCairoTexture(surface, false, ref texture);
    }

    static double[] StatusColor(QuestStatus status) => status switch
    {
        QuestStatus.Locked => new[] { 0.42, 0.42, 0.44 },
        QuestStatus.Available => new[] { 0.92, 0.92, 0.9 },
        QuestStatus.Active => new[] { 0.5, 0.72, 1.0 },
        QuestStatus.Complete => new[] { 1.0, 0.82, 0.25 },
        _ => new[] { 0.5, 0.85, 0.45 }
    };

    void Draw(Context ctx, int width, int height)
    {
        // Parchment-dark background with a faint grid, so dragging shows movement.
        ctx.SetSourceRGBA(0.13, 0.12, 0.11, 1);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();
        double cell = scaled(CellSize);
        double ox = Bounds.InnerWidth / 2 + scaled(view.PanX), oy = Bounds.InnerHeight / 2 + scaled(view.PanY);
        ctx.SetSourceRGBA(1, 1, 1, 0.05);
        ctx.LineWidth = 1;
        for (double x = ox % cell; x < width; x += cell) { ctx.MoveTo(x, 0); ctx.LineTo(x, height); }
        for (double y = oy % cell; y < height; y += cell) { ctx.MoveTo(0, y); ctx.LineTo(width, y); }
        ctx.Stroke();

        if (quests.Count == 0)
        {
            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
            ctx.SetFontSize(scaled(14));
            string none = Lang.Get("createacolony:quests-chapter-empty");
            ctx.SetSourceRGBA(1, 1, 1, 0.6);
            ctx.MoveTo(width / 2.0 - ctx.TextExtents(none).XAdvance / 2, height / 2.0);
            ctx.ShowText(none);
            return;
        }

        var shown = quests.Where(Shown).ToList();
        double r = scaled(NodeRadius);

        // Lines from each quest's requirements, green once they are done.
        foreach (var q in shown)
        {
            var (x2, y2) = Centre(q);
            foreach (string need in q.Requires)
            {
                var from = shown.FirstOrDefault(s => s.Code == need);
                if (from == null) continue;
                var (x1, y1) = Centre(from);
                bool done = StatusOf(from) == QuestStatus.Claimed;
                ctx.SetSourceRGBA(done ? 0.5 : 0.6, done ? 0.85 : 0.6, done ? 0.45 : 0.6, done ? 0.9 : 0.5);
                ctx.LineWidth = scaled(done ? 2.5 : 1.5);
                if (!done) ctx.SetDash(new[] { scaled(4), scaled(4) }, 0);
                ctx.MoveTo(x1, y1);
                ctx.LineTo(x2, y2);
                ctx.Stroke();
                ctx.SetDash(Array.Empty<double>(), 0);
            }
        }

        // The quests themselves.
        foreach (var q in shown)
        {
            var (cx, cy) = Centre(q);
            if (cx < -r * 2 || cy < -r * 2 || cx > width + r * 2 || cy > height + r * 2) continue;
            var status = StatusOf(q);
            var color = StatusColor(status);
            bool isSelected = q.Code == selected, isHovered = q == hovered;

            Shape(ctx, q.Shape, cx, cy, r);
            ctx.SetSourceRGBA(0.2, 0.19, 0.17, 1);
            ctx.FillPreserve();
            ctx.SetSourceRGBA(color[0], color[1], color[2], status == QuestStatus.Locked ? 0.7 : 1);
            ctx.LineWidth = scaled(isSelected ? 4 : isHovered ? 3 : 2);
            ctx.Stroke();
            if (status == QuestStatus.Complete)
            {
                // A glow around quests ready to claim.
                Shape(ctx, q.Shape, cx, cy, r + scaled(4));
                ctx.SetSourceRGBA(color[0], color[1], color[2], 0.35);
                ctx.LineWidth = scaled(3);
                ctx.Stroke();
            }
            if (status == QuestStatus.Claimed)
            {
                // A small tick in the corner.
                ctx.SetSourceRGBA(color[0], color[1], color[2], 1);
                ctx.LineWidth = scaled(2.5);
                ctx.MoveTo(cx + r * 0.45, cy + r * 0.55);
                ctx.LineTo(cx + r * 0.65, cy + r * 0.78);
                ctx.LineTo(cx + r * 1.0, cy + r * 0.4);
                ctx.Stroke();
            }
            if (states.TryGetValue(q.Code, out var s) && s.Tracked)
            {
                ctx.Arc(cx + r * 0.75, cy - r * 0.75, scaled(4), 0, 2 * Math.PI);
                ctx.SetSourceRGBA(1, 1, 1, 1);
                ctx.Fill();
            }
        }

        // The name of the quest under the pointer.
        if (hovered != null && Shown(hovered))
        {
            var (cx, cy) = Centre(hovered);
            string text = Lang.Get(QuestLibrary.TitleKey(hovered));
            ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
            ctx.SetFontSize(scaled(13));
            double boxWidth = ctx.TextExtents(text).XAdvance + scaled(8), boxHeight = ctx.FontExtents.Height + scaled(4);
            double lx = Math.Clamp(cx - boxWidth / 2, scaled(2), Math.Max(scaled(2), width - boxWidth - scaled(2)));
            double ly = cy - r - boxHeight - scaled(4);
            if (ly < scaled(2)) ly = Math.Min(height - boxHeight - scaled(2), cy + r + scaled(4));
            ctx.SetSourceRGBA(0, 0, 0, 0.8);
            ctx.Rectangle(lx, ly, boxWidth, boxHeight);
            ctx.Fill();
            ctx.SetSourceRGBA(1, 1, 1, 0.5);
            ctx.LineWidth = 1;
            ctx.Rectangle(lx + 0.5, ly + 0.5, boxWidth - 1, boxHeight - 1);
            ctx.Stroke();
            ctx.SetSourceRGBA(1, 1, 1, 0.95);
            ctx.MoveTo(lx + scaled(4), ly + scaled(2) + ctx.FontExtents.Ascent);
            ctx.ShowText(text);
        }
    }

    /// <summary>Traces a quest's outline: round, square, a five-pointed star or a hexagon.</summary>
    static void Shape(Context ctx, string shape, double cx, double cy, double r)
    {
        switch (shape)
        {
            case QuestShapes.Square:
                double s = r * 0.9, c = r * 0.25;
                ctx.NewPath();
                ctx.MoveTo(cx - s + c, cy - s);
                ctx.LineTo(cx + s - c, cy - s);
                ctx.Arc(cx + s - c, cy - s + c, c, -Math.PI / 2, 0);
                ctx.LineTo(cx + s, cy + s - c);
                ctx.Arc(cx + s - c, cy + s - c, c, 0, Math.PI / 2);
                ctx.LineTo(cx - s + c, cy + s);
                ctx.Arc(cx - s + c, cy + s - c, c, Math.PI / 2, Math.PI);
                ctx.LineTo(cx - s, cy - s + c);
                ctx.Arc(cx - s + c, cy - s + c, c, Math.PI, 3 * Math.PI / 2);
                ctx.ClosePath();
                break;
            case QuestShapes.Star:
                ctx.NewPath();
                for (int i = 0; i < 10; i++)
                {
                    double angle = -Math.PI / 2 + i * Math.PI / 5;
                    double radius = i % 2 == 0 ? r * 1.15 : r * 0.62;
                    double x = cx + Math.Cos(angle) * radius, y = cy + Math.Sin(angle) * radius;
                    if (i == 0) ctx.MoveTo(x, y); else ctx.LineTo(x, y);
                }
                ctx.ClosePath();
                break;
            case QuestShapes.Hexagon:
                ctx.NewPath();
                for (int i = 0; i < 6; i++)
                {
                    double angle = -Math.PI / 2 + i * Math.PI / 3;
                    double x = cx + Math.Cos(angle) * r * 1.05, y = cy + Math.Sin(angle) * r * 1.05;
                    if (i == 0) ctx.MoveTo(x, y); else ctx.LineTo(x, y);
                }
                ctx.ClosePath();
                break;
            default:
                ctx.NewPath();
                ctx.Arc(cx, cy, r, 0, 2 * Math.PI);
                break;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        texture.Dispose();
    }
}
