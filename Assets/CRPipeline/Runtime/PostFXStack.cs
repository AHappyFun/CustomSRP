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
        Copy
    }

    private int fxSourceID = Shader.PropertyToID("_PostFXSource");

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
        RenderTextureFormat format = RenderTextureFormat.Default;
        int fromID = sourceID, toID = bloomPyramidID + 1;

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
        
        Draw(fromID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);

        for (i -= 1; i >= 0; i--)
        {
            buffer.ReleaseTemporaryRT(fromID);
            buffer.ReleaseTemporaryRT(fromID - 1);
            fromID -= 2;
        }
        
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