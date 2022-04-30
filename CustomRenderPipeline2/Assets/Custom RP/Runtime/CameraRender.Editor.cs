using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

partial class CameraRender
{
    partial void DrawGizmos();
    partial void DrawUnsupportedShaders();

    partial void PrepareForSceneWindow();

    partial void PrepareBuffer();
    
#if UNITY_EDITOR
    private static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    private static Material _errorMaterial;

    string SampleName { get; set; }
    
    partial void DrawGizmos () {
        if (Handles.ShouldRenderGizmos()) {
            _context.DrawGizmos(_camera, GizmoSubset.PreImageEffects);
            _context.DrawGizmos(_camera, GizmoSubset.PostImageEffects);
        }
    }
    
    partial void DrawUnsupportedShaders()
    {
        if (_errorMaterial == null)
        {
            _errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        var drawSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(_camera))
        {
            overrideMaterial = _errorMaterial
        };
        
        for (int i = 0; i < legacyShaderTagIds.Length; i++)
        {
            //set the shader passes this drawcall can render
            drawSettings.SetShaderPassName(i,legacyShaderTagIds[i]);
        }
        var filterSettings = FilteringSettings.defaultValue;
        _context.DrawRenderers(_cullingResults,ref drawSettings,ref filterSettings);
    }

    partial void PrepareForSceneWindow()
    {
        if (_camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(_camera);
        }
    }

    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor only");
        _commandBuffer.name = SampleName = _camera.name;
        Profiler.EndSample();
    }
#else
    
    string SampleName => bufferName;
    
#endif
}
