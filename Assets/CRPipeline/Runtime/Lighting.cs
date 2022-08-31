using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{

    CullingResults cullingResults;

    const int maxDirLightCount = 4;
    const int maxOtherLightCount = 64;
    
    static int dirLightCountID = Shader.PropertyToID("_DirectionLightCount"); //Buildin也为最多4个
    static int dirLightColorsID = Shader.PropertyToID("_DirectionLightColors");
    static int dirLightDirectionsID = Shader.PropertyToID("_DirectionLightDirections");
    static int dirLightShadowDataID = Shader.PropertyToID("_DirectionLightShadowData");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirs = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    static int otherLightCountID = Shader.PropertyToID("_OtherLightCount");
    static int otherLightColorsID = Shader.PropertyToID("_OtherLightColors");
    static int otherLightPositionsID = Shader.PropertyToID("_OtherLightPositions");
    static int otherLightDirectionsID = Shader.PropertyToID("_OtherLightDirections"); //for spot light
    static int otherLightSpotAnglesID = Shader.PropertyToID("_OtherLightSpotAngles"); //for spot light 
    static int otherLightShadowDataID = Shader.PropertyToID("_OtherLightShadowData");

    static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightDirections = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
    static Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];

    static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";
    
    const string bufferName = "Lighting";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSetting shadowSetting, bool useLightsPerobject)
    {
        this.cullingResults = cullingResults;

        buffer.BeginSample(bufferName);
        shadows.Setup(context, cullingResults, shadowSetting);
        //灯光数据
        SetupLights(useLightsPerobject);
        //渲染ShadowMap
        shadows.Render();
        
        buffer.EndSample(bufferName);

        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights(bool useLightsPerobject)
    {
        NativeArray<int> indexMap = useLightsPerobject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;

        //只设置可见光 剔除结果
        NativeArray<VisibleLight> visableLights = cullingResults.visibleLights;

        int dirLightCount = 0, otherLightCount = 0;
        int i;
        for (i = 0; i < visableLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight light = visableLights[i];
            switch (light.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                    {
                        SetupDirectionalLight(dirLightCount++, i, ref light);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupPointLight(otherLightCount++, i, ref light);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupSpotLight(otherLightCount++, i, ref light);
                    }
                    break;
            }

            if (useLightsPerobject)
            {
                indexMap[i] = newIndex;
            }

        }
        
        //剔除不可见的光
        if (useLightsPerobject)
        {
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }
        else
        {
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }
        
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
        
    }

    //传递Dir灯光数据到Shader里
    void SetupDirectionalLight(int lightIndex, int visibleIndex, ref VisibleLight light)
    {
        dirLightColors[lightIndex] = light.finalColor;  //finalColor已经被instensity影响，unity默认没有转换到线性空间
        dirLightDirs[lightIndex] = -light.localToWorldMatrix.GetColumn(2); //从矩阵里拿到灯的方向，就是Z轴的方向       
        dirLightShadowData[lightIndex] = shadows.ReserveDirectionalShadows(light.light, visibleIndex); //灯光的阴影数据
    }

    //point light
    void SetupPointLight(int lightIndex, int visibleIndex, ref VisibleLight light)
    {
        otherLightColors[lightIndex] = light.finalColor;
        Vector4 pos = light.localToWorldMatrix.GetColumn(3); //最后一列是位移
        pos.w = 1f / Mathf.Max(light.range * light.range, 0.00001f);
        
        otherLightPositions[lightIndex] = pos;
        otherLightDirections[lightIndex] = Vector4.one;
        otherLightSpotAngles[lightIndex] = new Vector4(0f, 1f);

        otherLightShadowData[lightIndex] = shadows.ReserveOtherShadows(light.light, visibleIndex);
    }

    //spot light
    void SetupSpotLight(int lightIndex, int visibleIndex, ref VisibleLight light)
    {
        otherLightColors[lightIndex] = light.finalColor;
        Vector4 pos = light.localToWorldMatrix.GetColumn(3); //最后一列是位移
        pos.w = 1f / Mathf.Max(light.range * light.range, 0.00001f);
        
        otherLightPositions[lightIndex] = pos;
        otherLightDirections[lightIndex] = -light.localToWorldMatrix.GetColumn(2);

        Light l = light.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * l.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[lightIndex] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        
        otherLightShadowData[lightIndex] = shadows.ReserveOtherShadows(light.light, visibleIndex);
    }

    public void CleanUp()
    {
        shadows.CleanUp();
    }
}
