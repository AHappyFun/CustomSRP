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
    bool UseDynamicBatching = true, UseGPUInstancing = true, UseSRPBatcher = true, UseLightsPerObject = true;

    [SerializeField]
    private CameraBufferSettings cameraBufferSettings = new CameraBufferSettings
    {
        allowHDR = true,
        renderScale = 1f
    };

    [SerializeField]
    ShadowSetting shadowSetting = default;

    [SerializeField]
    PostFXSettings postFXSettings = default;
    
    public enum ColorLUTResolution
    {
        _16 = 16,
        _32 = 32,
        _64 = 64
    }

    [SerializeField]
    ColorLUTResolution colorLutResolution = ColorLUTResolution._32;

    [SerializeField]
    private Shader cameraRenderShader;
    

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRP(cameraBufferSettings, UseDynamicBatching, UseGPUInstancing, UseSRPBatcher, UseLightsPerObject, shadowSetting, postFXSettings, (int)colorLutResolution, cameraRenderShader);
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