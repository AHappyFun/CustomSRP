﻿Shader "CustomRP/CRP Unlit"
{
    Properties
    {
		_BaseColor("BaseColor", color) = (1,1,1,1)
		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipline" = "UniversalRenderPipeline"}

        Pass
        {
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]

			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"
			#include "UnlitPass.hlsl"

			#pragma vertex vert
			#pragma fragment frag

			ENDHLSL
		}
    }
}