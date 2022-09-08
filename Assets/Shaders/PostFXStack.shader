Shader "CRP/PostProcess/PostFXStack"
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
            Name "Bloom Combine Add"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomCombineAddPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Bloom Combine Scatter"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomCombineScatterPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Bloom Scatter Final"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomScatterFinalPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Bloom Prefilter"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomPrefilterPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Bloom Prefilter Fireflies"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomPrefilterFirefliesPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "ToneMapping Neutral"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ToneMappingNeutralPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "ToneMapping Reinhard"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ToneMappingReinhardPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "ToneMapping ACES"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ToneMappingACESPassFragment
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