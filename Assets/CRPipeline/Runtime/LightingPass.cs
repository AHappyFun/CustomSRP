
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class LightingPass
{
    private Lighting lighting;

    private CullingResults cullingResults;

    private ShadowSetting shadowSetting;

    private bool useLightsPerObject;

    private int renderingLayerMask;
    
    private static readonly ProfilingSampler sampler = new ProfilingSampler("Lighting Pass");

    void Render(RenderGraphContext context) => lighting.Setup(context, cullingResults, shadowSetting,
        useLightsPerObject, renderingLayerMask);

    public static void Record(RenderGraph renderGraph, Lighting lighting, CullingResults cullingResults,
        ShadowSetting shadowSetting, bool useLightsPerObject, int renderingLayerMask)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out LightingPass pass, sampler);
        pass.lighting = lighting;
        pass.cullingResults = cullingResults;
        pass.shadowSetting = shadowSetting;
        pass.useLightsPerObject = useLightsPerObject;
        pass.renderingLayerMask = renderingLayerMask;
        builder.SetRenderFunc<LightingPass>((pass, context) => pass.Render(context));
    }
}
