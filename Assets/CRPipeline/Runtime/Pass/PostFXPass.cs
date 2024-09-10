
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class PostFXPass
{
        
    private static readonly ProfilingSampler groupSampler = new ProfilingSampler("Post FX");
    private static readonly ProfilingSampler finalSampler = new ProfilingSampler("Final Post FX");
    
    public static int finalSrcBlendID = Shader.PropertyToID("_FinalSrcBlend");
    public static int finalDstBlendID = Shader.PropertyToID("_FinalDstBlend");
    
    private static readonly int copyBicubicID = Shader.PropertyToID("_CopyBicubic");
    private static readonly int fxaaConfigID = Shader.PropertyToID("_FXAAConfig");

    private static readonly GlobalKeyword fxaaQualityLowKeyword = GlobalKeyword.Create("FXAA_QUALITY_LOW");
    private static readonly GlobalKeyword fxaaQualityMediumKeyword = GlobalKeyword.Create("FXAA_QUALITY_MEDIUM");

    private static readonly GraphicsFormat colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
    
    private PostFXStack postFXStack;

    private bool keepAlpha;
    
    enum ScaleMode {None, Linear, Bicubic}

    private ScaleMode scaleMode;

    private TextureHandle colorSource, colorGradingResult, scaledResult;

    void ConfigureFXAA(CommandBuffer buffer, CameraBufferSettings.FXAA fxaa)
    {
        if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Low)
        {
            buffer.SetKeyword(fxaaQualityLowKeyword, true);
            buffer.SetKeyword(fxaaQualityMediumKeyword, false);
        }
        else if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium)
        {
            buffer.SetKeyword(fxaaQualityLowKeyword, false);
            buffer.SetKeyword(fxaaQualityMediumKeyword, true);
        }
        else
        {
            buffer.SetKeyword(fxaaQualityLowKeyword, false);
            buffer.SetKeyword(fxaaQualityMediumKeyword, false);
        }
        buffer.SetGlobalVector(fxaaConfigID, new Vector4(fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending));
    }


    public void Render(RenderGraphContext context)
    {
        CommandBuffer buffer = context.cmd;
        buffer.SetGlobalFloat(finalSrcBlendID, 1f);
        buffer.SetGlobalFloat(finalDstBlendID, 0f);

        RenderTargetIdentifier finalSource;
        PostFXStack.Pass finalPass;
        CameraBufferSettings.FXAA fxaa = postFXStack.BufferSettings.fxaa;
        
        if (fxaa.enabled)
        {
            finalSource = colorGradingResult;
            finalPass = keepAlpha ? PostFXStack.Pass.FXAA : PostFXStack.Pass.FXAAWithLuma;
            ConfigureFXAA(buffer, fxaa);
            postFXStack.Draw(buffer, colorSource, finalSource, keepAlpha ?  PostFXStack.Pass.ApplyColorGrading : PostFXStack.Pass.ApplyColorGradingWithLuma);
        }
        else
        {
            finalSource = colorSource;
            finalPass = PostFXStack.Pass.ApplyColorGrading;
        }

        if (scaleMode == ScaleMode.None)
        {
            postFXStack.DrawFinal(buffer, finalSource, finalPass);
        }
        else
        {
            postFXStack.Draw(buffer, finalSource,scaledResult, finalPass);
            buffer.SetGlobalFloat(copyBicubicID, scaleMode == ScaleMode.Bicubic ? 1 : 0);
            postFXStack.DrawFinal(buffer, scaledResult, PostFXStack.Pass.FinalRescale);
        }
        
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public static void Record(RenderGraph renderGraph, PostFXStack postFXStack, int colorLUTResolution, bool keepAlpha, in CameraRendererTextures cameraTextures)
    {

        using var _ = new RenderGraphProfilingScope(renderGraph, groupSampler);

        TextureHandle colorSource = BloomPass.Record(renderGraph, postFXStack, cameraTextures);

        TextureHandle colorLUT = ColorLutPass.Record(renderGraph, postFXStack, colorLUTResolution);
        
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(finalSampler.name, out PostFXPass pass, finalSampler);
       
        pass.postFXStack = postFXStack;
        pass.keepAlpha = keepAlpha;
        pass.colorSource = builder.ReadTexture(colorSource);
        
        builder.ReadTexture(colorLUT);

        if (postFXStack.BufferSize.x == postFXStack.Camera.pixelWidth)
        {
            pass.scaleMode = ScaleMode.None;
        }
        else
        {
            bool bicubicSample = postFXStack.BufferSettings.bicubicRescalingMode == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                                 postFXStack.BufferSettings.bicubicRescalingMode == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                                 postFXStack. BufferSize.x < postFXStack.Camera.pixelWidth;
            pass.scaleMode = bicubicSample ? ScaleMode.Bicubic : ScaleMode.Linear;
        }

        bool applyFXAA = postFXStack.BufferSettings.fxaa.enabled;
        if (applyFXAA || pass.scaleMode != ScaleMode.None)
        {
            var desc = new TextureDesc(postFXStack.BufferSize.x, postFXStack.BufferSize.y)
            {
                colorFormat = colorFormat
            };
            if (applyFXAA)
            {
                desc.name = "Color Grading Result";
                pass.colorGradingResult = builder.CreateTransientTexture(desc);
            }

            if (pass.scaleMode != ScaleMode.None)
            {
                desc.name = "Scaled Result";
                pass.scaledResult = builder.CreateTransientTexture(desc);
            }
        }
        
        
        builder.SetRenderFunc<PostFXPass>(static(pass, context)=> pass.Render(context));
    }
}
