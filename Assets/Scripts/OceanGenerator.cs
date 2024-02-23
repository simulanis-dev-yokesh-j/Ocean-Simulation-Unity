using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class OceanGenerator : MonoBehaviour
{
    [System.Serializable]
    public struct ComputeShaders
    {
        public ComputeShader InitSpectrum;   
        public ComputeShader TimeDependantSpectrum;
        public ComputeShader SpectrumWrapper;
        public ComputeShader FFT;
    }
    
    [System.Serializable]
    public struct InitSpectrumData
    {
        public int InitKernel;
        public int WrapperKernel;
        public RenderTexture InitSpectrum;
        public RenderTexture ConjugatedSpectrum;
        public RenderTexture WaveData;
        public Texture2D GaussianNoise;
    }
    
    [System.Serializable]
    public struct TimeDependantSpectrumData
    {
        public int Kernel;
        public RenderTexture FrequencyDomain;
        public RenderTexture HeightMap;
        public RenderTexture HeightDerivative;
        public RenderTexture HorizontalDisplacementMap;
        public RenderTexture JacobianXxZzMap;
        public RenderTexture JacobianXzMap;
    }
    
    [System.Serializable]
    public struct SpectrumWrapperData
    {
        public int Kernel;
        public RenderTexture NormalMap;
        public RenderTexture DisplacementMap;
        public RenderTexture FoamMap;
    }
    
    [Header("Shaders")]
    [SerializeField] private ComputeShaders _computeShaders;

    [Header("References")]
    [SerializeField] private GameObject _ocean;
    [SerializeField] private Material _material;
    [SerializeField] private RawImage _spectrumMapImage;
    [SerializeField] private RawImage _hightMapImage;
    [SerializeField] private RawImage _normalMapImage;

    [Header("Geometry Parameters")]
    [SerializeField] private int _resolution = 256;
    
    [Header("Spectrum Parameters")]
    [SerializeField] private int _seed = 10;
    [SerializeField] private int _size = 512;
    [SerializeField] private int _lengthScale = 1024;
    
    [Header("Ocean Parameters")]
    [SerializeField] private float _speed = 1f;
    [SerializeField] private float _windSpeed = 10f;
    [SerializeField, Range(0, 360)] private float _windAngle = 1;
    [SerializeField] private float _depth = 1000f;
    [SerializeField] private float _distanceToShore = 1000f;
    [SerializeField, Range(0, 2)] private float _waveChopyFactor = 0.8f;
    [SerializeField, Range(0, 2)] private float _foamIntensity = 0.5f;
    [SerializeField, Range(0, 1)] private float _foamDecay = 0.1f;
    private float _time = 0f;
    
    // Outputs
    private InitSpectrumData _initSpectrumData;
    private TimeDependantSpectrumData _timeDependantSpectrumData;
    private SpectrumWrapperData _spectrumWrapperData;
    
    // Other var
    private ComputeFFT _fft;
    private CommandBuffer _commandBuffer;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private static readonly int DisplacementMapID = Shader.PropertyToID("_DisplacementMap");
    private static readonly int NormalMapID = Shader.PropertyToID("_NormalMap");
    private static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
    private static readonly int LengthScale = Shader.PropertyToID("_LengthScale");

    private void Start()
    {
        _meshFilter = _ocean.AddComponent<MeshFilter>();
        _meshRenderer = _ocean.AddComponent<MeshRenderer>();
        _commandBuffer = new CommandBuffer();

        GeneratePlane(_size, _resolution);
        _meshRenderer.material = _material;
        
        InitRenderTextures();
        GenerateInitSpectrum();
        _fft = new ComputeFFT(_size, _commandBuffer, _computeShaders.FFT);

        _material.SetTexture(DisplacementMapID, _spectrumWrapperData.DisplacementMap);
        _material.SetTexture(NormalMapID, _spectrumWrapperData.NormalMap);
        _material.SetTexture(FoamMap, _spectrumWrapperData.FoamMap);
        _material.SetFloat(LengthScale, _lengthScale);
    }

    private void OnValidate()
    {
        if(Application.isPlaying == false)
            return;
        
        if(_commandBuffer == null)
            return;
        
        InitRenderTextures();
        GenerateInitSpectrum();
        _fft = new ComputeFFT(_size, _commandBuffer, _computeShaders.FFT);
        
        _material.SetTexture(DisplacementMapID, _spectrumWrapperData.DisplacementMap);
        _material.SetTexture(NormalMapID, _spectrumWrapperData.NormalMap);
        _material.SetTexture(FoamMap, _spectrumWrapperData.FoamMap);
        _material.SetFloat(LengthScale, _lengthScale);
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            Utilities.OutputTexture(_spectrumWrapperData.NormalMap);
        }
        
        _time += Time.deltaTime * _speed;
        GenerateFrequencyDomain();
        GenerateHeightMap();
        
        // Execute the command buffer
        Graphics.ExecuteCommandBuffer(_commandBuffer);
        _commandBuffer.Clear();
        
        _spectrumMapImage.texture = _timeDependantSpectrumData.FrequencyDomain;
        _hightMapImage.texture = _timeDependantSpectrumData.HeightMap;
        _normalMapImage.texture = _spectrumWrapperData.NormalMap;
    }

    private void InitRenderTextures()
    {
        _initSpectrumData.InitKernel = _computeShaders.InitSpectrum.FindKernel("CalculateInitSpectrum");
        _initSpectrumData.WrapperKernel = _computeShaders.InitSpectrum.FindKernel("ConjugateInitSpectrum");
        _timeDependantSpectrumData.Kernel = _computeShaders.TimeDependantSpectrum.FindKernel("CalculateFrequencyDomain");
        _spectrumWrapperData.Kernel = _computeShaders.SpectrumWrapper.FindKernel("ComputeWrapper");
        
        _initSpectrumData.InitSpectrum = Utilities.CreateRenderTexture(_size);
        _initSpectrumData.ConjugatedSpectrum = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _initSpectrumData.WaveData = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _initSpectrumData.GaussianNoise = GenerateGaussianNoise(_size);
        
        _timeDependantSpectrumData.FrequencyDomain = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.HeightMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.HeightDerivative = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.HorizontalDisplacementMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.JacobianXxZzMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _timeDependantSpectrumData.JacobianXzMap = Utilities.CreateRenderTexture(_size);
        
        _spectrumWrapperData.DisplacementMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _spectrumWrapperData.NormalMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
        _spectrumWrapperData.FoamMap = Utilities.CreateRenderTexture(_size, RenderTextureFormat.ARGBFloat);
    }

    private void GenerateInitSpectrum()
    {
        int kernel = _initSpectrumData.InitKernel;
        ComputeShader shader = _computeShaders.InitSpectrum;
    
        // Set variables
        _commandBuffer.SetComputeIntParam(shader, "Size", _size);
        _commandBuffer.SetComputeIntParam(shader, "LengthScale", _lengthScale);
        _commandBuffer.SetComputeFloatParam(shader, "WindSpeed", _windSpeed);
        _commandBuffer.SetComputeFloatParam(shader, "WindAngle", _windAngle);
        _commandBuffer.SetComputeFloatParam(shader, "Fetch", _distanceToShore * 1000);
        _commandBuffer.SetComputeFloatParam(shader, "Depth", _depth);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "Noise", _initSpectrumData.GaussianNoise);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "InitSpectrum", _initSpectrumData.InitSpectrum);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "WaveData", _initSpectrumData.WaveData);

        // Dispatch the compute shader
        _commandBuffer.DispatchCompute(_computeShaders.InitSpectrum, kernel, _size / 8, _size / 8, 1);
        
        kernel = _initSpectrumData.WrapperKernel;
        
        _commandBuffer.SetComputeIntParam(shader, "Size", _size);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "InitSpectrum", _initSpectrumData.InitSpectrum);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "ConjugatedSpectrum", _initSpectrumData.ConjugatedSpectrum);
        
        _commandBuffer.DispatchCompute(_computeShaders.InitSpectrum, kernel, _size / 8, _size / 8, 1);
    }

    private void GenerateFrequencyDomain()
    {
        int kernel = _timeDependantSpectrumData.Kernel;
        ComputeShader shader = _computeShaders.TimeDependantSpectrum;
    
        // Set variables
        _commandBuffer.SetComputeFloatParam(shader, "Time", _time);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "InitSpectrum", _initSpectrumData.ConjugatedSpectrum);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "WaveData", _initSpectrumData.WaveData);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "FrequencyDomain", _timeDependantSpectrumData.FrequencyDomain);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HeightMap", _timeDependantSpectrumData.HeightMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HeightDerivative", _timeDependantSpectrumData.HeightDerivative);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HorizontalDisplacementMap", _timeDependantSpectrumData.HorizontalDisplacementMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXxZzMap", _timeDependantSpectrumData.JacobianXxZzMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXzMap", _timeDependantSpectrumData.JacobianXzMap);

        // Dispatch the compute shader
        _commandBuffer.DispatchCompute(shader, kernel, _size / 8, _size / 8, 1);
    }

    private void GenerateHeightMap()
    {
        _fft.DoIFFT(_timeDependantSpectrumData.FrequencyDomain, _timeDependantSpectrumData.HeightMap);
        _fft.DoIFFT(_timeDependantSpectrumData.HeightDerivative);
        _fft.DoIFFT(_timeDependantSpectrumData.HorizontalDisplacementMap);
        _fft.DoIFFT(_timeDependantSpectrumData.JacobianXxZzMap);
        _fft.DoIFFT(_timeDependantSpectrumData.JacobianXzMap);

        int kernel = _spectrumWrapperData.Kernel;
        ComputeShader shader = _computeShaders.SpectrumWrapper;
        
        _commandBuffer.SetComputeFloatParam(shader, "DeltaTime", Time.deltaTime);
        _commandBuffer.SetComputeFloatParam(shader, "DisplacementFactor", _waveChopyFactor);
        _commandBuffer.SetComputeFloatParam(shader, "FoamIntensity", _foamIntensity);
        _commandBuffer.SetComputeFloatParam(shader, "FoamDecay", _foamDecay);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HeightDerivative", _timeDependantSpectrumData.HeightDerivative);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HeightMap", _timeDependantSpectrumData.HeightMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "HorizontalDisplacementMap", _timeDependantSpectrumData.HorizontalDisplacementMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXxZzMap", _timeDependantSpectrumData.JacobianXxZzMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "JacobianXzMap", _timeDependantSpectrumData.JacobianXzMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "NormalMap", _spectrumWrapperData.NormalMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "DisplacementMap", _spectrumWrapperData.DisplacementMap);
        _commandBuffer.SetComputeTextureParam(shader, kernel, "FoamMap", _spectrumWrapperData.FoamMap);

        _commandBuffer.DispatchCompute(shader, kernel, _size / 8, _size / 8, 1);
    }
    
    private void GeneratePlane(int size, int resolution)
    {
        Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        int[] triangles = new int[resolution * resolution * 6];
    
        float increment = 1f / resolution;
        int vertIndex = 0;
        int triIndex = 0;
    
        // Create vertices and triangles
        for (int y = 0; y <= resolution; y++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                vertices[vertIndex] = new Vector3(size * (x * increment - .5f), 0, size * (y * increment - .5f));
    
                if (x != resolution && y != resolution)
                {
                    triangles[triIndex] = vertIndex;
                    triangles[triIndex + 1] = vertIndex + resolution + 1;
                    triangles[triIndex + 2] = vertIndex + resolution + 2;
    
                    triangles[triIndex + 3] = vertIndex;
                    triangles[triIndex + 4] = vertIndex + resolution + 2;
                    triangles[triIndex + 5] = vertIndex + 1;
    
                    triIndex += 6;
                }
                vertIndex++;
            }
        }
    
        //Create mesh
        Mesh mesh = new Mesh();
        mesh.name = "Ocean Mesh";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
    
        //Assign mesh to mesh filter
        _meshFilter.mesh = mesh;
    }

    // private void GeneratePlane(int size, int resolution)
    // {
    //     Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
    //     int[] triangles = new int[resolution * resolution * 6];
    //
    //     // Create vertices
    //     for (int i = 0, y = 0; y <= resolution; y++)
    //     {
    //         for (int x = 0; x <= resolution; x++, i++)
    //         {
    //             vertices[i] = new Vector3(x * size / resolution, 0, y * size / resolution);
    //         }
    //     }
    //
    //     // Create triangular grid
    //     for (int ti = 0, vi = 0, y = 0; y < resolution; y++, vi++)
    //     {
    //         for (int x = 0; x < resolution; x++, ti += 6, vi++)
    //         {
    //             triangles[ti] = vi;
    //             triangles[ti + 3] = triangles[ti + 2] = vi + 1;
    //             triangles[ti + 4] = triangles[ti + 1] = vi + resolution + 1;
    //             triangles[ti + 5] = vi + resolution + 2;
    //         }
    //     }
    //
    //     //Create mesh
    //     Mesh mesh = new Mesh();
    //     mesh.name = "Ocean Mesh";
    //     mesh.indexFormat = IndexFormat.UInt32;
    //     mesh.vertices = vertices;
    //     mesh.triangles = triangles;
    //
    //     //Assign mesh to mesh filter
    //     _meshFilter.mesh = mesh;
    // }


    private RenderTexture CreateRenderTexture(int width, int height)
    {
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        return renderTexture;
    }

    //Code from https://stackoverflow.com/a/218600
    Texture2D GenerateGaussianNoise(int size)
    {
        Texture2D noise = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
        noise.filterMode = FilterMode.Point;
        
        Random.InitState(_seed);
        
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                noise.SetPixel(i, j, new Vector4(NormalRandom(), NormalRandom(), NormalRandom(), NormalRandom()));
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
