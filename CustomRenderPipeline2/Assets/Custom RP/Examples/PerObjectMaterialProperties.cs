using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    private static int
        _baseColorId = Shader.PropertyToID("_BaseColor"),
        _cutoffId = Shader.PropertyToID("_Cutoff"),
        _metallicId = Shader.PropertyToID("_Metallic"),
        _smoothnessId = Shader.PropertyToID("_Smoothness"),
        _emissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Color baseColor = Color.white;

    [SerializeField, Range(0.0f, 1.0f)] private float alphaCutoff = 0.5f,
        metallic = 0f,
        smoothness = 0.5f;
    
    [SerializeField,ColorUsage(false,true)]Color emissionColor = Color.black; 
    
    private static MaterialPropertyBlock _block;

    private void OnValidate()
    {
        if (_block == null)
            _block = new MaterialPropertyBlock();
        _block.SetColor(_baseColorId,baseColor);
        _block.SetFloat(_cutoffId,alphaCutoff);
        _block.SetFloat(_metallicId,metallic);
        _block.SetFloat(_smoothnessId,smoothness);
        _block.SetColor(_emissionColorId,emissionColor);
        GetComponent<Renderer>().SetPropertyBlock(_block);
    }

    private void Awake()
    {
        OnValidate();
    }
}
