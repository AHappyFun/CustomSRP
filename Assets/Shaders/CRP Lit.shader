Shader "CustomRP/CRP Lit"
{
    Properties
    {
    	//for lightmap
    	[HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
		[HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
    	
		_BaseColor("BaseColor", color) = (0.5,0.5,0.5,1)
		_BaseTexture("Base Texture", 2D) = "white"{}
		_Metallic("Metallic", range(0,1)) = 0
		_Smoothness("Smoothness",Range(0,1)) = 0.5
    	[NoScaleOffset]_EmissionTex("EmissionTex", 2D) = "white" {}
    	[HDR]_EmissionColor("EmissionColor", color) = (0,0,0,0)
		_AlphaCutoff("Alpha CutOff", Range(0,1)) = 0

		[Toggle(_CLIPPING)] _Clipping("AlphaTest", float) = 0
    	[KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", float) = 0
    	[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", float) = 1
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

    	HLSLINCLUDE
    	#include "ShaderLibrary/Common.hlsl"
		#include "ShaderLibrary/LitInput.hlsl"
    	ENDHLSL

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
			#pragma shader_feature _RECEIVE_SHADOWS
			#pragma shader_feature _PREMULTIPY_ALPHA
			
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ _SHADOW_MASK_DISTANCE
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER

			//需要处理Loop GLES3.0 
			#pragma target 3.5
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
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShaderLibrary/ShadowCasterPass.hlsl" 
			ENDHLSL
		}
    	
	    Pass
    	{
    		Tags{
    			"LightMode" = "Meta"
            }
    		Cull Off
    		
    		HLSLPROGRAM

    		#pragma target 3.5
    		#pragma vertex MetaPassVert
    		#pragma fragment MetaPassFrag
    		#include "ShaderLibrary/MetaPass.hlsl"
    		
    		ENDHLSL
    	}
    }

	CustomEditor "CustomShaderGUI"
}