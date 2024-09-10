using System.Diagnostics;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class GizmosPass
{
#if UNITY_EDITOR
    //private CameraRenderer renderer;

    private bool requiresDepthCopy;

    private CameraRendererCopier Copier;

    private TextureHandle depthAttachment;

    private static readonly ProfilingSampler sampler = new ProfilingSampler("Gizmos Pass");
    
    void Render(RenderGraphContext context)
    {
        //if (renderer.useIntermediateBuffer)
        //{
        //    //有这行Scene显示不正确
        //    //renderer.Draw(CameraRenderer.depthAttachmentID, BuiltinRenderTextureType.CameraTarget, true);
        //    renderer.ExecuteBuffer();
        //}
        //context.renderContext.DrawGizmos(renderer.camera, GizmoSubset.PreImageEffects);
        //context.renderContext.DrawGizmos(renderer.camera, GizmoSubset.PostImageEffects);
        
        CommandBuffer buffer = context.cmd;
        ScriptableRenderContext renderContext = context.renderContext;
        if (requiresDepthCopy)
        {
            //Copier.CopyByDrawing(buffer, depthAttachment, BuiltinRenderTextureType.CameraTarget, true);
            renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }
        renderContext.DrawGizmos(Copier.Camera, GizmoSubset.PreImageEffects);
        renderContext.DrawGizmos(Copier.Camera, GizmoSubset.PostImageEffects);
    }
#endif

    [Conditional("UNITY_EDITOR")]
    public static void Record(
        RenderGraph renderGraph, 
        bool useIntermediateBuffer,
        CameraRendererCopier copier,
        in CameraRendererTextures textures
        )
    {
#if UNITY_EDITOR
        if (Handles.ShouldRenderGizmos())
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out GizmosPass pass, sampler);
            pass.requiresDepthCopy = useIntermediateBuffer;
            pass.Copier = copier;
            if (useIntermediateBuffer)
            {
                pass.depthAttachment = builder.ReadTexture(textures.depthAttachment);
            }
            builder.SetRenderFunc<GizmosPass>((pass, context) => pass.Render(context));
        }
#endif
    }
}
