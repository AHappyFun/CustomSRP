using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings
{
    public bool overridePostFX = false;
    public PostFXSettings postFXSettings = default;

    public bool allowFXAA = false;

    public bool keepAlpha = false;

    public bool CopyColor = true;
    public bool CopyDepth = true;

    [RenderingLayerMaskField]
    public int renderingLayerMask = -1;

    public bool maskLights = false;
    
    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source, destination;
    }

    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };

    public enum RenderScaleMode
    {
        Inherit, Multiply, Override
    }

    public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

    [Range(0.1f, 2f)]
    public float renderScale = 1f;

    public float GetRenderScale(float scale)
    {
        return  renderScaleMode == RenderScaleMode.Inherit ? scale :
                renderScaleMode == RenderScaleMode.Override ? renderScale :
                scale * renderScale;
    }
}

[Serializable]
public struct CameraBufferSettings
{
    public bool allowHDR;

    public bool copyColor, copyColorReflections;
    
    public bool copyDepth, copyDepthReflections;

    [Range(0.1f, 2f)]
    public float renderScale;
    
    //public bool bicubicRescaling;

    public enum BicubicRescalingMode
    {
        Off, UpOnly, UpAndDown
    }

    public BicubicRescalingMode bicubicRescalingMode;

    [Serializable]
    public struct FXAA
    {
        public bool enabled;

        [Range(0.0312f, 0.0833f)]
        public float fixedThreshold;
        
        // The minimum amount of local contrast required to apply algorithm.
        //   0.333 - too little (faster)
        //   0.250 - low quality
        //   0.166 - default
        //   0.125 - high quality 
        //   0.063 - overkill (slower)
        [Range(0.063f, 0.333f)]
        public float relativeThreshold;

        [Range(0f, 1f)]
        public float subpixelBlending;
        
        public enum Quality
        {
            Low, Medium, High
        }

        public Quality quality;

    }

    public FXAA fxaa;
}
