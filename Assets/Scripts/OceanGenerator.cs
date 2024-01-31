using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class OceanGenerator : MonoBehaviour
{
    [System.Serializable]
    public struct OceanRenderData
    {
        public int InitSpectrumKernel;
        public RenderTexture InitSpectrum;
        public int FrequencyDomainKernel;
        public RenderTexture FrequencyDomain;
        public int HeightMapKernel;
        public RenderTexture HeightMap;
        public RenderTexture NormalMap;
        public RenderTexture WaveData;
        [FormerlySerializedAs("PermutedHightMapKernel")] public int PermuteHeightMapKernel;
        public Texture2D GaussianNoise;
    }

    [Header("References")]
    [SerializeField] private GameObject _ocean;
    [SerializeField] private Material _material;
    [SerializeField] private ComputeShader _computeShader;
    [SerializeField] private RawImage _spectrumMapImage;
    [SerializeField] private RawImage _hightMapImage;
    [SerializeField] private RawImage _normalMapImage;

    [Header("Geometry Parameters")]
    [SerializeField] private int _resolution = 256;
    
    [Header("Spectrum Parameters")]
    [SerializeField] private int _seed = 10;
    [SerializeField] private int _size = 512;
    [SerializeField] private int _lengthScale = 1024;
    [SerializeField] private float _amplitude = 0.6f;
    [SerializeField] private float _windSpeed = 10f;
    [SerializeField] private Vector2 _windDirection = new Vector2(1, 1);
    [SerializeField] private float _depth = 1000f;
    [SerializeField] private float _displacementFactor = 0.8f;
    private float _time = 0f;
    
    // Outputs
    private OceanRenderData _oceanData;  
    
    // Other var
    FastFourierTransform _fft;
    [SerializeField] private ComputeShader _fftComputeShader;
    private CommandBuffer _commandBuffer;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private static readonly int HeightMapID = Shader.PropertyToID("_HeightMap");
    private static readonly int NormalMapID = Shader.PropertyToID("_NormalMap");

    private void Start()
    {
        _meshFilter = _ocean.AddComponent<MeshFilter>();
        _meshRenderer = _ocean.AddComponent<MeshRenderer>();
        _commandBuffer = new CommandBuffer();
        _fft = new FastFourierTransform(_size, _fftComputeShader);

        GeneratePlane(_size, _resolution);
        _meshRenderer.material = _material;
        
        InitRenderTextures();
        GenerateInitSpectrum();

        // _material.SetTexture(HeightMapID, _oceanData.HeightMap);
        //_material.SetTexture(NormalMapID, _oceanData.NormalMap);
    }

    private void OnValidate()
    {
        if(Application.isPlaying == false)
            return;
        
        if(_commandBuffer == null)
            return;
        
        InitRenderTextures();
        GenerateInitSpectrum();
    }
    
    private void Update()
    {
        _time += Time.deltaTime;
        GenerateFrequencyDomain();
        GenerateHeightMap();
        
        // Execute the command buffer
        Graphics.ExecuteCommandBuffer(_commandBuffer);
        _commandBuffer.Clear();
        
        _spectrumMapImage.texture = _oceanData.FrequencyDomain;
        _hightMapImage.texture = _oceanData.HeightMap;
        
        _material.SetTexture(HeightMapID, _oceanData.HeightMap);
    }

    // button
    [ContextMenu("Save Height Texture")]
    public void SaveFrequencyTexture()
    {
        // Save texture to file
        RenderTexture.active = _oceanData.HeightMap;
        Texture2D texture = new Texture2D(_oceanData.HeightMap.width, _oceanData.HeightMap.height, TextureFormat.RGBAHalf, false);
        texture.ReadPixels(new Rect(0, 0, _oceanData.HeightMap.width, _oceanData.HeightMap.height), 0, 0);
        texture.Apply();
        byte[] bytes = texture.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.dataPath + "/Height.png", bytes);
    }
    
    private void InitRenderTextures()
    {
        _oceanData.InitSpectrumKernel = _computeShader.FindKernel("CalculateInitSpectrum");
        _oceanData.InitSpectrum = CreateRenderTexture(_size, _size);
        _oceanData.FrequencyDomainKernel = _computeShader.FindKernel("CalculateFrequencyDomain");
        _oceanData.FrequencyDomain = FastFourierTransform.CreateRenderTexture(_size);
        _oceanData.HeightMapKernel = _computeShader.FindKernel("CalculateHeight");
        _oceanData.HeightMap = FastFourierTransform.CreateRenderTexture(_size);
        _oceanData.NormalMap = CreateRenderTexture(_size, _size);
        _oceanData.WaveData = CreateRenderTexture(_size, _size);
        _oceanData.PermuteHeightMapKernel = _computeShader.FindKernel("PermuteHeightMap");
        _oceanData.GaussianNoise = GenerateGaussianNoise(_size);
    }

    private void GenerateInitSpectrum()
    {
        RenderTexture initSpectrum = _oceanData.InitSpectrum;
        int kernel = _oceanData.InitSpectrumKernel;
    
        // Set variables
        _commandBuffer.SetComputeIntParam(_computeShader, "Size", _size);
        _commandBuffer.SetComputeIntParam(_computeShader, "LengthScale", _lengthScale);
        _commandBuffer.SetComputeFloatParam(_computeShader, "Amplitude", _amplitude);
        _commandBuffer.SetComputeFloatParam(_computeShader, "WindSpeed", _windSpeed);
        _commandBuffer.SetComputeVectorParam(_computeShader, "WindDirection", _windDirection.normalized);
        _commandBuffer.SetComputeFloatParam(_computeShader, "Depth", _depth);
        _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "Noise", _oceanData.GaussianNoise);
        _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "InitSpectrum", initSpectrum);
        _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "WaveData", _oceanData.WaveData);

        // Dispatch the compute shader
        _commandBuffer.DispatchCompute(_computeShader, kernel, _size / 8, _size / 8, 1);
    }

    private void GenerateFrequencyDomain()
    {
        int kernel = _oceanData.FrequencyDomainKernel;
    
        // Set variables
        _commandBuffer.SetComputeFloatParam(_computeShader, "Time", _time);
        _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "InitSpectrum", _oceanData.InitSpectrum);
        _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "WaveData", _oceanData.WaveData);
        _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "FrequencyDomain", _oceanData.FrequencyDomain);

        // Dispatch the compute shader
        _commandBuffer.DispatchCompute(_computeShader, kernel, _size / 8, _size / 8, 1);
    }

    private void GenerateHeightMap()
    {
        // Find kernel
        // int kernel = _oceanData.HeightMapKernel;
        //
        // // Set constants
        // _commandBuffer.SetComputeIntParam(_computeShader, "Size", _size);
        // _commandBuffer.SetComputeIntParam(_computeShader, "LengthScale", _lengthScale);
        // _commandBuffer.SetComputeFloatParam(_computeShader, "DisplacementFactor", _displacementFactor);
        // _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "FrequencyDomain", _oceanData.FrequencyDomain);
        // _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "WaveData", _oceanData.WaveData);
        // _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "HeightMap", _oceanData.HeightMap);
        // _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "NormalMap", _oceanData.NormalMap);
        //
        // // Dispatch the compute shader
        // _commandBuffer.DispatchCompute(_computeShader, kernel, _size / 8, _size / 8, 1);
        
        // _hightMapImage.texture = _oceanData.HeightMap;
        // _normalMapImage.texture = _oceanData.NormalMap;
        
        _fft.IFFT2D(_oceanData.FrequencyDomain, _oceanData.HeightMap, false, false);
        
        int kernel = _oceanData.PermuteHeightMapKernel;
        _commandBuffer.SetComputeIntParam(_computeShader, "Size", _size);
        _commandBuffer.SetComputeTextureParam(_computeShader, kernel, "HeightMap", _oceanData.HeightMap);
        _commandBuffer.DispatchCompute(_computeShader, kernel, _size / 8, _size / 8, 1);
    }
    
    private void GeneratePlane(int size, int resolution)
    {
        Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
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
                uvs[vertIndex] = new Vector2(x * increment, y * increment);
                
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
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        //Assign mesh to mesh filter
        _meshFilter.mesh = mesh;
    }


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
    
    private void PrintSpectrumTexture(RenderTexture renderTexture)
    {
        // Create a new Texture2D with a format that supports negative values
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAHalf, false);

        // Set the active RenderTexture
        RenderTexture.active = renderTexture;

        // Read the pixels from the RenderTexture
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

        // Apply the changes
        texture2D.Apply();

        // Reset the active RenderTexture
        RenderTexture.active = null;

        // Now you have a Texture2D object with the data from the RenderTexture
        // You can access the pixel data, keeping in mind that the values can be negative

        Vector3 lowestX = new Vector3(0, 0, 0);
        Vector3 highestX = new Vector3(0, 0, 0);
        Vector3 lowestY = new Vector3(0, 0, 0);
        Vector3 highestY = new Vector3(0, 0, 0);

        // Print the pixel values
        for (int i = 0; i < texture2D.width; i++)
        {
            for (int j = 0; j < texture2D.height; j++)
            {
                Color pixelColor = texture2D.GetPixel(i, j);
                Debug.Log("Pixel (" + i + ", " + j + "): " + pixelColor);

                if (pixelColor.r < lowestX.x)
                {
                    lowestX.x = pixelColor.r;
                    lowestX.y = i;
                    lowestX.z = j;
                } 
                
                if (pixelColor.r > highestX.x)
                {
                    highestX.x = pixelColor.r;
                    highestX.y = i;
                    highestX.z = j;
                }
                
                if (pixelColor.g < lowestY.x)
                {
                    lowestY.x = pixelColor.g;
                    lowestY.y = i;
                    lowestY.z = j;
                }
                
                if (pixelColor.g > highestY.x)
                {
                    highestY.x = pixelColor.g;
                    highestY.y = i;
                    highestY.z = j;
                }
            }
        }
        
        print($"Lowest X: {lowestX.x} at ({lowestX.y}, {lowestX.z})");
        print($"Highest X: {highestX.x} at ({highestX.y}, {highestX.z})");
        print($"Lowest Y: {lowestY.x} at ({lowestY.y}, {lowestY.z})");
        print($"Highest Y: {highestY.x} at ({highestY.y}, {highestY.z})");
    }

    private void OnDestroy()
    {
        _commandBuffer.Release();
    }
}
