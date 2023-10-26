using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class OceanGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _ocean;
    [SerializeField] private Material _material;
    [SerializeField] private ComputeShader _spectrumComputeShader;
    [SerializeField] private RawImage _spectrumMapImage;
    [SerializeField] private ComputeShader _hightMapComputeShader;
    [SerializeField] private RawImage _hightMapImage;

    [Header("Spectrum Parameters")] 
    [SerializeField] private int _size = 256;
    [SerializeField] private int _resolution = 256;
    [SerializeField] private float _windSpeed = 10f;
    [SerializeField] private float _fetch = 1000f;
    [SerializeField] private float _peakEnhancementFactor = 3.3f;
    [SerializeField] private float _depth = 1000f;
    
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private ComputeBuffer _computeBuffer;
    private Texture2D _gaussianNoise;

    private void Start()
    {
        // _meshFilter = _ocean.AddComponent<MeshFilter>();
        // _meshRenderer = _ocean.AddComponent<MeshRenderer>();
        //
        // GeneratePlane(_size, _resolution);
        // _meshRenderer.material = _material;
        
        _gaussianNoise = GenerateGaussianNoise(_resolution);
        InitSpectrumMap();
        GenerateHightMap();
    }

    private void OnValidate()
    {
        if(Application.isPlaying == false)
            return;
        
        _gaussianNoise = GenerateGaussianNoise(_resolution);
        InitSpectrumMap();
        GenerateHightMap();
    }

    private void InitSpectrumMap()
    {
        // Create a new RenderTexture
        RenderTexture renderTexture = new RenderTexture(_resolution, _resolution, 24);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        
        // Find kernel
        int kernel = _spectrumComputeShader.FindKernel("CalculateSpectrum");
        
        // Set constants
        _spectrumComputeShader.SetInt("Size", _size);
        _spectrumComputeShader.SetInt("Resolution", _resolution);
        _spectrumComputeShader.SetFloat("WindSpeed", _windSpeed);
        _spectrumComputeShader.SetFloat("Fetch", _fetch);
        _spectrumComputeShader.SetFloat("PeakEnhancementFactor", _peakEnhancementFactor);
        _spectrumComputeShader.SetFloat("Depth", _depth);
        _spectrumComputeShader.SetTexture(0, "Noise", _gaussianNoise);

        // Bind the RenderTexture to the compute shader
        _spectrumComputeShader.SetTexture(kernel, "SpectrumMap", renderTexture);

        // Dispatch the compute shader
        _spectrumComputeShader.Dispatch(kernel, _resolution / 8, _resolution / 8, 1);

        // Set the texture of the RawImage to the RenderTexture
        _spectrumMapImage.texture = renderTexture;
    }

    private void GenerateHightMap()
    {
        // Create a new RenderTexture
        RenderTexture renderTexture = new RenderTexture(_resolution, _resolution, 24);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        
        // Find kernel
        int kernel = _hightMapComputeShader.FindKernel("CalculateHeight");
        
        // Set constants
        _hightMapComputeShader.SetInt("Size", _size);
        _hightMapComputeShader.SetInt("Resolution", _resolution);
        _hightMapComputeShader.SetTexture(kernel, "SpectrumMap", _spectrumMapImage.texture);
        // Bind the RenderTexture to the compute shader
        _hightMapComputeShader.SetTexture(kernel, "HeightMap", renderTexture);
        
        // Dispatch the compute shader
        _hightMapComputeShader.Dispatch(kernel, _resolution / 8, _resolution / 8, 1);
        
        // Set the texture of the RawImage to the RenderTexture
        _hightMapImage.texture = renderTexture;
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
    
    Texture2D GenerateGaussianNoise(int size)
    {
        Texture2D noise = new Texture2D(size, size, TextureFormat.RGFloat, false, true);
        noise.filterMode = FilterMode.Point;
        
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                noise.SetPixel(i, j, new Vector4(NormalRandom(), NormalRandom()));
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
}
