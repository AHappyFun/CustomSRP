using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public readonly ref struct ShadowResources
{
    public readonly TextureHandle directionalAtlas, otherAtlas;

    public readonly ComputeBufferHandle directionalCascadeShadowBuffer;
    public readonly ComputeBufferHandle directionalShadowMatrixBuffer;
    public readonly ComputeBufferHandle otherShadowDataBuffer;

    public ShadowResources(
        TextureHandle directionalAtlas, 
        TextureHandle otherAtlas,
        ComputeBufferHandle directionalCascadeShadowBuffer,
        ComputeBufferHandle directionalShadowMatrixBuffer,
        ComputeBufferHandle otherShadowDataBuffer
        )
    {
        this.directionalAtlas = directionalAtlas;
        this.otherAtlas = otherAtlas;
        this.directionalCascadeShadowBuffer = directionalCascadeShadowBuffer;
        this.directionalShadowMatrixBuffer = directionalShadowMatrixBuffer;
        this.otherShadowDataBuffer = otherShadowDataBuffer;
    }
}

public partial class Shadows
{
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }
    
    //开阴影的方向光有数量限制
    ShadowedDirectionalLight[] shadowedDirectionLights = new ShadowedDirectionalLight[maxShadowdDirectionalLightCount];

    ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowdOtherLightCount];
    
    const int maxShadowdDirectionalLightCount = 4 , maxCascades = 4;
    const int maxShadowdOtherLightCount = 16;
    
    private TextureHandle directionalShadowAtlas, otherShadowAtlas;

    private ComputeBufferHandle otherShadowDataBuffer;
    private ComputeBufferHandle directionalCascadeShadowDataBuffer;
    private ComputeBufferHandle directionalShadowMatrixBuffer;

    private CommandBuffer buffer;

    ScriptableRenderContext context;

    CullingResults cullingResults;

    public ShadowSetting settings;

    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    
    static int dirShadowMatricesID = Shader.PropertyToID("_DirectionalShadowMatrices");
    static int directionalCascadeDataID = Shader.PropertyToID("_DirectionalCascadeData");
    
    static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    static int shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");  //ShadowPancking 阴影平坠开关
    
    static int otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas");
    static int otherShadowDataID = Shader.PropertyToID("_OtherShadowData");
    
    public static int pcssShadowLightWidthId = Shader.PropertyToID("_PCSSLightWidth");
    
    //---3个SturctedBuffer---
    private static readonly Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowdDirectionalLightCount * maxCascades];
    
    private static readonly DirectionalCascadeShadowData[] directionalCascadeShadowData =new DirectionalCascadeShadowData[maxCascades];
    
    private static readonly OtherShadowBufferData[] otherShadowData = new OtherShadowBufferData[maxShadowdOtherLightCount];

    //xy平行光 zw其他灯
    //x: atlasSize y: 1/atlasSize
    Vector4 atlasSizes;

    private static GlobalKeyword[] directionalFilterKeywords =
    {
        GlobalKeyword.Create("_DIRECTIONAL_PCF3") ,
        GlobalKeyword.Create("_DIRECTIONAL_PCF5"),
        GlobalKeyword.Create("_DIRECTIONAL_PCF7")
    };

    //级联渐变方式
    private static GlobalKeyword[] cascadeBlendKeywords =
    {
        GlobalKeyword.Create("_CASCADE_BLEND_SOFT"),
        GlobalKeyword.Create("_CASCADE_BLEND_DITHER")
    };

    private static GlobalKeyword[] shadowMaskKeywords =
    {
        GlobalKeyword.Create("_SHADOW_MASK_ALWAYS"),
        GlobalKeyword.Create("_SHADOW_MASK_DISTANCE")
    };

    private static GlobalKeyword[] otherFilerKeywords =
    {
        GlobalKeyword.Create("_OTHER_PCF3"),
        GlobalKeyword.Create("_OTHER_PCF5"),
        GlobalKeyword.Create("_OTHER_PCF7")
    };
    
    private bool useShadowMask;
    
    /// <summary>
    /// 获取Shadow TextureHandle
    /// </summary>
    public ShadowResources GetShadowRenderResource(RenderGraph renderGraph, RenderGraphBuilder builder)
    {
        int atlasSize = (int)settings.directional.atlasSize;
        var desc = new TextureDesc(atlasSize, atlasSize)
        {
            depthBufferBits = DepthBits.Depth32,
            isShadowMap = true, //加了这个就不会有StencilBuffer
            name = "Directional Shadow Atlas"
        };
        directionalShadowAtlas = ShadowedDirectionLightCount > 0
            ? builder.WriteTexture(renderGraph.CreateTexture(desc))
            : renderGraph.defaultResources.defaultShadowTexture;

        atlasSize = (int)settings.other.atlasSize;
        desc.width = desc.height = atlasSize;
        desc.name = "Other Shadow Atlas";

        otherShadowAtlas = ShadowedOtherLightCount > 0
            ? builder.WriteTexture(renderGraph.CreateTexture(desc))
            : renderGraph.defaultResources.defaultShadowTexture;

        otherShadowDataBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(new ComputeBufferDesc
        {
            name = "Other Shadow Data",
            stride = OtherShadowBufferData.stride,
            count = maxShadowdOtherLightCount
        }));
        
        directionalCascadeShadowDataBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(new ComputeBufferDesc
        {
            name = "Directional Shadow Data",
            stride = DirectionalCascadeShadowData.stride,
            count = maxCascades
        }));

        directionalShadowMatrixBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(new ComputeBufferDesc
        {
            name = "Directional Shadow Matrix",
            stride = 4 * 16,
            count = maxShadowdDirectionalLightCount * maxCascades
        }));

        return new ShadowResources(directionalShadowAtlas, otherShadowAtlas, directionalCascadeShadowDataBuffer, directionalShadowMatrixBuffer, otherShadowDataBuffer);
    }

    public void Setup(CullingResults cullingResults, ShadowSetting settings)
    {
        this.cullingResults = cullingResults;
        this.settings = settings;
        ShadowedDirectionLightCount = 0;
        ShadowedOtherLightCount = 0;
        this.useShadowMask = false;
    }

    //记录当前平行光数量，其他灯光数量
    int ShadowedDirectionLightCount, ShadowedOtherLightCount;
    
    //Dir灯光阴影Data
    //x ShadowStrength
    //y tileIndex
    //z normalBias
    //w maskChannel
    public Vector4 ReserveDirectionalShadows(Light light, int visableLightIndex)
    {
        //追踪投射阴影的灯光条件 1.数量没超限制 2.灯开了阴影 3.阴影强度不为0 
        if(ShadowedDirectionLightCount < maxShadowdDirectionalLightCount 
           && light.shadows != LightShadows.None 
           && light.shadowStrength > 0f)
        {
            float maskChannel = -1;
            LightBakingOutput lightBaking = light.bakingOutput;
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }
            
            //GetShadowCasterBounds光源在场景里是否至少有一个ShadowCaster的物体
            if (!cullingResults.GetShadowCasterBounds(visableLightIndex, out Bounds b))
            {
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }
            
            
            shadowedDirectionLights[ShadowedDirectionLightCount] = new ShadowedDirectionalLight {
                visibleLightIndex = visableLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset =  light.shadowNearPlane
            };
            return new Vector4(
                light.shadowStrength, 
                settings.directional.cascadeCount * ShadowedDirectionLightCount++,
                light.shadowNormalBias,
                maskChannel
            );
        }
        return new Vector4(0f ,0f ,0f ,-1f);
    }

    //其他灯光阴影Data
    //x ShadowStrength
    //y otherLightIndex
    //z isPoint
    //w maskChannel
    public Vector4 ReserveOtherShadows(Light light, int visableLightIndex)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }

        float maskChannel = -1;
        LightBakingOutput lightBaking = light.bakingOutput;
        if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed 
            && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        bool isPoint = light.type == LightType.Point;
        int newLightCount = ShadowedOtherLightCount + (isPoint ? 6 : 1);

        if (newLightCount > maxShadowdOtherLightCount ||
            !cullingResults.GetShadowCasterBounds(visableLightIndex, out Bounds b))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        shadowedOtherLights[ShadowedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visableLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint
        };
        
        Vector4 data = new Vector4(light.shadowStrength, ShadowedOtherLightCount++, isPoint ? 1f : 0f, maskChannel);
        ShadowedOtherLightCount = newLightCount;
        return data;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Render(RenderGraphContext context)
    {
        buffer = context.cmd;
        this.context = context.renderContext;
        //--------------
        //Draw ShadowMap 三种灯光的ShadowMap
        if(ShadowedDirectionLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        if (ShadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        
        //SetData
        buffer.SetGlobalBuffer(dirShadowMatricesID, directionalShadowMatrixBuffer);
        buffer.SetGlobalBuffer(directionalCascadeDataID, directionalCascadeShadowDataBuffer);
        buffer.SetGlobalBuffer(otherShadowDataID, otherShadowDataBuffer);
        
        buffer.SetGlobalTexture(dirShadowAtlasId, directionalShadowAtlas);
        buffer.SetGlobalTexture(otherShadowAtlasId, otherShadowAtlas);
        
        //---------------
        SetKeywords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
        
        //各个灯都需要的数据 
        buffer.SetGlobalInt(cascadeCountId, ShadowedDirectionLightCount > 0 ? settings.directional.cascadeCount : 0);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(
            shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f*f))
        );
        
        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
        
        ExecuteBuffer();
    }

    //渲染平行光的ShadowMap，多灯光 多级联 进行分块
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
        //请求RT
        //buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        
        //切换RenderTarget到ShadowMap
        buffer.SetRenderTarget(directionalShadowAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        
        //ClearBuffer先
        buffer.ClearRenderTarget(true, false, Color.clear);
        
        //平行光开启ShadowPanck
        buffer.SetGlobalFloat(shadowPancakingId, 1f);

        buffer.BeginSample("DirectionalShadows");
        ExecuteBuffer();
        
        //ShadowMap划分Tile 4x4
        int tiles = ShadowedDirectionLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : (tiles <= 4 ? 2 : 4);
        int tileSize = atlasSize / split;
        for (int i = 0; i < ShadowedDirectionLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        //Dir shadowMatrix buffer
        buffer.SetBufferData(directionalShadowMatrixBuffer, dirShadowMatrices, 0, 0, maxShadowdDirectionalLightCount * settings.directional.cascadeCount);

        //Dir shadowData buffer
        buffer.SetBufferData(directionalCascadeShadowDataBuffer, directionalCascadeShadowData, 0, 0, maxShadowdDirectionalLightCount);
        
        SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);

        
        buffer.EndSample("DirectionalShadows");
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
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Orthographic)
        {
            useRenderingLayerMaskTest = true
        };

        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = dirLightIndex * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);

        float tileScale = 1f / split;
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
               directionalCascadeShadowData[i] = new DirectionalCascadeShadowData(splitData.cullingSphere, tileSize, settings.directional.filter);
            }

            int tileIndex = tileOffset + i;

            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            //VP矩阵传给Shader，以便转换到灯光Clip空间，然后采样ShadowMap
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);
            
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            //绘制
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }

    }

    //渲染其他灯光的ShadowMap
    void RenderOtherShadows()
    {
        int atlasSize = (int)settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;
        //请求RT
       // buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
       
        //切换RenderTarget到ShadowMap
        buffer.SetRenderTarget(otherShadowAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        
        //ClearBuffer先
        buffer.ClearRenderTarget(true, false, Color.clear);
        
        buffer.SetGlobalFloat(shadowPancakingId, 0f);

        buffer.BeginSample("OtherLightShadows");
        ExecuteBuffer();
        
        //ShadowMap划分Tile 4x4
        int tiles = ShadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : (tiles <= 4 ? 2 : 4);
        int tileSize = atlasSize / split;
        for (int i = 0; i < ShadowedOtherLightCount;)
        {
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i++;
            }
        }
        
        buffer.SetBufferData(otherShadowDataBuffer, otherShadowData, 0, 0, ShadowedOtherLightCount);

        
        SetKeywords(otherFilerKeywords, (int)settings.other.filter - 1);
        
        buffer.EndSample("OtherLightShadows");
        ExecuteBuffer();
    }

    //渲染单个聚光灯的ShadowMap
    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Perspective)
        {
            useRenderingLayerMaskTest = true
        };
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;
        
        //动态计算NormalBias
        //因为透视矩阵，尖刺远处比较大近处小,normalbias也需要随着距离变大
        //单位像素的距离
        float distancePerTexelSize = 2f / (tileSize * projectionMatrix.m00);
        //根据PCF核的大小，2x2就偏1个像素，3x3就2个, 5x5就3个
        float filterSize = distancePerTexelSize * ((float) settings.other.filter + 1f);
        //NormalBias偏移考虑最大情况根号2
        float bias = light.normalBias * filterSize * 1.4142136f;
        
        Vector2 offset = SetTileViewport(index, split, tileSize);
        float tileScale = 1f / split;
        otherShadowData[index] = new OtherShadowBufferData(
            offset, 
            tileScale,
            bias, 
            atlasSizes.w * 0.5f,
            ConvertToAtlasMatrix(projectionMatrix * viewMatrix,offset,tileScale)
         );

        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }

    //渲染单个点光源的ShadowMap
    //渲染Cubemap 6个面
    void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex, BatchCullingProjectionType.Perspective)
        {
            useRenderingLayerMaskTest = true
        };

        //tan45 = 1 
        float texelSize = 2f / (tileSize);
        
        float filterSize = texelSize * ((float) settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        
        float tileScale = 1f / split;
        //fov bias 因为有边界，稍微扩大fov
        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        for (int i = 0; i < 6; i++)
        {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex,(CubemapFace)i, fovBias, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
            );
            //Y取反 因为unity画出来ShadowMap是反的，所以需要Y取反
            viewMatrix.m10 = -viewMatrix.m10;
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;
            
            shadowSettings.splitData = splitData;
            int tileIndex = index + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            otherShadowData[tileIndex] = new OtherShadowBufferData(
                offset, 
                tileScale,
                bias, 
                atlasSizes.w * 0.5f,
                ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale)
            );

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
        

    }

    void SetKeywords(GlobalKeyword[] keywords, int enableIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enableIndex)
            {
                buffer.SetKeyword(keywords[i], i == enableIndex);
            }
            //else
            //{
            //    buffer.SetKeyword(keywords[i]);
            //}
        }
    }

    /// <summary>
    /// 设置ShadowMap的绘制偏移 offset x 列 y 行
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
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
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
        //现在不会有默认RT了，不需要自己控制RealeaseRT
        
        //因为有一张默认的RT
        //buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        //if(ShadowedOtherLightCount > 0)
        //{
        //    buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        //}
        //ExecuteBuffer();
    }
}

partial class Shadows
{
    [StructLayout(LayoutKind.Sequential)]
    struct OtherShadowBufferData
    {
        public const int stride = 4 * 4 + 4 * 16;

        public Vector4 tileData;

        public Matrix4x4 shadowMatrix;

        public OtherShadowBufferData(Vector2 offset, float scale, float bias, float border, Matrix4x4 matrix)
        {
            //防止采样到边界之外 tiledata.xy是最小UV tiledata.xy + data.z是最大UV
            float uvPerHalfPixel = border;// atlasSizes.w * 0.5f;
            tileData.x = offset.x * scale + uvPerHalfPixel; //最小U + 半个像素UV
            tileData.y = offset.y * scale + uvPerHalfPixel; //最小V + 半个像素UV
            tileData.z = scale - 2 * uvPerHalfPixel; //UV的扩展范围减去一个像素
            tileData.w = bias;
            
            shadowMatrix = matrix;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DirectionalCascadeShadowData
    {
        public const int stride = 4 * 4 * 2;
        
        public Vector4 cullingSphere, cascadeData;

        public DirectionalCascadeShadowData(Vector4 cullingSphere, float tileSize, ShadowSetting.FilterMode filterMode)
        {
            //一个像素对应世界空间的距离
            float perDistance = 2f * cullingSphere.w / tileSize;
            //根据PCF核的大小，2x2就偏1个像素，3x3就2个, 5x5就3个
            float filterSize = perDistance * ((float)filterMode + 1f);
            cullingSphere.w -= filterSize;
            cullingSphere.w *= cullingSphere.w;
            this.cullingSphere = cullingSphere; //储存平方
            
            //NormalBias偏移考虑最大情况根号2
            cascadeData = new Vector4(1f / cullingSphere.w, filterSize* 1.4142136f);
        }
    }
}
