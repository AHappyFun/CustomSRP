using System;
using UnityEngine;


[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField]
    private Shader shader = default;

    private Material material;

    public Material Mat
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            return material;
        }
    }
    
    [Serializable]
    public struct BloomSettings
    {
        [Range(0, 16)]
        public int MaxIterations;

        [Min(1f)]
        public int downScaleLimit;
    }

    [SerializeField]
    private BloomSettings bloomSettings = default;

    public BloomSettings Bloom => bloomSettings;
}
