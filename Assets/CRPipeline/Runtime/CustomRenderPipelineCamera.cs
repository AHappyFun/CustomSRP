using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{
    [SerializeField]
    private CameraSettings settings = default;

    public CameraSettings Settings => settings ?? (settings = new CameraSettings());
}
