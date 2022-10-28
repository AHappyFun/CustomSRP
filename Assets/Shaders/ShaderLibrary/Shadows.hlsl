#ifndef CUSTOM_SHADOWS_INCLUDE

#define CUSTOM_SHADOWS_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

//PCF
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

#if defined(_OTHER_PCF3)
	#define OTHER_FILTER_SAMPLES 4
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
	#define OTHER_FILTER_SAMPLES 9
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
	#define OTHER_FILTER_SAMPLES 16
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4


TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);
SAMPLER(sampler_LinearClamp);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
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

struct OtherShadowData
{
	float strength;
	int tileIndex;
	bool isPoint;
	int shadowMaskChannel;
	float3 lightPositionWS;
	float3 lightDirectionWS;
	float3 spotDirectionWS;
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
	//这个距离衰减基于视角空间，作为3种阴影的全局衰减
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
	if( i == _CascadeCount && _CascadeCount > 0)
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

//--------------PCSS Test-----------------------
static const float2 PoissonOffsets[32] = {
    float2(0.06407013, 0.05409927),
    float2(0.7366577, 0.5789394),
    float2(-0.6270542, -0.5320278),
    float2(-0.4096107, 0.8411095),
    float2(0.6849564, -0.4990818),
    float2(-0.874181, -0.04579735),
    float2(0.9989998, 0.0009880066),
    float2(-0.004920578, -0.9151649),
    float2(0.1805763, 0.9747483),
    float2(-0.2138451, 0.2635818),
    float2(0.109845, 0.3884785),
    float2(0.06876755, -0.3581074),
    float2(0.374073, -0.7661266),
    float2(0.3079132, -0.1216763),
    float2(-0.3794335, -0.8271583),
    float2(-0.203878, -0.07715034),
    float2(0.5912697, 0.1469799),
    float2(-0.88069, 0.3031784),
    float2(0.5040108, 0.8283722),
    float2(-0.5844124, 0.5494877),
    float2(0.6017799, -0.1726654),
    float2(-0.5554981, 0.1559997),
    float2(-0.3016369, -0.3900928),
    float2(-0.5550632, -0.1723762),
    float2(0.925029, 0.2995041),
    float2(-0.2473137, 0.5538505),
    float2(0.9183037, -0.2862392),
    float2(0.2469421, 0.6718712),
    float2(0.3916397, -0.4328209),
    float2(-0.03576927, -0.6220032),
    float2(-0.04661255, 0.7995201),
    float2(0.4402924, 0.3640312),
};

float2 getReceiverPlaneDepthBias (float3 shadowCoord)
{
	float2 biasUV;
	float3 dx = ddx (shadowCoord);
	float3 dy = ddy (shadowCoord);

	biasUV.x = dy.y * dx.z - dx.y * dy.z;
	biasUV.y = dx.x * dy.z - dy.x * dx.z;
	biasUV *= 1.0f / ((dx.x * dy.y) - (dx.y * dy.x));
	return biasUV;
}

float _PCSSLightWidth;
float _PCSSBias;
real PCSS(float3 coord, float DReceive, float4 positionCS)
{
    real attenuation;

	float2 DepthBiasDotFactors = getReceiverPlaneDepthBias(coord);
	
	float fractionalSamplingError = 2.0 * dot(_ShadowAtlasSize.yy, abs(DepthBiasDotFactors));
	fractionalSamplingError = min(fractionalSamplingError, _PCSSBias);
#if defined(UNITY_REVERSED_Z)
	fractionalSamplingError *= -1.0;
#endif

	DReceive -= fractionalSamplingError;
	
    //pcss
    float2 shadowUV = coord.xy;

	
	float Angle = RandomAngle(positionCS.xy, .1f);
	float SinAngle, CosAngle;
	sincos(Angle, SinAngle, CosAngle);
	float2x2 RotMatrix = float2x2(CosAngle, -SinAngle, SinAngle, CosAngle);	
    
    //搜索DB的范围 和 当前采样点的距离有关，距离灯光越近，范围越小
    float SearchWidth = (_PCSSLightWidth) * (DReceive - 0.05) / DReceive;
    
    float DAverageBlocker = 0;
    float BlockerSum = 0.0;
    float BlockCount = 0.0001f;
    
    //1.求平均Distance Blocker
    for (int i = 0; i < 32; i++)
    {
        float2 offset = PoissonOffsets[i] * SearchWidth;
    	offset = mul(RotMatrix, offset);
    	
        float D_sample = SAMPLE_TEXTURE2D(_DirectionalShadowAtlas, sampler_LinearClamp, shadowUV + offset).r;

#if  defined(UNITY_REVERSED_Z)
        if(D_sample < DReceive)
#else
        if(D_sample > DReceive)
#endif         
        {
            BlockerSum += D_sample;
            BlockCount += 1.0;
        }
    }

    
#if  defined(UNITY_REVERSED_Z)
    DAverageBlocker = 1 - DAverageBlocker;
#endif

    //2.计算软的范围
    float W_Penumbra = abs(DReceive - DAverageBlocker) * _PCSSLightWidth / DAverageBlocker;

    //3.根据W范围做PCF
    float sum = 0;
    for (int i = 0; i < 32; i++)
    {
        float2 offset = PoissonOffsets[i] * W_Penumbra * _ShadowAtlasSize.yy;
    	offset = mul(RotMatrix, offset);
    		
        sum += SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, float3(shadowUV + offset, DReceive)).r;
    }

    attenuation = sum / 32;

    return attenuation;
}

//-------------------PCSS Test End----------------------

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
	
	float shadow = 1.0;
#if defined(_PCSS_SOFT)
	shadow = PCSS(positionSTS, positionSTS.z, surfaceWS.positionCS);
	if(global.cascadeBlend < 1.0)
	{
		normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)).xyz;
		shadow = lerp(PCSS(positionSTS, positionSTS.z, surfaceWS.positionCS), shadow, global.cascadeBlend);
	}
#else
	shadow = FilterDirectionalShadow(positionSTS);
	if(global.cascadeBlend < 1.0)
	{
		normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)).xyz;
		shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
	}
#endif
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

float GetDirectionalShadowAttenuation(DirectionalShadowData directional, MyShadowData global, Surface surfaceWS)
{
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

//other Light
float SampleOtherShadowAtlas(float3 positionSTS, float3 bounds)
{
	positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
	return SAMPLE_TEXTURE2D_SHADOW(
		_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

float FilterOtherShadow(float3 positionSTS, float3 bounds)
{
	#if defined(OTHER_FILTER_SETUP)
		real weights[OTHER_FILTER_SAMPLES];
		real2 positions[OTHER_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.wwzz;
		OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < OTHER_FILTER_SAMPLES; i++)
		{
			shadow += weights[i] * SampleOtherShadowAtlas(
				float3(positions[i].xy, positionSTS.z), bounds
			);
		}
		return shadow;
	#else
		return SampleOtherShadowAtlas(positionSTS, bounds);
	#endif
}

static const float3 pointShadowPlanes[6] = {
	float3(-1.0, 0.0, 0.0),
	float3(1.0, 0.0, 0.0),
	float3(0.0, -1.0, 0.0),
	float3(0.0, 1.0, 0.0),
	float3(0.0, 0.0, -1.0),
	float3(0.0, 0.0, 1.0),
};

float GetOtherRealtimeShadow(OtherShadowData other, MyShadowData global, Surface surfaceWS)
{
	float tileIndex = other.tileIndex;
	float3 lightPlane = other.spotDirectionWS;
	if(other.isPoint)
	{
		float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
		tileIndex += faceOffset;
		lightPlane = pointShadowPlanes[faceOffset];
	}
	float4 tileData = _OtherShadowTiles[tileIndex];
	float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
	float distanceToLightPlane = dot(surfaceToLight, lightPlane);
	float3 normalBias = surfaceWS.interpolatedNormal * (distanceToLightPlane * tileData.w);
	float4 positionSTS = mul(
		_OtherShadowMatrices[tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	);
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

float GetOtherShadowAttenuation(OtherShadowData other, MyShadowData global, Surface surfaceWS)
{
#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
#endif
	
	float shadow;
	if(other.strength * global.strength <= 0.0 )
	{
		shadow = GetBakedShadow(global.shadowMask, other.shadowMaskChannel, other.strength);
	}
	else
	{
		shadow = GetOtherRealtimeShadow(other, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(global, shadow, other.shadowMaskChannel, other.strength);
	}
	return shadow;
	
}






#endif
