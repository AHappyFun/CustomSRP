
#ifndef CUSTOM_UNLIT_PASS_INCLUDE
#define CUSTOM_UNLIT_PASS_INCLUDE


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


Varyings unlitVert(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 worldPos = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(worldPos);

	output.uv0 = TransformBaseUV(input.uv0);

	return output;
}

half4 unlitFrag(Varyings input) :SV_TARGET
{
	 UNITY_SETUP_INSTANCE_ID(input);

	 half4 finalColor = GetBase(input.uv0);
#if defined(_CLIPPING)
	 clip(finalColor.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AlphaCutoff));
#endif

	 return finalColor ;

 }

#endif
