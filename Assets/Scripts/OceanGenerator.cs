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
    [Header("Shaders")]
    [SerializeField] private CompShaders compShaders;

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
    [SerializeField] private int lengthScale0 = 256;
    [SerializeField] private int lengthScale1 = 128;
    [SerializeField] private int lengthScale2 = 64;
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
    private static readonly int DisplacementMap1ID = Shader.PropertyToID("_DisplacementMap1");
    private static readonly int DisplacementMap2ID = Shader.PropertyToID("_DisplacementMap2");
    private static readonly int DisplacementMap3ID = Shader.PropertyToID("_DisplacementMap3");
    private static readonly int NormalMap1ID = Shader.PropertyToID("_NormalMap1");
    private static readonly int NormalMap2ID = Shader.PropertyToID("_NormalMap2");
    private static readonly int NormalMap3ID = Shader.PropertyToID("_NormalMap3");
    private static readonly int FoamMap1 = Shader.PropertyToID("_FoamMap1");
    private static readonly int FoamMap2 = Shader.PropertyToID("_FoamMap2");
    private static readonly int FoamMap3 = Shader.PropertyToID("_FoamMap3");
    private static readonly int LengthScale1 = Shader.PropertyToID("_LengthScale1");
    private static readonly int LengthScale2 = Shader.PropertyToID("_LengthScale2");
    private static readonly int LengthScale3 = Shader.PropertyToID("_LengthScale3");

    private void Start()
    {
        _commandBuffer = new CommandBuffer();
        _oceanGeometry = GetComponent<OceanGeometry>();
        _fft = new ComputeFFT(_size, _commandBuffer, compShaders.FFT);
        _gaussianNoise = GenerateGaussianNoise(_size);

        _oceanCascade1 = new OceanCascade(compShaders, _commandBuffer);
        _oceanCascade3 = new OceanCascade(compShaders, _commandBuffer);
        _oceanCascade2 = new OceanCascade(compShaders, _commandBuffer);
        
        float boundary1 = 2 * Mathf.PI / lengthScale1 * SomeMagicNumber;
        float boundary2 = 2 * Mathf.PI / lengthScale2 * SomeMagicNumber;

        _oceanCascade1.Init(_size, lengthScale0, 0.0001f, boundary1, _oceanSettings, _fft, _gaussianNoise);
        _oceanCascade2.Init(_size, lengthScale1, boundary1, boundary2, _oceanSettings, _fft, _gaussianNoise);
        _oceanCascade3.Init(_size, lengthScale2, boundary2, 999999, _oceanSettings, _fft, _gaussianNoise);

        AssignMaterialUniforms();
        _oceanGeometry.GenerateGrid(_material);
    }

    private void OnValidate()
    {
        if(Application.isPlaying == false)
            return;
        
        if(_commandBuffer == null)
            return;
        
        _fft = new ComputeFFT(_size, _commandBuffer, compShaders.FFT);
        
        float boundary1 = 2 * Mathf.PI / lengthScale1 * SomeMagicNumber;
        float boundary2 = 2 * Mathf.PI / lengthScale2 * SomeMagicNumber;

        _oceanCascade1.Init(_size, lengthScale0, 0.0001f, boundary1, _oceanSettings, _fft, _gaussianNoise);
        _oceanCascade2.Init(_size, lengthScale1, boundary1, boundary2, _oceanSettings, _fft, _gaussianNoise);
        _oceanCascade3.Init(_size, lengthScale2, boundary2, 999999, _oceanSettings, _fft, _gaussianNoise);

        AssignMaterialUniforms();
    }
    
    private void Update()
    {
        _time += Time.deltaTime;
        
        _oceanCascade1.Update(_time);
        _oceanCascade2.Update(_time);
        _oceanCascade3.Update(_time);
        
        // Execute the command buffer
        Graphics.ExecuteCommandBuffer(_commandBuffer);
        _commandBuffer.Clear();

        _cascade1.texture = _oceanCascade1.GetInitSpectrumData().InitSpectrum;
        _cascade2.texture = _oceanCascade2.GetInitSpectrumData().InitSpectrum;
        _cascade3.texture = _oceanCascade3.GetInitSpectrumData().InitSpectrum;
    }
    
    private void AssignMaterialUniforms()
    {
        _material.SetTexture(DisplacementMap1ID, _oceanCascade1.GetSpectrumWrapperData().DisplacementMap);
        _material.SetTexture(DisplacementMap2ID, _oceanCascade2.GetSpectrumWrapperData().DisplacementMap);
        _material.SetTexture(DisplacementMap3ID, _oceanCascade3.GetSpectrumWrapperData().DisplacementMap);
        
        _material.SetTexture(NormalMap1ID, _oceanCascade1.GetSpectrumWrapperData().NormalMap);
        _material.SetTexture(NormalMap2ID, _oceanCascade2.GetSpectrumWrapperData().NormalMap);
        _material.SetTexture(NormalMap3ID, _oceanCascade3.GetSpectrumWrapperData().NormalMap);
        
        _material.SetTexture(FoamMap1, _oceanCascade1.GetSpectrumWrapperData().FoamMap);
        _material.SetTexture(FoamMap2, _oceanCascade2.GetSpectrumWrapperData().FoamMap);
        _material.SetTexture(FoamMap3, _oceanCascade3.GetSpectrumWrapperData().FoamMap);
        
        _material.SetFloat(LengthScale1, lengthScale0);
        _material.SetFloat(LengthScale2, lengthScale1);
        _material.SetFloat(LengthScale3, lengthScale2);
    }

    //Code from https://stackoverflow.com/a/218600
    Texture2D GenerateGaussianNoise(int size)
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
    float NormalRandom()
    {
        return Mathf.Cos(2 * Mathf.PI * Random.value) * Mathf.Sqrt(-2 * Mathf.Log(Random.value));
    }

    private void OnDestroy()
    {
        _commandBuffer.Release();
    }
}
