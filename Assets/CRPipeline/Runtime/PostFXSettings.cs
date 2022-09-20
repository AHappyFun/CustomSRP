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
    private bool IsActive;

    public bool Active => IsActive;
    

    //Bloom
    [SerializeField]
    private BloomSettings bloomSettings = new BloomSettings
    {
        scatter = 0.7f
    };
    public BloomSettings Bloom => bloomSettings;

    //ToneMapping
    [SerializeField]
    private ToneMappingSettings toneMapping = default;

    public ToneMappingSettings ToneMapping => toneMapping;

    //Color Adjustment
    [SerializeField] 
    private ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings
    {
        colorFilter = Color.white
    };

    public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;

    //White Balance
    [SerializeField]
    private WhiteBalanceSettings whiteBalance = default;

    public WhiteBalanceSettings WhiteBlance => whiteBalance;

    //SplitToning
    [SerializeField]
    private SplitToningSettings splitToning = new SplitToningSettings
    {
        shadows = Color.gray,
        highLights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;

    //ChannelMixer
    [SerializeField]
    private ChannelMixerSettings channelMixer = new ChannelMixerSettings
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    public ChannelMixerSettings ChannelMixer => channelMixer;

    //ShadowMidtonesHighlights
    [SerializeField]
    private ShadowMidtonesHighlightsSettings shadowsMidtonesHighlights = new ShadowMidtonesHighlightsSettings
    {
        shadows = Color.white,
        midtones = Color.white,
        highlights = Color.white,
        shadowEnd = 0.3f,
        highlightsStart = 0.55f,
        highlightsEnd = 1f
    };

    public ShadowMidtonesHighlightsSettings ShadowsMidtonesHighlights => shadowsMidtonesHighlights;
    
    //-----Setting Struct-------
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
            None = 0,
            Neutral = 1,
            Reinhard = 2,
            ACES = 3 
        }

        public Mode mode;
    }

    [Serializable]
    public struct ColorAdjustmentsSettings
    {
        public float postExposure;

        [Range(-100f, 100f)]
        public float contrast;

        [ColorUsage(false, true)]
        public Color colorFilter;

        [Range(-180f, 180f)]
        public float hueShift;

        [Range(-100f, 100f)]
        public float saturation;
    }

    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f)]
        public float temperature, tint;
    }

    [Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)]
        public Color shadows, highLights;

        [Range(-100f, 100f)]
        public float balance;
    }
    
    [Serializable]
    public struct ChannelMixerSettings
    {
        public Vector3 red, green, blue;
    }
    
    [Serializable]
    public struct ShadowMidtonesHighlightsSettings
    {
        [ColorUsage(false, true)]
        public Color shadows, midtones, highlights;

        [Range(0f, 2f)]
        public float shadowsStart, shadowEnd, highlightsStart, highlightsEnd;
    }
}
