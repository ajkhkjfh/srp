using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI:ShaderGUI
{
    
    private MaterialEditor _materialEditor;
    private Object[] _materials;
    private MaterialProperty[] _properties;

    private bool _showPreset;
    
    enum ShadowMode {
        On, Clip, Dither, Off
    }

    ShadowMode Shadows {
        set {
            if (SetProperty("_Shadows", (float)value)) {
                SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
            }
        }
    }
    
    void SetShadowCasterPass () {
        MaterialProperty shadows = FindProperty("_Shadows", _properties, false);
        if (shadows == null || shadows.hasMixedValue) {
            return;
        }
        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        foreach (Material m in _materials) {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }
    
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();

        base.OnGUI(materialEditor, properties);
        this._materialEditor = materialEditor;
        this._materials = materialEditor.targets;
        this._properties = properties;
        
        EditorGUILayout.Space();
        _showPreset = EditorGUILayout.Foldout(_showPreset, "Presets", true);
        if (_showPreset)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }
        if (EditorGUI.EndChangeCheck()) {
            SetShadowCasterPass();
            CopyLightMappingProperties();
        }

        BakeEmission();
    }

    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", _properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", _properties, false);
        if (mainTex != null && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }

        MaterialProperty color = FindProperty("_Color", _properties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", _properties, false);
        if (color != null && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }
    
    void BakeEmission()
    {
        EditorGUI.BeginChangeCheck();
        _materialEditor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in _materialEditor.targets)
            {
                m.globalIlluminationFlags &=
                    ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }
    bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, _properties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }
        return false;
    }

    void SetKeyword(string keyword, bool enable)
    {
        if (enable)
        {
            foreach (Material m in _materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in _materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0f)) {
            SetKeyword(keyword, value);
        }
    }

    private bool Clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }

    private bool PremultiplyAlpha
    {
        set => SetProperty("_PremultiplyAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    private BlendMode SrcBlend
    {
        set => SetProperty("_SrcBlend", (float)value);
    }

    private BlendMode DstBlend
    {
        set => SetProperty("_DstBlend", (float) value);
    }

    private bool ZWrite
    {
        set => SetProperty("_ZWrite", value ? 1.0f : 0.0f);
    }

    bool HasProperty(string name) => FindProperty(name, _properties, false) != null;

    private bool HasPremultiplyAlpha => HasProperty("_PremultiplyAlpha");

    RenderQueue RenderQueue {
        set {
            foreach (Material m in _materials) {
                m.renderQueue = (int)value;
            }
        }
    }

    bool PressButton(string name)
    {
        if (GUILayout.Button(name))
        {
            _materialEditor.RegisterPropertyChangeUndo(name);
            return true;
        }

        return false;
    }

    void OpaquePreset()
    {
        if (PressButton("Opaque"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    void ClipPreset()
    {
        if(PressButton("Clip"))
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }

    void FadePreset()
    {
        if (PressButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    void TransparentPreset()
    {
        if (HasPremultiplyAlpha && PressButton("Transparent"))
        {
            Clipping = false;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }
}
