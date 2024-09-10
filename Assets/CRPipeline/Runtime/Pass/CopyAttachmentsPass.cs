
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public readonly struct CameraRendererCopier
{
    private static readonly int sourceTextureID = Shader.PropertyToID("_SourceTexture");
    private static readonly int srcBlendID = Shader.PropertyToID("_CameraSrcBlend");
    private static readonly int dstBlendID = Shader.PropertyToID("_CameraDstBlend");

    private static readonly Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);

    private static readonly bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    private readonly CameraSettings.FinalBlendMode finalBlendMode;

    public static bool RequiresRenderTargetResetAfterCopy => !copyTextureSupported;

    public readonly Camera Camera => camera;

    private readonly Material material;

    private readonly Camera camera;

    public CameraRendererCopier(Material material, Camera camera, CameraSettings.FinalBlendMode finalBlendMode)
    {
        this.material = material;
        this.camera = camera;
        this.finalBlendMode = finalBlendMode;
    }

    public readonly void Copy(
        CommandBuffer buffer,
        RenderTargetIdentifier from,
        RenderTargetIdentifier to,
        bool isDepth)
    {
        if (copyTextureSupported)
        {
            buffer.CopyTexture(from, to);
        }
        else
        {
            CopyByDrawing(buffer, from, to, isDepth);
        }
    }

    public readonly void CopyByDrawing(
        CommandBuffer buffer,
        RenderTargetIdentifier from,
        RenderTargetIdentifier to,
        bool isDepth)
    {
        buffer.SetGlobalTexture(sourceTextureID, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    public readonly void CopyToCameraTarget(
        CommandBuffer buffer,
        RenderTargetIdentifier from)
    {
        buffer.SetGlobalFloat(srcBlendID, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendID, (float)finalBlendMode.destination);
        buffer.SetGlobalTexture(sourceTextureID, from);
        buffer.SetRenderTarget(
            BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero
            && camera.rect == fullViewRect ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(
            Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        buffer.SetGlobalFloat(srcBlendID, 1f);
        buffer.SetGlobalFloat(dstBlendID, 0f);
        
    }
    


}

/// <summary>
/// Copy Attachments Pass，复制Color和Depth到Texture，用于其他阶段使用。
/// </summary>
public class CopyAttachmentsPass
{
    private static readonly ProfilingSampler sampler = new ProfilingSampler("Copy Attachments Pass");

    //private CameraRenderer renderer;

    private bool CopyColor, CopyDepth;

    private CameraRendererCopier Copier;

    private TextureHandle colorAttachment, depthAttachment, colorCopy, depthCopy;

    private static readonly int colorCopyID = Shader.PropertyToID("_CameraColorTexture");
    private static readonly int depthCopyID = Shader.PropertyToID("_CameraDepthTexture");

    void Render(RenderGraphContext context)
    {
        //renderer.CopyAttachments();
        CommandBuffer buffer = context.cmd;
        if (CopyColor)
        {
            Copier.Copy(buffer, colorAttachment, colorCopy, false);
            buffer.SetGlobalTexture(colorCopyID, colorCopy);
        }
        if (CopyDepth)
        {
            Copier.Copy(buffer, depthAttachment, depthCopy, true);
            buffer.SetGlobalTexture(depthCopyID, depthCopy);
        }

        if (CameraRendererCopier.RequiresRenderTargetResetAfterCopy)
        {
            buffer.SetRenderTarget(
                colorAttachment,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                depthAttachment,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
                );
        }
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public static void Record(
        RenderGraph renderGraph, 
        bool copyColor,
        bool copyDepth,
        CameraRendererCopier copier,
        in CameraRendererTextures textures
        )
    {
        if (copyColor || copyDepth)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out CopyAttachmentsPass pass, sampler);

            pass.CopyColor = copyColor;
            pass.CopyDepth = copyDepth;
            pass.Copier = copier;

            pass.colorAttachment = builder.ReadTexture(textures.colorAttachment);
            pass.depthAttachment = builder.ReadTexture(textures.depthAttachment);

            if (copyColor)
            {
                pass.colorCopy = builder.WriteTexture(textures.colorCopy);
            }

            if (copyDepth)
            {
                pass.depthCopy = builder.WriteTexture(textures.depthCopy);
            }
           
            builder.SetRenderFunc<CopyAttachmentsPass>((pass, context) => pass.Render(context));
        }
        
       
    }

}
