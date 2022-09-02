#ifndef CUSTOM_FX_PASSES_INCLUDE
#define CUSTOM_FX_PASSES_INCLUDE


struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

TEXTURE2D(_PostFXSource);
SAMPLER(sampler_linear_clamp);

float4 _ProjectionParams;

float4 _PostFXSource_TexelSize;

float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}

float4 GetSource(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}

float4 BloomHorizontalPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };
    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    for (int i = 0;i < 5; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
    }
    return float4(color, 1.0);
}

float4 BloomVerticalPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };
    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    for (int i = 0;i < 5; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().y;
        color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
    }
    return float4(color, 1.0);
}

//根据VertexID得到PosCS和screenUV
Varyings DefaultPassVertex(uint vertexID : SV_VertexID)
{
    Varyings output;
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0, 1.0
    );
    output.screenUV = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
    );
    if(_ProjectionParams.x < 0.0)
    {
        output.screenUV.y = 1 - output.screenUV.y;
    }
    return output;
}

float4 CopyPassFragment(Varyings input) : SV_TARGET
{
    return GetSource(input.screenUV);
    //return float4(input.screenUV, 0.0, 1.0);   
}

#endif