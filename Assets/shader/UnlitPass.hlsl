
#ifndef CUSTOM_UNLIT_PASS_INCLUDE

#define CUSTOM_UNLIT_PASS_INCLUDE

CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
sampler2D _BaseTexture;
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


v2f vert(a2v i){
	v2f o;
	o.pos = TransformObjectToHClip(i.vertex.xyz);
	o.uv = i.uv;
	return o;
}

  half4 frag(v2f v):SV_TARGET
 {
 	  half4 tex = tex2D(_BaseTexture, v.uv);
	  half3 color = tex.rgb * _BaseColor.rgb;

 	  #if defined(_CLIPPING)
		clip(tex.a - _AlphaCutoff);
	  #endif

      return half4(color, tex.a);
 }

#endif
