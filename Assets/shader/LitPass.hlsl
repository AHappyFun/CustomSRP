#ifndef CUSTOM_LIT_PASS_INCLUDE

#define CUSTOM_LIT_PASS_INCLUDE

CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
sampler2D _BaseTexture;
float _Metallic;
float _Smoothness;
float _AlphaCutoff;
CBUFFER_END

struct a2v{
	float3 vertex: POSITION;
	float3 normal: NORMAL;
	float2 uv: TEXCOORD0;
};

struct v2f{
	float4 pos : SV_POSITION;
	float3 worldNormal: VAR_NORMAL;
	float3 worldPos :VAR_POSITION;
	float2 uv : TEXCOORD0;
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
	o.uv = i.uv;
	return o;
}

  half4 frag(v2f v):SV_TARGET
 {
	
	  half4 tex = tex2D(_BaseTexture, v.uv);
	
	  Surface s;
	  s.normal = normalize(v.worldNormal);
	  s.viewDir = normalize(_WorldSpaceCameraPos - v.worldPos);
	  s.color = _BaseColor.rgb * tex.rgb;
	  s.alpha = tex.a;
	  s.metallic = _Metallic;
	  s.smoothness = _Smoothness;

	  #if defined(_CLIPPING)
		clip(s.alpha - _AlphaCutoff);
	  #endif

	  #if defined(_PREMULTIPY_ALPHA)
		BRDF brdf = GetBRDF(s, true);
	  #else 
		BRDF brdf = GetBRDF(s);
	  #endif

	  float3 color = GetLighting(s, brdf);

      return half4(color, s.alpha);
 }

#endif
