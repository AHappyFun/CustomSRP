using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class Lighting
{

    CullingResults cullingResults;

    const int maxDirLightCount = 4;
    const int maxOtherLightCount = 64;
    
    static int dirLightCountID = Shader.PropertyToID("_DirectionLightCount"); //Buildin也为最多4个
    static int dirLightColorsID = Shader.PropertyToID("_DirectionLightColors");
    static int dirLightDirectionsID = Shader.PropertyToID("_DirectionLightDirectionsAndMasks");
    static int dirLightShadowDataID = Shader.PropertyToID("_DirectionLightShadowData");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirs = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    static int otherLightCountID = Shader.PropertyToID("_OtherLightCount");
    static int otherLightColorsID = Shader.PropertyToID("_OtherLightColors");
    static int otherLightPositionsID = Shader.PropertyToID("_OtherLightPositions");
    static int otherLightDirectionsID = Shader.PropertyToID("_OtherLightDirectionsAndMasks"); //for spot light
    static int otherLightSpotAnglesID = Shader.PropertyToID("_OtherLightSpotAngles"); //for spot light 
    static int otherLightShadowDataID = Shader.PropertyToID("_OtherLightShadowData");

    static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightDirections = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];

    static readonly GlobalKeyword lightsPerObjectKeyword = GlobalKeyword.Create("_LIGHTS_PER_OBJECT");
    
    static readonly GlobalKeyword shadowPCSSKeyword = GlobalKeyword.Create("_PCSS_SOFT");
    
    //const string bufferName = "Lighting";
    //CommandBuffer buffer = new CommandBuffer
    //{
    //    name = bufferName
    //};

    private CommandBuffer buffer;

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

    public ShadowTextures GetShadowTextures(RenderGraph renderGraph, RenderGraphBuilder builder)
    {
        return shadows.GetRenderTextures(renderGraph, builder);
    }

    /// <summary>
    /// 统计灯光数量以及设置灯光数据Data
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
            VisibleLight light = visableLights[i];
            Light l = light.light;
            
            if ((l.renderingLayerMask & renderingLayerMasks) != 0)
            {
                switch (light.lightType)
                {
                    case LightType.Directional:
                        if (dirLightCount < maxDirLightCount)
                        {
                            SetupDirectionalLight(dirLightCount++, i, ref light, l);
                        }
                        break;
                    case LightType.Point:
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;
                            SetupPointLight(otherLightCount++, i, ref light, l);
                        }
                        break;
                    case LightType.Spot:
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;
                            SetupSpotLight(otherLightCount++, i, ref light, l);
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

    public void Render(RenderGraphContext context)
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
        if (dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorsID, dirLightColors);
            buffer.SetGlobalVectorArray(dirLightDirectionsID, dirLightDirs);
            buffer.SetGlobalVectorArray(dirLightShadowDataID, dirLightShadowData);
        }
        
        //otherlight
        buffer.SetGlobalInt(otherLightCountID, otherLightCount);
        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsID, otherLightColors);
            buffer.SetGlobalVectorArray(otherLightPositionsID, otherLightPositions);
            buffer.SetGlobalVectorArray(otherLightDirectionsID, otherLightDirections);
            buffer.SetGlobalVectorArray(otherLightSpotAnglesID, otherLightSpotAngles);
            buffer.SetGlobalVectorArray(otherLightShadowDataID, otherLightShadowData);
        }
        
        //渲染ShadowMap
        shadows.Render(context);
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //传递Dir灯光数据到Shader里
    void SetupDirectionalLight(int lightIndex, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
        dirLightColors[lightIndex] = visibleLight.finalColor;  //finalColor已经被instensity影响，unity默认没有转换到线性空间
        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2); //从矩阵里拿到灯的方向，就是Z轴的方向      
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        dirLightDirs[lightIndex] = dirAndMask;   
        dirLightShadowData[lightIndex] = shadows.ReserveDirectionalShadows(light, visibleIndex); //灯光的阴影数据
    }

    //point light
    void SetupPointLight(int lightIndex, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
        otherLightColors[lightIndex] = visibleLight.finalColor;
        Vector4 pos = visibleLight.localToWorldMatrix.GetColumn(3); //最后一列是位移
        pos.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        
        otherLightPositions[lightIndex] = pos;
        
        otherLightSpotAngles[lightIndex] = new Vector4(0f, 1f);
        
        Vector4 dirAndMask = Vector4.zero;
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        otherLightDirections[lightIndex] = dirAndMask;

        otherLightShadowData[lightIndex] = shadows.ReserveOtherShadows(light, visibleIndex);
    }

    //spot light
    void SetupSpotLight(int lightIndex, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
        otherLightColors[lightIndex] = visibleLight.finalColor;
        
        Vector4 pos = visibleLight.localToWorldMatrix.GetColumn(3); //最后一列是位移
        pos.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        
        otherLightPositions[lightIndex] = pos;
        
        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        otherLightDirections[lightIndex] = dirAndMask;

        //Light l = visibleLight.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[lightIndex] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        
        otherLightShadowData[lightIndex] = shadows.ReserveOtherShadows(visibleLight.light, visibleIndex);
    }

    public void CleanUp()
    {
        shadows.CleanUp();
    }
}
