using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

//自定义管线类，继承RenderPipeline，主要实现Render方法
public partial class CustomRP: RenderPipeline
{

    private ScriptableCullingParameters cullingParameters;
    private CullingResults cullResults;

    bool useDynamicBatch, useGPUInstance, useLightsPerObject, openHDR;

    ShadowSetting shadowSettings;

    PostFXSettings postFXSettings;

    CameraRenderer renderer = new CameraRenderer();


    public CustomRP(bool openHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject, ShadowSetting shadowSetting, PostFXSettings postFXSettings)
    {
        cullResults = new CullingResults();

        this.openHDR = openHDR;
        this.useDynamicBatch = useDynamicBatching;
        this.useGPUInstance = useGPUInstancing;
        this.shadowSettings = shadowSetting;
        this.postFXSettings = postFXSettings;
        this.useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true; //灯光线性空间

        InitializeForEditor();
    }

   //public override void Dispose(bool disposing)
   //{
   //    base.Dispose();
   //}

    //遍历执行Camera的Render方法
    protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        foreach (var cam in cameras)
        {
            renderer.Render(renderContext, cam, openHDR, this.useDynamicBatch, this.useGPUInstance, useLightsPerObject, shadowSettings, this.postFXSettings);       
        }
    }
}



public partial class CustomRP : RenderPipeline
{
    partial void InitializeForEditor();

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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Lightmapping.ResetDelegate();
    }
#endif
}

