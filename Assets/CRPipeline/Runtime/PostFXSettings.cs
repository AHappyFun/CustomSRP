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
    

    [SerializeField]
    private BloomSettings bloomSettings = new BloomSettings
    {
        scatter = 0.7f
    };
    public BloomSettings Bloom => bloomSettings;

    [SerializeField]
    private ToneMappingSettings toneMapping = default;

    public ToneMappingSettings ToneMapping => toneMapping;
    
    [Serializable]
    public struct BloomSettings
    {
        [Range(0, 16)]
        public int MaxIterations;

        [Min(1f)]
        public int downScaleLimit;

        public bool bicubicUpsampling;

        [Range(0f, 1f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;

        [Min(0f)]
        public float intensity;

        /// <summary>
        /// fade闪烁
        /// </summary>
        public bool fadeFireflies;

        public enum Mode
        {   
            Additive, Scattering
        }

        public Mode mode;

        [Range(0.05f, 0.95f)]
        public float scatter;
    }
    
    [Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None = -1,
            Neutral = 0,
            Reinhard = 1,
            ACES = 2 
        }

        public Mode mode;
    }
}
