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

//CookTorrance �߹�
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
	float oneMinusReflect = OneMinusReflect(surface.metallic); //������Խ�ߣ�diffuseԽ��
	brdf.diffuse = surface.color * oneMinusReflect;
	if(applyAlphaToDiffuse){	
		brdf.diffuse *= surface.alpha;
	}

	brdf.specular = lerp(MIN_REFLECT, surface.color, surface.metallic); //���Ž�����Ҳ���

	//roughness = 1 - smoothness 
	brdf.perceptualRoughness  = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	//ƽ��
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);

	brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflect);

	return brdf;
};

//��ӹ�BRDF
float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular)
{

	float fresnelStrength = surface.fresnelStrength * Pow4(1.0 - saturate(dot(surface.normal, surface.viewDir)));
	
	float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
	reflection /= brdf.roughness * brdf.roughness + 1.0;
	
	return diffuse * brdf.diffuse + reflection;
}

#endif
