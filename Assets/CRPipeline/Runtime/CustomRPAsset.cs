﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 继承RenderPipelineAsset，是ScriptObject类型，相当于把管线的配置给序列化保存了
/// </summary>
[CreateAssetMenu(menuName = "Rendering/我的自定义SRP")]
public class CustomRPAsset : RenderPipelineAsset
{
    [SerializeField]
    bool UseDynamicBatching = true, UseGPUInstancing = true, UseSRPBatcher = true, UseLightsPerObject = true;

    [SerializeField]
    bool openHDR = true;

    [SerializeField]
    ShadowSetting shadowSetting = default;

    [SerializeField]
    PostFXSettings postFXSettings = default;
    

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRP(openHDR, UseDynamicBatching, UseGPUInstancing, UseSRPBatcher, UseLightsPerObject, shadowSetting, postFXSettings);
    }

    
}
