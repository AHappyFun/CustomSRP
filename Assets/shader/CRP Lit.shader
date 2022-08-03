Shader "CustomRP/CRP Lit"
{
    Properties
    {
		_BaseColor("BaseColor", color) = (0.5,0.5,0.5,1)
		_BaseTexture("Base Texture", 2D) = "white"{}
		_Metallic("Metallic", range(0,1)) = 0
		_Smoothness("Smoothness",Range(0,1)) = 0.5
		_AlphaCutoff("Alpha CutOff", Range(0,1)) = 0

		[Toggle(_CLIPPING)] _Clipping("AlphaTest", float) = 0
		[Toggle(_PREMULTIPY_ALPHA)] _PremulAlpha("Pre Mul Alpha", float) = 0

		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" 
				   "RenderPipline" = "UniversalRenderPipeline"
		}


        Pass
        {
			Tags{
				"LightMode" = "CustomLit"
			}
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma shader_feature _CLIPPING
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
			#pragma shader_feature _PREMULTIPY_ALPHA

			#pragma target 3.5
			//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"
			#include "ShaderLibrary/LitPass.hlsl"

			#pragma vertex litVert
			#pragma fragment litFrag

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
			#pragma shader_feature _CLIPPING
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShaderLibrary/ShadowCasterPass.hlsl" 
			ENDHLSL
		}
    }

	CustomEditor "CustomShaderGUI"
}