#ifndef CUSTOM_BRDF_INCLUDE
#define CUSTOM_BRDF_INCLUDE


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

struct BRDF{
	float3 diffuse;
	float3 specular;
	float roughness;
	float perceptualRoughness;
	float fresnel;
};

#define MIN_REFLECT 0.04
float OneMinusReflect(float metallic){
	float range = 1.0 - MIN_REFLECT;
	return ( 1.0 - metallic )* range;
}

//CookTorrance 高光
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

BRDF GetBRDF(Surface surface, bool applyAlphaToDiffuse = false){
	BRDF brdf;
	float oneMinusReflect = OneMinusReflect(surface.metallic); //金属度越高，diffuse越少
	brdf.diffuse = surface.color * oneMinusReflect;
	if(applyAlphaToDiffuse){	
		brdf.diffuse *= surface.alpha;
	}

	brdf.specular = lerp(MIN_REFLECT, surface.color, surface.metallic); //随着金属度也差不多

	//roughness = 1 - smoothness 
	brdf.perceptualRoughness  = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	//平方
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);

	brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflect);

	return brdf;
};

//间接光BRDF
float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular)
{

	float3 indirectDiffuse = diffuse * brdf.diffuse;

	//fresnel反射
	float fresnelStrength = surface.fresnelStrength * Pow4(1.0 - saturate(dot(surface.normal, surface.viewDir)));
	
	float3 indirectSpecular = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);

	//受到粗糙度影响
	indirectSpecular /= brdf.roughness * brdf.roughness + 1.0;
	
	return (indirectDiffuse + indirectSpecular) * surface.occlusion;
}

#endif
