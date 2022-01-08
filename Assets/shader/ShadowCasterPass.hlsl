#ifndef CUSTOM_SHADOWCASTER_PASS_INCLUDE

#define CUSTOM_SHADOWCASTER_PASS_INCLUDE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"

sampler2D _BaseTexture;
CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
float _AlphaCutoff;
CBUFFER_END

struct a2v{
	float3 vertex: POSITION;
	float2 uv: TEXCOORD0;
};

struct v2f{
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
};

v2f ShadowCasterPassVertex(a2v i){
	v2f o;
	o.pos = TransformObjectToHClip(i.vertex.xyz);
	o.uv = i.uv;
	return o;
}

  half4 ShadowCasterPassFragment(v2f v):SV_TARGET
 {
	  half4 baseTex = tex2D(_BaseTexture, v.uv);
	  half4 col = baseTex * _BaseColor;

	  #if defined(_CLIPPING)
		clip(col.a - _AlphaCutoff);
	  #endif

	  return col;
 }

#endif
