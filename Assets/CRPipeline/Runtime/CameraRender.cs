using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

/// <summary>
/// 核心Class，RenderPipeline调用Render方法
/// </summary>
public class CameraRenderer
{
    ScriptableRenderContext context;

    public Camera camera;
    
    PostFXStack postFXStack = new PostFXStack();

    static CameraSettings defaultCameraSettings = new CameraSettings();
    
    const string bufferName = "---Render Camera---";
#if UNITY_EDITOR
    string sampleName = bufferName;
#endif

    private CommandBuffer commandBuffer;

    private Material material;


    private Vector2Int bufferSize;

    public const float renderScaleMin = 0.1f, renderScaleMax = 2f;

    public CameraRenderer(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(material);
    }

    public void Render(RenderGraph renderGraph, ScriptableRenderContext ctx, Camera cam, CustomRPSettings customRPSettings)
    {
        context = ctx;
        camera = cam;

        CameraBufferSettings cameraBufferSettings = customRPSettings.cameraBufferSettings;
        PostFXSettings postFXSettings = customRPSettings.postFXSettings;
        ShadowSetting shadowSetting = customRPSettings.shadowSetting;
        bool useLightsPerObject = customRPSettings.UseLightsPerObject;
        
        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;
        ProfilingSampler cameraSampler = crpCamera ? crpCamera.Sampler : ProfilingSampler.Get(cam.cameraType);

        bool useColorTexture, useDepthTexture;
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

        bool enablePostFX = postFXSettings != null && postFXSettings.IsSupportPostFX(camera) && postFXSettings.Active;

        float renderScale = cameraSettings.GetRenderScale(cameraBufferSettings.renderScale);
        bool useScaledRendering = renderScale < 0.99f || renderScale > 1.0f;
        
#if UNITY_EDITOR
        //PrepareCameraBuffer();
#endif
        
#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(cam);
            useScaledRendering = false;
        }
#endif

        if (!cam.TryGetCullingParameters(out ScriptableCullingParameters scriptableCullingParameters))
        {
            return;
        }

        scriptableCullingParameters.shadowDistance = Mathf.Min(shadowSetting.maxDistance, cam.farClipPlane);
        CullingResults cullingResults = context.Cull(ref scriptableCullingParameters);
        
        //--------------
        cameraBufferSettings.allowHDR &= camera.allowHDR;
        bool useHDR = cameraBufferSettings.allowHDR;
        
        Vector2Int bufferSize = default;
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

        cameraBufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;

        var renderGraphParameters = new RenderGraphParameters
        {
            commandBuffer = CommandBufferPool.Get(),
            currentFrameIndex = Time.frameCount,
            executionName = cameraSampler.name,
            rendererListCulling = true,
            scriptableRenderContext = context
        };
        
        //是否使用中间Buffer，就是是否拷贝深度和Color在中间用
        bool useIntermediateBuffer = useScaledRendering || useColorTexture || useDepthTexture || enablePostFX || !useLightsPerObject;
        
        //改用RenderGraph
        using (renderGraph.RecordAndExecute(renderGraphParameters))
        {
            using var _ = new RenderGraphProfilingScope(renderGraph, cameraSampler);
            
            //设置灯光数据、绘制ShadowMap
            LightResources lightResources = LightingPass.Record(renderGraph, cullingResults, bufferSize, shadowSetting, useLightsPerObject, cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
            
            //摄像机渲染物体相关设置
            CameraRendererTextures camTextures = SetupPass.Record(renderGraph,useIntermediateBuffer, useColorTexture, useDepthTexture, useHDR, bufferSize, camera);
            
            //画不透明几何
            GeometryPass.Record(renderGraph, cam, cullingResults, useLightsPerObject, cameraSettings.renderingLayerMask, true, camTextures, lightResources);
            
            //画Skybox几何
            SkyboxPass.Record(renderGraph, cam, camTextures);
            
            //Copy Color and Depth ，复制中间Buffer。复制的操作不会在FrameBuffer显示，会在RenderDoc显示。
            var copier = new CameraRendererCopier(material, camera, cameraSettings.finalBlendMode);
            CopyAttachmentsPass.Record(renderGraph, useColorTexture, useDepthTexture, copier, camTextures);
            
            //画半透明几何
            GeometryPass.Record(renderGraph, cam, cullingResults, useLightsPerObject, cameraSettings.renderingLayerMask, false, camTextures, lightResources);
            
            //画错误shader
            UnsupportedShadersPass.Record(renderGraph, cam, cullingResults);
            
            //后处理
            if (enablePostFX)
            {
                postFXStack.BufferSettings = cameraBufferSettings;
                postFXStack.BufferSize = bufferSize;
                postFXStack.Camera = camera;
                postFXStack.FinalBlendMode = cameraSettings.finalBlendMode;
                postFXStack.Settings = postFXSettings;
                PostFXPass.Record(renderGraph, postFXStack, (int)customRPSettings.colorLutResolution, cameraSettings.keepAlpha, camTextures);
            }
            else if (useIntermediateBuffer)
            {
                FinalPass.Record(renderGraph, copier, camTextures);
            }
            
            //画Gizmos
            GizmosPass.Record(renderGraph, useIntermediateBuffer, copier, camTextures);
        }

        //清理RT等资源
        Cleanup();
        
        context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
        context.Submit();
        
        CommandBufferPool.Release(renderGraphParameters.commandBuffer);
    }
    

#if UNITY_EDITOR
    void PrepareCameraBuffer()
    {
        commandBuffer.name = sampleName = camera.name;
    }
#else 
    void DrawGizmosBeforePostProcess();
   
    void DrawGizmosAfterPostProcess();
#endif

    public void ExecuteBuffer()
    {
        //执行和清除buffer通常在一起
        context.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
    }
    
    void Cleanup()
    {
     
    }
}
