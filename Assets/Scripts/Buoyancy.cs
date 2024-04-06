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
        
        _oceanCpuData.OnDisplacementsReceived += OnDisplacementsReceived;
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
    
    private void OnDisplacementsReceived(Texture2D dis1, Texture2D dis2, Texture2D dis3)
    {
        foreach (var voxel in _voxels)
        {
            var worldPoint = voxel.GetPosition();
            float height = SampleHeight(worldPoint.x, worldPoint.z, dis1, dis2, dis3);
            voxel.SetWaterHeight(height);
        }
    }

    private float SampleHeight(float worldX, float worldZ, Texture2D dis1, Texture2D dis2, Texture2D dis3)
    {
        var size = dis1.width;
        var lengthScale1 = _oceanGenerator.GetCascadeSettings1().LengthScale;
        var lengthScale2 = _oceanGenerator.GetCascadeSettings2().LengthScale;
        var lengthScale3 = _oceanGenerator.GetCascadeSettings3().LengthScale;
        var sampleX = worldX;
        var sampleZ = worldZ;
        float height = 0;
        
        for (int i = 0; i < 8; i++)
        {
            float disX = 0;
            float disY = 0;
            float disZ = 0;
            
            int x1 = Mathf.FloorToInt((sampleX % lengthScale1) / lengthScale1 * size);
            int y1 = Mathf.FloorToInt((sampleZ % lengthScale1) / lengthScale1 * size);
            
            int x2 = Mathf.FloorToInt((sampleX % lengthScale2) / lengthScale2 * size);
            int y2 = Mathf.FloorToInt((sampleZ % lengthScale2) / lengthScale2 * size);  
            
            int x3 = Mathf.FloorToInt((sampleX % lengthScale3) / lengthScale3 * size);
            int y3 = Mathf.FloorToInt((sampleZ % lengthScale3) / lengthScale3 * size);
            
            disX += dis1.GetPixel(x1, y1).r;
            disY += dis1.GetPixel(x1, y1).g;
            disZ += dis1.GetPixel(x1, y1).b;
            
            disX += dis2.GetPixel(x2, y2).r;
            disY += dis2.GetPixel(x2, y2).g;
            disZ += dis2.GetPixel(x2, y2).b;
            
            disX += dis3.GetPixel(x3, y3).r;
            disY += dis3.GetPixel(x3, y3).g;
            disZ += dis3.GetPixel(x3, y3).b;

            // print($"({sampleX}, {sampleZ}): ({disX}, {disY}, {disZ})");
            sampleX = worldX - disX;
            height = disY;
            sampleZ = worldZ - disZ;
        }

        return height;
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