using System;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{
    [SerializeField]
    private CameraSettings settings = default;

    private ProfilingSampler sampler;

    public ProfilingSampler Sampler => sampler ??= new ProfilingSampler(GetComponent<Camera>().name);

    public CameraSettings Settings => settings ?? (settings = new CameraSettings());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnEnable()
    {
        sampler = null;
    }
#endif
}
