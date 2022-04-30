using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    private const string _bufferName = "Shadows";

    private CommandBuffer _commandBuffer = new CommandBuffer() {name = _bufferName};

    private ScriptableRenderContext _context;
    private CullingResults _cullingResults;
    private ShadowSettings _shadowSettings;

    private const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

    private int _shadowedDirectionalLightCount;
    struct ShadowedDirectionalLight
    {
        public int _visibleLightIndex;
        public float _slopeScaleBias;
        public float _nearPlaneOffset;
    }

    private ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    private static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };
    
    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    private static int _dirShaderAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int _dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    private static int _cascadeCountId = Shader.PropertyToID("_CascadeCount");
    private static int _cascadeCullingSphereId = Shader.PropertyToID("_CascadeCullingSpheres");
    private static int _cascadeDataId = Shader.PropertyToID("_CascadeData");
    private static int _shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    private static int _shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    private static Vector4[] _cascadeData = new Vector4[maxCascades];
    private static Matrix4x4[] _dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount*maxCascades];
    private static Vector4[] _cascadeCullingSpheres = new Vector4[maxCascades];

    private static string[] shadowMaskKeywords = {"_SHADOW_MASK_ALWAYS","_SHADOW_MASK_DISTANCE"};

    private bool useShadowMask;
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings shadowSettings)
    {
        
        this._context = context;
        this._cullingResults = cullingResults;
        this._shadowSettings = shadowSettings;
        _shadowedDirectionalLightCount = 0;
        useShadowMask = false;
    }

    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (_shadowedDirectionalLightCount < maxShadowedDirectionalLightCount&&
            light.shadows!=LightShadows.None&&light.shadowStrength>0.0f)
        {
            float maskChannel = -1;
            LightBakingOutput lightBaking = light.bakingOutput;
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            if (!_cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                return new Vector4(-light.shadowStrength, 0f, 0f,maskChannel);
            }
            ShadowedDirectionalLights[_shadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    _visibleLightIndex = visibleLightIndex,
                    _slopeScaleBias = light.shadowBias,
                    _nearPlaneOffset = light.shadowNearPlane
                };
            return new Vector4(light.shadowStrength, 
                _shadowSettings.directional.cascadeCount*_shadowedDirectionalLightCount++,
                light.shadowNormalBias,maskChannel);
        }

        return new Vector4(0f, 0f, 0f, -1f);
    }
    
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows != LightShadows.None && light.shadowStrength > 0f)
        {
            LightBakingOutput lightBakingOutput = light.bakingOutput;
            if (lightBakingOutput.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBakingOutput.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                return new Vector4(
                    light.shadowStrength, 0f, 0f, lightBakingOutput.occlusionMaskChannel);
            }
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }

    public void Render()
    {
        if (_shadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            _commandBuffer.GetTemporaryRT(_dirShaderAtlasId,1,1,32,
                FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        }
        
        _commandBuffer.BeginSample(_bufferName);
        SetKeyWords(shadowMaskKeywords,useShadowMask?
            QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask?0:1:-1);
        _commandBuffer.EndSample(_bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int) _shadowSettings.directional.altasSize;
        //render texture shadowMap
        _commandBuffer.GetTemporaryRT(_dirShaderAtlasId,atlasSize,atlasSize,
            32,FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
        //identify a render texture and how its data should be loaded and stored
        _commandBuffer.SetRenderTarget(_dirShaderAtlasId,
            RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        _commandBuffer.ClearRenderTarget(true,false,Color.clear);
        _commandBuffer.BeginSample(_bufferName);
        ExecuteBuffer();


        int tiles = _shadowedDirectionalLightCount *_shadowSettings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        for (int i = 0; i < _shadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i,split,tileSize);            
        }
        
        _commandBuffer.SetGlobalInt(_cascadeCountId,_shadowSettings.directional.cascadeCount);
        _commandBuffer.SetGlobalVectorArray(_cascadeCullingSphereId,_cascadeCullingSpheres);
        
        _commandBuffer.SetGlobalMatrixArray(_dirShadowMatricesId,_dirShadowMatrices);

        _commandBuffer.SetGlobalVectorArray(_cascadeDataId,_cascadeData);
        
        float f = 1.0f - _shadowSettings.directional.cascadeFade;
        _commandBuffer.SetGlobalVector(_shadowDistanceFadeId,
            new Vector4(1f/_shadowSettings.maxDistance,1f/_shadowSettings.distanceFade
            ,1f/(1f-f*f)));
        
        SetKeyWords(directionalFilterKeywords,(int)_shadowSettings.directional.filter-1);
        SetKeyWords(directionalFilterKeywords,(int)_shadowSettings.directional.cascadeBlend-1);
        
        _commandBuffer.SetGlobalVector(_shadowAtlasSizeId,new Vector4(atlasSize,1f/atlasSize));
        
        _commandBuffer.EndSample(_bufferName);
        ExecuteBuffer();
    }


    

    void SetKeyWords(string[] keywords,int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                _commandBuffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                _commandBuffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }
    
    Vector2 SetTileViewport(int index, int split,float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        _commandBuffer.SetViewport(new Rect(offset.x*tileSize,offset.y*tileSize,tileSize,tileSize));
        return offset;
    }
    
    void RenderDirectionalShadows(int index, int split,int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowDrawingSettings = new ShadowDrawingSettings(_cullingResults, light._visibleLightIndex);

        int cascadeCount = _shadowSettings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = _shadowSettings.directional.CascadeRatios;

        float cullingFactor =
            Mathf.Max(0f, 0.8f - _shadowSettings.directional.cascadeFade);
        for (int i = 0; i < cascadeCount; i++)
        {
            _cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light._visibleLightIndex, i, cascadeCount, ratios, tileSize, light._nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);

            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowDrawingSettings.splitData = splitData;

            if (index == 0)
            {
                SetCascadeData(i,splitData.cullingSphere,tileSize);
            }
            int tileIndex = tileOffset + i;
            
            _dirShadowMatrices[tileIndex] =
                ConvertToAtlasMatrix(projMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);
        
            _commandBuffer.SetViewProjectionMatrices(viewMatrix,projMatrix);
            
            _commandBuffer.SetGlobalDepthBias(0f,light._slopeScaleBias);
            ExecuteBuffer();
            _context.DrawShadows(ref shadowDrawingSettings);
            _commandBuffer.SetGlobalDepthBias(0f,0f);
        }
    }

    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float) _shadowSettings.directional.filter + 1f);
        //联级阴影半球数据用于计算阴影的强度，衰减过度等
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        _cascadeCullingSpheres[index] = cullingSphere;
        //联级阴影数据用于解决自阴影和阴影走样等问题
        _cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize*1.4142136f);
    }
    
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer) {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }
    
    public void Cleanup()
    {
        _commandBuffer.ReleaseTemporaryRT(_dirShaderAtlasId);
        ExecuteBuffer();
    }
    
    void ExecuteBuffer()
    {
        _context.ExecuteCommandBuffer(_commandBuffer);
        _commandBuffer.Clear();
    }
}
