#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

TEXTURE2D(_BaseTexture);   //纹理和采样器不可以实例
TEXTURE2D(_DistortionTexture);
SAMPLER(sampler_BaseTexture);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseTexture_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _AlphaCutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)  
    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance)
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange)
    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength)
    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseTexture_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(InputConfig cfg)
{
    float4 baseTex = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, cfg.baseUV);

    if(cfg.flipbookBlending)
    {
        baseTex = lerp(baseTex, SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, cfg.flipbookUVB.xy), cfg.flipbookUVB.z);     
    }
    if(cfg.nearFade)
    {
        float nearAtten = (cfg.fragment.depth - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NearFadeDistance)) /
            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NearFadeRange);
        baseTex.a *= saturate(nearAtten);
    }
    if(cfg.softParticles)
    {
        float depthDelta = cfg.fragment.bufferDepth - cfg.fragment.depth;
        float nearAtten = (depthDelta - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SoftParticlesDistance)) /
            UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SoftParticlesRange);
        baseTex.a *= saturate(nearAtten);
    }
    
    float4 albedo = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    return baseTex * albedo * cfg.color;
}

float GetCutOff()
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AlphaCutoff);
}

float GetMetallic(InputConfig cfg)
{
    return 0;
}

float GetSmoothness(InputConfig cfg)
{
    return 0;
}

float3 GetEmission (InputConfig cfg) {
    return GetBase(cfg).rgb;
}

float GetFresnel(InputConfig cfg)
{
    return 0;
}

float GetFinalAlpha(float alpha)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ZWrite) ? 1.0 : alpha;
}

float GetDistortionBlend(InputConfig cfg)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DistortionBlend);
}

float2 GetDistortion(InputConfig cfg)
{

    float4 rawMap = SAMPLE_TEXTURE2D(_DistortionTexture, sampler_BaseTexture, cfg.baseUV);
      
    if(cfg.flipbookBlending)
    {
        rawMap = lerp(rawMap,
            SAMPLE_TEXTURE2D(_DistortionTexture, sampler_BaseTexture, cfg.flipbookUVB.xy),
            cfg.flipbookUVB.z
        );
    }
    return DecodeNormal(rawMap, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DistortionStrength)).xy;
}



#endif