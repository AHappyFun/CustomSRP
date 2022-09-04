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

    public bool IsActive => settings != null;

    enum Pass
    {
        BloomHorizontal,
        BloomVertical,
        BloomCombine,
        BloomPrefilter,
        Copy
    }


    private int bloomBicubicUpsamlingID = Shader.PropertyToID("_BloomBicubicUpsampling");
    private int bloomPrefilterID = Shader.PropertyToID("_BloomPrefilter");
    private int bloomThresholdID = Shader.PropertyToID("_BloomThreshold");
    private int bloomIntensity = Shader.PropertyToID("_BloomIntensity");
    private int fxSourceID = Shader.PropertyToID("_PostFXSource");
    private int fxSource2ID = Shader.PropertyToID("_PostFXSource2"); 

    public PostFXStack()
    {
        bloomPyramidID = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }
    
    //---bloom---------
    private const int maxBloomPyramidLevels = 16;
    private int bloomPyramidID;
    void DoBloom(int sourceID)
    {
        buffer.BeginSample("Bloom");

        PostFXSettings.BloomSettings bloom = settings.Bloom;

        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;

        if (bloom.MaxIterations == 0 || bloom.intensity <= 0f || height < bloom.downScaleLimit * 2 || width < bloom.downScaleLimit * 2)
        {
            Draw(sourceID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            buffer.EndSample("Bloom");
            return;
        }

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdID, threshold);
        
        
        RenderTextureFormat format = RenderTextureFormat.Default;
        buffer.GetTemporaryRT(bloomPrefilterID, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceID, bloomPrefilterID, Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        
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
        buffer.SetGlobalFloat(bloomIntensity, 1f);
        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromID - 1);
            toID -= 5;

            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2ID, toID + 1);
                Draw(fromID, toID, Pass.BloomCombine);
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

        buffer.SetGlobalFloat(bloomIntensity, bloom.intensity);
        buffer.SetGlobalTexture(fxSource2ID, sourceID);
        Draw(fromID, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);
        buffer.ReleaseTemporaryRT(fromID);
        buffer.ReleaseTemporaryRT(bloomPrefilterID);
        
        buffer.EndSample("Bloom");
    }

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
    {
        this.context = context;
        this.camera = camera;
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        ApplySceneViewState();
    }

    public void Render(int sourceID)
    {
        DoBloom(sourceID);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceID, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
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