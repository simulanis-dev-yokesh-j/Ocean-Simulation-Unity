using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class BuoyanceVoxel
{
    private Transform _parent;
    private float _size;
    private Vector3 _offset;
    private float _waterHeight;
    
    public BuoyanceVoxel(Transform parent, float size, Vector3 offset)
    {
        _parent = parent;
        _size = size;
        _offset = offset;
    }

    public void SetWaterHeight(float height)
    {
        _waterHeight = height;
    }

    public Vector3 GetPosition()
    {
        return _parent.TransformPoint(_offset);
    }

    private float GetHeightDifference()
    {
        var worldPoint = GetPosition();
        return Mathf.Max(_waterHeight - (worldPoint.y - _size/2), 0);
    }

    public bool IsUnderWater()
    {
        return GetHeightDifference() > 0;
    }

    public float GetDisplacedVolume()
    {
        var newHeight = GetHeightDifference();
        return newHeight * _size * _size;
    }
}

public class Buoyancy : MonoBehaviour
{
    [SerializeField] private OceanGenerator _oceanGenerator;
    [SerializeField] private float _voxelSize = 1;

    private const float _gravity = 9.81f;
    private float _lengthScale1;
    private float _lengthScale2;
    private float _lengthScale3;
    private List<BuoyanceVoxel> _voxels;
    private bool _requested;

    private BoxCollider _collider;
    private Rigidbody _rigidbody;
    private RenderTexture _heightMap;
    private Texture2D _heightMapTexture;
    

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        _heightMap = _oceanGenerator.GetCascadeHeightsMap();
        _lengthScale1 = _oceanGenerator.GetLengthScale1();
        _lengthScale2 = _oceanGenerator.GetLengthScale2();
        _lengthScale3 = _oceanGenerator.GetLengthScale3();
        _heightMapTexture = new Texture2D(_heightMap.width, _heightMap.height, TextureFormat.RGBAHalf, false);
        _heightMapTexture.wrapMode = TextureWrapMode.Repeat;
        _oceanGenerator.OnRuntimeUpdate += OnRuntimeUpdate;
        
        PopulateVoxels();
    }

    private void OnRuntimeUpdate()
    {
        _heightMap = _oceanGenerator.GetCascadeHeightsMap();
        _lengthScale1 = _oceanGenerator.GetLengthScale1();
        _lengthScale2 = _oceanGenerator.GetLengthScale2();
        _lengthScale3 = _oceanGenerator.GetLengthScale3();
    }

    private void PopulateVoxels()
    {
        var bounds = _collider.bounds;
        var size = bounds.size;
        var min = bounds.min;

        int xCount = Mathf.FloorToInt(size.x / _voxelSize);
        int yCount = Mathf.FloorToInt(size.y / _voxelSize);
        int zCount = Mathf.FloorToInt(size.z / _voxelSize);

        _voxels = new List<BuoyanceVoxel>();

        for (int i = 0; i < xCount; i++)
        {
            for (int j = 0; j < yCount; j++)
            {
                for (int k = 0; k < zCount; k++)
                {
                    float x = min.x + i * _voxelSize + _voxelSize / 2;
                    float y = min.y + j * _voxelSize + _voxelSize / 2;
                    float z = min.z + k * _voxelSize + _voxelSize / 2;
                    Vector3 offset = transform.InverseTransformPoint(new Vector3(x, y, z));
                    _voxels.Add(new BuoyanceVoxel(transform, _voxelSize, offset));
                }
            }
        }
    }

    private void ApplyForces()
    {
        foreach (var voxel in _voxels)
        {
            if(!voxel.IsUnderWater())
                continue;
            
            var displacedVolume = voxel.GetDisplacedVolume();
            var buoyancyForce = displacedVolume * _gravity * Vector3.up  / _voxels.Count;
            _rigidbody.AddForceAtPosition(buoyancyForce, voxel.GetPosition());
        }
    }

    private void RequestWaterHeight()
    {
        if(_requested)
            return;
        
        _requested = true;
        AsyncGPUReadback.Request(_heightMap, 0, 0, _heightMap.width, 0, _heightMap.height, 0, 1, TextureFormat.RGBAHalf, OnCompleteReadback);
    }

    private void FixedUpdate()
    {
        RequestWaterHeight();
        ApplyForces();
        
        //Apply forward force
        _rigidbody.AddForce(transform.forward * 2f);
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
        var size = _heightMapTexture.width;

        foreach (var voxel in _voxels)
        {
            var worldPoint = voxel.GetPosition();

            int x = Mathf.FloorToInt((worldPoint.x % _lengthScale1) / _lengthScale1 * size);
            int y = Mathf.FloorToInt((worldPoint.z % _lengthScale1) / _lengthScale1 * size);
            var pixel1 = _heightMapTexture.GetPixel(x, y);
            
            x = Mathf.FloorToInt((worldPoint.x % _lengthScale2) / _lengthScale2 * size);
            y = Mathf.FloorToInt((worldPoint.z % _lengthScale2) / _lengthScale2 * size);
            var pixel2 = _heightMapTexture.GetPixel(x, y);
            
            x = Mathf.FloorToInt((worldPoint.x % _lengthScale3) / _lengthScale3 * size);
            y = Mathf.FloorToInt((worldPoint.z % _lengthScale3) / _lengthScale3 * size);
            var pixel3 = _heightMapTexture.GetPixel(x, y);

            var height = pixel1.r + pixel2.g + pixel3.b;
            
            voxel.SetWaterHeight(height);
        }
        
        _requested = false;
    }

    private void OnDrawGizmos()
    {
        if (_voxels == null)
            return;

        foreach (var voxel in _voxels)
        {
            if (voxel.IsUnderWater())
                Gizmos.color = Color.blue;
            else
                Gizmos.color = Color.red;
            
            Gizmos.DrawWireCube(voxel.GetPosition(), Vector3.one * _voxelSize);
        }
    }

    private void OnDestroy()
    {
        _oceanGenerator.OnRuntimeUpdate -= OnRuntimeUpdate;
    }
}