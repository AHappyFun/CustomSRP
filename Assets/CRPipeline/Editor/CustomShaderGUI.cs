using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{

    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);
        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;

        PresetGUI();
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

    bool ZWrite
    {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
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

    #region GUI

    bool showPreset;
    void PresetGUI()
    {
        EditorGUILayout.Space();
        showPreset = EditorGUILayout.Foldout(showPreset, "Presets", true);
        if (showPreset)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }
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

    /// <summary>
    /// 不透明设置
    /// </summary>
    void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
            Clipping = false;
            PreMulAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    /// <summary>
    /// AlphaTest设置
    /// </summary>
    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            PreMulAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }

    /// <summary>
    /// Fade设置 ： 不进行预乘Alpha的透明
    /// </summary>
    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            PreMulAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    /// <summary>
    /// Transparent设置：进行预乘Alpha的透明
    /// </summary>
    void TransparentPreset()
    {
        if (HasPreMulAlpha && PresetButton("Transparent"))
        {
            Clipping = false;
            PreMulAlpha = true;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }
    #endregion
}
