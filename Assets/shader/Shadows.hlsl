#ifndef CUSTOM_SHADOWS_INCLUDE

#define CUSTOM_SHADOWS_INCLUDE

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4


TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

# include "Surface.hlsl"

struct DirectionalShadowData{
	float strength;
	int tileOffset;
};

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
