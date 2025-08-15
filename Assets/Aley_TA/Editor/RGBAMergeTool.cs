
using UnityEngine;
using UnityEditor;
using System.IO;

public class TextureChannelCombiner : EditorWindow
{
    private Texture2D[] sourceTextures = new Texture2D[4];
    private int[] channelSelections = new int[4];
    private bool includeAlpha = true;
    private string savePath = "Assets/CombinedTexture.tga";

    private static readonly string[] channels = { "R", "G", "B", "A" };

    [MenuItem("美术Tools/贴图通道合并工具")]
    static void OpenWindow()
    {
        GetWindow<TextureChannelCombiner>("通道合并工具");
    }

    void OnGUI()
    {
        GUILayout.Label("选择来源图与通道", EditorStyles.boldLabel);

        string[] channelNames = { "R 通道", "G 通道", "B 通道", "A 通道" };

        for (int i = 0; i < 4; i++)
        {
            GUILayout.BeginHorizontal();
            sourceTextures[i] = (Texture2D)EditorGUILayout.ObjectField(channelNames[i], sourceTextures[i], typeof(Texture2D), false);
            channelSelections[i] = EditorGUILayout.Popup(channelSelections[i], channels, GUILayout.Width(50));
            GUILayout.EndHorizontal();
        }

        includeAlpha = EditorGUILayout.Toggle("包含 Alpha 通道", includeAlpha);

        GUILayout.Space(10);
        GUILayout.Label("保存路径", EditorStyles.boldLabel);
        savePath = EditorGUILayout.TextField("保存为 TGA", savePath);

        if (GUILayout.Button("合并并导出"))
        {
            CombineChannelsToTexture();
        }
    }

    void CombineChannelsToTexture()
    {
        int width = 0;
        int height = 0;

        // 获取第一张非空图片的尺寸
        foreach (var tex in sourceTextures)
        {
            if (tex != null)
            {
                width = tex.width;
                height = tex.height;
                break;
            }
        }

        if (width == 0 || height == 0)
        {
            Debug.LogError("请至少指定一张有效图片。");
            return;
        }

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false, false);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float[] channelValues = new float[4];

            for (int i = 0; i < 4; i++)
            {
                if (sourceTextures[i] != null)
                {
                    Color c = sourceTextures[i].GetPixel(x, y);
                    channelValues[i] = GetChannelValue(c, channelSelections[i]);
                }
                else
                {
                    // 默认值：RGB=0, A=1
                    channelValues[i] = (i == 3) ? 1f : 0f;
                }
            }

            Color newColor = new Color(channelValues[0], channelValues[1], channelValues[2], includeAlpha ? channelValues[3] : 1f);
            result.SetPixel(x, y, newColor);
        }

        result.Apply();

        byte[] tgaBytes = ImageConversion.EncodeToTGA(result);
        File.WriteAllBytes(savePath, tgaBytes);
        AssetDatabase.Refresh();

        Debug.Log("合并完成，保存路径：" + savePath);
    }

    float GetChannelValue(Color c, int channelIndex)
    {
        switch (channelIndex)
        {
            case 0: return c.r;
            case 1: return c.g;
            case 2: return c.b;
            case 3: return c.a;
            default: return 0f;
        }
    }
}
