
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class SetupPass
{
    private CameraRenderer renderer;

    private static readonly ProfilingSampler sampler = new ProfilingSampler("Setup");

    void Render(RenderGraphContext context) => renderer.Setup();

    public static void Record(RenderGraph renderGraph, CameraRenderer renderer)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out SetupPass pass, sampler);
        pass.renderer = renderer;
        builder.SetRenderFunc<SetupPass>((pass, context) => pass.Render(context));
    }
}
