#ifndef CUSTOM_LIT_PASS_INCLUDE

#define CUSTOM_LIT_PASS_INCLUDE

CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
CBUFFER_END

struct a2v{
	float3 vertex: POSITION;
	float3 normal: NORMAL;
};

struct v2f{
	float4 pos : SV_POSITION;
	float3 worldNormal: VAR_NORMAL;
};

float3 InComingLight(v2f v , Light light){
	return saturate(dot(v.worldNormal, light.direction)) * light.color;
}

float3 GetLighting(v2f v){
	float3 color = 0.0;
	for(int i = 0; i < GetDirLightCount(); i++){
		color += InComingLight(v, GetDirectionLight(i));
	}
	return color;
}

v2f vert(a2v i){
	v2f o;
	o.pos = TransformObjectToHClip(i.vertex.xyz);
	o.worldNormal = TransformObjectToWorldNormal(i.normal);
	return o;
}

  half4 frag(v2f v):SV_TARGET
 {
	  float lightColor = GetLighting(v);
	  float3 diffuse = lightColor * _BaseColor;
      return half4(diffuse, 1);
 }

#endif
