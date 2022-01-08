#ifndef CUSTOM_Light_INCLUDE
#define CUSTOM_Light_INCLUDE

#define MAX_DIRECTIONLIGHTCOUNT  4

#include "Shadows.hlsl"

//这个数据CusRP从CPU发送过来
CBUFFER_START(_CustomLight)
	int _DirectionLightCount;
	float4 _DirectionLightColors[MAX_DIRECTIONLIGHTCOUNT];
	float4 _DirectionLightDirections[MAX_DIRECTIONLIGHTCOUNT];
	float4 _DirectionLightShadowData[MAX_DIRECTIONLIGHTCOUNT];
CBUFFER_END

struct Light{
	float3 color;
	float3 direction;
	float attenuation;
};

int GetDirLightCount(){
	return _DirectionLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex){
	DirectionalShadowData data;
	data.strength = _DirectionLightShadowData[lightIndex].x;
	data.tileOffset = _DirectionLightShadowData[lightIndex].y;
	return data;
}

Light GetDirectionLight(int lightIndex, Surface surfaceWS){
	Light light;
	light.color = _DirectionLightColors[lightIndex].rgb;
	light.direction = _DirectionLightDirections[lightIndex].xyz;
	DirectionalShadowData shadowData = GetDirectionalShadowData(lightIndex);
	light.attenuation = GetDirectionalShadowAttenuation(shadowData, surfaceWS);
	return light;
}



#endif
