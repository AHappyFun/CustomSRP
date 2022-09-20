Shader "CustomRP/Hidden/CameraRenderer"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
            #include "ShaderLibrary/Common.hlsl"
            #include "ShaderLibrary/CameraRendererPasses.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "Copy"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment CopyPassFragment              
            ENDHLSL
        }
        
        Pass
        {
            Name "Copy Depth"
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment CopyDepthPassFragment              
            ENDHLSL
        }
    }
}