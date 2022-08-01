#ifndef CUSTOM_SHADOWCASTER_PASS_INCLUDE

#define CUSTOM_SHADOWCASTER_PASS_INCLUDE

#include "Common.hlsl"

TEXTURE2D(_BaseTexture);   //纹理和采样器不可以实例
SAMPLER(sampler_BaseTexture);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseTexture_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _AlphaCutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes {
	float3 positionOS : POSITION;
	float2 uv0 : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 uv0 : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowCasterPassVertex(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 worldPos = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(worldPos);

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseTexture_ST);
	output.uv0 = input.uv0 * baseST.xy + baseST.zw;

	return output;
}

void ShadowCasterPassFragment(Varyings input)
{
	UNITY_SETUP_INSTANCE_ID(input);
	half4 baseTex = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, input.uv0);
	half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	half4 finalColor = baseTex * baseColor;
#if defined(_CLIPPING)
	clip(baseTex.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AlphaCutoff));
#endif

}

#endif
