
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class FinalPass
{
    //private CameraRenderer renderer;

    private CameraSettings.FinalBlendMode finalBlendMode;

    private static readonly ProfilingSampler sampler = new ProfilingSampler("Final Pass");

    private CameraRendererCopier Copier;

    private TextureHandle colorAttachment;

    void Render(RenderGraphContext context)
    {
        //renderer.DrawFinal(finalBlendMode);
        //renderer.ExecuteBuffer();
        CommandBuffer buffer = context.cmd;
        Copier.CopyToCameraTarget(buffer, colorAttachment);
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public static void Record(RenderGraph renderGraph, CameraRendererCopier copier, in CameraRendererTextures textures)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out FinalPass pass, sampler);
        //pass.renderer = renderer;
        //pass.finalBlendMode = finalBlendMode;
        pass.Copier = copier;
        pass.colorAttachment = builder.ReadTexture(textures.colorAttachment);
        builder.SetRenderFunc<FinalPass>((pass, context)=> pass.Render(context));
    }
}
