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
	
	float3 indirect = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
	float3 direct = 0;
	
	for(int i = 0; i < GetDirLightCount(); i++){
		Light light = GetDirectionLight(i, surfaceWS, shadowData);
		direct += GetLighting(surfaceWS, light, brdf);
	}
	
#if defined(_LIGHTS_PER_OBJECT)
	for(int j = 0; j < min(unity_LightData.y, 8); j++){
		int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];		
		Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
		direct += GetLighting(surfaceWS, light, brdf);
	}
#else
	for(int j = 0; j < GetOtherLightCount(); j++){
		Light light = GetOtherLight(j, surfaceWS, shadowData);
		direct += GetLighting(surfaceWS, light, brdf);
	}
#endif

	return indirect + direct;
}










#endif
