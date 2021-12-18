Shader "Unlit/URP sam"
{
    Properties
    {
		_BaseColor("BaseColor", color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipline" = "UniversalRenderPipeline"}

        Pass
        {
			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/core.hlsl"
			//cbuffer UnityPerMaterial{
			//	float4 _BaseColor;
			//};

			//SRP Batcher
			CBUFFER_START(UnityPerMaterial)
			half4 _BaseColor;
			CBUFFER_END

			#pragma vertex vert
			#pragma fragment frag

			struct a2v{
				float3 vertex: POSITION;
			};

			struct v2f{
				float4 pos : SV_POSITION;
			};

			v2f vert(a2v i){
				v2f o;
				o.pos = TransformObjectToHClip(i.vertex.xyz);
				return o;
			}

			  half4 frag(v2f v):SV_TARGET
             {

                  return half4(_BaseColor.rgb, 1);
             }

			ENDHLSL
		}
    }
}