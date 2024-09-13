
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;


public static class CameraDebugger
{
    private const string panelName = "Forward+";
    
    private static readonly int debugOpacityID = Shader.PropertyToID("_DebugOpacity");

    private static Material material;

    private static bool showTiles;

    private static float opacity = 0.5f;

    public static bool IsActive => showTiles && opacity > 0f;

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Init(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        DebugManager.instance.GetPanel(panelName, true).children.Add(
            new DebugUI.BoolField()
            {
                displayName = "Show Tiles",
                tooltip = "是否显示Forward+ Tile",
                getter = static () => showTiles,
                setter = static value => showTiles = value
            },
            new DebugUI.FloatField()
            {
                displayName = "DebugOpacity",
                tooltip = "Debug的透明度",
                min = static ()=> 0f,
                max = static () => 1f,
                getter = static () => opacity,
                setter = static value => opacity = value
            }
        );
    }

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void CleanUp()
    {
        CoreUtils.Destroy(material);
        DebugManager.instance.RemovePanel(panelName);
    }
    
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Render(RenderGraphContext renderGraphContext)
    {
        CommandBuffer buffer = renderGraphContext.cmd;
        buffer.SetGlobalFloat(debugOpacityID, opacity);
        buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        renderGraphContext.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
