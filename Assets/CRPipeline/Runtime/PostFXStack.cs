using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class PostFXStack
{
    public enum Pass
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
    
    public static int finalSrcBlendID = Shader.PropertyToID("_FinalSrcBlend");
    public static int finalDstBlendID = Shader.PropertyToID("_FinalDstBlend");
    
    public static int fxSourceID = Shader.PropertyToID("_PostFXSource");
    
    static readonly Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);
    
    public CameraBufferSettings BufferSettings { get; set; }
    
    public Vector2Int BufferSize { get; set; }
    
    public Camera Camera { get; set; }

    public CameraSettings.FinalBlendMode FinalBlendMode { get; set; }
    
    public PostFXSettings Settings { get; set; }

    public void Draw(CommandBuffer buffer, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, Settings.Mat, (int)pass, MeshTopology.Triangles, 3);
    }

    public void Draw(CommandBuffer buffer, RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceID, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, Settings.Mat, (int)pass, MeshTopology.Triangles, 3);
    }
    
    public void DrawFinal(CommandBuffer buffer, RenderTargetIdentifier from, Pass pass)
    {
        
        buffer.SetGlobalFloat(finalSrcBlendID, (float)FinalBlendMode.source);
        buffer.SetGlobalFloat(finalDstBlendID, (float)FinalBlendMode.destination);
        
        buffer.SetGlobalTexture(fxSourceID, from);
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, 
            FinalBlendMode.destination == BlendMode.Zero && Camera.rect == fullViewRect ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load
            , RenderBufferStoreAction.Store);
        
        buffer.SetViewport(Camera.pixelRect);
        
        buffer.DrawProcedural(Matrix4x4.identity, Settings.Mat, (int)pass, MeshTopology.Triangles, 3);
    }


}