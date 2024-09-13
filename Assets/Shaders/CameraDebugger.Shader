Shader "CustomRP/Hidden/CameraDebugger"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
            #include "ShaderLibrary/Common.hlsl"
            #include "ShaderLibrary/CameraDebuggerPasses.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "Forward+ Tiles"
            
            Blend SrcAlpha OneMinusSrcColor
            
            HLSLPROGRAM
                #pragma target 4.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ForwardPlusTilesPassFragment              
            ENDHLSL
        }
    }
}