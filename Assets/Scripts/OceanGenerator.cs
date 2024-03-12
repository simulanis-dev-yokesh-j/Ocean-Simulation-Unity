using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[System.Serializable]
public struct CompShaders
{
    public ComputeShader InitSpectrum;   
    public ComputeShader TimeDependantSpectrum;
    public ComputeShader SpectrumWrapper;
    public ComputeShader CascadeWrapper;
    public ComputeShader FFT;
}

[System.Serializable]
public class OceanSettings
{
    public float WindSpeed = 31f;
    [Range(0, 360)] public float WindAngle = 45;
    public float Depth = 1000f;
    public float DistanceToShore = 1000f;
    [Range(0, 2)] public float WaveChopyFactor = 0.95f;
    [Range(0, 2)] public float FoamIntensity = 0.5f;
    [Range(0, 1)] public float FoamDecay = 0.1f;
}

public class OceanGenerator : MonoBehaviour
{
    public struct CascadeWrapperData
    {
        public int Kernel;
        public RenderTexture CascadeHeightsMap;
    }
    
    [Header("Shaders")]
    [SerializeField] private CompShaders _computeShaders;

    [Header("References")]
    [SerializeField] private Material _material;
    
    [Header("UI")]
    [SerializeField] private RawImage _cascade1;
    [SerializeField] private RawImage _cascade2;
    [SerializeField] private RawImage _cascade3;

    [Header("Ocean Parameters")] 
    public int SomeMagicNumber = 8;
    [SerializeField] private int _seed = 10;
    [SerializeField] private int _size = 512;
    [SerializeField] private int lengthScale1 = 256;
    [SerializeField] private int lengthScale2 = 128;
    [SerializeField] private int lengthScale3 = 64;
    [SerializeField] private OceanCascade _oceanCascade1;
    [SerializeField] private OceanCascade _oceanCascade2;
    [SerializeField] private OceanCascade _oceanCascade3;
    [SerializeField] private OceanSettings _oceanSettings;
    
    // Other var
    private float _time;
    private ComputeFFT _fft;
    private CommandBuffer _commandBuffer;
    private OceanGeometry _oceanGeometry;
    private Texture2D _gaussianNoise;
    private CascadeWrapperData _cascadeWrapperData;
    private static readonly int _CascadeHeightsMapId = Shader.PropertyToID("_CascadeHeightsMap");
    private static readonly int _HoriDisplacementMap1Id = Shader.PropertyToID("_HoriDisplacementMap1");
    private static readonly int _HoriDisplacementMap2Id = Shader.PropertyToID("_HoriDisplacementMap2");
    private static readonly int _HoriDisplacementMap3Id = Shader.PropertyToID("_HoriDisplacementMap3");
    private static readonly int _NormalMap1Id = Shader.PropertyToID("_NormalMap1");
    private static readonly int _NormalMap2Id = Shader.PropertyToID("_NormalMap2");
    private static readonly int _NormalMap3Id = Shader.PropertyToID("_NormalMap3");
    private static readonly int _FoamMap1Id = Shader.PropertyToID("_FoamMap1");
    private static readonly int _FoamMap2Id = Shader.PropertyToID("_FoamMap2");
    private static readonly int _FoamMap3Id = Shader.PropertyToID("_FoamMap3");
    private static readonly int _LengthScale1Id = Shader.PropertyToID("_LengthScale1");
    private static readonly int _LengthScale2Id = Shader.PropertyToID("_LengthScale2");
    private static readonly int _LengthScale3Id = Shader.PropertyToID("_LengthScale3");

    private void Awake()
    {
        _commandBuffer = new CommandBuffer();
        _oceanGeometry = GetComponent<OceanGeometry>();
        _fft = new ComputeFFT(_size, _commandBuffer, _computeShaders.FFT);
        _gaussianNoise = GenerateGaussianNoise(_size);

        _oceanCascade1 = new OceanCascade(_computeShaders, _commandBuffer);
        _oceanCascade3 = new OceanCascade(_computeShaders, _commandBuffer);
        _oceanCascade2 = new OceanCascade(_computeShaders, _commandBuffer);
        
        float boundary1 = 2 * Mathf.PI / lengthScale2 * SomeMagicNumber;
        float boundary2 = 2 * Mathf.PI / lengthScale3 * SomeMagicNumber;

        _oceanCascade1.Init(_size, lengthScale1, 0.0001f, boundary1, _oceanSettings, _fft, _gaussianNoise);
        _oceanCascade2.Init(_size, lengthScale2, boundary1, boundary2, _oceanSettings, _fft, _gaussianNoise);
        _oceanCascade3.Init(_size, lengthScale3, boundary2, 999999, _oceanSettings, _fft, _gaussianNoise);
        InitCascadeWrapper();

        AssignMaterialUniforms();
        _oceanGeometry.GenerateGrid(_material);
    }

    private void OnValidate()
    {
        if(Application.isPlaying == false)
            return;
        
        if(_commandBuffer == null)
            return;
        
        _fft = new ComputeFFT(_size, _commandBuffer, _computeShaders.FFT);
        
        float boundary1 = 2 * Mathf.PI / lengthScale2 * SomeMagicNumber;
        float boundary2 = 2 * Mathf.PI / lengthScale3 * SomeMagicNumber;

        _oceanCascade1.Init(_size, lengthScale1, 0.0001f, boundary1, _oceanSettings, _fft, _gaussianNoise);
        _oceanCascade2.Init(_size, lengthScale2, boundary1, boundary2, _oceanSettings, _fft, _gaussianNoise);
        _oceanCascade3.Init(_size, lengthScale3, boundary2, 999999, _oceanSettings, _fft, _gaussianNoise);
        InitCascadeWrapper();

        AssignMaterialUniforms();
    }
    
    private void InitCascadeWrapper()
    {
        _cascadeWrapperData.Kernel = _computeShaders.CascadeWrapper.FindKernel("WrapCascadeHeights");

        _cascadeWrapperData.CascadeHeightsMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _cascadeWrapperData.CascadeHeightsMap.Create();
    }
    
    private void Update()
    {
        _time += Time.deltaTime;
        
        _oceanCascade1.Update(_time);
        _oceanCascade2.Update(_time);
        _oceanCascade3.Update(_time);
        WrapCascadeHeights();
        
        // Execute the command buffer
        Graphics.ExecuteCommandBuffer(_commandBuffer);
        _commandBuffer.Clear();

        _cascade1.texture = _oceanCascade1.GetInitSpectrumData().InitSpectrum;
        _cascade2.texture = _oceanCascade2.GetInitSpectrumData().InitSpectrum;
        _cascade3.texture = _oceanCascade3.GetInitSpectrumData().InitSpectrum;
    }
    
    private void WrapCascadeHeights()
    {
        int kernel = _cascadeWrapperData.Kernel;
        ComputeShader shader = _computeShaders.CascadeWrapper;
        
        _commandBuffer.SetComputeTextureParam(shader, kernel, "CascadeHeight1", _oceanCascade1.GetTimeDependantSpectrumData().HeightMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "CascadeHeight2", _oceanCascade2.GetTimeDependantSpectrumData().HeightMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "CascadeHeight3", _oceanCascade3.GetTimeDependantSpectrumData().HeightMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "CascadeHeights", _cascadeWrapperData.CascadeHeightsMap);
        
        _commandBuffer.DispatchCompute(shader, kernel, _size / 8, _size / 8, 1);
    }
    
    private void AssignMaterialUniforms()
    {
        _material.SetTexture(_CascadeHeightsMapId, _cascadeWrapperData.CascadeHeightsMap);
        _material.SetTexture(_HoriDisplacementMap1Id, _oceanCascade1.GetTimeDependantSpectrumData().HorizontalDisplacementMap);
        _material.SetTexture(_HoriDisplacementMap2Id, _oceanCascade2.GetTimeDependantSpectrumData().HorizontalDisplacementMap);
        _material.SetTexture(_HoriDisplacementMap3Id, _oceanCascade3.GetTimeDependantSpectrumData().HorizontalDisplacementMap);
        
        _material.SetTexture(_NormalMap1Id, _oceanCascade1.GetTimeDependantSpectrumData().HeightDerivative);
        _material.SetTexture(_NormalMap2Id, _oceanCascade2.GetTimeDependantSpectrumData().HeightDerivative);
        _material.SetTexture(_NormalMap3Id, _oceanCascade3.GetTimeDependantSpectrumData().HeightDerivative);
        
        _material.SetTexture(_FoamMap1Id, _oceanCascade1.GetSpectrumWrapperData().FoamMap);
        _material.SetTexture(_FoamMap2Id, _oceanCascade2.GetSpectrumWrapperData().FoamMap);
        _material.SetTexture(_FoamMap3Id, _oceanCascade3.GetSpectrumWrapperData().FoamMap);
        
        _material.SetFloat(_LengthScale1Id, lengthScale1);
        _material.SetFloat(_LengthScale2Id, lengthScale2);
        _material.SetFloat(_LengthScale3Id, lengthScale3);
    }

    //Code from https://stackoverflow.com/a/218600
    private Texture2D GenerateGaussianNoise(int size)
    {
        Texture2D noise = new Texture2D(size, size, TextureFormat.RGFloat, false, true);
        noise.filterMode = FilterMode.Point;
        
        Random.InitState(_seed);
        
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                noise.SetPixel(i, j, new Vector4(NormalRandom(), NormalRandom(),0, 0));
            }
        }
        
        noise.Apply();
        return noise;
    }
    
    /// <summary>
    /// Box-Muller transform to generate a random number from a normal distribution
    /// </summary>
    /// <returns></returns>
    private float NormalRandom()
    {
        return Mathf.Cos(2 * Mathf.PI * Random.value) * Mathf.Sqrt(-2 * Mathf.Log(Random.value));
    }
    
    public RenderTexture GetCascadeHeightsMap()
    {
        return _cascadeWrapperData.CascadeHeightsMap;
    }
    
    public float GetLengthScale1()
    {
        return lengthScale1;
    }
    
    public float GetLengthScale2()
    {
        return lengthScale2;
    }
    
    public float GetLengthScale3()
    {
        return lengthScale3;
    }

    private void OnDestroy()
    {
        _commandBuffer.Release();
    }
}
