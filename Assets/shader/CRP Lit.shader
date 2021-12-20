Shader "CustomRP/CRP Lit"
{
    Properties
    {
		_BaseColor("BaseColor", color) = (0.5,0.5,0.5,1)
		_Metallic("Metallic", range(0,1)) = 0
		_Smoothness("Smoothness",Range(0,1)) = 0.5
		_AlphaCutoff("Alpha CutOff", Range(0,1)) = 0
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

			HLSLPROGRAM
			#pragma target 3.5
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"
			#include "Light.hlsl"
			#include "LitPass.hlsl"
			//#include "BRDF.hlsl"


			#pragma vertex vert
			#pragma fragment frag

			ENDHLSL
		}
    }
}