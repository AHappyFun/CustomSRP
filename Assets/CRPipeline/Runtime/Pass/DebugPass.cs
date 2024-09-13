using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class DebugPass
{
    private static readonly ProfilingSampler sampler = new ProfilingSampler("Debug Pass");

    
    //void Render(RenderGraphContext context)
    //{
    //    CameraDebugger.Render(context);
    //}

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Record(
        RenderGraph renderGraph,
        CustomRPSettings RPSettings,
        Camera camera,
        in LightResources lightResources)
    {

        if (CameraDebugger.IsActive && camera.cameraType <= CameraType.SceneView && !RPSettings.UseLightsPerObject)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out DebugPass pass, sampler);

            builder.ReadComputeBuffer(lightResources.tileBuffer);
            
            builder.SetRenderFunc<DebugPass>(static(pass, context)=> CameraDebugger.Render(context));
        }
        
    }
}