
using UnityEditor.Rendering.LookDev;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class VisibleGeometryPass
{
    private CameraRenderer renderer;

    private bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

    private int renderingLayerMask;
    
    private static readonly ProfilingSampler sampler = new ProfilingSampler("Visible Geometry");

    void Render(RenderGraphContext context) => renderer.DrawVisableGeometry(useDynamicBatching, useGPUInstancing,
        useLightsPerObject, renderingLayerMask);

    public static void Record(RenderGraph renderGraph, CameraRenderer renderer, bool useDynamicBatching,
        bool useGPUInstancing, bool useLightsPerObject, int renderingLayerMask)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out VisibleGeometryPass pass, sampler);
        pass.renderer = renderer;
        pass.useDynamicBatching = useDynamicBatching;
        pass.useGPUInstancing = useGPUInstancing;
        pass.useLightsPerObject = useLightsPerObject;
        pass.renderingLayerMask = renderingLayerMask;
        builder.SetRenderFunc<VisibleGeometryPass>((pass, context)=> pass.Render(context));
    }
}
