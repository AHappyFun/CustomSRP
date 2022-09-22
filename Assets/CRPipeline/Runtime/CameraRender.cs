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

    static CameraSettings defaultCameraSettings = new CameraSettings();

    private bool useHDR, useScaledRendering;

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

    //private static int frameBufferID = Shader.PropertyToID("_CameraFrameBuffer");
    private static int bufferSizeID = Shader.PropertyToID("_CameraBufferSize");
    private static int colorAttachmentID = Shader.PropertyToID("_CameraColorAttachment");
    private static int depthAttachmentID = Shader.PropertyToID("_CameraDepthAttachment");
    private static int colorTextureID = Shader.PropertyToID("_CameraColorTexture");
    private static int depthTextureID = Shader.PropertyToID("_CameraDepthTexture");
    private static int sourceTextureID = Shader.PropertyToID("_SourceTexture");
    private static int srcBlendID = Shader.PropertyToID("_CameraSrcBlend");
    private static int dstBlendID = Shader.PropertyToID("_CameraDstBlend");

    private bool useColorTexture, useDepthTexture, useIntermediateBuffer;

    private static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;
    
    const string bufferName = "---Render Camera---";
#if UNITY_EDITOR
    string sampleName = bufferName;
#endif
    private CommandBuffer commandBuffer = new CommandBuffer { name = bufferName };

    private Material material;

    private Texture2D missingTexture;

    private Vector2Int bufferSize;

    public const float renderScaleMin = 0.1f, renderScaleMax = 2f;

    public CameraRenderer(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        missingTexture = new Texture2D(1,1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Missing"
        };
        missingTexture.SetPixel(0,0,Color.white * 0.5f);
        missingTexture.Apply(true, true);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }

    public void Render(ScriptableRenderContext ctx, Camera cam, CameraBufferSettings cameraBufferSettings, bool useDynamicBatch, bool useGPUIInstance, bool useLightsPerObject ,ShadowSetting shadowSetting, PostFXSettings postFXSettings, int colorLUTResolution)
    {
        context = ctx;
        camera = cam;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;
        
        if (camera.cameraType == CameraType.Reflection)
        {
            useColorTexture = cameraBufferSettings.copyColorReflections;
            useDepthTexture = cameraBufferSettings.copyDepthReflections;
        }
        else
        {
            useColorTexture = cameraBufferSettings.copyColor && cameraSettings.CopyColor;
            useDepthTexture = cameraBufferSettings.copyDepth && cameraSettings.CopyDepth;
        }

        if (cameraSettings.overridePostFX)
        {
            if(cameraSettings.postFXSettings != null)
                postFXSettings = cameraSettings.postFXSettings;
        }

        float renderScale = cameraSettings.GetRenderScale(cameraBufferSettings.renderScale);
        useScaledRendering = renderScale < 0.99f || renderScale > 1.0f;
        
#if UNITY_EDITOR
        PrepareCameraBuffer();
#endif

        PrepareUIForSceneWindow();

        //剔除检测
        if (!Cull(shadowSetting.maxDistance))
        {
            return;
        }

        useHDR = cameraBufferSettings.allowHDR && camera.allowHDR;
        if (useScaledRendering)
        {
            renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
            bufferSize.x = (int) (camera.pixelWidth * renderScale);
            bufferSize.y = (int) (camera.pixelHeight * renderScale);
        }
        else
        {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }
        
        commandBuffer.BeginSample(sampleName);
        
        commandBuffer.SetGlobalVector(bufferSizeID, new Vector4(1f/bufferSize.x, 1f/bufferSize.y, bufferSize.x, bufferSize.y));
        
        ExecuteBuffer();
        //设置灯光数据、绘制ShadowMap
        lighting.Setup(context, cullingResults, shadowSetting, useLightsPerObject, cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);

        cameraBufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
        postFXStack.Setup(context, cam, bufferSize, postFXSettings, useHDR, colorLUTResolution, cameraSettings.finalBlendMode, cameraBufferSettings.bicubicRescalingMode, cameraBufferSettings.fxaa);
        
        commandBuffer.EndSample(sampleName);

        //摄像机渲染物体相关设置
        Setup();

        //画可见几何体
        DrawVisableGeometry(useDynamicBatch, useGPUIInstance, useLightsPerObject, cameraSettings.renderingLayerMask);

        //画错误shader
        DrawUnsupportShaders();

        //画Gizmos
        DrawGizmosBeforePostProcess();
        
        //叠加后处理
        if (postFXStack.IsActive)
        {
            postFXStack.Render(colorAttachmentID);
        }
        else if (useIntermediateBuffer)
        {
            DrawFinal(cameraSettings.finalBlendMode);
            ExecuteBuffer();
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

        useIntermediateBuffer = useScaledRendering || useColorTexture || useDepthTexture || postFXStack.IsActive;

        if (useIntermediateBuffer)
        {
            if (clearFlags > CameraClearFlags.Color)
            {
                clearFlags = CameraClearFlags.Color;
            }
            //HDR FrameBuffer R16B16G16A16_SFloat
            commandBuffer.GetTemporaryRT(colorAttachmentID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            commandBuffer.GetTemporaryRT(depthAttachmentID, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
            commandBuffer.SetRenderTarget(colorAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, 
                depthAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        }

        //commandBuffer里注入样本，可以在Profiler和FrameDebugger里看到，需要有开始和结束
        commandBuffer.BeginSample(bufferName);
        
        commandBuffer.SetGlobalTexture(colorTextureID, missingTexture);
        commandBuffer.SetGlobalTexture(depthTextureID, missingTexture);

        //渲染前ClearRT设置  是否清除Depth、Color、Stencil三个buffer
        commandBuffer.ClearRenderTarget(
            clearFlags <= CameraClearFlags.Depth,
            clearFlags == CameraClearFlags.Color,
            clearFlags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
        );

        ExecuteBuffer();

    }

    /// <summary>
    /// 画几何体
    /// </summary>
    void DrawVisableGeometry(bool useDynamicBatch, bool useGPUIInstance, bool useLightsPerObject, int renderingLayerMask)
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

        FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask : (uint)renderingLayerMask);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        //天空盒
        context.DrawSkybox(camera);

        //拷贝buffer到深度图和颜色图
        if (useColorTexture || useDepthTexture)
        {
            CopyAttachments();
        }

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
            useScaledRendering = false;
        }
    }
    
    /// <summary>
    /// 画Gizmos
    /// </summary>
    void DrawGizmosBeforePostProcess()
    {
        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            if (useIntermediateBuffer) {
                Draw(depthAttachmentID, BuiltinRenderTextureType.CameraTarget, true);
                ExecuteBuffer();
            }
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

    /// <summary>
    /// 复制Attachment到Color和Depth图
    /// </summary>
    void CopyAttachments()
    {
        if (useColorTexture)
        {
            commandBuffer.GetTemporaryRT(colorTextureID, bufferSize.x, bufferSize.y,
                0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
            );
            if (copyTextureSupported) {
                commandBuffer.CopyTexture(colorAttachmentID, colorTextureID);
            }
            else {
                Draw(colorAttachmentID, colorTextureID);
            }
        }
        
        if (useDepthTexture)
        {
            commandBuffer.GetTemporaryRT(depthTextureID, bufferSize.x, bufferSize.y,
                32, FilterMode.Point, RenderTextureFormat.Depth
            );
            if (copyTextureSupported)
            {
                commandBuffer.CopyTexture(depthAttachmentID, depthTextureID);
            }
            else
            {
                Draw(depthAttachmentID, depthTextureID, true);
            }
        }

        if (!copyTextureSupported)
        {
            commandBuffer.SetRenderTarget(
                colorAttachmentID,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                depthAttachmentID,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
        }
        
        ExecuteBuffer();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
        commandBuffer.SetGlobalTexture(sourceTextureID, from);
        commandBuffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        commandBuffer.DrawProcedural(Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }
    
    void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
    {
        
        commandBuffer.SetGlobalFloat(srcBlendID, (float)finalBlendMode.source);
        commandBuffer.SetGlobalFloat(dstBlendID, (float)finalBlendMode.destination);
        
        commandBuffer.SetGlobalTexture(sourceTextureID, colorAttachmentID);
        
        commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, 
            finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load
            , RenderBufferStoreAction.Store);
        
        commandBuffer.SetViewport(camera.pixelRect);
        
        commandBuffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        
        commandBuffer.SetGlobalFloat(srcBlendID, 1f);
        commandBuffer.SetGlobalFloat(dstBlendID, 0f);
    }

    void Cleanup()
    {
        lighting.CleanUp();
        if (useIntermediateBuffer)
        {
            commandBuffer.ReleaseTemporaryRT(colorAttachmentID);
            commandBuffer.ReleaseTemporaryRT(depthAttachmentID);

            if (useColorTexture)
            {
                commandBuffer.ReleaseTemporaryRT(colorTextureID);
            }
            if (useDepthTexture)
            {
                commandBuffer.ReleaseTemporaryRT(depthTextureID);
            }
        }
    }
}
