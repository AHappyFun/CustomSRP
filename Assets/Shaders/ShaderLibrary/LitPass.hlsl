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
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
    TRANSFER_GI_DATA(input,output);
	
	output.pos = TransformObjectToHClip(input.vertex.xyz);
	//o.worldNormal = mul(UNITY_MATRIX_M, i.normal);  //不正确的写法
	output.worldNormal = TransformObjectToWorldNormal(input.normal);  //正确的写法
	output.worldPos = TransformObjectToWorld(input.vertex.xyz);

	output.uv = TransformBaseUV(input.uv);
	return output;
}

half4 litFrag(Varyings input) :SV_TARGET
{
	  UNITY_SETUP_INSTANCE_ID(input);

	  ClipLOD(input.pos.xy, unity_LODFade.x);
	
	  input.worldNormal = normalize(input.worldNormal);
	
	  float4 base = GetBase(input.uv);
	
	  Surface surf;
	  surf.position = input.worldPos;
	  surf.normal = normalize(input.worldNormal);
	  surf.viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
	  surf.depth = -TransformWorldToView(input.worldPos).z; //摄像机空间-Z unity前方是-Z
	  surf.color = base.rgb;
	  surf.alpha = base.a;
	  surf.metallic = GetMetallic();
	  surf.smoothness = GetSmoothness();
	  surf.dither = InterleavedGradientNoise(input.pos.xy, 0);

	  //
	  #if defined(_CLIPPING)
		clip(surf.alpha - GetCutOff());
	  #endif
	  
	  #if defined(_PREMULTIPY_ALPHA)
		BRDF brdf = GetBRDF(surf, true);
	  #else 
		BRDF brdf = GetBRDF(surf);
	  #endif

	  GI gi = GetGI(GI_FRAGMENT_DATA(input), surf);
	
	  float3 color = GetLighting(surf, brdf, gi);

	  color += GetEmission(input.uv);

      return half4(color, surf.alpha);
 }

#endif
