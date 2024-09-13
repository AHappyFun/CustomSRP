
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class GeometryPass
{
    private static readonly ProfilingSampler samplerOpaque = new ProfilingSampler("Opaque Geometry");
    private static readonly ProfilingSampler samplerTransparent = new ProfilingSampler("Transparent Geometry");

    static ShaderTagId[] ShaderTagIds =
    {
        new ShaderTagId("SRPDefaultUnlit"),//SRP默认Tag
        new ShaderTagId("CustomLit")//自定义受光材质Tag
    };

    private RendererListHandle list;

    void Render(RenderGraphContext context)
    {
        context.cmd.DrawRendererList(list);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }

    public static void Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults, bool useLightsPerObject, int renderingLayerMask, bool opaque, in CameraRendererTextures camTextures, in LightResources lightResources)
    {
        ProfilingSampler sampler = opaque ? samplerOpaque : samplerTransparent;
        
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out GeometryPass pass, sampler);
        
        pass.list = builder.UseRendererList(renderGraph.CreateRendererList(
            new RendererListDesc(ShaderTagIds, cullingResults, camera)
            {
                sortingCriteria = opaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent,
                rendererConfiguration = PerObjectData.Lightmaps 
                                        | PerObjectData.ShadowMask
                                        | PerObjectData.LightProbe
                                        | PerObjectData.LightProbeProxyVolume
                                        | PerObjectData.OcclusionProbe 
                                        | PerObjectData.OcclusionProbeProxyVolume
                                        | PerObjectData.ReflectionProbes
                                        | (useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None),
                renderQueueRange = opaque ? RenderQueueRange.opaque : RenderQueueRange.transparent,
                renderingLayerMask = (uint)renderingLayerMask
            }));

        builder.ReadWriteTexture(camTextures.colorAttachment);
        builder.ReadWriteTexture(camTextures.depthAttachment);

        if (!opaque)
        {
            if (camTextures.colorCopy.IsValid())
            {
                builder.ReadTexture(camTextures.colorCopy);
            }
            if (camTextures.depthCopy.IsValid())
            {
                builder.ReadTexture(camTextures.depthCopy);
            }
        }

        builder.ReadComputeBuffer(lightResources.dirLightDataBuffer);
        builder.ReadComputeBuffer(lightResources.otherLightDataBuffer);
        
        //读取TileBuffer，做tile光照
        if (lightResources.tileBuffer.IsValid())
        {
            builder.ReadComputeBuffer(lightResources.tileBuffer);
        }
        
        builder.ReadTexture(lightResources.shadowResources.directionalAtlas);
        builder.ReadTexture(lightResources.shadowResources.otherAtlas);
        builder.ReadComputeBuffer(lightResources.shadowResources.directionalCascadeShadowBuffer);
        builder.ReadComputeBuffer(lightResources.shadowResources.directionalShadowMatrixBuffer);
        builder.ReadComputeBuffer(lightResources.shadowResources.otherShadowDataBuffer);

        
        builder.SetRenderFunc<GeometryPass>(static(pass, context) => pass.Render(context));
    }
}
