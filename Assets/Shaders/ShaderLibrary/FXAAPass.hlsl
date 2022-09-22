#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED

float GetLuma(float2 uv)
{
    return Luminance(GetSource(uv));
}

//FXAA AfterColorGrad
float4 FXAAPassFragment(Varyings input) : SV_TARGET
{
    return GetLuma(input.screenUV);
}

#endif