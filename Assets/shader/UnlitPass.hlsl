
#ifndef CUSTOM_UNLIT_PASS_INCLUDE

#define CUSTOM_UNLIT_PASS_INCLUDE

CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
CBUFFER_END

struct a2v{
	float3 vertex: POSITION;
};

struct v2f{
	float4 pos : SV_POSITION;
};


v2f vert(a2v i){
	v2f o;
	o.pos = TransformObjectToHClip(i.vertex.xyz);
	return o;
}

  half4 frag(v2f v):SV_TARGET
 {
      return half4(_BaseColor.rgba);
 }

#endif
