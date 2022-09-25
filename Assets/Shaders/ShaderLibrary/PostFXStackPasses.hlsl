#ifndef CUSTOM_FX_PASSES_INCLUDE
#define CUSTOM_FX_PASSES_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);

float4 _PostFXSource_TexelSize;

bool _BloomBicubicUpsampling;

float4 _BloomThreshold;

float _BloomIntensity;

float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}

float4 GetSource(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}

float4 GetSource2(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}

float4 GetSourceBicubic(float2 screenUV)
{
    return SampleTexture2DBicubic(TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp),
        screenUV, _PostFXSource_TexelSize.zwxy, 1.0, 1.0);
}

float3 ApplyBloomThreshold(float3 color)
{
    float brightness = Max3(color.r, color.g, color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);
    return color * contribution;
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

float4 BloomPrefilterPassFragment(Varyings input) : SV_TARGET
{
    float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
    return float4(color, 1.0);
}

//多个像素一起做筛选
//
float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float weightSum = 0.0;
    float2 offsets[] = {
        float2(0.0, 0.0),
        float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)
    };   
    for (int i = 0;i < 5; i++)
    {
        float3 c = GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
        c = ApplyBloomThreshold(c);
        //转换到流明，越亮权重越低
        float w = 1.0 / (Luminance(c) +1.0);
        color += c * w;
        weightSum += w;
    }
    color /= weightSum;
    return float4(color, 1.0);
}

float4 BloomCombineAddPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if(_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float4 highRes = GetSource2(input.screenUV);
    return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}

float4 BloomCombineScatterPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if(_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    
    float3 highRes = GetSource2(input.screenUV).rgb;
    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float4 BloomScatterFinalPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if(_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float4 highRes = GetSource2(input.screenUV);
    lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
    return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}

float4 _ColorAdjustments;
float4 _ColorFilter;
float4 _WhiteBalance;
float4 _SplitToningShadows, _SplitToningHighLights;
float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;
float4 _SmhShadows, _SmhMidtones, _SmhHighlights, _SmhRange;

float Luminance(float3 color, bool useACES)
{
    return useACES ? AcesLuminance(color) : Luminance(color);
}

float3 ColorGradingPostExposure(float3 color)
{
    return color * _ColorAdjustments.x;
}

//白平衡
//转换到LMS然后进行相乘
//LMS是人眼三种感光锥类型
float3 ColorGradingWhiteBalance(float3 color)
{
    color = LinearToLMS(color);
    color *= _WhiteBalance.rgb;
    return LMSToLinear(color);
}

//对比度  (color - 中间灰度) * 对比度 + 中间灰度
float3 ColorGradingContrast(float3 color, bool useACES)
{
    //转到合适的空间再进行对比度矫正 效果会好些
    color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) :  LinearToLogC(color);
    color =  (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
    return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) :  LogCToLinear(color);
}

float3 ColorGradingColorFilter(float3 color)
{
    return color * _ColorFilter.rgb;
}

//色相偏移
//先转换到HSV 然后改变H的值
float3 ColorGradingHueShift(float3 color)
{
    color = RgbToHsv(color);
    float hue = color.x + _ColorAdjustments.z;
    color.x = RotateHue(hue, 0.0, 1.0);
    return HsvToRgb(color);
}

//饱和度
//(color - 亮度) * 饱和度 + 亮度
float3 ColorGradingSaturation(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    return (color - luminance) * _ColorAdjustments.w + luminance;
}

//色调分离
//用于分离图像的阴影和高光，一般阴影推向冷蓝色，高光推向暖橙色
float3 ColorGradingSplitToning(float3 color, bool useACES)
{
    color = PositivePow(color, 1.0/2.2);
    float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
    float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
    float3 highLights = lerp(0.5, _SplitToningHighLights.rgb, t);
    color = SoftLight(color, shadows);
    color = SoftLight(color, highLights);
    return PositivePow(color, 2.2);
}

//通道混合
//通过RGB权重产生新的颜色值
float3 ColorGradingChannelMixer(float3 color)
{
    return mul(float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb), color);
}

//另一种色调分离
float3 ColorGradingShadowsMidtonesHighlights(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    float shadowsWeight = 1.0 - smoothstep(_SmhRange.x, _SmhRange.y, luminance);
    float highlightsWeight = smoothstep(_SmhRange.z, _SmhRange.w, luminance);
    float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
    return color * _SmhShadows.rgb * shadowsWeight +
           color * _SmhMidtones.rgb * midtonesWeight +
           color * _SmhHighlights.rgb * highlightsWeight;
}

float3 ColorGrading(float3 color, bool useACES = false)
{
    //color = min(color, 60.0);
    
    //后曝光  pow(2, x) 非线性提高整体颜色亮度
    color = ColorGradingPostExposure(color);
    //白平衡 LMS
    color = ColorGradingWhiteBalance(color);
    //对比度 
    color = ColorGradingContrast(color, useACES);
    //颜色滤镜 直接乘一个颜色
    color = ColorGradingColorFilter(color);
    //对比度可能会产生负值
    color = max(color, 0.0);
    //色调分离
    color = ColorGradingSplitToning(color, useACES);
    //通道混合
    color = ColorGradingChannelMixer(color);
    //通道混合可能产生负值
    color = max(color, 0.0);
    color = ColorGradingShadowsMidtonesHighlights(color, useACES);
    //色相偏移
    color = ColorGradingHueShift(color);
    //饱和度
    color = ColorGradingSaturation(color, useACES);
    //饱和度可能产生负值
    color = max(color, 0.0);
    return max(useACES ? ACEScg_to_ACES(color) : color, 0.0);
}

float4 _ColorGradingLUTParams;
bool _ColorGradingLUTInLogC;
float3 GetColorGradeLUT(float2 uv, bool useACES = false)
{
    float3 color = GetLutStripValue(uv, _ColorGradingLUTParams);
    return ColorGrading(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}

//LUT ColorGrading
float4 ColorGradingNonePassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV);
    return float4(color, 1.0);
}

float4 ColorGradingNeutralPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV);
    color = NeutralTonemap(color);
    return float4(color, 1.0);
}

float4 ColorGradingReinhardPassFragment(Varyings input) :SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV);
    color /= color + 1.0;
    return float4(color, 1.0);
}

float4 ColorGradingACESPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV, true);
    color = AcesTonemap(color);
    return float4(color, 1.0);
}

//ColorGrading
float4 ToneMappingNonePassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ColorGrading(color.rgb);
    return color;
}

float4 ToneMappingNeutralPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ColorGrading(color.rgb);
    color.rgb = NeutralTonemap(color.rgb);
    return color;
}

float4 ToneMappingReinhardPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ColorGrading(color.rgb);
    color.rgb /= color.rgb + 1.0;
    return color;
}

float4 ToneMappingACESPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ColorGrading(color.rgb, true);
    color.rgb = AcesTonemap(color.rgb);
    return color;
}

//ColorGradingLUT
TEXTURE2D(_ColorGradingLUT);

float3 ApplyColorGradingLUT(float3 color)
{
    return ApplyLut2D(
        TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
        saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
        _ColorGradingLUTParams.xyz
    );
}

float4 ApplyColorGradingPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color);
    return color;
}

float4 ApplyColorGradingWithLumaPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color);
    color.a = sqrt(Luminance(color.rgb));
    return color;
}

//根据VertexID得到PosCS和screenUV
// clip 0(-1, -1)  1( -1, 3) 2(3, -1)
// UV   0(0, 0)    1(0, 2)   2(2, 0)
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
}

bool _CopyBicubic;
float4 FinalPassFragmentRescale(Varyings input) : SV_TARGET
{
    if(_CopyBicubic)
    {
        return GetSourceBicubic(input.screenUV);
    }
    else
    {
        return GetSource(input.screenUV);
    }
}

#endif