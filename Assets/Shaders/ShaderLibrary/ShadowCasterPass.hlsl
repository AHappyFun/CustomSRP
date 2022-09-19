#ifndef CUSTOM_SHADOWCASTER_PASS_INCLUDE
#define CUSTOM_SHADOWCASTER_PASS_INCLUDE


struct Attributes {
	float3 positionOS : POSITION;
	float2 uv0 : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS_SS : SV_POSITION;
	float2 uv0 : VAR_BASE_UV;
	float2 detailUV : VAR_DETAIL_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

bool _ShadowPancaking;

Varyings ShadowCasterPassVertex(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 worldPos = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(worldPos);

	if(_ShadowPancaking)
	{		
		//防止阴影在近裁剪之前被剪掉
		//ReversedZ   NDC Z范围(1,0) DX
		//NoReversedZ NDC Z范围(-1, 1) OpenGL
		#if UNITY_REVERSED_Z
			output.positionCS_SS.z = min(output.positionCS_SS.z, output.positionCS_SS.w * UNITY_NEAR_CLIP_VALUE);
		#else
			output.positionCS_SS.z = max(output.positionCS_SS.z, output.positionCS_SS.w * UNITY_NEAR_CLIP_VALUE);
		#endif
	}

	output.uv0 = TransformBaseUV(input.uv0);
	
	return output;
}

void ShadowCasterPassFragment(Varyings input)
{
	UNITY_SETUP_INSTANCE_ID(input);
	
	InputConfig cfg = GetInputConfig(input.positionCS_SS, input.uv0, 0.0);
	
	ClipLOD(cfg.fragment, unity_LODFade.x);

	half4 base = GetBase(cfg);

#if defined(_SHADOWS_CLIP)
	clip(base.a - GetCutOff());
#elif defined(_SHADOWS_DITHER)
	float dither = InterleavedGradientNoise(cfg.fragment.positionSS, 0);
	clip(base.a - dither);
#endif

}

#endif
