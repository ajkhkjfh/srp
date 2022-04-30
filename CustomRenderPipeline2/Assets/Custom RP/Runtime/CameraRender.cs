using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRender
{
    private ScriptableRenderContext _context;
    private Camera _camera;
    
    private const string _bufferName = "Render Camera";
    private CommandBuffer _commandBuffer = new CommandBuffer() {name = _bufferName};

    private CullingResults _cullingResults;

    private static ShaderTagId _unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    private static ShaderTagId _lishaderTagId = new ShaderTagId("CustomLit");

    private Lighting _lighting = new Lighting();
    

    public void Render(ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing,bool useLightsPerObject,ShadowSettings shadowSettings)
    {
        this._context = context;
        this._camera = camera;

        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
            return;
        
        _commandBuffer.BeginSample(SampleName);
        ExecuteCommandBuffer();
        _lighting.Setup(context,_cullingResults,shadowSettings,useLightsPerObject);
        _commandBuffer.EndSample(SampleName);
        ExecuteCommandBuffer();
        
        Setup();
        DrawVisibleGeometry(useDynamicBatching,useGPUInstancing,useLightsPerObject);
        DrawUnsupportedShaders();
        DrawGizmos();
        
        _lighting.Cleanup();
        
        Submit();
    }

    void Setup()
    {
        _context.SetupCameraProperties(_camera);
        
        CameraClearFlags flags = _camera.clearFlags;
        _commandBuffer.ClearRenderTarget
        (
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
                _camera.backgroundColor.linear : Color.clear
        );
        _commandBuffer.BeginSample(SampleName);
        ExecuteCommandBuffer();
    }
    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing,bool useLightsPerObject)
    {
        PerObjectData lightsPerObjectFlags =
            useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        
        var sortingSettings = new SortingSettings(_camera) {criteria = SortingCriteria.CommonOpaque};
        var drawSettings = new DrawingSettings(_unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.ReflectionProbes|PerObjectData.Lightmaps|
                            PerObjectData.ShadowMask|PerObjectData.LightProbe|
                            PerObjectData.OcclusionProbe| PerObjectData.LightProbeProxyVolume|
                            PerObjectData.OcclusionProbeProxyVolume|lightsPerObjectFlags
        };
        drawSettings.SetShaderPassName(1, _lishaderTagId);

        var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
        _context.DrawRenderers(_cullingResults,ref drawSettings,ref filterSettings);

        _context.DrawSkybox(_camera);
        
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawSettings.sortingSettings = sortingSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        _context.DrawRenderers(_cullingResults,ref drawSettings,ref filterSettings);
    }

    void Submit()
    {
        _commandBuffer.EndSample(SampleName);
        ExecuteCommandBuffer();
        _context.Submit();
    }

    void ExecuteCommandBuffer()
    {
        //copy the commands from the commandbuffer to the context and then clear the buffer
        _context.ExecuteCommandBuffer(_commandBuffer);
        _commandBuffer.Clear();
    }

    bool Cull(float maxShaderDistance)
    {
        if (_camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShaderDistance,_camera.farClipPlane);
            _cullingResults = _context.Cull(ref p);
            return true;
        }
        return false;
    }

    
}
