#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

//LightMap
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

//ShadowMask
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

//LightProbeVolume
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

//Skybox reflection
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

//IBL
TEXTURECUBE(_IBLCubeMap);
SAMPLER(sampler_IBLCubeMap);
TEXTURE2D(_IBLSpecularLUT);
SAMPLER(sampler_IBLSpecularLUT);

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

//采样LightMap
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

//采样LightProbe
float3 SampleLightProbe(Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return 0.0;
    #else
        if(unity_ProbeVolumeParams.x)
        {
            return SampleProbeVolumeSH4(
                TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
                surfaceWS.position, surfaceWS.normal,
                unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
                unity_ProbeVolumeParams.xyz, unity_ProbeVolumeSizeInv.xyz             
            );
        }
        else
        {    
            float4 params[7];
            params[0] = unity_SHAr;
            params[1] = unity_SHAg;
            params[2] = unity_SHAb;
            params[3] = unity_SHBr;
            params[4] = unity_SHBg;
            params[5] = unity_SHBb;
            params[6] = unity_SHC;
            return max(0.0, SampleSH9(params, surfaceWS.normal));
        }
        
    #endif
}

//采样Bake阴影
float4 SampleBakedShadow(float2 lightMapUV, Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV);
    #else
        if(unity_ProbeVolumeParams.x)
        {
            return SampleProbeOcclusion(
                TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
                surfaceWS.position, unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
                unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
            );
        }
        else
        {       
            return unity_ProbesOcclusion;
        }
    #endif
}

//采样skybox cubemap
//其实是IBL Specular，预滤波环境贴图（mipmap）, 使用法线和视线的反射方向进行查找
float3 SampleEnvironment(Surface surfaceWS, BRDF brdf)
{
    //根据mipmap 级别选择反射粗糙度
    float3 uvw = reflect(-surfaceWS.viewDir, surfaceWS.normal);
    float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    float4 env = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, uvw, mip);
    
    //HDR处理
    return DecodeHDREnvironment(env, unity_SpecCube0_HDR);
}


float3 FresnelSchlickRoughness(float cosTheta, float3 F0, float roughness)
{
    return F0 + (max(float3(1.0 - roughness, 1.0 - roughness, 1.0 - roughness), F0) - F0) * pow(1.0 - cosTheta, 5.0);
}

float3 IBLGI(Surface surfaceWS, BRDF brdf)
{
    float F0 = lerp(MIN_REFLECT, surfaceWS.color, surfaceWS.metallic);
    float f = FresnelSchlickRoughness(max(dot(surfaceWS.normal, surfaceWS.viewDir), 0.0), F0, brdf.roughness);

    float kd_ibl = (1 - f) * (1 - surfaceWS.metallic);
    float3 diffuse = SAMPLE_TEXTURECUBE_LOD(_IBLCubeMap, sampler_IBLCubeMap, surfaceWS.normal, 0);
    float3 diffuseCol = kd_ibl * diffuse * surfaceWS.color;

    //预滤波
    float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    float3 reflectDir = reflect(-surfaceWS.viewDir, surfaceWS.normal);
    float4 prefilteredColor  = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDir, mip);

    //查找表
    float nv = dot(surfaceWS.normal, surfaceWS.viewDir);
    float2 envBrdf = SAMPLE_TEXTURE2D(_IBLSpecularLUT, sampler_IBLSpecularLUT, float2(max(nv, 0.0), brdf.roughness)).rg;
    float3 specularCol = prefilteredColor * (envBrdf.r * f + envBrdf.y);

    float3 indirect = (diffuseCol + specularCol) * surfaceWS.occlusion;
    return indirect;
}


struct GI
{
    float3 diffuse;
    float3 specular;
    ShadowMask shadowMask;
};

GI GetGI(float2 lightMapUV, Surface surfaceWS, BRDF brdf)
{
    GI gi;
#if defined(_IBL_GI) 
#else
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
#endif

#if defined(_IBL_GI)
#else
    gi.specular = SampleEnvironment(surfaceWS, brdf);
#endif

    gi.shadowMask.always = false;
    gi.shadowMask.distance = false;
    gi.shadowMask.shadows = 1.0;

    #if defined(_SHADOW_MASK_DISTANCE)
        gi.shadowMask.distance = true;
        gi.shadowMask.shadows = SampleBakedShadow(lightMapUV, surfaceWS);
    #elif defined(_SHADOW_MASK_ALWAYS)
        gi.shadowMask.always = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #endif
        
    return gi;
}


#endif