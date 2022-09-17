#ifndef CUSTOM_LIGHT_INCLUDE
#define CUSTOM_LIGHT_INCLUDE

#define MAX_DIRECTIONLIGHTCOUNT  4
#define MAX_OTHERLIGHTCOUNT 64

#include "Shadows.hlsl"

struct Light {
	float3 color;
	float3 direction;
	float attenuation;
	uint renderingLayerMask;
};

//这个数据CusRP从CPU发送过来
CBUFFER_START(_CustomLight)
	int _DirectionLightCount;
	float4 _DirectionLightColors[MAX_DIRECTIONLIGHTCOUNT];
	float4 _DirectionLightDirectionsAndMasks[MAX_DIRECTIONLIGHTCOUNT];
	float4 _DirectionLightShadowData[MAX_DIRECTIONLIGHTCOUNT];

	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHERLIGHTCOUNT];
	float4 _OtherLightPositions[MAX_OTHERLIGHTCOUNT];
	float4 _OtherLightDirectionsAndMasks[MAX_OTHERLIGHTCOUNT];
	float4 _OtherLightSpotAngles[MAX_OTHERLIGHTCOUNT];
	float4 _OtherLightShadowData[MAX_OTHERLIGHTCOUNT];
CBUFFER_END

int GetDirLightCount() {
	return _DirectionLightCount;
}

int GetOtherLightCount()
{
	return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex, MyShadowData shadowData) {
	DirectionalShadowData data;
	data.strength = _DirectionLightShadowData[lightIndex].x;
	data.tileIndex = _DirectionLightShadowData[lightIndex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionLightShadowData[lightIndex].z;
	data.shadowMaskChannel = _DirectionLightShadowData[lightIndex].w;
	return data;
}

OtherShadowData GetOtherShadowData(int lightIndex)
{
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;
	data.tileIndex = _OtherLightShadowData[lightIndex].y;
	data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	data.lightPositionWS = 0.0;
	data.lightDirectionWS = 0.0;
	data.spotDirectionWS = 0.0;
	return data;
}

Light GetDirectionLight(int lightIndex, Surface surfaceWS, MyShadowData shadowData) {
	Light light;
	light.color = _DirectionLightColors[lightIndex].rgb;
	light.direction = _DirectionLightDirectionsAndMasks[lightIndex].xyz;
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(lightIndex, shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
	light.renderingLayerMask = asuint(_DirectionLightDirectionsAndMasks[lightIndex].w);
	//light.attenuation = shadowData.cascadeIndex * 0.25; //debug cascade
	return light;
}

Light GetOtherLight(int lightIndex, Surface surfaceWS, MyShadowData shadowdata)
{
	Light light;
	light.color = _OtherLightColors[lightIndex].rgb;
	float3 pos = _OtherLightPositions[lightIndex].xyz;
	float3 dis = pos - surfaceWS.position;
	light.direction = normalize(dis);

	//point atten
	float distanceSqr = max(dot(dis, dis), 0.00001);
	float distanceAtten = rcp(distanceSqr);
	float rangeAtten = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[lightIndex].w)));
	//spot atten
	float3 spotDirection = _OtherLightDirectionsAndMasks[lightIndex].xyz;
	float4 spotAngle = _OtherLightSpotAngles[lightIndex];
	float spotAtten = Square(saturate(dot(spotDirection, light.direction) * spotAngle.x + spotAngle.y));


	OtherShadowData otherShadowData = GetOtherShadowData(lightIndex);
	otherShadowData.lightPositionWS = pos;
	otherShadowData.lightDirectionWS = light.direction;
	otherShadowData.spotDirectionWS = spotDirection;
	float shadowAtten = GetOtherShadowAttenuation(otherShadowData, shadowdata, surfaceWS);
	
	
	light.attenuation = spotAtten * rangeAtten * distanceAtten;
	light.attenuation *= shadowAtten;

	light.renderingLayerMask = asuint(_OtherLightDirectionsAndMasks[lightIndex].w);
	return light;
}


#endif