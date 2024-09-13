#ifndef CUSTOM_LIGHTING_INCLUDE
#define CUSTOM_LIGHTING_INCLUDE

bool RenderingLayersOverlap(Surface sur, Light light)
{
	return (sur.renderingLayerMask & light.renderingLayerMask) != 0;
}


float3 InComingLight(Surface v , Light light){
	return saturate(dot(v.normal, light.direction) * light.attenuation) * light.color;
}

float3 GetLighting(Surface surface, Light light, BRDF brdf){
	return InComingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Fragment frag, Surface surfaceWS, BRDF brdf, GI gi){
	MyShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;

	//间接
	float3 indirect;
#if defined(_IBL_GI)
	indirect = IBLGI(surfaceWS, brdf);
#else
	indirect = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
#endif

	//直接
	float3 direct = 0;
	for(int i = 0; i < GetDirLightCount(); i++){
		Light light = GetDirectionLight(i, surfaceWS, shadowData);
		if(RenderingLayersOverlap(surfaceWS, light))
		{
			direct += GetLighting(surfaceWS, light, brdf);
		}
	}

	//otherLight
#if defined(_LIGHTS_PER_OBJECT)
	for(int j = 0; j < min(unity_LightData.y, 8); j++){
		int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];		
		Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
		if(RenderingLayersOverlap(surfaceWS, light))
		{
			direct += GetLighting(surfaceWS, light, brdf);
		}
	}
#else
	//forward+
	ForwardPlusTile tile = GetForwardPlusTile(frag.screenUV);
	int lastLightIndex = tile.GetLastLightIndexInTile();
	int firstLightIndex = tile.GetFirstLightIndexInTile();
	
	for(int j = firstLightIndex; j <= lastLightIndex; j++)
	{
		Light light = GetOtherLight(tile.GetLightIndex(j), surfaceWS, shadowData);
		if(RenderingLayersOverlap(surfaceWS, light))
		{
			direct += GetLighting(surfaceWS, light, brdf);
		}
	}
#endif

	return indirect + direct;
}










#endif
