using UnityEngine;
using UnityEngine.Rendering;

public class OceanDataReader : MonoBehaviour
{
    private static OceanDataReader _instance;
    public static OceanDataReader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<OceanDataReader>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("Ocean Data Reader");
                    _instance = go.AddComponent<OceanDataReader>();
                }
            }
            return _instance;
        }
    }

    private ComputeShader readbackShader;
    private ComputeBuffer resultBuffer;
    private CommandBuffer commandBuffer;
    private int readHeightKernel;
    
    [System.Serializable]
    public struct SampleResult
    {
        public float height;
        public Vector2 normal;
        public float foam;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeReader();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitializeReader()
    {
        // Create a simple compute shader for reading texture data if none exists
        commandBuffer = new CommandBuffer { name = "Ocean Data Readback" };
        resultBuffer = new ComputeBuffer(1, sizeof(float) * 4); // height + normal.xy + foam
    }

    public static Vector3 SampleOceanHeight(Vector3 worldPosition, OceanGenerator oceanGenerator)
    {
        if (oceanGenerator == null) return Vector3.zero;

        // Get world offset from infinite ocean manager
        Vector3 worldOffset = Vector3.zero;
      //  var infiniteOcean = FindObjectOfType<InfiniteOceanManager>();
      //  if (infiniteOcean != null)
      //  {
     //       worldOffset = infiniteOcean.GetWorldOffset();
     //   }

        Vector3 totalDisplacement = Vector3.zero;
        
        // Sample all three cascades with world offset
        for (int i = 0; i < 3; i++)
        {
            var displacementMap = oceanGenerator.GetDisplacementMap(i);
            float lengthScale = oceanGenerator.GetLengthScale(i);
            
            if (displacementMap != null && lengthScale > 0)
            {
                Vector2 offsetPosition = new Vector2(worldPosition.x + worldOffset.x, worldPosition.z + worldOffset.z);
                Vector2 uv = offsetPosition / lengthScale;
                Vector3 displacement = SampleRenderTexturePoint(displacementMap, uv);
                totalDisplacement += displacement;
            }
        }
        
        return totalDisplacement;
    }

    public static Vector3 SampleOceanNormal(Vector3 worldPosition, OceanGenerator oceanGenerator)
    {
        if (oceanGenerator == null) return Vector3.up;

        // Get world offset from infinite ocean manager
        Vector3 worldOffset = Vector3.zero;
       // var infiniteOcean = FindObjectOfType<InfiniteOceanManager>();
    //    if (infiniteOcean != null)
        {
    //        worldOffset = infiniteOcean.GetWorldOffset();
        }

        Vector2 totalDerivatives = Vector2.zero;
        
        // Sample all three cascades with world offset
        for (int i = 0; i < 3; i++)
        {
            var normalMap = oceanGenerator.GetNormalMap(i);
            float lengthScale = oceanGenerator.GetLengthScale(i);
            
            if (normalMap != null && lengthScale > 0)
            {
                Vector2 offsetPosition = new Vector2(worldPosition.x + worldOffset.x, worldPosition.z + worldOffset.z);
                Vector2 uv = offsetPosition / lengthScale;
                Vector3 normalSample = SampleRenderTexturePoint(normalMap, uv);
                Vector2 derivatives = new Vector2(normalSample.x, normalSample.y);
                totalDerivatives += derivatives;
            }
        }
        
        // Convert derivatives to normal
        return Vector3.Normalize(new Vector3(-totalDerivatives.x, 1f, -totalDerivatives.y));
    }

    private static Vector3 SampleRenderTexturePoint(RenderTexture rt, Vector2 uv)
    {
        // Wrap UV coordinates to handle tiling
        uv.x = uv.x - Mathf.Floor(uv.x);
        uv.y = uv.y - Mathf.Floor(uv.y);
        
        // Convert to pixel coordinates
        int x = Mathf.Clamp(Mathf.RoundToInt(uv.x * rt.width), 0, rt.width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(uv.y * rt.height), 0, rt.height - 1);
        
        // Use AsyncGPUReadback for better performance in builds
        // For now, use a synchronous approach with Texture2D
        return SampleRenderTextureCPU(rt, x, y);
    }

    private static Vector3 SampleRenderTextureCPU(RenderTexture source, int x, int y)
    {
        // Create temporary texture for readback
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = source;
        
        // Create temporary texture
        Texture2D temp = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        temp.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
        temp.Apply();
        
        // Get the pixel color
        Color pixel = temp.GetPixel(0, 0);
        
        // Cleanup
        RenderTexture.active = currentRT;
        DestroyImmediate(temp);
        
        return new Vector3(pixel.r, pixel.g, pixel.b);
    }

    // Optimized batch sampling for multiple points
    public static void SampleOceanDataBatch(Vector3[] worldPositions, OceanGenerator oceanGenerator, out float[] heights, out Vector3[] normals)
    {
        int pointCount = worldPositions.Length;
        heights = new float[pointCount];
        normals = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 displacement = SampleOceanHeight(worldPositions[i], oceanGenerator);
            heights[i] = displacement.y;
            normals[i] = SampleOceanNormal(worldPositions[i], oceanGenerator);
        }
    }

    private void OnDestroy()
    {
        if (commandBuffer != null)
        {
            commandBuffer.Release();
        }
        
        if (resultBuffer != null)
        {
            resultBuffer.Release();
        }
    }
} 