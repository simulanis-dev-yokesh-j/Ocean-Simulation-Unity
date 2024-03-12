using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Buoyancy : MonoBehaviour
{
    [SerializeField] private OceanGenerator _oceanGenerator;
    [SerializeField] private float _forceModifier = 1;
    
    private const float _gravity = 9.81f;
    
    private float[] _waterHeights;
    private float _lengthScale;
    private Vector2Int _textureSize;
    private Vector3[] _buoyancyPoints;
    
    private BoxCollider _collider;
    private Rigidbody _rigidbody;
    private RenderTexture _heightMap;
    private Texture2D _heightMapTexture;
    private AsyncGPUReadbackRequest _request;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        _heightMap = _oceanGenerator.GetOceanCascade1().GetTimeDependantSpectrumData().HeightMap;
        _textureSize = new Vector2Int(_heightMap.width, _heightMap.height);
        _lengthScale = _oceanGenerator.GetLengthScale1();
        _heightMapTexture = new Texture2D(_textureSize.x, _textureSize.y, TextureFormat.RGBAHalf, false);
        _heightMapTexture.wrapMode = TextureWrapMode.Repeat;

        GenerateBuoyancyPoints(2, 3);
        RequestWaterHeight();
    }

    private void GenerateBuoyancyPoints(int xCount, int zCount)
    {
        _buoyancyPoints = new Vector3[xCount * zCount];
        _waterHeights = new float[xCount * zCount];
    
        var bounds = _collider.bounds;
        var size = bounds.size;
        var min = bounds.min;
    
        float dx = size.x / (xCount - 1);
        float dz = size.z / (zCount - 1);
    
        for (int i = 0; i < xCount; i++)
        {
            for (int j = 0; j < zCount; j++)
            {
                float x = min.x + i * dx;
                float z = min.z + j * dz;
                _buoyancyPoints[i * zCount + j] = transform.InverseTransformPoint(new Vector3(x, min.y, z));
                _waterHeights[i * zCount + j] = 0;
            }
        }
    }

    private void FixedUpdate()
    {
        for(int i = 0; i < _buoyancyPoints.Length; i++)
        {
            var waterHeight = _waterHeights[i];
            var point = transform.TransformPoint(_buoyancyPoints[i]);
            
            // Only apply buoyancy if the point is below the water
            if (point.y > waterHeight) 
                continue;
            
            var displacedVolume = DisplacedVolume(point.y, waterHeight);
            var buoyancyForce = _gravity * displacedVolume * Vector3.up * _forceModifier;
        
            // add force at buoyancy point
            _rigidbody.AddForceAtPosition(buoyancyForce, point);
        }
    }

    private float DisplacedVolume(float pointHeight, float waterHeight)
    {
        var submergedHeight = Mathf.Max(0, waterHeight - pointHeight);
        var volume = submergedHeight * _lengthScale * _lengthScale; // Use the actual submerged height
        return volume;
    }

    
    private void RequestWaterHeight()
    {
        _request = AsyncGPUReadback.Request(_heightMap, 0, 0, _textureSize.x, 0, _textureSize.y, 0, 1, TextureFormat.RGBAHalf, OnCompleteReadback);
    }

    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.Log("Failed to read GPU texture");
            return;
        }

        var data = request.GetData<byte>();
        _heightMapTexture.LoadRawTextureData(data);
        _heightMapTexture.Apply();

        for (int i = 0; i < _buoyancyPoints.Length; i++)
        {
            var point = _buoyancyPoints[i];
            var worldPoint = transform.TransformPoint(point);
            var uv = worldPoint / _lengthScale;
            var height = _heightMapTexture.GetPixelBilinear(uv.x, uv.y).g;
            _waterHeights[i] = height;
        }

        RequestWaterHeight();
    }

    // private void OnDrawGizmos()
    // {
    //     //Draw the buoyancy points
    //     if (_buoyancyPoints == null) return;
    //     
    //     Gizmos.color = Color.blue;
    //     foreach (var point in _buoyancyPoints)
    //     {
    //         var worldPoint = transform.TransformPoint(point);
    //         Gizmos.DrawSphere(worldPoint, 0.1f);
    //     }
    // }
}