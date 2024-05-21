using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[System.Serializable]
public struct OceanComputeShaders
{
    public ComputeShader InitSpectrum;   
    public ComputeShader TimeDependantSpectrum;
    public ComputeShader SpectrumWrapper;
    public ComputeShader FFT;
}

[System.Serializable]
public class CascadeSettings
{
    public int LengthScale = 512;
    public float LowCutoff = 0;
    public float HighCutoff = 9999;
}

[System.Serializable]
public class OceanSettings
{ 
    public int Seed = 10;
    public int Size = 512;
    public float WindSpeed = 17f;
    [Range(0, 360)] 
    public float WindAngle = 45;
    public float Depth = 500f;
    [FormerlySerializedAs("Sweel")] [Range(0, 1)]
    public float Swell = 0.5f;
    public float DistanceToShore = 1000f;
    [Range(0, 2)] 
    public float WaveChopyFactor = 0.9f;
    [Range(0, 2)] 
    public float FoamIntensity = 0.45f;
    [Range(0, 1)] 
    public float FoamDecay = 0.08f;
}

public class OceanGenerator : MonoBehaviour
{
    [SerializeField] private OceanComputeShaders _computeShaders;
    [SerializeField] private OceanSettings _oceanSettings;
    [SerializeField] private CascadeSettings _cascadeSettings1;
    [SerializeField] private CascadeSettings _cascadeSettings2;
    [SerializeField] private CascadeSettings _cascadeSettings3;
    [SerializeField] private Material _material;

    private WaveCascade _waveCascade1;
    private WaveCascade _waveCascade2;
    private WaveCascade _waveCascade3;
    // Other var
    private float _time;
    private ComputeFFT _fft;
    private CommandBuffer _commandBuffer;
    private OceanGeometry _oceanGeometry;
    private Texture2D _gaussianNoise;
    private static readonly int DisplacementMapID1 = Shader.PropertyToID("_DisplacementMap1");
    private static readonly int DisplacementMapID2 = Shader.PropertyToID("_DisplacementMap2");
    private static readonly int DisplacementMapID3 = Shader.PropertyToID("_DisplacementMap3");
    private static readonly int NormalMapID1 = Shader.PropertyToID("_NormalMap1");
    private static readonly int NormalMapID2 = Shader.PropertyToID("_NormalMap2");
    private static readonly int NormalMapID3 = Shader.PropertyToID("_NormalMap3");
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
        
        _fft = new ComputeFFT(_oceanSettings.Size, _commandBuffer, _computeShaders.FFT);
        _gaussianNoise = GenerateGaussianNoise(_oceanSettings.Size);
        _waveCascade1 = new WaveCascade(_oceanSettings, _cascadeSettings1, _commandBuffer, _fft, _computeShaders, _gaussianNoise);
        _waveCascade2 = new WaveCascade(_oceanSettings, _cascadeSettings2, _commandBuffer, _fft, _computeShaders, _gaussianNoise);
        _waveCascade3 = new WaveCascade(_oceanSettings, _cascadeSettings3, _commandBuffer, _fft, _computeShaders, _gaussianNoise);

        AssignMaterialProperties();
        _oceanGeometry.GenerateGrid(_material);
    }

    private void AssignMaterialProperties()
    {
        _material.SetTexture(DisplacementMapID1, _waveCascade1.GetSpectrumWrapperData().DisplacementMap);
        _material.SetTexture(DisplacementMapID2, _waveCascade2.GetSpectrumWrapperData().DisplacementMap);
        _material.SetTexture(DisplacementMapID3, _waveCascade3.GetSpectrumWrapperData().DisplacementMap);
        
        _material.SetTexture(NormalMapID1, _waveCascade1.GetSpectrumWrapperData().NormalMap);
        _material.SetTexture(NormalMapID2, _waveCascade2.GetSpectrumWrapperData().NormalMap);
        _material.SetTexture(NormalMapID3, _waveCascade3.GetSpectrumWrapperData().NormalMap);
        
        _material.SetTexture(FoamMap1, _waveCascade1.GetSpectrumWrapperData().FoamMap);
        _material.SetTexture(FoamMap2, _waveCascade2.GetSpectrumWrapperData().FoamMap);
        _material.SetTexture(FoamMap3, _waveCascade3.GetSpectrumWrapperData().FoamMap);
        
        _material.SetFloat(LengthScale1, _cascadeSettings1.LengthScale);
        _material.SetFloat(LengthScale2, _cascadeSettings2.LengthScale);
        _material.SetFloat(LengthScale3, _cascadeSettings3.LengthScale);
    }

    private void OnValidate()
    {
        if(Application.isPlaying == false)
            return;
        
        if(_commandBuffer == null)
            return;
        
        _fft = new ComputeFFT(_oceanSettings.Size, _commandBuffer, _computeShaders.FFT);
        _gaussianNoise = GenerateGaussianNoise(_oceanSettings.Size);
        _waveCascade1 = new WaveCascade(_oceanSettings, _cascadeSettings1, _commandBuffer, _fft, _computeShaders, _gaussianNoise);
        _waveCascade2 = new WaveCascade(_oceanSettings, _cascadeSettings2, _commandBuffer, _fft, _computeShaders, _gaussianNoise);
        _waveCascade3 = new WaveCascade(_oceanSettings, _cascadeSettings3, _commandBuffer, _fft, _computeShaders, _gaussianNoise);
        
        AssignMaterialProperties();
    }
    
    private void Update()
    {
        _time += Time.deltaTime;
        _waveCascade1.Update(_time);
        _waveCascade2.Update(_time);
        _waveCascade3.Update(_time);
        
        // Execute the command buffer
        Graphics.ExecuteCommandBuffer(_commandBuffer);
        _commandBuffer.Clear();
    }

    //Code from https://stackoverflow.com/a/218600
    Texture2D GenerateGaussianNoise(int size)
    {
        Texture2D noise = new Texture2D(size, size, TextureFormat.RGFloat, false, true);
        noise.filterMode = FilterMode.Point;
        
        Random.InitState(_oceanSettings.Seed);
        
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
