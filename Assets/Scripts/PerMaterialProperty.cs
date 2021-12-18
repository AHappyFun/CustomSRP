using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PerMaterialProperty : MonoBehaviour
{
    static int baseColorID = Shader.PropertyToID("_BaseColor");

    [SerializeField]
    Color baseColor = Color.blue;

    static MaterialPropertyBlock block;

    private void Awake()
    {
        OnValidate();
    }


    private void OnValidate()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetColor(baseColorID, baseColor);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }
}
