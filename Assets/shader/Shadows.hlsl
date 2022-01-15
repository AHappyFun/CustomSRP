#ifndef CUSTOM_SHADOWS_INCLUDE

#define CUSTOM_SHADOWS_INCLUDE

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4


TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _cascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
CBUFFER_END

# include "Common.hlsl"
# include "Surface.hlsl"


struct DirectionalShadowData{
	float strength;
	int tileOffset;
};

struct MyShadowData{
	int cascadeIndex;
};

MyShadowData GetShadowData(Surface surfaceWS){
	MyShadowData data;
	int i = 0;
	//计算出应该采样哪一级cascade
	for(i=0; i< _cascadeCount; i++){
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
		if(distanceSqr < sphere.w){
			break;
		}
	}
	data.cascadeIndex = i;
	return data;
}

float SampleDirectionalShadowAtlas(float3 positionSTS){
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

float3 GetDirectionalShadowAttenuation(DirectionalShadowData data, Surface surfaceWS){
	if(data.strength <= 0.0){
		return 1.0;
	}
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[data.tileOffset],
		float4(surfaceWS.position, 1.0)
	).xyz;
	float shadow = SampleDirectionalShadowAtlas(positionSTS);
	return lerp(1.0, shadow, data.strength);
}


#endif
