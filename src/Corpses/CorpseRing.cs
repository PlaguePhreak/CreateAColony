using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace CreateAColony.Corpses;

/// <summary>
/// The orange ring that fills around the cursor while right-click is held on a corpse.
/// One per client, shared by every corpse; the corpse being held tells it how far along it is.
/// </summary>
public class CorpseRing : IRenderer
{
    const int Segments = 32;
    const float OuterRadius = 24;
    const float InnerRadius = 18;
    const float FadeInSeconds = 0.2f;
    const float FadeOutSeconds = 0.4f;
    static readonly Vec4f Orange = new(1f, 0x95 / 255f, 0f, 1f);

    readonly ICoreClientAPI capi;
    readonly MeshRef[] meshes = new MeshRef[Segments + 1];
    Entity? holder;
    float progress;
    float alpha;

    public double RenderOrder => 0;
    public int RenderRange => 10;

    public CorpseRing(ICoreClientAPI capi)
    {
        this.capi = capi;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "createacolony-corpsering");
    }

    public void Show(Entity corpse, float fraction)
    {
        holder = corpse;
        progress = GameMath.Clamp(fraction, 0, 1);
    }

    public void Hide(Entity corpse)
    {
        if (holder == corpse) holder = null;
    }

    /// <summary>The ring drawn up to a number of segments, made the first time it is needed.</summary>
    MeshRef MeshFor(int segments)
    {
        if (meshes[segments] != null) return meshes[segments];
        var data = new MeshData((segments + 1) * 2, segments * 6, false, false, true, false);
        float inner = InnerRadius / OuterRadius;
        for (int i = 0; i <= segments; i++)
        {
            double angle = (double)i / Segments * Math.PI * 2;
            float x = (float)Math.Sin(angle), y = -(float)Math.Cos(angle);
            data.AddVertexSkipTex(x, y, 0);
            data.AddVertexSkipTex(x * inner, y * inner, 0);
            if (i == 0) continue;
            int o = i * 2;
            data.AddIndices(new[] { o - 2, o - 1, o, o, o - 1, o + 1 });
        }
        return meshes[segments] = capi.Render.UploadMesh(data);
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        bool visible = holder != null;
        alpha = GameMath.Clamp(alpha + deltaTime / (visible ? FadeInSeconds : -FadeOutSeconds), 0, 1);
        int segments = (int)Math.Ceiling(progress * Segments);
        if (alpha <= 0 || segments <= 0) return;

        var render = capi.Render;
        var shader = render.CurrentActiveShader;
        shader.Uniform("rgbaIn", new Vec4f(Orange.R, Orange.G, Orange.B, alpha));
        shader.Uniform("extraGlow", 0);
        shader.Uniform("applyColor", 0);
        shader.Uniform("tex2d", 0);
        shader.Uniform("noTexture", 1f);
        shader.UniformMatrix("projectionMatrix", render.CurrentProjectionMatrix);

        int x = capi.Input.MouseGrabbed ? render.FrameWidth / 2 : capi.Input.MouseX;
        int y = capi.Input.MouseGrabbed ? render.FrameHeight / 2 : capi.Input.MouseY;
        render.GlPushMatrix();
        render.GlTranslate(x, y, 0);
        render.GlScale(OuterRadius, OuterRadius, 0);
        shader.UniformMatrix("modelViewMatrix", render.CurrentModelviewMatrix);
        render.GlPopMatrix();
        render.RenderMesh(MeshFor(segments));
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
        foreach (var mesh in meshes) mesh?.Dispose();
    }
}
