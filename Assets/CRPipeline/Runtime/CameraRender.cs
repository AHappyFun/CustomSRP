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

    Lighting lighting = new Lighting();
    
    PostFXStack postFXStack = new PostFXStack();

    static CameraSettings defaultCameraSettings = new CameraSettings();

    //private bool useScaledRendering;
    //private bool useHDR;
    
    //private static int frameBufferID = Shader.PropertyToID("_CameraFrameBuffer");
    //public static int bufferSizeID = Shader.PropertyToID("_CameraBufferSize");
    //public static int colorAttachmentID = Shader.PropertyToID("_CameraColorAttachment");
    //public static int depthAttachmentID = Shader.PropertyToID("_CameraDepthAttachment");
    //public static int colorTextureID = Shader.PropertyToID("_CameraColorTexture");
    //public static int depthTextureID = Shader.PropertyToID("_CameraDepthTexture");
    //public static int sourceTextureID = Shader.PropertyToID("_SourceTexture");
    //public static int srcBlendID = Shader.PropertyToID("_CameraSrcBlend");
    //public static int dstBlendID = Shader.PropertyToID("_CameraDstBlend");

    //public bool useColorTexture, useDepthTexture, useIntermediateBuffer;

   // private static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;
    
    const string bufferName = "---Render Camera---";
#if UNITY_EDITOR
    string sampleName = bufferName;
#endif
    //private CommandBuffer commandBuffer = new CommandBuffer { name = bufferName };

    private CommandBuffer commandBuffer;

    private Material material;

    //private Texture2D missingTexture;

    private Vector2Int bufferSize;

    public const float renderScaleMin = 0.1f, renderScaleMax = 2f;

    public CameraRenderer(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        //missingTexture = new Texture2D(1,1)
        //{
        //    hideFlags = HideFlags.HideAndDontSave,
        //    name = "Missing"
        //};
        //missingTexture.SetPixel(0,0,Color.white * 0.5f);
        //missingTexture.Apply(true, true);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(material);
        //CoreUtils.Destroy(missingTexture);
    }

    public void Render(RenderGraph renderGraph, ScriptableRenderContext ctx, Camera cam, CameraBufferSettings cameraBufferSettings, bool useLightsPerObject ,ShadowSetting shadowSetting, PostFXSettings postFXSettings, int colorLUTResolution)
    {
        context = ctx;
        camera = cam;
        
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

        //剔除检测
        //if (!Cull(shadowSetting.maxDistance))
        //{
        //    return;
        //}
        if (!cam.TryGetCullingParameters(out ScriptableCullingParameters scriptableCullingParameters))
        {
            return;
        }

        scriptableCullingParameters.shadowDistance = Mathf.Min(shadowSetting.maxDistance, cam.farClipPlane);
        CullingResults cullingResults = context.Cull(ref scriptableCullingParameters);
        
        //--------------
        bool useHDR = cameraBufferSettings.allowHDR && camera.allowHDR;
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
        postFXStack.Setup(cam, bufferSize, postFXSettings, cameraSettings.keepAlpha, useHDR, colorLUTResolution, cameraSettings.finalBlendMode, cameraBufferSettings.bicubicRescalingMode, cameraBufferSettings.fxaa);

        //var cameraSampler = new ProfilingSampler(cam.name);
        var renderGraphParameters = new RenderGraphParameters
        {
            commandBuffer = CommandBufferPool.Get(),
            currentFrameIndex = Time.frameCount,
            executionName = cameraSampler.name,
            rendererListCulling = true,
            scriptableRenderContext = context
        };
        
        //是否使用中间Buffer，就是是否拷贝深度和Color在中间用
        bool useIntermediateBuffer = useScaledRendering || useColorTexture || useDepthTexture || postFXStack.IsActive;
        
        //改用RenderGraph
        using (renderGraph.RecordAndExecute(renderGraphParameters))
        {
            using var _ = new RenderGraphProfilingScope(renderGraph, cameraSampler);
            
            //设置灯光数据、绘制ShadowMap
            LightingPass.Record(renderGraph, lighting, cullingResults, shadowSetting, useLightsPerObject, cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
            
            //摄像机渲染物体相关设置
            CameraRendererTextures textures = SetupPass.Record(renderGraph,useIntermediateBuffer, useColorTexture, useDepthTexture, useHDR, bufferSize, camera);
            
            //画不透明几何
            GeometryPass.Record(renderGraph, cam, cullingResults, useLightsPerObject, cameraSettings.renderingLayerMask, true, textures);
            
            //画Skybox几何
            SkyboxPass.Record(renderGraph, cam, textures);
            
            //Copy Color and Depth ，复制中间Buffer。复制的操作不会在FrameBuffer显示，会在RenderDoc显示。
            var copier = new CameraRendererCopier(material, camera, cameraSettings.finalBlendMode);
            CopyAttachmentsPass.Record(renderGraph, useColorTexture, useDepthTexture, copier, textures);
            
            //画半透明几何
            GeometryPass.Record(renderGraph, cam, cullingResults, useLightsPerObject, cameraSettings.renderingLayerMask, false, textures);
            
            //画错误shader
            UnsupportedShadersPass.Record(renderGraph, cam, cullingResults);
            
            //后处理
            if (postFXStack.IsActive)
            {
                PostFXPass.Record(renderGraph, postFXStack, textures);
            }
            else if (useIntermediateBuffer)
            {
                FinalPass.Record(renderGraph, copier, textures);
            }
            
            //画Gizmos
            GizmosPass.Record(renderGraph, useIntermediateBuffer, copier, textures);
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

    void Submit()
    {
        //commandBuffer.EndSample(bufferName);

        ExecuteBuffer();

        context.Submit();
    }

    public void ExecuteBuffer()
    {
        //执行和清除buffer通常在一起
        context.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
    }

    //CullingResults cullingResults;
    //bool Cull(float maxShadowDistance)
    //{
    //    if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
    //    {
    //        //剔除参数
    //        p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
    //        
    //        //cullingResult是渲染对象集，通过ScriptableCullingParamters里的条件进行剔除对象
    //        cullingResults = context.Cull(ref p);
    //        return true;
    //    }
    //    return false;
    //}
    void Cleanup()
    {
        lighting.CleanUp();
    }
}
