
#ifndef CUSTOM_SURFACE_INCLUDE

#define CUSTOM_SURFACE_INCLUDE

struct Surface{
	float3 position;
	float3 normal;
	float3 interpolatedNormal;
	float3 viewDir;
	float depth;
	float3 color;
	float alpha;
	float metallic;
	float smoothness;
	float occlusion;
	
	float fresnelStrength;
	float dither;

	uint renderingLayerMask;
};

#endif
