using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;


/// <summary>
/// ColorLUT Pass，后处理调色
/// </summary>
public class ColorLutPass
{
    private static readonly ProfilingSampler sampler = new ProfilingSampler("ColorLUT Pass");
    
    private readonly int colorAdjustmentsID = Shader.PropertyToID("_ColorAdjustments");
    private readonly int colorFilterID = Shader.PropertyToID("_ColorFilter");

    private readonly int whiteBalanceID = Shader.PropertyToID("_WhiteBalance");

    private readonly int splitToningShadowsID = Shader.PropertyToID("_SplitToningShadows");
    private readonly int splitToningHighLightsID = Shader.PropertyToID("_SplitToningHighLights");

    private readonly int channelMixerRedID = Shader.PropertyToID("_ChannelMixerRed");
    private readonly int channelMixerGreenID = Shader.PropertyToID("_ChannelMixerGreen");
    private readonly int channelMixerBlueID = Shader.PropertyToID("_ChannelMixerBlue");

    private readonly int smhShadowsID = Shader.PropertyToID("_SmhShadows");
    private readonly int smhMidtonesID = Shader.PropertyToID("_SmhMidtones");
    private readonly int smhHighlightsID = Shader.PropertyToID("_SmhHighlights");
    private readonly int smhRangeID = Shader.PropertyToID("_SmhRange");

    private readonly int colorGradingLUTID = Shader.PropertyToID("_ColorGradingLUT");
    private readonly int colorGradingLUTParamsID = Shader.PropertyToID("_ColorGradingLUTParams");
    private readonly int colorGradingLUTInLogCID = Shader.PropertyToID("_ColorGradingLUTInLogC");

    private static readonly GraphicsFormat colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);

    private PostFXStack postStack;
        
    private int colorLUTResolution;

    private TextureHandle colorLUT;

    public void Render(RenderGraphContext context)
    {
        PostFXSettings settings = postStack.Settings;
        CommandBuffer buffer = context.cmd;
        ConfigureColorAdjustments(buffer, settings);
        ConfigureWhiteBalance(buffer, settings);
        ConfigureSplitToning(buffer, settings);
        ConfigureChannelMixer(buffer, settings);
        ConfigureShadowMidtonesHighlights(buffer, settings);
        
        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        
        buffer.SetGlobalVector(colorGradingLUTParamsID, new Vector4(
                lutHeight, 0.5f / lutWidth, 0.5f/lutHeight, lutHeight/(lutHeight - 1f)
            )
        );

        PostFXSettings.ToneMappingSettings.Mode ToneMappingMode = settings.ToneMapping.mode;
        PostFXStack.Pass pass = PostFXStack.Pass.ToneMappingNone + (int)ToneMappingMode;
        buffer.SetGlobalFloat(
            colorGradingLUTInLogCID, postStack.BufferSettings.allowHDR && pass != PostFXStack.Pass.ToneMappingNone ? 1f : 0f
        );
        
        postStack.Draw(buffer, colorLUT, pass);
        
        buffer.SetGlobalVector(colorGradingLUTParamsID, new Vector4(
                1f / lutWidth, 1f / lutHeight, lutHeight - 1f
            )
        );
        
        buffer.SetGlobalTexture(colorGradingLUTID, colorLUT);
        
    }

    public static TextureHandle Record(RenderGraph renderGraph, PostFXStack stack, int colorLUTResolution)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out ColorLutPass pass, sampler);

        pass.postStack = stack;
        pass.colorLUTResolution = colorLUTResolution;
        
        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;

        var desc = new TextureDesc(lutWidth, lutHeight)
        {
            colorFormat = colorFormat,
            name = "Color LUT"
        };

        pass.colorLUT = builder.WriteTexture(renderGraph.CreateTexture(desc));
        builder.SetRenderFunc<ColorLutPass>(static(pass, context) => pass.Render(context));

        return pass.colorLUT;

    }
    
    //--------支持的方法--------
    void ConfigureColorAdjustments(CommandBuffer buffer, PostFXSettings settings)
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

    void ConfigureWhiteBalance(CommandBuffer buffer, PostFXSettings settings)
    {
        PostFXSettings.WhiteBalanceSettings whiteBlance = settings.WhiteBlance;
        buffer.SetGlobalVector(whiteBalanceID, ColorUtils.ColorBalanceToLMSCoeffs(
                whiteBlance.temperature, whiteBlance.tint
            )
        );
    }

    void ConfigureSplitToning(CommandBuffer buffer, PostFXSettings settings)
    {
        PostFXSettings.SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowsID, splitColor);
        buffer.SetGlobalColor(splitToningHighLightsID, splitToning.highLights);
    }

    void ConfigureChannelMixer(CommandBuffer buffer, PostFXSettings settings)
    {
        PostFXSettings.ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedID, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenID, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueID, channelMixer.blue);
    }

    void ConfigureShadowMidtonesHighlights(CommandBuffer buffer, PostFXSettings settings)
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
}
