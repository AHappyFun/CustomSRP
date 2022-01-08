#ifndef CUSTOM_BRDF_INCLUDE
#define CUSTOM_BRDF_INCLUDE


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Common.hlsl"

struct BRDF{
	float3 diffuse;
	float3 specular;
	float roughness;
};

#define MIN_REFLECT 0.04
float OneMinusReflect(float metallic){
	float range = 1.0 - MIN_REFLECT;
	return ( 1.0 - metallic )* range;
}


float SpecularStrength(Surface surface, BRDF brdf, Light light)
{
	float3 h = SafeNormalize(light.direction + surface.viewDir);
	float nh2 = Square(saturate(dot(surface.normal, h)));
	float lh2 = Square(saturate(dot(light.direction, h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1.0) + 1.0001);
	float normalization = brdf.roughness * 4.0 + 2.0;
	return r2/(d2 * max(0.1, lh2) * normalization);
}

float3 DirectBRDF(Surface surface, BRDF brdf, Light light){
	return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

BRDF GetBRDF(Surface surface, bool preMultipyAlpha = false){
	BRDF brdf;
	float oneMinusReflect = OneMinusReflect(surface.metallic); //金属度越高，diffuse越少
	brdf.diffuse = surface.color * oneMinusReflect;
	if(preMultipyAlpha){	
		brdf.diffuse *= surface.alpha;
	}
	//brdf.specular = surface.color - brdf.diffuse; //能量守恒
	brdf.specular = lerp(MIN_REFLECT, surface.color, surface.metallic); //随着金属度也差不多

	float perceptualRoughness  = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	brdf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness);

	return brdf;
};

#endif
