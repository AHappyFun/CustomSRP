Shader "CustomRP/CRP Unlit Particle"
{
    Properties
    {
		[HDR]_BaseColor("BaseColor", color) = (1,1,1,1)
    	[Toggle(_VERTEX_COLORS)] _VertexColors("Vertex Colors Enable", float) = 0
    	[Toggle(_FLIPBOOK_BLENDING)] _FlipbookBlending("Flipbook Blending", float) = 0
		_BaseTexture("Base Texture", 2D) = "white"{}
		_AlphaCutoff("Alpha CutOff", Range(0,1)) = 0
    	
    	[Toggle(_NEAR_FADE)] _NearFade("Near Fade", float) = 0
    	_NearFadeDistance("Near Fade Distance", Range(0.0, 10.0)) = 1
    	_NearFadeRange("Near Fade Range", Range(0.01, 10.0)) = 1

		[Toggle(_CLIPPING)] _Clipping("AlphaTest", float) = 0
    	[KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", float) = 0

		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipline" = "UniversalRenderPipeline"}

        HLSLINCLUDE
    		#include "ShaderLibrary/Common.hlsl"
			#include "ShaderLibrary/UnLitInput.hlsl"
    	ENDHLSL
    	
        Pass
        {
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]


			HLSLPROGRAM
			
			#pragma shader_feature _CLIPPING
			#pragma shader_feature _VERTEX_COLORS
			#pragma shader_feature _FLIPBOOK_BLENDING
			#pragma shader_feature _NEAR_FADE
			#include "ShaderLibrary/UnlitPass.hlsl"

			#pragma multi_compile_instancing
			#pragma vertex unlitVert
			#pragma fragment unlitFrag

			ENDHLSL
		}
    	
    	Pass
		{
			Tags{
				"LightMode" = "ShadowCaster"
			}
			ColorMask 0

			HLSLPROGRAM
			#pragma target 3.5
			#pragma multi_compile_instancing
			//#pragma shader_feature _CLIPPING
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShaderLibrary/ShadowCasterPass.hlsl" 
			ENDHLSL
		}
    	
    }

	CustomEditor "CustomShaderGUI"
}