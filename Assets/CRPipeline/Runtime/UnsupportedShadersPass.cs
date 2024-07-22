
using System.Diagnostics;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class UnsupportedShadersPass
{
#if UNITY_EDITOR
    private CameraRenderer renderer;
    
    private static readonly ProfilingSampler sampler = new ProfilingSampler("Unsupported Shaders");
    
    void Render(RenderGraphContext context) => renderer.DrawUnsupportShaders();
#endif

    [Conditional("UNITY_EDITOR")]
    public static void Record(RenderGraph renderGraph, CameraRenderer renderer)
    {
#if UNITY_EDITOR

        using RenderGraphBuilder builder =
            renderGraph.AddRenderPass(sampler.name, out UnsupportedShadersPass pass, sampler);

        pass.renderer = renderer;
        
        builder.SetRenderFunc<UnsupportedShadersPass>((pass, context)=> pass.Render(context));
#endif
    }

}
