#ifndef CUSTOM_COMMON_INCLUDE
#define CUSTOM_COMMON_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

SAMPLER(sampler_linear_clamp);
SAMPLER(samlper_point_clamp);

bool IsOrthographicCamera()
{
	return unity_OrthoParams.w;
}

float OrthographicDepthBufferToLinear(float rawDepth)
{
#if UNITY_REVERSED_Z
	rawDepth = 1.0 - rawDepth;
#endif
	return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"


float Square(float v) {
	return v * v;
}

float DistanceSquared(float3 pA, float3 pB) {
	return dot(pA - pB, pA - pB);
}

void ClipLOD(Fragment fragment, float fade)
{
	#if defined(LOD_FADE_CROSSFADE)
		float dither = InterleavedGradientNoise(fragment.positionSS, 0);
		clip(fade + (fade < 0.0 ? dither : -dither));
	#endif
}

float RandomAngle(int2 pixCoord, int frameCount)
{
	const float t = 2.0 * PI / 16.0;
	const float Dither[16] = {
		t * 0,  t * 8,  t * 2,  t * 10,
        t * 12, t * 4,  t * 14, t * 6,
        t * 3,  t * 11, t * 1,  t * 9,
        t * 15, t * 7,  t * 13, t * 5,
    };
	float angle = Dither[(pixCoord.x + frameCount) % 4 + 4 * ((pixCoord.y + frameCount) % 4)];
	return angle;
}

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_DISTANCE) || defined(_SHADOW_MASK_ALWAYS)
	#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"



float3 DecodeNormal(float4 sample, float scale)
{
	#if defined(UNITY_NO_DXT5nm)
		return UnpackNormalRGB(sample, scale);
	#else
		return UnpackNormalmapRGorAG(sample, scale);
	#endif

}

float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS)
{
	float3x3 tangentToWorldMatrix = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return mul(normalTS, tangentToWorldMatrix);
}

struct InputConfig
{
	float4 color;
	float2 baseUV;
	float2 detailUV;
	float3 flipbookUVB;
	bool flipbookBlending;
	bool useMask;
	bool useDetail;
	Fragment fragment;
	bool nearFade;
	bool softParticles;
};

InputConfig GetInputConfig(float4 positionSS, float2 baseUV, float2 detailUV = 0.0)
{
	InputConfig c;
	c.baseUV = baseUV;
	c.detailUV = detailUV;
	c.useMask = false;
	c.useDetail = false;
	c.color = 1.0;
	c.flipbookBlending = false;
	c.flipbookUVB = 0.0;
	c.fragment = GetFragment(positionSS);
	c.nearFade = false;
	c.softParticles = false;
#if defined(_MASK_MAP)
	c.useMask = true;
#endif

#if defined(_DETAIL_MAP)
	c.useDetail = true;
#endif
	
	return c;
}


#endif