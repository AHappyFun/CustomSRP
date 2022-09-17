using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRPAsset))]
public class CustomLightEditor : LightEditor
{
    private static GUIContent renderingLayerMaskLabel =
        new GUIContent("RenderingLayerMask", "Functional version of above property");
    
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        RenderingLayerMaskDrawer.Draw(settings.renderingLayerMask, renderingLayerMaskLabel);
        
        if (!settings.lightType.hasMultipleDifferentValues 
            && (LightType)settings.lightType.enumValueIndex == LightType.Spot)
        {
            settings.DrawInnerAndOuterSpotAngle();
        }
        
        settings.ApplyModifiedProperties();

        var light = target as Light;
        if (light.cullingMask != -1)
        {
            EditorGUILayout.HelpBox(
                light.type == LightType.Directional ?
                "Culling Mask Only affects shadows." :
                "Culling Mask Only affects shadoow unless Use Lights Per Objects is on.",
                MessageType.Warning
            );
        }
    }
    
}


