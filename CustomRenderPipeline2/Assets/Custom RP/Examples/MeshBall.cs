
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class MeshBall : MonoBehaviour
{
    private static int _baseColorId = Shader.PropertyToID("_BaseColor"),
        _cutoffId = Shader.PropertyToID("_Cutoff"),
        _metallicId = Shader.PropertyToID("_Metallic"),
        _smoothnessId = Shader.PropertyToID("_Smoothness");
        
    
    [SerializeField] private Mesh mesh = default;

    [SerializeField] private Material material = default;
    
    [SerializeField, Range(0f, 1f)] float cutoff = 0.5f;

    [SerializeField] private LightProbeProxyVolume lightProbeProxyVolume = null;

    private Matrix4x4[] _matrices = new Matrix4x4[1000];
    private Vector4[] _baseColors = new Vector4[1000];

    private float[] _metallic = new float[1000], _smoothness = new float[1000];

    private MaterialPropertyBlock _block;
    
    private void Awake()
    {
        for (int i = 0; i < _matrices.Length; i++)
        {
            _matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere * 10f, 
                Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f),
                Vector3.one * Random.Range(0.5f, 1.5f));
            _baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f));
            _metallic[i] = Random.value < 0.25f ? 1.0f : 0.0f;
            _smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    private void Update()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
            _block.SetFloat(_cutoffId, cutoff);
            _block.SetVectorArray(_baseColorId,_baseColors);
            _block.SetFloatArray(_metallicId,_metallic);
            _block.SetFloatArray(_smoothnessId,_smoothness);

           
            if (!lightProbeProxyVolume)
            {
                var positions = new Vector3[1000];
                for (int i = 0; i < _matrices.Length; i++)
                {
                    positions[i] = _matrices[i].GetColumn(3);
                }

                var lightPropes = new SphericalHarmonicsL2[1000];
                var occlusionProbes = new Vector4[1000];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
                    positions, lightPropes, occlusionProbes);
                _block.CopySHCoefficientArraysFrom(lightPropes);
                _block.CopyProbeOcclusionArrayFrom(occlusionProbes);
            }
        }
        Graphics.DrawMeshInstanced(mesh,0,material,_matrices,1000,_block,
            ShadowCastingMode.On,true,0,null,lightProbeProxyVolume?
                LightProbeUsage.UseProxyVolume:LightProbeUsage.CustomProvided,
            lightProbeProxyVolume);
    }
}
