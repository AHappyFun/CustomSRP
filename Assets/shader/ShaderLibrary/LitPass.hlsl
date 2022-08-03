#ifndef CUSTOM_LIT_PASS_INCLUDE
#define CUSTOM_LIT_PASS_INCLUDE

#include "ShaderLibrary/Common.hlsl"
#include "ShaderLibrary/Surface.hlsl"
#include "ShaderLibrary/Shadows.hlsl"
#include "ShaderLibrary/Light.hlsl"
#include "ShaderLibrary/BRDF.hlsl"
#include "ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_BaseTexture);   //纹理和采样器不可以实例
SAMPLER(sampler_BaseTexture);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseTexture_ST)
	UNITY_DEFINE_INSTANCED_PROP(float, _AlphaCutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes{
	float3 vertex: POSITION;
	float3 normal: NORMAL;
	float2 uv: TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
	float4 pos : SV_POSITION;
	float3 worldNormal: VAR_NORMAL;
	float3 worldPos :VAR_POSITION;
	float2 uv : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};



Varyings litVert(Attributes i){
	Varyings o;
	o.pos = TransformObjectToHClip(i.vertex.xyz);
	//o.worldNormal = mul(UNITY_MATRIX_M, i.normal);  //不正确的写法
	o.worldNormal = TransformObjectToWorldNormal(i.normal);  //正确的写法
	o.worldPos = TransformObjectToWorld(i.vertex.xyz);
	o.uv = i.uv;
	return o;
}

half4 litFrag(Varyings v) :SV_TARGET
{
	  UNITY_SETUP_INSTANCE_ID(v);
	  v.worldNormal = normalize(v.worldNormal);
	  half3 albedo = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor).rgb;
	  half4 baseTex = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, v.uv);
	

	  Surface s;
	  s.position = v.worldPos;
	  s.normal = normalize(v.worldNormal);
	  s.viewDir = normalize(_WorldSpaceCameraPos - v.worldPos);
	  s.depth = -TransformWorldToView(v.worldPos).z; //摄像机空间-Z unity前方是-Z
	  s.color = albedo * baseTex.rgb;
	  s.alpha = baseTex.a;
	  s.metallic = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Metallic);
	  s.smoothness = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Smoothness);
	  s.dither = InterleavedGradientNoise(v.pos.xy, 0);

	  //
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
