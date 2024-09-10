
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class PostFXPass
{
    private PostFXStack postFXStack;

    private TextureHandle colorAttachment;
    
    private static readonly ProfilingSampler sampler = new ProfilingSampler("Post FX");

    public void Render(RenderGraphContext context) => postFXStack.Render(context, colorAttachment);

    public static void Record(RenderGraph renderGraph, PostFXStack postFXStack, in CameraRendererTextures textures)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out PostFXPass pass, sampler);
        pass.postFXStack = postFXStack;
        pass.colorAttachment = builder.ReadTexture(textures.colorAttachment);
        builder.SetRenderFunc<PostFXPass>((pass, context)=> pass.Render(context));
    }
}
