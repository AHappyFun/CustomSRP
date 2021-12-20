
#ifndef CUSTOM_Light_INCLUDE

#define CUSTOM_Light_INCLUDE

#define MAX_DIRECTIONLIGHTCOUNT  4

//这个数据CusRP从CPU发送过来
CBUFFER_START(_CustomLight)
	int _DirectionLightCount;
	float4 _DirectionLightColors[MAX_DIRECTIONLIGHTCOUNT];
	float4 _DirectionLightDirections[MAX_DIRECTIONLIGHTCOUNT];
CBUFFER_END

struct Light{
	float3 color;
	float3 direction;
};

int GetDirLightCount(){
	return _DirectionLightCount;
}

Light GetDirectionLight(int lightIndex){
	Light light;
	light.color = _DirectionLightColors[lightIndex].rgb;
	light.direction = _DirectionLightDirections[lightIndex].xyz;
	return light;
}



#endif
