using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;


//自定义管线类，继承RenderPipeline，主要实现Render方法
public class CustomRP: RenderPipeline
{

    private ScriptableCullingParameters cullingParameters;
    private CullingResults cullResults;

    bool useDynamicBatch, useGPUInstance;

    ShadowSetting shadowSettings;

    CameraRenderer renderer = new CameraRenderer();


    public CustomRP(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, ShadowSetting shadowSetting)
    {
        cullResults = new CullingResults();

        this.useDynamicBatch = useDynamicBatching;
        this.useGPUInstance = useGPUInstancing;
        this.shadowSettings = shadowSetting;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
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
            renderer.Render(renderContext, cam, this.useDynamicBatch, this.useGPUInstance, shadowSettings);       
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

    static Material errorMat;

    Lighting lighting = new Lighting();

    static ShaderTagId unlitShaderTagID = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagID = new ShaderTagId("CustomLit");
    static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("Always"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };


    const string bufferName = "---Render Camera---";
# if UNITY_EDITOR
     string sampleName = bufferName;
#endif
    private CommandBuffer commandBuffer = new CommandBuffer { name = bufferName };

    public void Render(ScriptableRenderContext ctx, Camera cam, bool useDynamicBatch, bool useGPUIInstance, ShadowSetting shadowSetting)
    {
        context = ctx;
        camera = cam;

#if UNITY_EDITOR
        PrepareCameraBuffer();
#endif

        PrepareUIForSceneWindow();

        //剔除检测
        if (!Cull(shadowSetting.maxDistance)) 
        {
            return;
        }
        commandBuffer.BeginSample(sampleName);
        ExecuteBuffer();
        lighting.Setup(context, cullingResults, shadowSetting);
        commandBuffer.EndSample(sampleName);

        Setup();

        DrawVisableGeometry(useDynamicBatch, useGPUIInstance);

        DrawUnsupportShaders();

        DrawGizmos();

        lighting.CleanUp();

        Submit();
    }

    void Setup()
    {
        //设置摄像机的MVP矩阵以及其他属性
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
        drawingSettings.SetShaderPassName(1, litShaderTagID);

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
    bool Cull(float maxShadowDistance)
    {
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
}

public class Lighting
{

    CullingResults cullingResults;

    const int maxDirLightCount = 4;
    static int dirLightCountID = Shader.PropertyToID("_DirectionLightCount"); //Buildin也为最多4个
    static int dirLightColorsID = Shader.PropertyToID("_DirectionLightColors");
    static int dirLightDirectionsID = Shader.PropertyToID("_DirectionLightDirections");
    static int dirLightShadowDataID = Shader.PropertyToID("_DirectionLightShadowData");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirs = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    const string bufferName = "Lighting";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSetting shadowSetting)
    {
        this.cullingResults = cullingResults;

        buffer.BeginSample(bufferName);
        shadows.Setup(context, cullingResults, shadowSetting);
        SetupLights();
        shadows.Render();
        buffer.EndSample(bufferName);

        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights()
    {
        NativeArray<VisibleLight> visableLights = cullingResults.visibleLights;
        for (int i = 0; i < visableLights.Length; i++)
        {          
            VisibleLight light = visableLights[i];
            if(light.lightType == LightType.Directional)
            {
                SetupDirectionalLight(i, ref light);
                if (i >= maxDirLightCount)
                {
                    break;
                }
            }

        }
        buffer.SetGlobalInt(dirLightCountID, visableLights.Length);
        buffer.SetGlobalVectorArray(dirLightColorsID, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsID, dirLightDirs);
        buffer.SetGlobalVectorArray(dirLightShadowDataID, dirLightShadowData);
    }

    //传递灯光数据到Shader里
    void SetupDirectionalLight(int lightIndex, ref VisibleLight light)
    {
        dirLightColors[lightIndex] = light.finalColor;
        dirLightDirs[lightIndex] = -light.localToWorldMatrix.GetColumn(2); //从矩阵里拿到灯的方向       
        dirLightShadowData[lightIndex] = shadows.ReserveDirectionalShadows(light.light, lightIndex);
    }

    public void CleanUp()
    {
        shadows.CleanUp();
    }
}
