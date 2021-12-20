#ifndef CUSTOM_LIT_PASS_INCLUDE

#define CUSTOM_LIT_PASS_INCLUDE

CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
float _Metallic;
float _Smoothness;
float _AlphaCutoff;
CBUFFER_END

struct a2v{
	float3 vertex: POSITION;
	float3 normal: NORMAL;
};

struct v2f{
	float4 pos : SV_POSITION;
	float3 worldNormal: VAR_NORMAL;
	float3 worldPos :VAR_POSITION;
};

struct Surface{
	float3 normal;
	float3 viewDir;
	float3 color;
	float alpha;
	float metallic;
	float smoothness;
};

#include "BRDF.hlsl"

float3 InComingLight(Surface v , Light light, BRDF brdf){
	return saturate(dot(v.normal, light.direction)) * light.color;
}

float3 GetLighting(Surface surface, Light light, BRDF brdf){
	return InComingLight(surface, light, brdf) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface v, BRDF brdf){
	float3 color = 0.0;
	for(int i = 0; i < GetDirLightCount(); i++){
		color += GetLighting(v, GetDirectionLight(i), brdf) * v.color;
	}
	return color;
}

v2f vert(a2v i){
	v2f o;
	o.pos = TransformObjectToHClip(i.vertex.xyz);
	o.worldNormal = TransformObjectToWorldNormal(i.normal);
	o.worldPos = TransformObjectToWorld(i.vertex.xyz);
	return o;
}

  half4 frag(v2f v):SV_TARGET
 {

	  Surface s;
	  s.normal = normalize(v.worldNormal);
	  s.viewDir = normalize(_WorldSpaceCameraPos - v.worldPos);
	  s.color = _BaseColor.rgb;
	  s.alpha = _BaseColor.a;
	  s.metallic = _Metallic;
	  s.smoothness = _Smoothness;

	  BRDF brdf = GetBRDF(s);

	  float3 color = GetLighting(s, brdf);

      return half4(color, 1);
 }

#endif
