
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class LightingPass
{
    private Lighting lighting = new Lighting();
    
    private static readonly ProfilingSampler sampler = new ProfilingSampler("Lighting ShadowMap Pass");

    void Render(RenderGraphContext context) => lighting.Render(context);

    public static ShadowTextures Record(RenderGraph renderGraph, CullingResults cullingResults,
        ShadowSetting shadowSetting, bool useLightsPerObject, int renderingLayerMask)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out LightingPass pass, sampler);
        
        pass.lighting.Setup(cullingResults, shadowSetting, useLightsPerObject, renderingLayerMask);

        builder.SetRenderFunc<LightingPass>(static(pass, context) => pass.Render(context));
        
        builder.AllowPassCulling(false);

        return pass.lighting.GetShadowTextures(renderGraph, builder);
    }
}
