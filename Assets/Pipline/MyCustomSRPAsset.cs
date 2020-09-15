using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// 继承RenderPipelineAsset，是ScriptObject类型，相当于把管线的配置给序列化保存了
/// </summary>
[CreateAssetMenu(menuName = "Rendering/我的自定义SRP")]
public class MyCustomSRPAsset : RenderPipelineAsset
{
    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new CustomSRP();
    }
}

//自定义管线类，继承RenderPipeline，主要实现Render方法
public class CustomSRP: RenderPipeline
{
    private CommandBuffer commandBuffer;
    private ScriptableCullingParameters cullingParameters;
    private CullResults cullResults;

    public CustomSRP()
    {
        commandBuffer = new CommandBuffer { name = "Render Cam" };
        cullResults = new CullResults();
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    //遍历执行Camera的Render方法
    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);
        foreach (var cam in cameras)
        {
            Render(renderContext, cam);
        }
    }

    //单个Camera的Render方法
    private void Render(ScriptableRenderContext renderContext, Camera camera)
    {
        //command buffer 指令
        commandBuffer.ClearRenderTarget(true, false, Color.clear);
        commandBuffer.BeginSample("Render Cam");
        renderContext.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();

        var flags = camera.clearFlags;
        commandBuffer.ClearRenderTarget(
            (flags & CameraClearFlags.Depth) != 0,
            (flags & CameraClearFlags.Color) != 0,
            camera.backgroundColor );

        //剔除
        if(!CullResults.GetCullingParameters(camera, out cullingParameters))
        {
            return;
        }
#if UNITY_EDITOR
        if(camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
        CullResults.Cull(ref cullingParameters, renderContext, ref cullResults);


        //绘制
        renderContext.SetupCameraProperties(camera);
        renderContext.ExecuteCommandBuffer(commandBuffer);

        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("SRPDefaultUnlit"));
        drawSettings.sorting.flags = SortFlags.CommonOpaque;
        var filterSettings = new FilterRenderersSettings(true) { renderQueueRange = RenderQueueRange.opaque};

        //1.Opaque不透明
        renderContext.DrawRenderers(cullResults.visibleRenderers, ref drawSettings, filterSettings);
        //2.Skybox天空盒
        renderContext.DrawSkybox(camera);

        drawSettings.sorting.flags = SortFlags.CommonTransparent;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        //3.Transparent透明
        renderContext.DrawRenderers(cullResults.visibleRenderers, ref drawSettings, filterSettings);

        DrawDefaultPipeline(renderContext, camera);

        commandBuffer.EndSample("Render Cam");
        renderContext.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();

        renderContext.Submit();
    }

    Material error;
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    void DrawDefaultPipeline(ScriptableRenderContext context,Camera camera)
    {
        if(error == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            error = new Material(errorShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));
        drawSettings.SetOverrideMaterial(error, 0);
        var filterSettings = new FilterRenderersSettings(true);
        context.DrawRenderers(cullResults.visibleRenderers, ref drawSettings, filterSettings);
    }
}
