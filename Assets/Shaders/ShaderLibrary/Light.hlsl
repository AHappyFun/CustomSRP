#ifndef CUSTOM_LIGHT_INCLUDE
#define CUSTOM_LIGHT_INCLUDE

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
	int _OtherLightCount;
CBUFFER_END

struct DirectionalLightData
{
	float4 color, directionAndMask, shadowData;
};

StructuredBuffer<DirectionalLightData> _DirectionLightData;

struct OtherLightData
{
	float4 color, position, directionAndMask, spotAngle, shadowData;
};

StructuredBuffer<OtherLightData> _OtherLightData;

int GetDirLightCount() {
	return _DirectionLightCount;
}

int GetOtherLightCount()
{
	return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex, float4 lightShadowData, MyShadowData shadowData) {
	DirectionalShadowData data;
	data.strength = lightShadowData.x;
	data.tileIndex = lightShadowData.y + shadowData.cascadeIndex;
	data.normalBias = lightShadowData.z;
	data.shadowMaskChannel = lightShadowData.w;
	return data;
}

OtherShadowData GetOtherShadowData(float4 lightShadowData)
{
	OtherShadowData data;
	data.strength = lightShadowData.x;
	data.tileIndex = lightShadowData.y;
	data.isPoint =lightShadowData.z == 1.0;
	data.shadowMaskChannel = lightShadowData.w;
	data.lightPositionWS = 0.0;
	data.lightDirectionWS = 0.0;
	data.spotDirectionWS = 0.0;
	return data;
}

Light GetDirectionLight(int lightIndex, Surface surfaceWS, MyShadowData shadowData) {
	Light light;
	DirectionalLightData light_data = _DirectionLightData[lightIndex];
	
	light.color = light_data.color.rgb;
	light.direction = light_data.directionAndMask.xyz;
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(lightIndex, light_data.shadowData, shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
	light.renderingLayerMask = asuint(light_data.directionAndMask.w);
	//light.attenuation = shadowData.cascadeIndex * 0.25; //debug cascade
	return light;
}

Light GetOtherLight(int lightIndex, Surface surfaceWS, MyShadowData shadowdata)
{
	Light light;
	OtherLightData light_data = _OtherLightData[lightIndex];
	
	light.color = light_data.color.rgb;
	float3 pos = light_data.position.xyz;
	float3 dis = pos - surfaceWS.position;
	light.direction = normalize(dis);

	//point atten
	float distanceSqr = max(dot(dis, dis), 0.00001);
	float distanceAtten = rcp(distanceSqr);
	float rangeAtten = Square(saturate(1.0 - Square(distanceSqr * light_data.position.w)));
	//spot atten
	float3 spotDirection = light_data.directionAndMask.xyz;
	float4 spotAngle = light_data.spotAngle;
	float spotAtten = Square(saturate(dot(spotDirection, light.direction) * spotAngle.x + spotAngle.y));


	OtherShadowData otherShadowData = GetOtherShadowData(light_data.shadowData);
	otherShadowData.lightPositionWS = pos;
	otherShadowData.lightDirectionWS = light.direction;
	otherShadowData.spotDirectionWS = spotDirection;
	float shadowAtten = GetOtherShadowAttenuation(otherShadowData, shadowdata, surfaceWS);
	
	light.attenuation = spotAtten * rangeAtten * distanceAtten;
	light.attenuation *= shadowAtten;

	light.renderingLayerMask = asuint(light_data.directionAndMask.w);
	return light;
}


#endif