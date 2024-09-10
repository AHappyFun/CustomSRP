
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

/// <summary>
/// CameraTextures相关 color和depth
/// </summary>
public ref struct CameraRendererTextures
{
    public TextureHandle colorAttachment, depthAttachment, colorCopy, depthCopy;

    public CameraRendererTextures(
        TextureHandle colorAttachment,
        TextureHandle depthAttachment,
        TextureHandle colorCopy,
        TextureHandle depthCopy)
    {
        this.colorAttachment = colorAttachment;
        this.depthAttachment = depthAttachment;
        this.colorCopy = colorCopy;
        this.depthCopy = depthCopy;
    }
}

/// <summary>
/// SetUp，摄像机渲染物体的设置
/// </summary>
public class SetupPass
{
    //private CameraRenderer renderer;

    private static readonly ProfilingSampler sampler = new ProfilingSampler("Setup");

    private bool useIntermediateAttachments;

    private TextureHandle colorAttachment, depthAttachment;

    private Vector2Int attachmentSize;

    private Camera camera;

    private CameraClearFlags clearFlags;

    private static readonly int attachmentSizeID = Shader.PropertyToID("_CameraBufferSize");

    void Render(RenderGraphContext context)
    {
        context.renderContext.SetupCameraProperties(camera);
        CommandBuffer cmd = context.cmd;
        if (useIntermediateAttachments)
        {
            cmd.SetRenderTarget(
                colorAttachment,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
                );
        }
        cmd.ClearRenderTarget(
            clearFlags <= CameraClearFlags.Depth,
            clearFlags <= CameraClearFlags.Color,
            clearFlags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
            );
        cmd.SetGlobalVector(attachmentSizeID, new Vector4(1f / attachmentSize.x, 1f/attachmentSize.y, attachmentSize.x, attachmentSize.y));
        
        context.renderContext.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    public static CameraRendererTextures Record(
        RenderGraph renderGraph, 
        bool useIntermediateAttachments,
        bool copyColor,
        bool copyDepth,
        bool useHDR,
        Vector2Int attachmentSize,
        Camera camera)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out SetupPass pass, sampler);
        //pass.renderer = renderer;
        pass.useIntermediateAttachments = useIntermediateAttachments;
        pass.attachmentSize = attachmentSize;
        pass.camera = camera;
        pass.clearFlags = camera.clearFlags;

        TextureHandle colorAttachment, depthAttachment;
        TextureHandle colorCopy = default, depthCopy = default;

        //CameraRendererTextures textures = new CameraRendererTextures();
        
        if (useIntermediateAttachments)
        {
            if (pass.clearFlags > CameraClearFlags.Color)
            {
                pass.clearFlags = CameraClearFlags.Color;
            }
            
            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
                name = "Color Attachment"
            };
            
            colorAttachment = pass.colorAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));
            if (copyColor)
            {
                desc.name = "Color Copy";
                colorCopy = renderGraph.CreateTexture(desc);
            }
            desc.depthBufferBits = DepthBits.Depth32;
            desc.name = "Depth Attachment";
            depthAttachment = pass.depthAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));
            if (copyDepth)
            {
                desc.name = "Depth Copy";
                depthCopy = renderGraph.CreateTexture(desc);
            }
        }
        else
        {
            colorAttachment = depthAttachment = pass.colorAttachment = pass.depthAttachment =
                builder.WriteTexture(renderGraph.ImportBackbuffer((BuiltinRenderTextureType.CameraTarget)));
        }
        
        builder.AllowPassCulling(false);
        builder.SetRenderFunc<SetupPass>(static(pass, context) => pass.Render(context));

        //return textures;
        return new CameraRendererTextures(colorAttachment, depthAttachment, colorCopy, depthCopy);
    }
}
