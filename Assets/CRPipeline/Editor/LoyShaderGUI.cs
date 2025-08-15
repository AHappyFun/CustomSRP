using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Loy材质 ShaderGUI
/// </summary>
public class LoyShaderGUI : ShaderGUI
{
    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;

    private bool showPreset = true;
    private bool showProp= true;
    private bool showEmissionGI= true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();

        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;
        if (HasProperty("_Mode"))
        {
            showPreset = EditorGUILayout.Foldout(showPreset, "==========材质基础信息=========");
            if(showPreset)
                PresetGUI();
        }


        //base.OnGUI(materialEditor, properties);
        showProp = EditorGUILayout.Foldout(showProp, "==========材质属性=========");
        if(showProp)
            OnPropGUI(materialEditor, properties);

        EditorGUILayout.Space(20);
        showEmissionGI = EditorGUILayout.Foldout(showEmissionGI, "==========自发光Bake设置=========");
        if (showEmissionGI)
        {
            BakeEmission();
        }

        if (EditorGUI.EndChangeCheck())
        {
            SetSurfaceMode();
            SetShadowMode();
            SetZWrite();
            //SetOutlinePass();
            CopyLightMappingProperties();
        }
    }

    public void OnPropGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        materialEditor.SetDefaultGUIWidths();

        for (int index = 0; index < properties.Length; ++index)
        {
            if (properties[index].name.Contains("Lable"))
            {
                EditorGUILayout.Space(20);
                EditorStyles.label.alignment = TextAnchor.MiddleCenter;
                EditorStyles.label.fontStyle = FontStyle.Bold;
                EditorStyles.label.fontSize = 16;
                EditorGUILayout.LabelField("——————————————————————"+ properties[index].displayName + "——————————————————————");
                EditorStyles.label.alignment = TextAnchor.MiddleLeft;
                EditorStyles.label.fontStyle = FontStyle.Normal;
                EditorStyles.label.fontSize = 12;
            }
            else
            {
                if ((properties[index].flags & MaterialProperty.PropFlags.HideInInspector) == 0)
                    materialEditor.ShaderProperty(EditorGUILayout.GetControlRect(true, materialEditor.GetPropertyHeight(properties[index], properties[index].displayName), EditorStyles.layerMaskField), properties[index], properties[index].displayName);
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space(20);
        EditorStyles.label.alignment = TextAnchor.MiddleCenter;
        EditorStyles.label.fontStyle = FontStyle.Bold;
        EditorStyles.label.fontSize = 16;
        EditorGUILayout.LabelField("——————————————————————-----RenderQueueAndOther-----——————————————————————");
        EditorStyles.label.alignment = TextAnchor.MiddleLeft;
        EditorStyles.label.fontStyle = FontStyle.Normal;
        EditorStyles.label.fontSize = 12;
        if (SupportedRenderingFeatures.active.editableMaterialRenderQueue)
            materialEditor.RenderQueueField();
        materialEditor.EnableInstancingField();
        materialEditor.DoubleSidedGIField();
    }

    /// <summary>
    /// 烘焙自发光
    /// </summary>
    void BakeEmission()
    {
        EditorGUI.BeginChangeCheck();
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in editor.targets)
            {
                m.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    #region SetProperty和Keyword方法
    bool HasProperty(string name)
    {
        return FindProperty(name, properties, false) != null;
    }

    bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, properties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }
        return false;
    }

    int GetPropertyInt(string name)
    {
        MaterialProperty property = FindProperty(name, properties, false);
        if (property != null)
        {
            return (int)property.floatValue;
        }
        return -1;
    }

    void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0f))
        {
            SetKeyword(keyword, value);
        }
    }

    #endregion

    #region 属性和Keyword
    bool Clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }

    bool HasPreMulAlpha => HasProperty("_PremulAlpha");

    bool PreMulAlpha
    {
        set => SetProperty("_PremulAlpha", "_PREMULTIPY_ALPHA", value);
    }

    BlendMode SrcBlend
    {
        set => SetProperty("_SrcBlend", (float)value);
    }

    BlendMode DstBlend
    {
        set => SetProperty("_DstBlend", (float)value);
    }

    private BlendMode SrcBlendAlpha
    {
        set => SetProperty("_SrcBlendAlpha", (float)value);
    }

    private BlendMode DstBlendAlpha
    {
        set => SetProperty("_DstBlendAlpha", (float)value);
    }

    bool ZWrite
    {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }

    bool Transparent
    {
        set => SetProperty("_Transparent", value ? 1f : 0f);
    }

    int Shadows
    {
        set => SetProperty("_Shadows", (int)value);
    }

    int Surface
    {
        set => SetProperty("_Mode", (int)value);
    }

    bool bDrawOutline
    {
        set => SetProperty("_DrawOutline", value ? 1f : 0f);
    }

    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }
    #endregion


    void PresetGUI()
    {
        EditorGUILayout.Space();

        SurfaceModeGUI();
        ZWriteGUI();
        ShadowGUI();
        //OutlineGUI();

        EditorGUILayout.Space(20);
    }

    /// <summary>
    /// 注册撤销
    /// </summary>
    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            editor.RegisterPropertyChangeUndo(name);
            return true;
        }
        return false;
    }



    private int SurfaceMode = 0;
    bool surfaceHasChanged = false;
    void SurfaceModeGUI()
    {
        int surface = GetPropertyInt("_Mode");
        int currentFace = surface;
        SurfaceMode = GUILayout.Toolbar(surface, new []{"不透明", "Clip", "透明预乘", "透明", "透明Additive"});
        surfaceHasChanged = currentFace != SurfaceMode;
    }

    private int ZWriteMode = 0;
    bool zWriteHasChanged = false;
    void ZWriteGUI()
    {
        int zwrite = GetPropertyInt("_ZWrite");
        int currentzZwrite= zwrite;
        ZWriteMode = GUILayout.Toolbar(zwrite, new []{"ZWrite Off", "ZWrite On"});
        zWriteHasChanged = currentzZwrite != SurfaceMode;
    }


    private int ShadowMode = 0;
    void ShadowGUI()
    {
        int Shadow = GetPropertyInt("_Shadows");
        if(Shadow == -1)
            return;

        ShadowMode = GUILayout.Toolbar(Shadow, new[] { "Shadow On", "Shadow Clip", "Shadow Dither", "Shadow Off" });
    }

    private bool bOutline = true;
    void OutlineGUI()
    {
        int drawOutline = GetPropertyInt("_DrawOutline");
        if(drawOutline == -1)
            return;

        bOutline = GUILayout.Toolbar(drawOutline, new[] { "不画描边", "画描边" }) == 1.0f ? true : false;
    }
    void SetSurfaceMode()
    {
        //没有修改不设置
        if (!surfaceHasChanged)
        {
            return;
        }
        surfaceHasChanged = false;

        if (SurfaceMode == 0)
        {
            Clipping = false;
            PreMulAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            SrcBlendAlpha = BlendMode.One;
            DstBlendAlpha = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
            Transparent = false;
            Surface = 0;
        }
        else if (SurfaceMode == 1)
        {
            Clipping = true;
            PreMulAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            SrcBlendAlpha = BlendMode.One;
            DstBlendAlpha = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
            Transparent = false;
            Surface = 1;
        }
        else if (SurfaceMode == 2) //transparent
        {
            Clipping = false;
            PreMulAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            SrcBlendAlpha = BlendMode.One;
            DstBlendAlpha = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
            Transparent = true;
            Surface = 2;
        }
        else if(SurfaceMode == 3) //fade
        {
            Clipping = false;
            PreMulAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            SrcBlendAlpha = BlendMode.One;
            DstBlendAlpha = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
            Transparent = true;
            Surface = 3;
        }
        else //additive
        {
            Clipping = false;
            PreMulAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.One;
            SrcBlendAlpha = BlendMode.One;
            DstBlendAlpha = BlendMode.One;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
            Transparent = true;
            Surface = 4;
        }
    }

    void SetZWrite()
    {
        ZWrite = ZWriteMode == 1;
    }

    /// <summary>
    /// 设置ShadowCasterPass 开关
    /// </summary>
    void SetShadowMode()
    {
        Shadows = ShadowMode;

        bool enabled = ShadowMode != 3;
        foreach (Material material in materials)
        {
            material.SetShaderPassEnabled("ShadowCaster", enabled);
        }

        if (ShadowMode == 1)
        {
            SetKeyword("_SHADOWS_CLIP", true);
            SetKeyword("_SHADOWS_DITHER", false);
        }
        else if (ShadowMode == 2)
        {
            SetKeyword("_SHADOWS_CLIP", false);
            SetKeyword("_SHADOWS_DITHER", true);
        }
        else
        {
            SetKeyword("_SHADOWS_CLIP", false);
            SetKeyword("_SHADOWS_DITHER", false);
        }
    }

    /// <summary>
    /// 设置SOutlinePass 开关
    /// </summary>
    void SetOutlinePass()
    {
        bool enabled = bOutline;
        bDrawOutline = enabled;
        foreach (Material material in materials)
        {
            material.SetShaderPassEnabled("SRPDefaultUnlit", enabled);
        }

    }

    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseTex = FindProperty("_BaseTexture", properties, false);
        if (mainTex != null && baseTex != null) {
            mainTex.textureValue = baseTex.textureValue;
            mainTex.textureScaleAndOffset = baseTex.textureScaleAndOffset;
        }
        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);
        if (color != null && baseColor != null) {
            color.colorValue = baseColor.colorValue;
        }
    }
}
