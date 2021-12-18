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
    [SerializeField]
    bool UseDynamicBatching = true, UseGPUInstancing = true, UseSRPBatcher = true;

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRP(UseDynamicBatching, UseGPUInstancing, UseSRPBatcher);
    }
}

//自定义管线类，继承RenderPipeline，主要实现Render方法
public class CustomRP: RenderPipeline
{

    private ScriptableCullingParameters cullingParameters;
    private CullingResults cullResults;

    bool useDynamicBatch, useGPUInstance;

    CameraRenderer renderer = new CameraRenderer();


    public CustomRP(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher)
    {
        cullResults = new CullingResults();

        this.useDynamicBatch = useDynamicBatching;
        this.useGPUInstance = useGPUInstancing;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
    }

   //public override void Dispose(bool disposing)
   //{
   //    base.Dispose();
   //}

    //遍历执行Camera的Render方法
    protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        foreach (var cam in cameras)
        {
            renderer.Render(renderContext, cam, this.useDynamicBatch, this.useGPUInstance);       
        }
    }

    /*
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
        if(!CullingResults.GetCullingParameters(camera, out cullingParameters))
        {
            return;
        }
#if UNITY_EDITOR
        if(camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
        CullingResults.Cull(ref cullingParameters, renderContext, ref cullResults);


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
    */
}

public class CameraRenderer
{
    ScriptableRenderContext context;

    Camera camera;

    static ShaderTagId unlitShaderTagID = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("Always"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    static Material errorMat;

    const string bufferName = "---Render Camera---";
# if UNITY_EDITOR
     string sampleName = bufferName;
#endif
    private CommandBuffer commandBuffer = new CommandBuffer { name = bufferName };

    public void Render(ScriptableRenderContext ctx, Camera cam, bool useDynamicBatch, bool useGPUIInstance)
    {
        context = ctx;
        camera = cam;

#if UNITY_EDITOR
        PrepareCameraBuffer();
#endif

        PrepareUIForSceneWindow();

        //剔除检测
        if (!Cull()) 
        {
            return;
        }

        Setup();

        DrawVisableGeometry(useDynamicBatch, useGPUIInstance);

        DrawUnsupportShaders();

        DrawGizmos();

        Submit();
    }

    void Setup()
    {
        context.SetupCameraProperties(camera);

        CameraClearFlags clearFlags = camera.clearFlags;


        //commandBuffer里注入样本，可以在Profiler和FrameDebugger里看到，需要有开始和结束
        commandBuffer.BeginSample(bufferName);

        //渲染前ClearRT设置  是否清除Depth、Color、Stencil三个buffer
        commandBuffer.ClearRenderTarget(
            clearFlags <= CameraClearFlags.Depth,
            clearFlags == CameraClearFlags.Color,
            clearFlags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
        );
        //commandBuffer.ClearRenderTarget(true, true, Color.clear); 

        ExecuteBuffer();

    }

    /// <summary>
    /// 画几何体
    /// </summary>
    void DrawVisableGeometry(bool useDynamicBatch, bool useGPUIInstance)
    {
        //排序设置、绘制设置 、过滤设置
        SortingSettings sortingSettings = new SortingSettings(camera) {
            criteria = SortingCriteria.CommonOpaque
        };

        //不透明
        DrawingSettings drawingSettings = new DrawingSettings(unlitShaderTagID, sortingSettings) {
            enableDynamicBatching = useDynamicBatch,
            enableInstancing = useGPUIInstance
        };

        FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        //天空盒
        context.DrawSkybox(camera);

        //透明
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    /// <summary>
    /// 画错误的shader
    /// </summary>
    void DrawUnsupportShaders()
    {
        if (!errorMat)
        {
            errorMat = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        DrawingSettings drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera)) {
            overrideMaterial = errorMat
        };
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    /// <summary>
    /// 画Gizmos
    /// </summary>
    void DrawGizmos()
    {
        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    /// <summary>
    /// 让UI可以在Scene渲染
    /// </summary>
    void PrepareUIForSceneWindow()
    {
        if(camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }

#if UNITY_EDITOR
    void PrepareCameraBuffer()
    {
        commandBuffer.name = sampleName = camera.name;
    }
#endif

    void Submit()
    {
        commandBuffer.EndSample(bufferName);

        ExecuteBuffer();

        context.Submit();
    }

    void ExecuteBuffer()
    {
        //执行和清除buffer通常在一起
        context.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
    }

    CullingResults cullingResults;
    bool Cull()
    {
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
}
