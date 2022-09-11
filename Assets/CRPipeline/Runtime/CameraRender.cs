using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraRenderer
{
    ScriptableRenderContext context;

    Camera camera;

    static Material errorMat;

    Lighting lighting = new Lighting();
    
    PostFXStack postFXStack = new PostFXStack();

    private bool useHDR;

    static ShaderTagId unlitShaderTagID = new ShaderTagId("SRPDefaultUnlit"); //SRP默认Tag
    static ShaderTagId litShaderTagID = new ShaderTagId("CustomLit");  //自定义受光材质Tag
    //buildin 旧Tag
    static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("Always"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    private static int frameBufferID = Shader.PropertyToID("_CameraFrameBuffer");


    const string bufferName = "---Render Camera---";
#if UNITY_EDITOR
    string sampleName = bufferName;
#endif
    private CommandBuffer commandBuffer = new CommandBuffer { name = bufferName };

    public void Render(ScriptableRenderContext ctx, Camera cam, bool openHDR, bool useDynamicBatch, bool useGPUIInstance, bool useLightsPerObject ,ShadowSetting shadowSetting, PostFXSettings postFXSettings, int colorLUTResolution)
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

        useHDR = openHDR && cam.allowHDR;
        
        commandBuffer.BeginSample(sampleName);
        ExecuteBuffer();
        //设置灯光数据、绘制ShadowMap
        lighting.Setup(context, cullingResults, shadowSetting, useLightsPerObject);
        
        postFXStack.Setup(context, cam, postFXSettings, useHDR, colorLUTResolution);
        
        commandBuffer.EndSample(sampleName);

        //摄像机渲染物体相关设置
        Setup();

        //画可见几何体
        DrawVisableGeometry(useDynamicBatch, useGPUIInstance, useLightsPerObject);

        //画错误shader
        DrawUnsupportShaders();

        //画Gizmos
        DrawGizmosBeforePostProcess();
        
        //叠加后处理
        if (postFXStack.IsActive)
        {
            postFXStack.Render(frameBufferID);
        }
        
        DrawGizmosAfterPostProcess();

        //清理RT等资源
        Cleanup();

        Submit();
    }

    
    void Setup()
    {
        //设置摄像机的MVP矩阵以及其他属性
        context.SetupCameraProperties(camera);

        CameraClearFlags clearFlags = camera.clearFlags;

        if (postFXStack.IsActive)
        {
            if (clearFlags > CameraClearFlags.Color)
            {
                clearFlags = CameraClearFlags.Color;
            }
            //HDR FrameBuffer R16B16G16A16_SFloat
            commandBuffer.GetTemporaryRT(frameBufferID, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            commandBuffer.SetRenderTarget(frameBufferID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

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
    void DrawVisableGeometry(bool useDynamicBatch, bool useGPUIInstance, bool useLightsPerObject)
    {

        PerObjectData lightsPerObjectsFlags =
            useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        
        //排序设置、绘制设置 、过滤设置
        SortingSettings sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };

        //不透明
        DrawingSettings drawingSettings = new DrawingSettings(unlitShaderTagID, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatch,
            enableInstancing = useGPUIInstance,
            perObjectData = PerObjectData.Lightmaps 
                            | PerObjectData.ShadowMask
                            | PerObjectData.LightProbe
                            | PerObjectData.LightProbeProxyVolume
                            | PerObjectData.OcclusionProbe 
                            | PerObjectData.OcclusionProbeProxyVolume
                            | PerObjectData.ReflectionProbes
                            | lightsPerObjectsFlags
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
        DrawingSettings drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera))
        {
            overrideMaterial = errorMat
        };
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 让UI可以在Scene渲染
    /// </summary>
    void PrepareUIForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }
    
    /// <summary>
    /// 画Gizmos
    /// </summary>
    void DrawGizmosBeforePostProcess()
    {
        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        }
    }
    
    void DrawGizmosAfterPostProcess()
    {
        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }
    
    void PrepareCameraBuffer()
    {
        commandBuffer.name = sampleName = camera.name;
    }
#else 
    void DrawGizmosBeforePostProcess();
   
    void DrawGizmosAfterPostProcess();
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
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //剔除参数
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            
            //cullingResult是渲染对象集，通过ScriptableCullingParamters里的条件进行剔除对象
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Cleanup()
    {
        lighting.CleanUp();
        if (postFXStack.IsActive)
        {
            commandBuffer.ReleaseTemporaryRT(frameBufferID);
        }
    }
}
