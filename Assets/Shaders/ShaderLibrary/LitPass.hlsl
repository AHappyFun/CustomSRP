#ifndef CUSTOM_LIT_PASS_INCLUDE
#define CUSTOM_LIT_PASS_INCLUDE

#include "ShaderLibrary/Surface.hlsl"
#include "ShaderLibrary/Shadows.hlsl"
#include "ShaderLibrary/Light.hlsl"
#include "ShaderLibrary/BRDF.hlsl"
#include "ShaderLibrary/GI.hlsl"
#include "ShaderLibrary/Lighting.hlsl"

struct Attributes{
	float3 vertex: POSITION;
	float3 normal: NORMAL;
	float4 tangent : TANGENT;
	float2 uv: TEXCOORD0;
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
	float4 positionCS_SS : SV_POSITION;
	float3 worldNormal: VAR_NORMAL;
	float3 worldPos :VAR_POSITION;
#if defined(_NORMAL_MAP)
	float4 worldTangent : VAR_TANGENT;
#endif
	float2 uv : VAR_BASE_UV;

#if defined(_DETAIL_MAP)
	float2 detailUV : VAR_DETAIL_UV;
#endif
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};



Varyings litVert(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
    TRANSFER_GI_DATA(input,output);
	
	output.positionCS_SS = TransformObjectToHClip(input.vertex.xyz);
	//o.worldNormal = mul(UNITY_MATRIX_M, i.normal);  //不正确的写法
	output.worldNormal = TransformObjectToWorldNormal(input.normal);  //正确的写法
	output.worldPos = TransformObjectToWorld(input.vertex.xyz);
#if defined(_NORMAL_MAP)
	output.worldTangent = float4(TransformObjectToWorldDir(input.tangent.xyz), input.tangent.w);
#endif
	output.uv = TransformBaseUV(input.uv);
#if defined(_DETAIL_MAP)
	output.detailUV = TransformDetailUV(input.uv);
#endif
	
	return output;
}

half4 litFrag(Varyings input) :SV_TARGET
{
	  UNITY_SETUP_INSTANCE_ID(input);

	
	  input.worldNormal = normalize(input.worldNormal);

	  InputConfig cfg = GetInputConfig(input.positionCS_SS, input.uv, 0.0);
	
	  ClipLOD(cfg.fragment, unity_LODFade.x);
	
#if defined(_DETAIL_MAP)
	  cfg.detailUV = input.detailUV;
	  cfg.useDetail = true;
#endif
	
	  float4 base = GetBase(cfg);
	
	  Surface surf;
	  surf.position = input.worldPos;

#if defined(_NORMAL_MAP)
	  surf.normal = NormalTangentToWorld(GetNormalTangentSpace(cfg), input.worldNormal, input.worldTangent);
	  surf.interpolatedNormal = input.worldNormal;
#else
	  surf.normal = normalize(input.worldNormal);
	  surf.interpolatedNormal = surf.normal;
#endif
	
	  surf.viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
	  surf.depth = -TransformWorldToView(input.worldPos).z; //摄像机空间-Z unity前方是-Z
	  surf.color = base.rgb;
	  surf.alpha = base.a;
	  surf.metallic = GetMetallic(cfg);
	  surf.smoothness = GetSmoothness(cfg);
	  surf.occlusion = GetOcclusion(cfg);
	  surf.fresnelStrength = GetFresnel(cfg);
	  surf.dither = InterleavedGradientNoise(cfg.fragment.positionSS, 0);
	  surf.renderingLayerMask = asuint(unity_RenderingLayer.x);

	  //
	  #if defined(_CLIPPING)
		clip(surf.alpha - GetCutOff());
	  #endif
	  
	  #if defined(_PREMULTIPY_ALPHA)
		BRDF brdf = GetBRDF(surf, true);
	  #else 
		BRDF brdf = GetBRDF(surf);
	  #endif

	  GI gi = GetGI(GI_FRAGMENT_DATA(input), surf, brdf);
	
	  float3 color = GetLighting(surf, brdf, gi);

	  color += GetEmission(cfg);

      return half4(color, GetFinalAlpha(surf.alpha));
 }

#endif
