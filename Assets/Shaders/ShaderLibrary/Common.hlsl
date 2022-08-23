#ifndef CUSTOM_COMMON_INCLUDE
#define CUSTOM_COMMON_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/common.hlsl"
#include "UnityInput.hlsl"

//float3 TransformObjectToWorld(float3 objPos) {
//	return mul(unity_ObjectToWorld, float4(objPos, 1.0)).xyz;
//}
//
//float4 TransformWorldToHClip(float3 worldPos) {
//	return mul(unity_MatrixVP, float4(worldPos, 1.0));
//}

float Square(float v) {
	return v * v;
}

float DistanceSquared(float3 pA, float3 pB) {
	return dot(pA - pB, pA - pB);
}

void ClipLOD(float2 positionCS, float fade)
{
	#if defined(LOD_FADE_CROSSFADE)
		float dither = InterleavedGradientNoise(positionCS.xy, 0);
		clip(fade + (fade < 0.0 ? dither : -dither));
	#endif
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


#endif