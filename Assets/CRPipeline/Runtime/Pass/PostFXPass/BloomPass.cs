using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;


/// <summary>
/// Bloom Pass
/// </summary>
public class BloomPass
{
    private static readonly ProfilingSampler sampler = new ProfilingSampler("Bloom Pass");
    
    private int bloomBicubicUpsamlingID = Shader.PropertyToID("_BloomBicubicUpsampling");

    private int bloomThresholdID = Shader.PropertyToID("_BloomThreshold");
    private int bloomIntensityID = Shader.PropertyToID("_BloomIntensity");
    private int bloomResultID = Shader.PropertyToID("_BloomResult");
    private int bloomPrefilterID = Shader.PropertyToID("_BloomPrefilter");
    
    public static int fxSourceID = Shader.PropertyToID("_PostFXSource");
    public static int fxSource2ID = Shader.PropertyToID("_PostFXSource2"); 
    
    
    private const int maxBloomPyramidLevels = 16;
    private int bloomPyramidID;

    private TextureHandle[] Pyramid = new TextureHandle[2 * maxBloomPyramidLevels + 1];

    private TextureHandle colorSource, bloomResult;

    private PostFXStack postStack;

    private int stepCount;

    void Render(RenderGraphContext context)
    {
        CommandBuffer buffer = context.cmd;
        PostFXSettings.BloomSettings bloomSettings = postStack.Settings.Bloom;
        
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloomSettings.threshold);
        threshold.y = threshold.x * bloomSettings.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdID, threshold);
        
        postStack.Draw(buffer, colorSource, Pyramid[0], bloomSettings.fadeFireflies ? PostFXStack.Pass.BloomPrefilterFireflies : PostFXStack.Pass.BloomPrefilter);
        
        int fromID = 0, toID = 2;

        int i;
        for (i = 0; i < stepCount; i++)
        {
            int midID = toID - 1;
            postStack.Draw(buffer, Pyramid[fromID], Pyramid[midID], PostFXStack.Pass.BloomHorizontal);
            postStack.Draw(buffer, Pyramid[midID], Pyramid[toID], PostFXStack.Pass.BloomVertical);
            fromID = toID;
            toID += 2;
        }
        
        buffer.SetGlobalFloat(bloomBicubicUpsamlingID, bloomSettings.bicubicUpsampling ? 1f : 0f);
        
        PostFXStack.Pass combinePass, finalPass;
        float finalIntensity;
        if (bloomSettings.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = PostFXStack.Pass.BloomCombineAdd;
            buffer.SetGlobalFloat(bloomIntensityID, 1f);
            finalIntensity = bloomSettings.intensity;
        }
        else
        {
            combinePass = PostFXStack.Pass.BloomCombineScatter;
            finalPass = PostFXStack.Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityID, bloomSettings.scatter);
            finalIntensity = Mathf.Min(bloomSettings.intensity, 0.95f);
        }
        
        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromID - 1);
            toID -= 5;

            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2ID, Pyramid[toID + 1]);
                postStack.Draw(buffer, Pyramid[fromID], Pyramid[toID], combinePass);
                fromID = toID;
                toID -= 2;
            }
        }
        
        buffer.SetGlobalFloat(bloomIntensityID, finalIntensity);
        buffer.SetGlobalTexture(fxSource2ID, colorSource);
        
        postStack.Draw(buffer, Pyramid[fromID], bloomResult, finalPass);
        
    }

    public static TextureHandle Record(RenderGraph renderGraph, PostFXStack stack,
        in CameraRendererTextures cameraTextures)
    {
        PostFXSettings.BloomSettings bloomSettings = stack.Settings.Bloom;
        
        int width, height;

        if (bloomSettings.ignoreRenderScale)
        {
            width = stack.Camera.pixelWidth;
            height = stack.Camera.pixelHeight;
        }
        else
        {
            width = stack.BufferSize.x;
            height = stack.BufferSize.y;
        }
        
        //bloom关闭的情况
        if (bloomSettings.MaxIterations == 0 || bloomSettings.intensity <= 0f || height < bloomSettings.downScaleLimit * 2 || width < bloomSettings.downScaleLimit * 2)
        {
            return cameraTextures.colorAttachment;
        }
        
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out BloomPass pass, sampler);

        pass.postStack = stack;
        pass.colorSource = builder.ReadTexture(cameraTextures.colorAttachment);

        var desc = new TextureDesc(width, height)
        {
            colorFormat =
                SystemInfo.GetGraphicsFormat(stack.BufferSettings.allowHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
            name = "Bloom PreFilter"
        };

        TextureHandle[] pyramid = pass.Pyramid;
        pyramid[0] = builder.CreateTransientTexture(desc);
        width /= 2;
        height /= 2;

        int pyramidIndex = 1;
        int i;
        for (i = 0; i < bloomSettings.MaxIterations; i++, pyramidIndex+=2)
        {
            if (height < bloomSettings.downScaleLimit || width < bloomSettings.downScaleLimit)
            {
                break;
            }

            desc.width = width;
            desc.height = height;
            desc.name = "Bloom Pyramid H";
            pyramid[pyramidIndex] = builder.CreateTransientTexture(desc);
            desc.name = "Bloom Pyramid V";
            pyramid[pyramidIndex + 1] = builder.CreateTransientTexture(desc);
            width /= 2;
            height /= 2;
        }

        pass.stepCount = i;

        desc.width = stack.BufferSize.x;
        desc.height = stack.BufferSize.y;
        desc.name = "Bloom Result";
        pass.bloomResult = builder.WriteTexture(renderGraph.CreateTexture(desc));
        builder.SetRenderFunc<BloomPass>(static (pass, context) => pass.Render(context));

        return pass.bloomResult;

    }

}