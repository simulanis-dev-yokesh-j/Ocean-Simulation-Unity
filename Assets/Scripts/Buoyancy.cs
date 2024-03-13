using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private float _buoyancyFactor = 1f;

    private const float _gravity = 9.81f;
    private List<BuoyanceVoxel> _voxels;

    
    private OceanCpuData _oceanCpuData;
    private BoxCollider _collider;
    private Rigidbody _rigidbody;


    private void Awake()
    {
        _oceanCpuData = _oceanGenerator.GetComponent<OceanCpuData>();
        _collider = GetComponent<BoxCollider>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        PopulateVoxels();
        
        _oceanCpuData.OnHeightMapReceived += OnHeightMapReceived;
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
            _rigidbody.AddForceAtPosition(buoyancyForce * _buoyancyFactor, voxel.GetPosition());
        }
    }

    private void FixedUpdate()
    {
        ApplyForces();
    }
    
    private void OnHeightMapReceived(Texture2D heightTexture)
    {
        var size = heightTexture.width;
        
        foreach (var voxel in _voxels)
        {
            var worldPoint = voxel.GetPosition();
            
            var lengthScale1 = _oceanGenerator.GetLengthScale1();
            var lengthScale2 = _oceanGenerator.GetLengthScale2();
            var lengthScale3 = _oceanGenerator.GetLengthScale3();

            int x = Mathf.FloorToInt((worldPoint.x % lengthScale1) / lengthScale1 * size);
            int y = Mathf.FloorToInt((worldPoint.z % lengthScale1) / lengthScale1 * size);
            var pixel1 = heightTexture.GetPixel(x, y);
            
            x = Mathf.FloorToInt((worldPoint.x % lengthScale2) / lengthScale2 * size);
            y = Mathf.FloorToInt((worldPoint.z % lengthScale2) / lengthScale2 * size);
            var pixel2 = heightTexture.GetPixel(x, y);
            
            x = Mathf.FloorToInt((worldPoint.x % lengthScale3) / lengthScale3 * size);
            y = Mathf.FloorToInt((worldPoint.z % lengthScale3) / lengthScale3 * size);
            var pixel3 = heightTexture.GetPixel(x, y);

            var height = pixel1.r + pixel2.g + pixel3.b;
            
            voxel.SetWaterHeight(height);
        }
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
}