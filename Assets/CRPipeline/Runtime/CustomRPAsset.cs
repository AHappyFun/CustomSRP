using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 继承RenderPipelineAsset，是ScriptObject类型，相当于把管线的配置给序列化保存了
/// </summary>
[CreateAssetMenu(menuName = "Rendering/我的自定义SRP")]
public partial class CustomRPAsset : RenderPipelineAsset
{
    [SerializeField]
    private CustomRPSettings RPSettings;
    
    protected override RenderPipeline CreatePipeline()
    {
        if ((RPSettings == null || RPSettings.cameraRenderShader == null))
        {
            RPSettings = new CustomRPSettings()
            {
            };
        }
        
        return new CustomRP(RPSettings);
    }

    
}

partial class CustomRPAsset
{
    #if UNITY_EDITOR

    private static string[] renderingLayerNames;

    static CustomRPAsset()
    {
        renderingLayerNames = new string[31];
        for (int i = 0; i < renderingLayerNames.Length; i++)
        {
            renderingLayerNames[i] = "Layer " + (i + 1);
        }
    }

    public override string[] renderingLayerMaskNames => renderingLayerNames;

#endif
}

[System.Serializable]
public class CustomRPSettings
{
    [SerializeField] 
    public bool UseSRPBatcher = true;
    
    [SerializeField]
    [Tooltip("Deprecated")]
    public bool UseLightsPerObject = true;

    [SerializeField]
    public ForwardPlusSettings ForwardPlusTileSetting;
    
    [SerializeField]
    public CameraBufferSettings cameraBufferSettings = new CameraBufferSettings
    {
        allowHDR = true,
        renderScale = 1f,
        fxaa = new CameraBufferSettings.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f
        }
    };

    [SerializeField]
    public ShadowSetting shadowSetting = default;

    [SerializeField]
    public PostFXSettings postFXSettings = default;
    
    public enum ColorLUTResolution
    {
        _16 = 16,
        _32 = 32,
        _64 = 64
    }

    [SerializeField]
    public ColorLUTResolution colorLutResolution = ColorLUTResolution._32;

    [SerializeField]
    public Shader cameraRenderShader;

    [SerializeField]
    public Shader cameraForwardPlusDebuggerShader;

}