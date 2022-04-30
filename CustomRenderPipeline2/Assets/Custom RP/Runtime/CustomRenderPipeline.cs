using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public partial class CustomRenderPipeline : RenderPipeline
{
    private CameraRender _cameraRender = new CameraRender();

    private bool _useDynamicBatching, _useGPUInstancing, _useLightsPerObject;

    private ShadowSettings _shadowSettings;
    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing,
        bool useSRPBatcher,bool useLightsPerObject,ShadowSettings shadowSettings)
    {
        this._shadowSettings = shadowSettings;
        this._useDynamicBatching = useDynamicBatching;
        this._useGPUInstancing = useGPUInstancing;
        this._useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
        InitializeForEditor();
    }
    
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            _cameraRender.Render(context,camera,_useDynamicBatching,
                _useGPUInstancing,_useLightsPerObject,_shadowSettings);
        }
    }
}
