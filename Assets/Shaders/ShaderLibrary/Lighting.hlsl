#ifndef CUSTOM_LIGHTING_INCLUDE
#define CUSTOM_LIGHTING_INCLUDE


float3 InComingLight(Surface v , Light light){
	return saturate(dot(v.normal, light.direction) * light.attenuation) * light.color;
}

float3 GetLighting(Surface surface, Light light, BRDF brdf){
	return InComingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surfaceWS, BRDF brdf, GI gi){
	MyShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	return gi.shadowMask.shadows.rgb;
	
	float3 indirect = gi.diffuse * brdf.diffuse;
	float3 direct = 0;
	
	for(int i = 0; i < GetDirLightCount(); i++){
		Light light = GetDirectionLight(i, surfaceWS, shadowData);
		direct += GetLighting(surfaceWS, light, brdf);
	}
	
	return indirect + direct;
}










#endif
