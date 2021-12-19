Shader "CustomRP/CRP Lit"
{
    Properties
    {
		_BaseColor("BaseColor", color) = (0.5,0.5,0.5,1)
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
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"
			#include "Light.hlsl"
			#include "LitPass.hlsl"

			#pragma vertex vert
			#pragma fragment frag

			ENDHLSL
		}
    }
}