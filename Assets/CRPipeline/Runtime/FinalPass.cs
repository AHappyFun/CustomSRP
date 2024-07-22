
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class FinalPass
{
    private CameraRenderer renderer;

    private CameraSettings.FinalBlendMode finalBlendMode;

    private static readonly ProfilingSampler sampler = new ProfilingSampler("Final");

    void Render(RenderGraphContext context)
    {
        renderer.DrawFinal(finalBlendMode);
        renderer.ExecuteBuffer();
    }

    public static void Record(RenderGraph renderGraph, CameraRenderer renderer,
        CameraSettings.FinalBlendMode finalBlendMode)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out FinalPass pass, sampler);
        pass.renderer = renderer;
        pass.finalBlendMode = finalBlendMode;
        builder.SetRenderFunc<FinalPass>((pass, context)=> pass.Render(context));
    }
}
