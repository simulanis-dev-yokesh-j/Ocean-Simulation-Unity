using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class OceanGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _ocean;
    [SerializeField] private Material _material;
    [SerializeField] private ComputeShader _computeShader;
    [SerializeField] private RawImage _spectrumMapImage;
    [SerializeField] private RawImage _hightMapImage;

    [Header("Spectrum Parameters")]
    [SerializeField] private float _time = 0f;
    [SerializeField] private int _seed = 10;
    [SerializeField] private int _size = 512;
    [SerializeField] private int _lengthScale = 1024;
    [SerializeField] private float _windSpeed = 10f;
    [SerializeField] private Vector2 _windDirection = new Vector2(1, 1);
    [SerializeField] private float _depth = 1000f;
    
    // Outputs
    RenderTexture _spectrumMap;
    RenderTexture _heightMap;
    
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Texture2D _gaussianNoise;

    private void Start()
    {
        // _meshFilter = _ocean.AddComponent<MeshFilter>();
        // _meshRenderer = _ocean.AddComponent<MeshRenderer>();
        //
        // GeneratePlane(_size, _resolution);
        // _meshRenderer.material = _material;
        
        InitComputeShaders();
    }

    // private void OnValidate()
    // {
    //     if(Application.isPlaying == false)
    //         return;
    //     
    //     InitComputeShaders();
    // }

    private void InitComputeShaders()
    {
        _gaussianNoise = GenerateGaussianNoise(_size);
        
        // Create new RenderTextures
        _spectrumMap = CreateRenderTexture(_size, _size);
        _heightMap = CreateRenderTexture(_size, _size);
        
        InitSpectrumMap();
        UpdateHeightMap();
    }

    private void Update()
    {
        _time += Time.deltaTime;
        InitSpectrumMap();
        UpdateHeightMap();
    }

    private void InitSpectrumMap()
    {
        // Find kernel
        int kernel = _computeShader.FindKernel("CalculateSpectrum");
        
        // Set constants
        _computeShader.SetFloat("Time", _time);
        _computeShader.SetInt("Size", _size);
        _computeShader.SetInt("LengthScale", _lengthScale);
        _computeShader.SetFloat("WindSpeed", _windSpeed);
        _computeShader.SetVector("WindDirection", _windDirection.normalized);
        _computeShader.SetFloat("Depth", _depth);
        _computeShader.SetTexture(0, "Noise", _gaussianNoise);

        // Bind the RenderTexture to the compute shader
        _computeShader.SetTexture(kernel, "SpectrumMap", _spectrumMap);

        // Dispatch the compute shader
        _computeShader.Dispatch(kernel, _size / 8, _size / 8, 1);
        
        _spectrumMapImage.texture = _spectrumMap;
    }
    
    private void UpdateHeightMap()
    {
        // Find kernel
        int kernel = _computeShader.FindKernel("CalculateHeight");
        
        // Set constants
        _computeShader.SetInt("Size", _size);
        _computeShader.SetInt("LengthScale", _lengthScale);
        _computeShader.SetTexture(kernel, "SpectrumMap", _spectrumMap);
        
        // Bind the RenderTexture to the compute shader
        _computeShader.SetTexture(kernel, "HeightMap", _heightMap);
        
        // Dispatch the compute shader
        _computeShader.Dispatch(kernel, _size / 8, _size / 8, 1);
        
        _hightMapImage.texture = _heightMap;
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
        mesh.vertices = vertices;
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
