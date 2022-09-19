
#ifndef CUSTOM_UNLIT_PASS_INCLUDE
#define CUSTOM_UNLIT_PASS_INCLUDE


struct Attributes {
	float3 positionOS : POSITION;
	float4 color : COLOR;
#if defined(_FLIPBOOK_BLENDING)
	float4 uv0 : TEXCOORD0;
	float flipbookBlend : TEXCOORD1;
#else
	float2 uv0 : TEXCOORD0;
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS : SV_POSITION;
#if defined(_VERTEX_COLORS)
	float4 color : VAR_COLOR;
#endif
	float2 uv0 : VAR_BASE_UV;
#if defined(_FLIPBOOK_BLENDING)
	float3 flipbookUVB : VAR_FLIPBOOK;
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};


Varyings unlitVert(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 worldPos = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(worldPos);

	output.uv0.xy = TransformBaseUV(input.uv0.xy);
#if defined(_FLIPBOOK_BLENDING)
	output.flipbookUVB.xy = TransformBaseUV(input.uv0.zw);
	output.flipbookUVB.z = input.flipbookBlend;
#endif
	
#if defined(_VERTEX_COLORS)
	output.color = input.color;
#endif	

	return output;
}

half4 unlitFrag(Varyings input) :SV_TARGET
{
	 UNITY_SETUP_INSTANCE_ID(input);

	 ClipLOD(input.positionCS.xy, unity_LODFade.x);

	 InputConfig cfg = GetInputConfig(input.uv0, 0.0);
#if defined(_VERTEX_COLORS)
	 cfg.color = input.color;
#endif

#if defined(_FLIPBOOK_BLENDING)
	 cfg.flipbookUVB = input.flipbookUVB;
	 cfg.flipbookBlending = true;
#endif

	
	 half4 finalColor = GetBase(cfg);
#if defined(_CLIPPING)
	 clip(finalColor.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _AlphaCutoff));
#endif

	 return float4(finalColor.rgb, GetFinalAlpha(finalColor.a)) ;

 }

#endif
