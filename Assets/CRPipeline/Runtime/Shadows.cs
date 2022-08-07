using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    //开阴影的方向光有数量限制
    ShadowedDirectionalLight[] shadowedDirectionLights = new ShadowedDirectionalLight[maxShadowdDirectionalLightCount];

    const string bufferName = "ShadowMap";

    const int maxShadowdDirectionalLightCount = 4 , maxCascades = 4;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    ShadowSetting settings;

    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"); 
    //static int shadowDistanceId = Shader.PropertyToID("_ShadowDistance");
    static int cascadeDataId = Shader.PropertyToID("_CascadeData");
    static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];
    static Vector4[] cascadeData = new Vector4[maxCascades];
    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowdDirectionalLightCount * maxCascades];

    private static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };

    //级联渐变方式
    private static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSetting settings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionLightCount = 0;
    }

    //记录当前平行光数量
    int ShadowedDirectionLightCount;
    
    //灯光阴影Data
    public Vector3 ReserveDirectionalShadows(Light light, int visableLightIndex)
    {
        //追踪投射阴影的灯光条件 1.数量没超限制 2.灯开了阴影 3.阴影强度不为0 4.GetShadowCasterBounds光源在场景里是否至少有一个ShadowCaster的物体
        if(ShadowedDirectionLightCount < maxShadowdDirectionalLightCount 
           && light.shadows != LightShadows.None 
           && light.shadowStrength > 0f 
           && cullingResults.GetShadowCasterBounds(visableLightIndex, out Bounds b))
        {
            shadowedDirectionLights[ShadowedDirectionLightCount] = new ShadowedDirectionalLight {
                visibleLightIndex = visableLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset =  light.shadowNearPlane
            };
            return new Vector3(
                light.shadowStrength, 
                settings.directional.cascadeCount * ShadowedDirectionLightCount++,
                light.shadowNormalBias
            );
        }
        return Vector3.zero;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Render()
    {
        if(ShadowedDirectionLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            //默认ShadowMap
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }

    //渲染平行光的ShadowMap，多灯光就要进行分块
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        //请求RT
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        //切换RenderTarget到ShadowMap
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //ClearBuffer先
        buffer.ClearRenderTarget(true, false, Color.clear);

        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        
        //ShadowMap划分Tile 4x4
        int tiles = ShadowedDirectionLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : (tiles <= 4 ? 2 : 4);
        int tileSize = atlasSize / split;
        for (int i = 0; i < ShadowedDirectionLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(
            shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f*f))
            );
        SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
        buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f/atlasSize));
        
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    //渲染单个平行光的ShadowMap
    //RenderShadow核心方法
    //ShadowMap的原理：从灯光的角度渲染场景，只保留深度信息。结果就是光线击中物体之前传播了多远。
    //但是定向光为无限远，没有具体位置。因此要找出与灯光方向匹配的viewMatrix和projectionMatrix，并提供一个裁剪空间立方体。
    //阴影投射使用的正交投影
    //加入Cascade级联
    void RenderDirectionalShadows(int dirLightIndex, int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionLights[dirLightIndex];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);

        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = dirLightIndex * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);

        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;
            
            if(dirLightIndex == 0)
            {
                SetCaseData(i, splitData.cullingSphere, tileSize);
            }

            int tileIndex = tileOffset + i;

            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            //VP矩阵传给Shader，以便转换到灯光Clip空间，然后采样ShadowMap
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, split);
            
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            //绘制
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }

    }

    void SetCaseData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float) settings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere; //储存平方
        cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize* 1.4142136f);
  
    }

    void SetKeywords(string[] keywords, int enableIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enableIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    /// <summary>
    /// 设置ShadowMap的绘制偏移
    /// </summary>
    /// <param name="tileIndex">格子的Index</param>
    /// <param name="split">划分数量，灯光数量和级联数量少的特殊情况下是1和2，正常是4</param>
    /// <param name="tileSize">每个格子的像素尺寸</param>
    /// <returns></returns>
    Vector2 SetTileViewport(int tileIndex, int split, float tileSize)
    {
        Vector2 offset = new Vector2(tileIndex % split, tileIndex / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
        ));
        return offset;
    }

    /// <summary>
    /// 因为划分了格子，对矩阵进行调整
    /// </summary>
    /// <param name="m"></param>
    /// <param name="offset"></param>
    /// <param name="split"></param>
    /// <returns></returns>
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        //判断Zbuffer 反向
        //OpenGL之外的图形API都使用ReverseZ，以便提高近处的精度
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
        //从-1~1 转换到 0~1 缩放0.5再平移0.5
        //只有x 和 y做ScaleOffset
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }

    public void CleanUp()
    {
        if(ShadowedDirectionLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(dirShadowAtlasId);
            ExecuteBuffer();
        }
    }
}
