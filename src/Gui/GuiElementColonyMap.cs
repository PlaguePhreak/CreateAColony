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

/// <summary>
/// The Town map: the ground, the colony's land tinted with a gold outline, each building's ground
/// (Town Hall gold, huts blue, buildings being built orange), the settlers as dots and the player.
/// North is up.
/// </summary>
public class GuiElementColonyMap : GuiElement
{
    readonly ColonyMapTerrain terrain;
    readonly ColonySnapshot snapshot;
    readonly Vec3d? player;

    public GuiElementColonyMap(ICoreClientAPI capi, ElementBounds bounds, ColonyMapTerrain terrain, ColonySnapshot snapshot, Vec3d? player) : base(capi, bounds)
    {
        this.terrain = terrain;
        this.snapshot = snapshot;
        this.player = player;
    }

    /// <summary>Size of one map pixel and where the map starts, in the given units, to fit a box of that size.</summary>
    public static (double Cell, double OffsetX, double OffsetY) Fit(ColonyMapTerrain terrain, double width, double height)
    {
        double cell = Math.Min(width / terrain.Width, height / terrain.Height);
        return (cell, (width - cell * terrain.Width) / 2, (height - cell * terrain.Height) / 2);
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

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        var (cell, ox, oy) = Fit(terrain, Bounds.InnerWidth, Bounds.InnerHeight);
        double left = Bounds.drawX + ox, top = Bounds.drawY + oy;
        int w = terrain.Width, h = terrain.Height;
        var mask = ClaimMask(terrain, snapshot);

        // The ground, with the colony's land tinted gold.
        var pixels = (int[])terrain.Pixels.Clone();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (mask[i]) pixels[i] = ColonyMapTerrain.Mix(pixels[i], 255, 210, 90, 0.18f);
        }
        using (var image = new ImageSurface(Format.Argb32, w, h))
        {
            image.Flush();
            int stride = image.Stride / 4;
            var rowBuffer = new int[stride];
            for (int row = 0; row < h; row++)
            {
                Array.Copy(pixels, row * w, rowBuffer, 0, w);
                Marshal.Copy(rowBuffer, 0, image.DataPtr + row * image.Stride, stride);
            }
            image.MarkDirty();
            ctx.Save();
            ctx.Translate(left, top);
            ctx.Scale(cell, cell);
            using var pattern = new SurfacePattern(image) { Filter = Filter.Nearest };
            ctx.SetSource(pattern);
            ctx.Rectangle(0, 0, w, h);
            ctx.Fill();
            ctx.Restore();
        }

        // The edge of the colony's land: every side of a claimed pixel that touches unclaimed land.
        bool Claimed(int px, int pz) => px >= 0 && pz >= 0 && px < w && pz < h && mask[pz * w + px];
        ctx.SetSourceRGBA(1, 0.82, 0.25, 1);
        ctx.LineWidth = Math.Max(1.5, scaled(2));
        for (int pz = 0; pz < h; pz++)
        {
            for (int px = 0; px < w; px++)
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
            ctx.SetSourceRGBA(color[0], color[1], color[2], 0.35);
            ctx.Rectangle(x, y, bw, bh);
            ctx.FillPreserve();
            ctx.SetSourceRGBA(color[0], color[1], color[2], 1);
            ctx.LineWidth = Math.Max(1, scaled(1.5));
            if (b.UnderConstruction || b.Level == 0) ctx.SetDash(new[] { scaled(3), scaled(2) }, 0);
            ctx.Stroke();
            ctx.SetDash(Array.Empty<double>(), 0);
        }

        // Settlers, then the player on top.
        foreach (var c in snapshot.Citizens.Where(c => c.State != "dead" && (c.X != 0 || c.Z != 0)))
        {
            double[] color = c.Visitor ? new[] { 1.0, 0.9, 0.3 } : c.State == "downed" ? new[] { 0.9, 0.3, 0.3 } : new[] { 0.4, 0.95, 0.45 };
            Dot(ctx, WorldToScreen(c.X + 0.5, c.Z + 0.5, cell, left, top), scaled(3), color);
        }
        if (player != null) Dot(ctx, WorldToScreen(player.X, player.Z, cell, left, top), scaled(4), new[] { 1.0, 1.0, 1.0 });

        // North arrow.
        ctx.SetSourceRGBA(1, 1, 1, 0.9);
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(scaled(14));
        ctx.MoveTo(Bounds.drawX + scaled(6), Bounds.drawY + scaled(16));
        ctx.ShowText("N ↑");
    }

    (double X, double Y) WorldToScreen(double x, double z, double cell, double left, double top) => WorldToScreen(terrain, x, z, cell, left, top);

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
}
