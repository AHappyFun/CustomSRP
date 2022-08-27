#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseTexture);   //纹理和采样器不可以实例
TEXTURE2D(_MaskTexture);   //MODS
TEXTURE2D(_NormalMap);     //法线贴图

TEXTURE2D(_EmissionTex);
SAMPLER(sampler_BaseTexture); //一个采样器可以给多个纹理

TEXTURE2D(_DetailTexture);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailTexture);

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseTexture_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _DetailTexture_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _AlphaCutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = INPUT_PROP(_BaseTexture_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV(float2 detailUV)
{
    float4 detailST = INPUT_PROP(_DetailTexture_ST);
    return detailUV * detailST.xy + detailST.zw;
}

float4 GetDetail(InputConfig cfg)
{
    if(cfg.useDetail)
    {
        float4 det = SAMPLE_TEXTURE2D(_DetailTexture, sampler_DetailTexture, cfg.detailUV);
        return det * 2.0 - 1.0;
    }
    return 0.0;
}

float4 GetMask(InputConfig cfg)
{
    if(cfg.useMask)
    {
        return SAMPLE_TEXTURE2D(_MaskTexture, sampler_BaseTexture, cfg.baseUV);
    }
    return 1.0;
}

float4 GetBase(InputConfig cfg)
{
    float4 col = INPUT_PROP(_BaseColor);
    
    float4 baseTex = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, cfg.baseUV);
    
    if(cfg.useDetail)
    {
        float detail = GetDetail(cfg).r * INPUT_PROP(_DetailAlbedo);
        float detailMask = GetMask(cfg).b;
        baseTex.rgb = lerp(sqrt(baseTex.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail) * detailMask);
        baseTex.rgb *= baseTex.rgb;
    }
    
    return baseTex * col;
}

float GetCutOff()
{
    return INPUT_PROP(_AlphaCutoff);
}

float GetMetallic(InputConfig cfg)
{
    float metaiilc = INPUT_PROP(_Metallic);
    metaiilc *= GetMask(cfg).r;
    return metaiilc;
}

float GetSmoothness(InputConfig cfg)
{
    float smoothness =  INPUT_PROP(_Smoothness);
    smoothness *= GetMask(cfg).a;

    if(cfg.useDetail)
    {
        float detail = GetDetail(cfg).b * INPUT_PROP(_DetailSmothness);
        float mask = GetMask(cfg).b;
        smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
    }
    
    return smoothness;
}

float GetOcclusion(InputConfig cfg)
{
    float strength = INPUT_PROP(_Occlusion);
    float occlusion = GetMask(cfg).g;
    occlusion = lerp(occlusion, 1.0, strength);
    return occlusion;
}

float3 GetEmission(InputConfig cfg)
{
    float4 tex = SAMPLE_TEXTURE2D(_EmissionTex, sampler_BaseTexture, cfg.baseUV);
    float4 col = INPUT_PROP(_EmissionColor);
    return tex.rgb * col.rgb;
}

float GetFresnel(InputConfig cfg)
{
    return INPUT_PROP(_Fresnel);
}

float3 GetNormalTangentSpace(InputConfig cfg)
{
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseTexture, cfg.baseUV);
    float scale = INPUT_PROP(_NormalScale);
    float3 normal = DecodeNormal(map, scale);

    if(cfg.useDetail)
    {
        map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailTexture, cfg.detailUV);
        scale = INPUT_PROP(_DetailNormalScale) * GetMask(cfg).b;
        float3 detailNormal = DecodeNormal(map, scale);
        normal = BlendNormalRNM(normal, detailNormal);
    } 
    
    return normal;
}




#endif