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


    const string bufferName = "---Render Camera---";
#if UNITY_EDITOR
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
        //设置灯光数据
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
        SortingSettings sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };

        //不透明
        DrawingSettings drawingSettings = new DrawingSettings(unlitShaderTagID, sortingSettings)
        {
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
        if (camera.cameraType == CameraType.SceneView)
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
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
}
