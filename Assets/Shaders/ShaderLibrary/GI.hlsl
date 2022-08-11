#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

#if defined(LIGHTMAP_ON)
    #define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
    #define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
    #define TRANSFER_GI_DATA(input, output) \
        output.lightMapUV = input.lightMapUV * \
        unity_LightmapST.xy + unity_LightmapST.zw;       
    #define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
    #define GI_ATTRIBUTE_DATA 
    #define GI_VARYINGS_DATA 
    #define TRANSFER_GI_DATA(input, output) 
    #define GI_FRAGMENT_DATA(input) 0.0
#endif


float3 SampleLightMap(float2 lightMapUV)
{
    #if defined(LIGHTMAP_ON)
        return SampleSingleLightmap(
            TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightMapUV,
            float4(1.0, 1.0, 0.0, 0.0),
            #if defined(UNITY_LIGHTMAP_FULL_HDR)
                false,
            #else
                true,
            #endif
            float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
            );
    #else
        return 0.0;
    #endif
}

float3 SampleLightProbe(Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return 0.0;
    #else
        float4 params[7];
        params[0] = unity_SHAr;
        params[1] = unity_SHAg;
        params[2] = unity_SHAb;
        params[3] = unity_SHBr;
        params[4] = unity_SHBg;
        params[5] = unity_SHBb;
        params[6] = unity_SHC;
        return max(0.0, SampleSH9(params, surfaceWS.normal));
    #endif
}

struct GI
{
    float3 diffuse;
};

GI GetGI(float2 lightMapUV, Surface surfaceWS)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
    return gi;
}


#endif