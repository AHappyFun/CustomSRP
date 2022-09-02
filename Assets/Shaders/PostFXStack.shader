﻿Shader "CRP/PostProcess/PostFXStack"
{
    
    SubShader
    {
        Cull off
        ZTest Always
        ZWrite off
        
        HLSLINCLUDE
            #include "ShaderLibrary/Common.hlsl"
            #include "ShaderLibrary/PostFXStackPasses.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "Bloom Horizontal"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomHorizontalPassFragment
            ENDHLSL
        }
        
                Pass
        {
            Name "Bloom Vertical"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomVerticalPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Copy"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment CopyPassFragment
            ENDHLSL
        }
        
    }
}