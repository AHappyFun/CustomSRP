#ifndef CUSTOM_SHADOWS_INCLUDE

#define CUSTOM_SHADOWS_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTION_FILTER_SAMPLERS 4
	#define DIRECTION_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTION_FILTER_SAMPLERS 9
    #define DIRECTION_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTION_FILTER_SAMPLERS 16
    #define DIRECTION_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4


TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
	float4 _CascadeData[MAX_CASCADE_COUNT];
CBUFFER_END


struct DirectionalShadowData{
	float strength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};

struct ShadowMask
{
	bool always;
	bool distance;
	float4 shadows;
};

struct MyShadowData{
	int cascadeIndex;
	float cascadeBlend;
	float strength;
	ShadowMask shadowMask;
};

float FadeShadowStrength(float distance, float scale, float fade)
{
	return saturate((1.0 - distance * scale) * fade);
}

MyShadowData GetShadowData(Surface surfaceWS){
	MyShadowData data;
	data.shadowMask.distance = false;
	data.shadowMask.shadows = 1.0;
	data.shadowMask.always = false;
	data.cascadeBlend = 1.0;
	//最大距离之外无阴影,做渐变
	data.strength = FadeShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
	int i = 0;
	//计算出应该采样哪一级cascade，最后i就是级联层级
	for(i = 0; i< _CascadeCount; i++){
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz); 
		//平方对比
		if(distanceSqr < sphere.w){
			float fade = FadeShadowStrength(
				distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
			);
			
			if(i == _CascadeCount - 1)
			{
				data.strength *= fade;   //最大距离的
			}
			else
			{
				data.cascadeBlend = fade; //级联的Fade
			}
			break;
		}
	}
	if( i == _CascadeCount)
	{
		data.strength = 0.0;
	}
	#if defined(_CASCADE_BLEND_DITHER)
		else if(data.cascadeBlend < surfaceWS.dither)
		{
			i += 1;
		}
	#endif
	
	#if !defined(_CASCADE_BLEND_SOFT)
		data.cascadeBlend = 1.0;
	#endif
	data.cascadeIndex = i;
	return data;
}

//采样ShadowMap
float SampleDirectionalShadowAtlas(float3 positionSTS){
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

float FilterDirectionalShadow(float3 positionSTS)
{
	#if defined(DIRECTION_FILTER_SETUP)
		float weights[DIRECTION_FILTER_SAMPLERS];
		float2 positions[DIRECTION_FILTER_SAMPLERS];
		float4 size = _ShadowAtlasSize.yyxx;
		DIRECTION_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for(int i = 0;i < DIRECTION_FILTER_SAMPLERS; i++)
		{
			shadow += weights[i] * SampleDirectionalShadowAtlas(
				float3(positions[i].xy, positionSTS.z)
			);
		}
		return shadow;
	#else
		return SampleDirectionalShadowAtlas(positionSTS);
	#endif
}

float GetCascadeShadow(DirectionalShadowData directional, MyShadowData global, Surface surfaceWS)
{
	float3 normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
	//转换到ShadowMap贴图空间
	float3 positionSTS = mul(
        _DirectionalShadowMatrices[directional.tileIndex],
        float4(surfaceWS.position + normalBias, 1.0)
    ).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);
	if(global.cascadeBlend < 1.0)
	{
		normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)).xyz;
		shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
	}
	return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel)
{
	float shadow = 1.0;
	if(mask.distance || mask.always)
	{
		if(channel >= 0)
		{		
			shadow = mask.shadows[channel];
		}
	}
	return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
	if(mask.distance|| mask.always)
	{
		return lerp(1.0, GetBakedShadow(mask, channel), strength);
	}
	return 1.0;
}

float MixBakedAndRealtimeShadows(MyShadowData global, float realTimeShadow, int shadowMaskChannel, float strength)
{
	float bakedShadow = GetBakedShadow(global.shadowMask, shadowMaskChannel);
	if(global.shadowMask.always)
	{
		realTimeShadow = lerp(1.0, realTimeShadow, global.strength);
		realTimeShadow = min(bakedShadow, realTimeShadow);
		return lerp(1.0, realTimeShadow, strength);
	}
	if(global.shadowMask.distance)
	{
		realTimeShadow = lerp(bakedShadow, realTimeShadow, global.strength);
		return lerp(1.0, realTimeShadow, strength);
	}
	return lerp(1.0, realTimeShadow, strength * global.strength);
}

float3 GetDirectionalShadowAttenuation(DirectionalShadowData directional, MyShadowData global, Surface surfaceWS){
#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
#endif
	
	float shadow;
	
	if(directional.strength * global.strength <= 0.0){
		return GetBakedShadow(global.shadowMask, directional.shadowMaskChannel,abs(directional.strength));
	}
	else
	{
		shadow = GetCascadeShadow(directional, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(global, shadow, directional.shadowMaskChannel, directional.strength);
	}
	return shadow;
		
}




#endif
