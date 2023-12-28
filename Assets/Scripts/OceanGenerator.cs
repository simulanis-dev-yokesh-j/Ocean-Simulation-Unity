using System;
using UnityEngine;
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
        public int NormalMapKernel;
        public RenderTexture NormalMap;
        public RenderTexture WaveData;
        public Texture2D GaussianNoise;
    }

    [Header("References")]
    [SerializeField] private GameObject _ocean;
    [SerializeField] private Material _material;
    [SerializeField] private ComputeShader _computeShader;
    [SerializeField] private RawImage _spectrumMapImage;
    [SerializeField] private RawImage _hightMapImage;

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
    private float _time = 0f;
    
    // Outputs
    private OceanRenderData _oceanData;  
    
    // Other var
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    private void Start()
    {
        _meshFilter = _ocean.AddComponent<MeshFilter>();
        _meshRenderer = _ocean.AddComponent<MeshRenderer>();
        
        GeneratePlane(_size, _resolution);
        _meshRenderer.material = _material;
        
        InitRenderTextures();
        GenerateInitSpectrum();
        GenerateFrequencyDomain();
        GenerateHeightMap();
        _material.SetTexture("_HeightMap", _oceanData.HeightMap);
        _material.SetTexture("_NormalMap", _oceanData.NormalMap);
    }

    private void OnValidate()
    {
        if(Application.isPlaying == false)
            return;
        
        InitRenderTextures();
        GenerateInitSpectrum();
        GenerateFrequencyDomain();
        GenerateHeightMap();
        _material.SetTexture("_HeightMap", _oceanData.HeightMap);
        _material.SetTexture("_NormalMap", _oceanData.NormalMap);
    }
    
    private void Update()
    {
        _time += Time.deltaTime;
        GenerateFrequencyDomain();
        GenerateHeightMap();
        GenerateNormalMap();
    }
    
    private void InitRenderTextures()
    {
        _oceanData.InitSpectrumKernel = _computeShader.FindKernel("CalculateInitSpectrum");
        _oceanData.InitSpectrum = CreateRenderTexture(_size, _size);
        _oceanData.FrequencyDomainKernel = _computeShader.FindKernel("CalculateFrequencyDomain");
        _oceanData.FrequencyDomain = CreateRenderTexture(_size, _size);
        _oceanData.HeightMapKernel = _computeShader.FindKernel("CalculateHeight");
        _oceanData.HeightMap = CreateRenderTexture(_size, _size);
        _oceanData.NormalMapKernel = _computeShader.FindKernel("CalculateNormal");
        _oceanData.NormalMap = CreateRenderTexture(_size, _size);
        _oceanData.WaveData = CreateRenderTexture(_size, _size);
        _oceanData.GaussianNoise = GenerateGaussianNoise(_size);
    }

    private void GenerateInitSpectrum()
    {
        RenderTexture initSpectrum = _oceanData.InitSpectrum;
        int kernel = _oceanData.InitSpectrumKernel;
        
        // Set variables
        _computeShader.SetInt("Size", _size);
        _computeShader.SetInt("LengthScale", _lengthScale);
        _computeShader.SetFloat("Amplitude", _amplitude);
        _computeShader.SetFloat("WindSpeed", _windSpeed);
        _computeShader.SetVector("WindDirection", _windDirection.normalized);
        _computeShader.SetFloat("Depth", _depth);
        _computeShader.SetTexture(kernel, "Noise", _oceanData.GaussianNoise);
        _computeShader.SetTexture(kernel, "InitSpectrum", initSpectrum);
        _computeShader.SetTexture(kernel, "WaveData", _oceanData.WaveData);

        // Dispatch the compute shader
        _computeShader.Dispatch(kernel, _size / 8, _size / 8, 1);
    }

    private void GenerateFrequencyDomain()
    {
        int kernel = _oceanData.FrequencyDomainKernel;
        
        // Set variables
        _computeShader.SetFloat("Time", _time);
        _computeShader.SetTexture(kernel, "InitSpectrum", _oceanData.InitSpectrum);
        _computeShader.SetTexture(kernel, "WaveData", _oceanData.WaveData);
        _computeShader.SetTexture(kernel, "FrequencyDomain", _oceanData.FrequencyDomain);

        // Dispatch the compute shader
        _computeShader.Dispatch(kernel, _size / 8, _size / 8, 1);
        
        // show image
        _spectrumMapImage.texture = _oceanData.FrequencyDomain;
    }
    
    private void GenerateHeightMap()
    {
        // Find kernel
        int kernel = _oceanData.HeightMapKernel;
        
        // Set constants
        _computeShader.SetInt("Size", _size);
        _computeShader.SetInt("LengthScale", _lengthScale);
        _computeShader.SetTexture(kernel, "FrequencyDomain", _oceanData.FrequencyDomain);
        _computeShader.SetTexture(kernel, "WaveData", _oceanData.WaveData);
        _computeShader.SetTexture(kernel, "HeightMap", _oceanData.HeightMap);
        //_computeShader.SetTexture(kernel, "NormalMap", _oceanData.NormalMap);
        
        // Dispatch the compute shader
        _computeShader.Dispatch(kernel, _size / 8, _size / 8, 1);
        
        _hightMapImage.texture = _oceanData.HeightMap;
        //PrintSpectrumTexture(_oceanData.HeightMap);
    }

    private void GenerateNormalMap()
    {
        int kernel = _oceanData.NormalMapKernel;
        
        // Set constants
        _computeShader.SetInt("Size", _size);
        _computeShader.SetTexture(kernel, "HeightMap", _oceanData.HeightMap);
        _computeShader.SetTexture(kernel, "NormalMap", _oceanData.NormalMap);
        
        // Dispatch the compute shader
        _computeShader.Dispatch(kernel, _size / 8, _size / 8, 1);
        
        //PrintSpectrumTexture(_oceanData.NormalMap);
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
}
