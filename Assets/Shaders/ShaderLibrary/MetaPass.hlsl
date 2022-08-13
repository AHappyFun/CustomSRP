#ifndef CUSTOM_META_PASS_INCLUDE
#define CUSTOM_META_PASS_INCLUDE

#include "ShaderLibrary/Surface.hlsl"
#include "ShaderLibrary/Shadows.hlsl"
#include "ShaderLibrary/Light.hlsl"
#include "ShaderLibrary/BRDF.hlsl"

bool4 unity_MetaFragmentControl;
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

struct Attributes{
    float3 vertex: POSITION;
    float2 uv: TEXCOORD0;
    float2 lightMapUV : TEXCOORD1;
};

struct Varyings{
    float4 pos : SV_POSITION;
    float2 uv : VAR_BASE_UV;
};

Varyings MetaPassVert(Attributes input)
{
    Varyings output;

    input.vertex.xy = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    input.vertex.z = input.vertex.z > 0.0 ? REAL_MIN : 0.0;
     
    output.pos = TransformWorldToHClip(input.vertex);
    output.uv = TransformBaseUV(input.uv);
    return output;
}

float4 MetaPassFrag(Varyings input) : SV_TARGET
{
    float4 base = GetBase(input.uv);
    
    Surface surf;
    ZERO_INITIALIZE(Surface, surf);
    surf.color = base.rgb;
    surf.metallic = GetMetallic();
    surf.smoothness = GetSmoothness();
    surf.alpha = base.a;

    BRDF brdf = GetBRDF(surf);

    float4 meta = 0;
    if(unity_MetaFragmentControl.x)
    {
        meta = float4(brdf.diffuse, 1.0);
        meta.rgb += brdf.specular * brdf.roughness * 0.5;
        meta.rgb = min(
            PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue
        );
    }
    else if (unity_MetaFragmentControl.y)
    {
        meta = float4(GetEmission(input.uv), 1);
    }
    return meta;
    
}

#endif