#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED

TEXTURE2D(_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

float4 _CameraBufferSize;

struct Fragment
{
    float2 positionSS;
    float depth;
    float2 screenUV;
    float bufferDepth; //eyeDepth
};

Fragment GetFragment(float4 positionSS)
{
    Fragment f;
    f.positionSS = positionSS.xy;
    f.screenUV = f.positionSS * _CameraBufferSize.xy; // Clip空间的直接除以 screen范围-1~1
    f.depth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w;
    f.bufferDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, samlper_point_clamp, f.screenUV, 0).r;
    f.bufferDepth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(f.bufferDepth) : LinearEyeDepth(f.bufferDepth, _ZBufferParams);
    return f;
}

float4 GetBufferColor(Fragment fragment, float2 uvOffset = float2(0.0, 0.0))
{
    float2 uv = fragment.screenUV + uvOffset;
    return SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_linear_clamp, uv, 0);
}

#endif