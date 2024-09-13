using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

//自定义管线类，继承RenderPipeline，主要实现Render方法
public partial class CustomRP: RenderPipeline
{
    //bool useDynamicBatch, useGPUInstance, useLightsPerObject;
    //bool useLightsPerObject;
    //CameraBufferSettings cameraBufferSettings;
    //ShadowSetting shadowSettings;
    //PostFXSettings postFXSettings;
    //private int colorLUTResolution;

    CameraRenderer renderer;
    
    private readonly CustomRPSettings RPSettings;
    
    public CustomRP(CustomRPSettings settings)
    {
        this.RPSettings = settings;
        GraphicsSettings.useScriptableRenderPipelineBatching = settings.UseSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true; //灯光线性空间
        renderer = new CameraRenderer(settings.cameraRenderShader, settings.cameraForwardPlusDebuggerShader);

        InitializeForEditor();
    }

    //遍历执行Camera的Render方法
    protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //foreach (var cam in cameras)
        //{
        //    renderer.Render(renderContext, cam, cameraBufferSettings, this.useDynamicBatch, this.useGPUInstance, useLightsPerObject, shadowSettings, this.postFXSettings, colorLUTResolution);       
        //}
    }

    //遍历执行Camera的Render方法 新
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        foreach (var cam in cameras)
        {
            renderer.Render(renderGraph, context, cam, RPSettings);       
        }
        renderGraph.EndFrame();
    }
}



public partial class CustomRP : RenderPipeline
{
    private readonly RenderGraph renderGraph = new RenderGraph("CRP RenderGraph");
    
    partial void InitializeForEditor();

    partial void DisposeForEditor();
    
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor();
        renderer.Dispose();
        renderGraph.Cleanup();
    }

#if UNITY_EDITOR

    partial void InitializeForEditor()
    {
        Lightmapping.SetDelegate(lightsDelegate);
    }
    
    private static Lightmapping.RequestLightsDelegate lightsDelegate =
        (Light[] lights, NativeArray<LightDataGI> output) =>
        {
            var lightData = new LightDataGI();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                switch (light.type)
                {
                    case LightType.Directional:
                        var dirLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref dirLight);
                        lightData.Init(ref dirLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        var rectLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectLight);
                        rectLight.mode = LightMode.Baked;
                        lightData.Init(ref rectLight);
                        break;

                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }

                lightData.falloff = FalloffType.InverseSquared;
                output[i] = lightData;
            }
        };

    partial void DisposeForEditor()
    {
        Lightmapping.ResetDelegate();
    }

#endif
}

