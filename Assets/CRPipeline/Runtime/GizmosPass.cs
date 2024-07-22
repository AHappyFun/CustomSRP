using System.Diagnostics;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class GizmosPass
{
#if UNITY_EDITOR
    private CameraRenderer renderer;

    private static readonly ProfilingSampler sampler = new ProfilingSampler("Gizmos Pass");
    
    void Render(RenderGraphContext context)
    {
        if (renderer.useIntermediateBuffer)
        {
            //有这行Scene显示不正确
            //renderer.Draw(CameraRenderer.depthAttachmentID, BuiltinRenderTextureType.CameraTarget, true);
            renderer.ExecuteBuffer();
        }
        context.renderContext.DrawGizmos(renderer.camera, GizmoSubset.PreImageEffects);
        context.renderContext.DrawGizmos(renderer.camera, GizmoSubset.PostImageEffects);
    }
#endif

    [Conditional("UNITY_EDITOR")]
    public static void Record(RenderGraph renderGraph, CameraRenderer renderer)
    {
#if UNITY_EDITOR
        if (Handles.ShouldRenderGizmos())
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out GizmosPass pass, sampler);
            pass.renderer = renderer;
            builder.SetRenderFunc<GizmosPass>((pass, context) => pass.Render(context));
        }
#endif
    }
}
