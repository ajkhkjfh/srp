using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEditor;


public class Lighting
{
    private const string _bufferName = "Lighting";

    private CommandBuffer _commandBuffer = new CommandBuffer { name = _bufferName};

    private const int _maxDirLightCount = 4,_maxOtherLightCount = 64;
    
    private static int _dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    private static int _dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
    private static int _dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    private static int _dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    private static Vector4[] _dirLightColors = new Vector4[_maxDirLightCount];
    private static Vector4[] _dirLightDirections = new Vector4[_maxDirLightCount];
    private static Vector4[] _dirLightShadowData = new Vector4[_maxDirLightCount];

    private static int _otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        _otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        _otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        _otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
        _otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
        _otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

    private static Vector4[] _otherLightColors = new Vector4[_maxOtherLightCount],
        _otherLightPositions = new Vector4[_maxOtherLightCount],
        _otherLightDirections = new Vector4[_maxOtherLightCount],
        _otherLightSpotAngles = new Vector4[_maxOtherLightCount],
        _otherlightShadowData = new Vector4[_maxOtherLightCount];

    private static string lightsPerObjectKeyword = "_LIGHT_PER_OBJECT";

    private CullingResults _cullingResults;

    private Shadows _shadows = new Shadows();
    public void Setup(ScriptableRenderContext context,CullingResults cullingResults,
        ShadowSettings shadowSettings,bool useLightPerObject)
    {
        this._cullingResults = cullingResults;
        _commandBuffer.BeginSample(_bufferName);
        _shadows.Setup(context,cullingResults,shadowSettings);
        
        SetupLights(useLightPerObject);
        _shadows.Render();
        
        _commandBuffer.EndSample(_bufferName);
        context.ExecuteCommandBuffer(_commandBuffer);
        _commandBuffer.Clear();
    }

    void SetupLights(bool useLightPerObject)
    {
        int dirLightCount = 0, otherLightCount = 0;
        NativeArray<int> indexMap = useLightPerObject ? _cullingResults.GetLightIndexMap(Allocator.Temp) : default;
        NativeArray<VisibleLight> visibleLights = _cullingResults.visibleLights;
        int i;
        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            switch (visibleLight.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < _maxDirLightCount)
                    {
                        
                        SetupDirectionalLight(dirLightCount++,ref visibleLight);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < _maxOtherLightCount)
                    {
                        newIndex = dirLightCount;
                        SetupPointLight(otherLightCount++,ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < _maxOtherLightCount)
                    {
                        newIndex = dirLightCount;
                        SetupSpotLight(otherLightCount++,ref visibleLight);
                    }
                    break;
            }
            if (useLightPerObject)
            {
                indexMap[i] = newIndex;
            }
        }

        if (useLightPerObject)
        {
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            _cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }
        else
        {
            Shader.DisableKeyword(lightsPerObjectKeyword);   
        }

        _commandBuffer.SetGlobalInt(_dirLightCountId,dirLightCount);
        if (dirLightCount > 0)
        {
            _commandBuffer.SetGlobalVectorArray(_dirLightColorsId,_dirLightColors);
            _commandBuffer.SetGlobalVectorArray(_dirLightDirectionsId,_dirLightDirections);
            _commandBuffer.SetGlobalVectorArray(_dirLightShadowDataId,_dirLightShadowData);
        }
        _commandBuffer.SetGlobalInt(_otherLightCountId,otherLightCount);
        if (otherLightCount > 0)
        {
            _commandBuffer.SetGlobalVectorArray(_otherLightColorsId,_otherLightColors);
            _commandBuffer.SetGlobalVectorArray(_otherLightPositionsId,_otherLightPositions);
            _commandBuffer.SetGlobalVectorArray(_otherLightDirectionsId,_otherLightDirections);
            _commandBuffer.SetGlobalVectorArray(_otherLightSpotAnglesId, _otherLightSpotAngles);
            _commandBuffer.SetGlobalVectorArray(_otherLightShadowDataId,_otherlightShadowData);
        }
        
        
    }
    
    void SetupDirectionalLight(int index,ref VisibleLight visibleLight)
    {
        _dirLightColors[index] = visibleLight.finalColor;
        _dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        _dirLightShadowData[index] = _shadows.ReserveDirectionalShadows(visibleLight.light,index);
    }


    void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
        _otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f/Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        _otherLightPositions[index] = position;
        _otherLightSpotAngles[index] = new Vector4(0f, 1f);
        Light light = visibleLight.light;
        _otherlightShadowData[index] = _shadows.ReserveOtherShadows(light,index);
    }

    void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
        _otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f/Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        _otherLightPositions[index] = position;
        _otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

        Light light = visibleLight.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        _otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        _otherlightShadowData[index] = _shadows.ReserveOtherShadows(light,index);
    }
    
    public void Cleanup()
    {
        _shadows.Cleanup();
    }

}
