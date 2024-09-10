
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LookDev;

public class SkyboxPass
{
    private static readonly ProfilingSampler sampler= new ProfilingSampler("Skybox");

    private Camera camera;

    void Render(RenderGraphContext context)
    {
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
        context.renderContext.DrawSkybox(camera);
    }

    public static void Record(RenderGraph renderGraph, Camera camera, in CameraRendererTextures textures)
    {
        if (camera.clearFlags == CameraClearFlags.Skybox)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out SkyboxPass pass, sampler);
            pass.camera = camera;
            builder.ReadWriteTexture(textures.colorAttachment);
            builder.ReadWriteTexture(textures.depthAttachment);
            builder.SetRenderFunc<SkyboxPass>(static(pass, context) => pass.Render(context));
        }
    }


}
