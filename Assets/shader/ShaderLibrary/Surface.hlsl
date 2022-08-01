
#ifndef CUSTOM_SURFACE_INCLUDE

#define CUSTOM_SURFACE_INCLUDE

struct Surface{
	float3 position;
	float3 normal;
	float3 viewDir;
	float depth;
	float3 color;
	float alpha;
	float metallic;
	float smoothness;
};

#endif
