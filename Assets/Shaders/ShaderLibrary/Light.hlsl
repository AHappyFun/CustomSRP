#ifndef CUSTOM_LIGHT_INCLUDE
#define CUSTOM_LIGHT_INCLUDE

#define MAX_DIRECTIONLIGHTCOUNT  4
#define MAX_OTHERLIGHTCOUNT 64

#include "Shadows.hlsl"

struct Light {
	float3 color;
	float3 direction;
	float attenuation;
};

//这个数据CusRP从CPU发送过来
CBUFFER_START(_CustomLight)
	int _DirectionLightCount;
	float4 _DirectionLightColors[MAX_DIRECTIONLIGHTCOUNT];
	float4 _DirectionLightDirections[MAX_DIRECTIONLIGHTCOUNT];
	float4 _DirectionLightShadowData[MAX_DIRECTIONLIGHTCOUNT];

	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHERLIGHTCOUNT];
	float4 _OtherLightPositions[MAX_OTHERLIGHTCOUNT];
	float4 _OtherLightDirections[MAX_OTHERLIGHTCOUNT];
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
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	return data;
}

Light GetDirectionLight(int lightIndex, Surface surfaceWS, MyShadowData shadowData) {
	Light light;
	light.color = _DirectionLightColors[lightIndex].rgb;
	light.direction = _DirectionLightDirections[lightIndex].xyz;
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(lightIndex, shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
	//light.attenuation = shadowData.cascadeIndex * 0.25; //debug cascade
	return light;
}

Light GetOtherLight(int lightIndex, Surface surfaceWS, MyShadowData shadowdata)
{
	Light light;
	light.color = _OtherLightColors[lightIndex].rgb;
	float3 dir = _OtherLightPositions[lightIndex].xyz - surfaceWS.position;
	light.direction = normalize(dir);

	//point atten
	float distanceSqr = max(dot(dir, dir), 0.00001);
	float rangeAtten = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[lightIndex].w)));
	//spot atten
	float4 spotAngle = _OtherLightSpotAngles[lightIndex];
	float spotAtten = Square(saturate(dot(_OtherLightDirections[lightIndex].xyz, light.direction) * spotAngle.x + spotAngle.y));


	OtherShadowData otherShadowData = GetOtherShadowData(lightIndex);
	float shadowAtten = GetOtherShadowAttenuation(otherShadowData, shadowdata, surfaceWS);
	
	
	light.attenuation = spotAtten * rangeAtten / distanceSqr;
	light.attenuation *= shadowAtten;
	return light;
}


#endif