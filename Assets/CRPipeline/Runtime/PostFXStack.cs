using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    private const string bufferName = "PostProcessFX";
    
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings;

    private bool useHDR;

    private bool keepAlpha;

    public bool IsActive => settings != null && settings.Active;

    private CameraSettings.FinalBlendMode finalBlendMode;

    CameraBufferSettings.FXAA fxaa;

    enum Pass
    {
        BloomHorizontal,
        BloomVertical,
        BloomCombineAdd,
        BloomCombineScatter,
        BloomScatterFinal,
        BloomPrefilter,
        BloomPrefilterFireflies,
        ToneMappingNone,
        ToneMappingNeutral,
        ToneMappingReinhard,
        ToneMappingACES,
        ApplyColorGrading,
        ApplyColorGradingWithLuma,
        FinalRescale,
        FXAA,
        FXAAWithLuma,
        Copy
    }

    public PostFXStack()
    {
        bloomPyramidID = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    private int finalSrcBlendID = Shader.PropertyToID("_FinalSrcBlend");
    private int finalDstBlendID = Shader.PropertyToID("_FinalDstBlend");
    private int colorGradingResultID = Shader.PropertyToID("_ColorGradingResult");
    private int finalResultID = Shader.PropertyToID("_FinalResult");
    private int copyBicubicID = Shader.PropertyToID("_CopyBicubic");

    private Vector2Int bufferSize;

    private bool bicubicRescaling;

    private CameraBufferSettings.BicubicRescalingMode bicubicRescalingMode;
    
    
    //---bloom---------
    private int bloomBicubicUpsamlingID = Shader.PropertyToID("_BloomBicubicUpsampling");
    private int bloomPrefilterID = Shader.PropertyToID("_BloomPrefilter");
    private int bloomThresholdID = Shader.PropertyToID("_BloomThreshold");
    private int bloomIntensityID = Shader.PropertyToID("_BloomIntensity");
    private int bloomResultID = Shader.PropertyToID("_BloomResult");
    
    private int fxSourceID = Shader.PropertyToID("_PostFXSource");
    private int fxSource2ID = Shader.PropertyToID("_PostFXSource2"); 
    
    private const int maxBloomPyramidLevels = 16;
    private int bloomPyramidID;
    bool DoBloom(int sourceID)
    {

        PostFXSettings.BloomSettings bloom = settings.Bloom;

        int width, height;

        if (bloom.ignoreRenderScale)
        {
            width = camera.pixelWidth / 2;
            height = camera.pixelHeight / 2;
        }
        else
        {
            width = bufferSize.x / 2;
            height = bufferSize.y / 2;
        }
        

        //bloom关闭的情况
        if (bloom.MaxIterations == 0 || bloom.intensity <= 0f || height < bloom.downScaleLimit * 2 || width < bloom.downScaleLimit * 2)
        {
            return false;
        }

        buffer.BeginSample("Bloom");
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdID, threshold);
        
        
        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT(bloomPrefilterID, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceID, bloomPrefilterID, bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);

        int fromID = bloomPrefilterID, toID = bloomPyramidID + 1;

        int i;
        for (i = 0; i < bloom.MaxIterations; i++)
        {
            if (height < bloom.downScaleLimit || width < bloom.downScaleLimit)
            {
                break;
            }

            int midId = toID - 1;
            buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            buffer.GetTemporaryRT(toID, width, height,0,  FilterMode.Bilinear, format);
            Draw(fromID, midId, Pass.BloomHorizontal);
            Draw(midId, toID, Pass.BloomVertical);
            fromID = toID;
            toID += 2;
            width /= 2;
            height /= 2;
        }
        
        buffer.SetGlobalFloat(bloomBicubicUpsamlingID, bloom.bicubicUpsampling ? 1f : 0f);
        buffer.SetGlobalFloat(bloomIntensityID, 1f);

        Pass combinePass, finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = Pass.BloomCombineAdd;
            buffer.SetGlobalFloat(bloomIntensityID, 1f);
            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BloomCombineScatter;
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityID, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }
        
        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromID - 1);
            toID -= 5;

            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2ID, toID + 1);
                Draw(fromID, toID, combinePass);
                buffer.ReleaseTemporaryRT(fromID);
                buffer.ReleaseTemporaryRT(toID + 1);
                fromID = toID;
                toID -= 2;
            }
        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidID);
        }

        buffer.SetGlobalFloat(bloomIntensityID, finalIntensity);
        buffer.SetGlobalTexture(fxSource2ID, sourceID);
        buffer.GetTemporaryRT(bloomResultID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
        Draw(fromID, bloomResultID, finalPass);
        
        buffer.ReleaseTemporaryRT(fromID);
        buffer.ReleaseTemporaryRT(bloomPrefilterID);
        
        buffer.EndSample("Bloom");
        return true;
    }
    
    //---ColorGrading-------------
    private int colorAdjustmentsID = Shader.PropertyToID("_ColorAdjustments");
    private int colorFilterID = Shader.PropertyToID("_ColorFilter");
    
    private int whiteBalanceID = Shader.PropertyToID("_WhiteBalance");
    
    private int splitToningShadowsID = Shader.PropertyToID("_SplitToningShadows");
    private int splitToningHighLightsID = Shader.PropertyToID("_SplitToningHighLights");
    
    private int channelMixerRedID = Shader.PropertyToID("_ChannelMixerRed");
    private int channelMixerGreenID = Shader.PropertyToID("_ChannelMixerGreen");
    private int channelMixerBlueID = Shader.PropertyToID("_ChannelMixerBlue");

    private int smhShadowsID = Shader.PropertyToID("_SmhShadows");
    private int smhMidtonesID = Shader.PropertyToID("_SmhMidtones");
    private int smhHighlightsID = Shader.PropertyToID("_SmhHighlights");
    private int smhRangeID = Shader.PropertyToID("_SmhRange");

    private int colorGradingLUTID = Shader.PropertyToID("_ColorGradingLUT");
    private int colorGradingLUTParamsID = Shader.PropertyToID("_ColorGradingLUTParams");
    private int colorGradingLUTInLogCID = Shader.PropertyToID("_ColorGradingLUTInLogC");
    void ConfigureColorAdjustments()
    {
        PostFXSettings.ColorAdjustmentsSettings colorAdjust = settings.ColorAdjustments;
        buffer.SetGlobalVector(colorAdjustmentsID, new Vector4(
                Mathf.Pow(2f, colorAdjust.postExposure),
                colorAdjust.contrast * 0.01f + 1f,
                colorAdjust.hueShift * (1f / 360f),
                colorAdjust.saturation * 0.01f + 1f
            ));
        buffer.SetGlobalColor(colorFilterID, colorAdjust.colorFilter.linear);
    }

    void ConfigureWhiteBalance()
    {
        PostFXSettings.WhiteBalanceSettings whiteBlance = settings.WhiteBlance;
        buffer.SetGlobalVector(whiteBalanceID, ColorUtils.ColorBalanceToLMSCoeffs(
                whiteBlance.temperature, whiteBlance.tint
            )
        );
    }

    void ConfigureSplitToning()
    {
        PostFXSettings.SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowsID, splitColor);
        buffer.SetGlobalColor(splitToningHighLightsID, splitToning.highLights);
    }

    void ConfigureChannelMixer()
    {
        PostFXSettings.ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedID, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenID, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueID, channelMixer.blue);
    }

    void ConfigureShadowMidtonesHighlights()
    {
        PostFXSettings.ShadowMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
        buffer.SetGlobalColor(smhShadowsID, smh.shadows.linear);
        buffer.SetGlobalColor(smhMidtonesID, smh.midtones.linear);
        buffer.SetGlobalColor(smhHighlightsID, smh.highlights.linear);
        buffer.SetGlobalVector(smhRangeID, new Vector4(
                smh.shadowsStart, smh.shadowEnd, smh.highlightsStart, smh.highlightsEnd
            )
        );
    }

    void DoFinal(int sourceID)
    {
        ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowMidtonesHighlights();

        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        buffer.GetTemporaryRT(
            colorGradingLUTID, lutWidth, lutHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR
        );
        buffer.SetGlobalVector(colorGradingLUTParamsID, new Vector4(
                lutHeight, 0.5f / lutWidth, 0.5f/lutHeight, lutHeight/(lutHeight - 1f)
            )
        );
        
        DoToneMapping(sourceID);
    }
    
    //---tonemapping----
    private int colorLUTResolution;
    
    void DoToneMapping(int sourceID)
    {
        PostFXSettings.ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass =  Pass.ToneMappingNone + (int)mode;
        
        buffer.SetGlobalFloat(
            colorGradingLUTInLogCID, useHDR && pass != Pass.ToneMappingNone ? 1f : 0f
        );
        
        buffer.BeginSample("ColorGradingLUT");
        //BakeColorLUT
        Draw(sourceID, colorGradingLUTID, pass);
        buffer.EndSample("ColorGradingLUT");
        
        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        buffer.SetGlobalVector(colorGradingLUTParamsID, new Vector4(
                1f / lutWidth, 1f / lutHeight, lutHeight - 1f
            )
        );
        
        buffer.SetGlobalFloat(finalSrcBlendID, 1f);
        buffer.SetGlobalFloat(finalDstBlendID, 0f);
        //fxaa
        if (fxaa.enabled)
        {
            ConfigureFXAA();
            buffer.GetTemporaryRT(colorGradingResultID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            Draw(sourceID, colorGradingResultID, keepAlpha ?  Pass.ApplyColorGrading : Pass.ApplyColorGradingWithLuma);
        }

        buffer.BeginSample("ToneMapping");
        
        if (bufferSize.x == camera.pixelWidth)
        {
            if (fxaa.enabled)
            {
                DrawFinal(colorGradingResultID, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                buffer.ReleaseTemporaryRT(colorGradingResultID);
            }
            else
            {
                DrawFinal(sourceID, Pass.ApplyColorGrading);
            }
        }
        else
        {
            buffer.GetTemporaryRT(finalResultID, bufferSize.x, bufferSize.y,
                0,FilterMode.Bilinear, RenderTextureFormat.Default);
            
            if (fxaa.enabled)
            {
                Draw(colorGradingResultID, finalResultID, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                buffer.ReleaseTemporaryRT(colorGradingResultID);
            }
            else
            {
                Draw(sourceID, finalResultID, Pass.ApplyColorGrading);
            }


            bool bicubicSample = bicubicRescalingMode == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                                 bicubicRescalingMode == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                                 bufferSize.x < camera.pixelWidth;
            
            buffer.SetGlobalFloat(copyBicubicID, bicubicSample ? 1f : 0f);
            DrawFinal(finalResultID, Pass.FinalRescale);
            buffer.ReleaseTemporaryRT(finalResultID);
        }

        buffer.EndSample("ToneMapping");
        
        buffer.ReleaseTemporaryRT(colorGradingLUTID);
    }
    
    //---FXAA----------
    private int fxaaConfigID = Shader.PropertyToID("_FXAAConfig");

    private const string fxaaQualityLowKeyword = "FXAA_QUALITY_LOW";
    private const string fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";

    void ConfigureFXAA()
    {
        if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Low)
        {
            buffer.EnableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        else if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium)
        {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.EnableShaderKeyword(fxaaQualityMediumKeyword);
        }
        else
        {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        buffer.SetGlobalVector(fxaaConfigID, new Vector4(fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending));
    }

    public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, PostFXSettings settings, bool keepAlpha, bool useHDR, int colorLUTResolution, CameraSettings.FinalBlendMode finalBlendMode, CameraBufferSettings.BicubicRescalingMode bicubicRescalingMode, CameraBufferSettings.FXAA fxaa)
    {
        this.fxaa = fxaa;
        this.bufferSize = bufferSize;
        this.keepAlpha = keepAlpha;
        this.useHDR = useHDR;
        this.context = context;
        this.camera = camera;
        this.colorLUTResolution = colorLUTResolution;
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        this.finalBlendMode = finalBlendMode;
        this.bicubicRescalingMode = bicubicRescalingMode;
        ApplySceneViewState();
    }

    public void Render(int sourceID)
    {
        if (DoBloom(sourceID))
        {
            DoFinal(bloomResultID);
            buffer.ReleaseTemporaryRT(bloomResultID);
        }
        else
        {
            DoFinal(sourceID);
        }
        
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceID, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Mat, (int)pass, MeshTopology.Triangles, 3);
    }

    void DrawFinal(RenderTargetIdentifier from, Pass pass)
    {
        
        buffer.SetGlobalFloat(finalSrcBlendID, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(finalDstBlendID, (float)finalBlendMode.destination);
        
        buffer.SetGlobalTexture(fxSourceID, from);
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, 
            finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load
            , RenderBufferStoreAction.Store);
        
        buffer.SetViewport(camera.pixelRect);
        
        buffer.DrawProcedural(Matrix4x4.identity, settings.Mat, (int)pass, MeshTopology.Triangles, 3);
    }
}

partial class PostFXStack
{
    partial void ApplySceneViewState();
    
#if UNITY_EDITOR

    partial void ApplySceneViewState()
    {
        if (camera.cameraType == CameraType.SceneView &&
            !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
        {
            settings = null;
        }
    }
    
#endif
}