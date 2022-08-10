#ifndef CUSTOM_LIT_PASS_INCLUDE
#define CUSTOM_LIT_PASS_INCLUDE

#include "ShaderLibrary/Common.hlsl"
#include "ShaderLibrary/Surface.hlsl"
#include "ShaderLibrary/Shadows.hlsl"
#include "ShaderLibrary/Light.hlsl"
#include "ShaderLibrary/BRDF.hlsl"
#include "ShaderLibrary/GI.hlsl"
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
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
	float4 pos : SV_POSITION;
	float3 worldNormal: VAR_NORMAL;
	float3 worldPos :VAR_POSITION;
	float2 uv : TEXCOORD0;
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};



Varyings litVert(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_TRANSFER_INSTANCE_ID(i, output);
    TRANSFER_GI_DATA(input,output);
	
	output.pos = TransformObjectToHClip(input.vertex.xyz);
	//o.worldNormal = mul(UNITY_MATRIX_M, i.normal);  //不正确的写法
	output.worldNormal = TransformObjectToWorldNormal(input.normal);  //正确的写法
	output.worldPos = TransformObjectToWorld(input.vertex.xyz);

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseTexture_ST);
	output.uv = input.uv * baseST.xy + baseST.zw;
	return output;
}

half4 litFrag(Varyings input) :SV_TARGET
{
	  UNITY_SETUP_INSTANCE_ID(v);
	  input.worldNormal = normalize(input.worldNormal);
	  half3 albedo = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _BaseColor).rgb;
	  half4 baseTex = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, input.uv);
	

	  Surface s;
	  s.position = input.worldPos;
	  s.normal = normalize(input.worldNormal);
	  s.viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
	  s.depth = -TransformWorldToView(input.worldPos).z; //摄像机空间-Z unity前方是-Z
	  s.color = albedo * baseTex.rgb;
	  s.alpha = baseTex.a;
	  s.metallic = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Metallic);
	  s.smoothness = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Smoothness);
	  s.dither = InterleavedGradientNoise(input.pos.xy, 0);

	  //
	  #if defined(_CLIPPING)
		clip(s.alpha - _AlphaCutoff);
	  #endif
	  
	  #if defined(_PREMULTIPY_ALPHA)
		BRDF brdf = GetBRDF(s, true);
	  #else 
		BRDF brdf = GetBRDF(s);
	  #endif

	  GI gi = GetGI(GI_FRAGMENT_DATA(input));
	
	  float3 color = GetLighting(s, brdf, gi);

      return half4(color, s.alpha);
 }

#endif
