
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class UnsupportedShadersPass
{
#if UNITY_EDITOR
    private CameraRenderer renderer;
    
    //buildin 旧Tag
    static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("Always"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    public static Material errorMat;
    
    RendererListHandle list;

    private static readonly ProfilingSampler sampler = new ProfilingSampler("Unsupported Shaders");

    void Render(RenderGraphContext context)
    {
        context.cmd.DrawRendererList(list);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    } // => renderer.DrawUnsupportShaders();
#endif

    [Conditional("UNITY_EDITOR")]
    public static void Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults)
    {
#if UNITY_EDITOR

        using RenderGraphBuilder builder =
            renderGraph.AddRenderPass(sampler.name, out UnsupportedShadersPass pass, sampler);

        if (errorMat == null)
        {
            errorMat = new(Shader.Find("Hidden/InternalErrorShader"));
        }


        pass.list = builder.UseRendererList(renderGraph.CreateRendererList(
            new RendererListDesc(legacyShaderTagIds, cullingResults, camera)
            {
                overrideMaterial = errorMat,
                renderQueueRange = RenderQueueRange.all
            }));


        
        builder.SetRenderFunc<UnsupportedShadersPass>(static(pass, context)=> pass.Render(context));
#endif
    }
}
