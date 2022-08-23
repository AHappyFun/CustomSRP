#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseTexture);   //纹理和采样器不可以实例
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_BaseTexture); //一个采样器可以给多个纹理


UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseTexture_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _AlphaCutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseTexture_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(float2 baseUV)
{
    float4 baseTex = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, baseUV);
    float4 albedo = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    return baseTex * albedo;
}

float GetCutOff()
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AlphaCutoff);
}

float GetMetallic()
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
}

float GetSmoothness()
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
}

float3 GetEmission(float2 baseUV)
{
    float4 tex = SAMPLE_TEXTURE2D(_EmissionTex, sampler_BaseTexture, baseUV);
    float4 col = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
    return tex.rgb * col.rgb;
}

float GetFresnel()
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Fresnel);
}


#endif