using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{

    CullingResults cullingResults;

    const int maxDirLightCount = 4;
    static int dirLightCountID = Shader.PropertyToID("_DirectionLightCount"); //Buildin也为最多4个
    static int dirLightColorsID = Shader.PropertyToID("_DirectionLightColors");
    static int dirLightDirectionsID = Shader.PropertyToID("_DirectionLightDirections");
    static int dirLightShadowDataID = Shader.PropertyToID("_DirectionLightShadowData");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
    static Vector4[] dirLightDirs = new Vector4[maxDirLightCount];
    static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

    const string bufferName = "Lighting";
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSetting shadowSetting)
    {
        this.cullingResults = cullingResults;

        buffer.BeginSample(bufferName);
        shadows.Setup(context, cullingResults, shadowSetting);
        SetupLights();
        shadows.Render();
        buffer.EndSample(bufferName);

        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights()
    {
        NativeArray<VisibleLight> visableLights = cullingResults.visibleLights;
        for (int i = 0; i < visableLights.Length; i++)
        {
            VisibleLight light = visableLights[i];
            if (light.lightType == LightType.Directional)
            {
                SetupDirectionalLight(i, ref light);
                if (i >= maxDirLightCount)
                {
                    break;
                }
            }

        }
        buffer.SetGlobalInt(dirLightCountID, visableLights.Length);
        buffer.SetGlobalVectorArray(dirLightColorsID, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsID, dirLightDirs);
        buffer.SetGlobalVectorArray(dirLightShadowDataID, dirLightShadowData);
    }

    //传递灯光数据到Shader里
    void SetupDirectionalLight(int lightIndex, ref VisibleLight light)
    {
        dirLightColors[lightIndex] = light.finalColor;
        dirLightDirs[lightIndex] = -light.localToWorldMatrix.GetColumn(2); //从矩阵里拿到灯的方向       
        dirLightShadowData[lightIndex] = shadows.ReserveDirectionalShadows(light.light, lightIndex);
    }

    public void CleanUp()
    {
        shadows.CleanUp();
    }
}
