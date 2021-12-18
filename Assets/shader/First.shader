Shader "Unlit/First"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
		_BaseColor("BaseColor", color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
			HLSLPROGRAM
			#include "UnlitPass.hlsl"
			cbuffer UnityPerMaterial{
				float4 _BaseColor;
			};
			#pragma vertex vert
			#pragma fragment frag


			ENDHLSL
		}
    }
}
