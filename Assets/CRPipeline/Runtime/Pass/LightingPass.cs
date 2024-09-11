
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public readonly ref struct LightResources
{
    public readonly ComputeBufferHandle dirLightDataBuffer;
    public readonly ComputeBufferHandle otherLightDataBuffer;
    public readonly ShadowResources shadowResources;

    public LightResources(ComputeBufferHandle otherLightDataBuffer, ComputeBufferHandle dirLightDataBuffer, ShadowResources shadowResources)
    {
        this.dirLightDataBuffer = dirLightDataBuffer;
        this.otherLightDataBuffer = otherLightDataBuffer;
        this.shadowResources = shadowResources;
    }

}

public partial class LightingPass
{
    private static readonly ProfilingSampler sampler = new ProfilingSampler("Light ShadowMap Pass");
    
    void Render(RenderGraphContext context) => RenderLighting(context);

    public static LightResources Record(RenderGraph renderGraph, CullingResults cullingResults,
        ShadowSetting shadowSetting, bool useLightsPerObject, int renderingLayerMask)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out LightingPass pass, sampler);
        
        pass.dirLightDataBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(new ComputeBufferDesc
        {
            name = "Direction Light Data",
            count =  maxDirLightCount,
            stride = DirectionalLightData.stride
        }));
        
        pass.otherLightDataBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(new ComputeBufferDesc
        {
            name = "Other Light Data",
            count =  maxOtherLightCount,
            stride = OtherLightData.stride
        }));

        pass.Setup(cullingResults, shadowSetting, useLightsPerObject, renderingLayerMask);
        
        builder.SetRenderFunc<LightingPass>(static(pass, context) => pass.Render(context));
        
        builder.AllowPassCulling(false);
        
        return new LightResources(
            pass.otherLightDataBuffer, 
            pass.dirLightDataBuffer,
            pass.GetShadowResources(renderGraph, builder)
       );
    }
}


partial class LightingPass
{   
    
    //structbuffer OtherLightData
    [StructLayout(LayoutKind.Sequential)]
    public struct OtherLightData
    {
        public const int stride = 4 * 4 * 5;

        public Vector4 color, position, directionAndMask, spotAngle, shadowData;
        
        public static OtherLightData CreatePointLight(ref VisibleLight visibleLight, Light light, Vector4 shadowData)
        {
            OtherLightData data;
            data.color = visibleLight.finalColor;
            data.position = visibleLight.localToWorldMatrix.GetColumn(3); //最后一列是位移
            data.position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            data.spotAngle = new Vector4(0f, 1f);
            data.directionAndMask = Vector4.zero;
            data.directionAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
            data.shadowData = shadowData;

            return data;
        }
    
        public static OtherLightData CreateSpotLight(ref VisibleLight visibleLight, Light light, Vector4 shadowData)
        {
            OtherLightData data;
            data.color = visibleLight.finalColor;
            data.position = visibleLight.localToWorldMatrix.GetColumn(3); //最后一列是位移
            data.position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        
            //Light l = visibleLight.light;
            float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
            float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
            float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
            data.spotAngle = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
            data.directionAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
            data.directionAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        
            data.shadowData = shadowData;

            return data;
        }
    }

    //structbuffer DirectionLightData
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectionalLightData
    {
        public const int stride = 4 * 4 * 3;

        public Vector4 color, directionAndMask, shadowData;

        public DirectionalLightData(ref VisibleLight visibleLight, Light light, Vector4 shadowData)
        {
            color = visibleLight.finalColor;  //finalColor已经被instensity影响，unity默认没有转换到线性空间
            Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2); //从矩阵里拿到灯的方向，就是Z轴的方向      
            dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
            directionAndMask = dirAndMask;
            this.shadowData = shadowData;
        }
    }

    public void RenderLighting(RenderGraphContext context)
    {
        CommandBuffer buffer = context.cmd;
        
        //pcss软阴影设置
        if (shadows.settings.UsePCSS)
        {
            buffer.SetKeyword(shadowPCSSKeyword, true);
            buffer.SetGlobalFloat(Shadows.pcssShadowLightWidthId, shadows.settings.PCSSLightWidth);
            buffer.SetGlobalFloat("_PCSSBias", shadows.settings.Bias);
        }
        else
        {
            buffer.SetKeyword(shadowPCSSKeyword, false);
        }
        
        buffer.SetKeyword(lightsPerObjectKeyword, useLightsPerObject);
        
        //dir light
        buffer.SetGlobalInt(dirLightCountID, dirLightCount);
        buffer.SetBufferData(dirLightDataBuffer, directionalLightData, 0, 0, dirLightCount);
        buffer.SetGlobalBuffer(dirLightDataID, dirLightDataBuffer);
        
        //otherlight
        buffer.SetGlobalInt(otherLightCountID, otherLightCount);
        buffer.SetBufferData(otherLightDataBuffer, otherLightData, 0, 0, otherLightCount);
        buffer.SetGlobalBuffer(otherLightDataID, otherLightDataBuffer);
        
        //渲染ShadowMap
        shadows.Render(context);
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    
    
    //----------------------------------------//
    
    const int maxDirLightCount = 4;
    const int maxOtherLightCount = 64;
    
    private ComputeBufferHandle otherLightDataBuffer;
    private ComputeBufferHandle dirLightDataBuffer;
    
    static int dirLightCountID = Shader.PropertyToID("_DirectionLightCount"); //Buildin也为最多4个
    static int dirLightDataID = Shader.PropertyToID("_DirectionLightData");

    static int otherLightCountID = Shader.PropertyToID("_OtherLightCount");
    static int otherLightDataID = Shader.PropertyToID("_OtherLightData"); //buffer组合下面的几个

    //使用SturctedBuffer代替
    private static readonly DirectionalLightData[] directionalLightData= new DirectionalLightData[maxDirLightCount];
    //使用OtherLightData SturctedBuffer代替
    private static readonly OtherLightData[] otherLightData = new OtherLightData[maxOtherLightCount];

    static readonly GlobalKeyword lightsPerObjectKeyword = GlobalKeyword.Create("_LIGHTS_PER_OBJECT");
    static readonly GlobalKeyword shadowPCSSKeyword = GlobalKeyword.Create("_PCSS_SOFT");
    
    CullingResults cullingResults;
    Shadows shadows = new Shadows();

    private int dirLightCount;
    private int otherLightCount;
    private bool useLightsPerObject;

    public void Setup(CullingResults cullingResults, ShadowSetting shadowSetting, bool useLightsPerobject, int renderingLayerMask)
    {
        this.cullingResults = cullingResults;
        this.useLightsPerObject = useLightsPerobject;
        //Shadow设置
        shadows.Setup(cullingResults, shadowSetting);
        //灯光数据
        SetupLights(renderingLayerMask);
        
    }
    public ShadowResources GetShadowResources(RenderGraph renderGraph, RenderGraphBuilder builder)
    {
        return shadows.GetShadowRenderResource(renderGraph, builder);
    }

    /// <summary>
    /// 统计灯光数量以及设置灯光数据Data到ShaderData里
    /// </summary>
    void SetupLights(int renderingLayerMasks)
    {
        NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;

        //只设置可见光 剔除结果
        NativeArray<VisibleLight> visableLights = cullingResults.visibleLights;

        dirLightCount = 0;
        otherLightCount = 0;
        
        int i;
        for (i = 0; i < visableLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visableLight = visableLights[i];
            Light l = visableLight.light;
            
            if ((l.renderingLayerMask & renderingLayerMasks) != 0)
            {
                switch (visableLight.lightType)
                {
                    case LightType.Directional:
                        if (dirLightCount < maxDirLightCount)
                        {
                            directionalLightData[dirLightCount++] = new DirectionalLightData(ref visableLight, l,shadows.ReserveDirectionalShadows(visableLight.light, i));
                        }
                        break;
                    case LightType.Point:
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;
                            otherLightData[otherLightCount++] = OtherLightData.CreatePointLight(ref visableLight, l, shadows.ReserveOtherShadows(visableLight.light, i));
                        }
                        break;
                    case LightType.Spot:
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;
                            otherLightData[otherLightCount++] = OtherLightData.CreateSpotLight(ref visableLight, l, shadows.ReserveOtherShadows(visableLight.light, i));
                        }
                        break;
                }
            }

            if (useLightsPerObject)
            {
                indexMap[i] = newIndex;
            }

        }
        
        //剔除不可见的光
        if (useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
        }
        
    }
        
    public void CleanUp()
    {
        shadows.CleanUp();
    }
}