Shader "CustomRP/CRP Unlit"
{
    Properties
    {
        _RenderSettingLable("RenderSettings", int) = 0
    	[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [HideInInspector]_Shadows("Shadows", float) = 0
        [HideInInspector]_Mode ("__mode", Float) = 0.0
        [HideInInspector]_Transparent("__transparent", Float) = 0.0

        _MaterialSettingLable("MaterialSettings", int) = 0
		_AlphaCutoff("Alpha CutOff", Range(0,1)) = 0
        [Toggle(_CLIPPING)] _Clipping("AlphaTest", float) = 0
		[Toggle(_PREMULTIPY_ALPHA)] _PremulAlpha("Pre Mul Alpha", float) = 0

        _MainTexLable("主贴图", int) = 0
		[HDR]_BaseColor("BaseColor", color) = (1,1,1,1)
		_BaseTexture("Base Texture", 2D) = "white"{}
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
            Cull[_Cull]

			HLSLPROGRAM
			
			#pragma shader_feature _CLIPPING	
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

	CustomEditor "LoyShaderGUI"
}