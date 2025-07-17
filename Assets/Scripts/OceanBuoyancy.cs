using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BuoyancyPoint
{
    public Vector3 localPosition;
    public float radius = 0.5f;
    public float submergedVolume;
    public float waterHeight;
    public Vector3 waterNormal;
    
    [HideInInspector] public Vector3 worldPosition;
    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public Vector3 force;
}

[System.Serializable]
public class BuoyancySettings
{
    [Header("Physics")]
    public float waterDensity = 1000f;
    public float dragCoefficient = 0.47f;
    public float angularDragCoefficient = 0.3f;
    
    [Header("Wave Interaction")]
    public float waveHeightMultiplier = 1f;
    public bool enableWaveForces = true;
    public float viscosityCoefficient = 0.02f;
    
    [Header("Performance")]
    public int maxSamplesPerFrame = 8;
    public float updateFrequency = 60f;
    public bool useAsyncSampling = true;
}

public class OceanBuoyancy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OceanGenerator oceanGenerator;
    [SerializeField] private Rigidbody rigidBody;
    
    [Header("Buoyancy Configuration")]
    [SerializeField] private BuoyancySettings settings;
    [SerializeField] private List<BuoyancyPoint> buoyancyPoints = new List<BuoyancyPoint>();
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color submergedColor = Color.blue;
    [SerializeField] private Color emergentColor = Color.red;
    
    // Cache for ocean data
    private Material oceanMaterial;
    private RenderTexture[] displacementMaps = new RenderTexture[3];
    private RenderTexture[] normalMaps = new RenderTexture[3];
    private float[] lengthScales = new float[3];
    
    // Performance optimization
    private int currentSampleIndex = 0;
    private float lastUpdateTime;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    
    // Physics data
    private Vector3 centerOfBuoyancy;
    private float totalSubmergedVolume;
    private Vector3 totalBuoyancyForce;
    private Vector3 totalDragForce;
    
    private void Start()
    {
        InitializeBuoyancy();
    }
    
    private void InitializeBuoyancy()
    {
        if (rigidBody == null)
            rigidBody = GetComponent<Rigidbody>();
            
        if (oceanGenerator == null)
            oceanGenerator = FindObjectOfType<OceanGenerator>();
            
        if (oceanGenerator != null)
        {
            // Get references to ocean data
            oceanMaterial = oceanGenerator.GetComponent<OceanGeometry>().GetComponentInChildren<MeshRenderer>().material;
            
            // Cache ocean properties
            lengthScales[0] = oceanMaterial.GetFloat("_LengthScale1");
            lengthScales[1] = oceanMaterial.GetFloat("_LengthScale2");
            lengthScales[2] = oceanMaterial.GetFloat("_LengthScale3");
            
            displacementMaps[0] = oceanMaterial.GetTexture("_DisplacementMap1") as RenderTexture;
            displacementMaps[1] = oceanMaterial.GetTexture("_DisplacementMap2") as RenderTexture;
            displacementMaps[2] = oceanMaterial.GetTexture("_DisplacementMap3") as RenderTexture;
            
            normalMaps[0] = oceanMaterial.GetTexture("_NormalMap1") as RenderTexture;
            normalMaps[1] = oceanMaterial.GetTexture("_NormalMap2") as RenderTexture;
            normalMaps[2] = oceanMaterial.GetTexture("_NormalMap3") as RenderTexture;
        }
        
        // Generate default buoyancy points if none exist
        if (buoyancyPoints.Count == 0)
        {
            GenerateDefaultBuoyancyPoints();
        }
        
        lastUpdateTime = Time.time;
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }
    
    private void GenerateDefaultBuoyancyPoints()
    {
        // Create buoyancy points based on collider bounds
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Bounds bounds = col.bounds;
            Vector3 localBounds = transform.InverseTransformVector(bounds.size);
            
            // Create a grid of buoyancy points under the waterline
            int pointsX = Mathf.Max(2, Mathf.RoundToInt(localBounds.x / 2f));
            int pointsZ = Mathf.Max(2, Mathf.RoundToInt(localBounds.z / 2f));
            
            for (int x = 0; x < pointsX; x++)
            {
                for (int z = 0; z < pointsZ; z++)
                {
                    Vector3 localPos = new Vector3(
                        (x / (float)(pointsX - 1) - 0.5f) * localBounds.x,
                        -localBounds.y * 0.3f, // Place below center
                        (z / (float)(pointsZ - 1) - 0.5f) * localBounds.z
                    );
                    
                    buoyancyPoints.Add(new BuoyancyPoint
                    {
                        localPosition = localPos,
                        radius = Mathf.Min(localBounds.x, localBounds.z) / (pointsX + pointsZ) * 0.5f
                    });
                }
            }
        }
    }
    
    private void FixedUpdate()
    {
        if (oceanGenerator == null || rigidBody == null)
            return;
            
        // Check if we need to update this frame
        if (ShouldUpdateThisFrame())
        {
            UpdateBuoyancyPhysics();
            ApplyForces();
        }
    }
    
    private bool ShouldUpdateThisFrame()
    {
        float timeSinceLastUpdate = Time.time - lastUpdateTime;
        
        // Always update if enough time has passed
        if (timeSinceLastUpdate >= 1f / settings.updateFrequency)
            return true;
            
        // Update more frequently if object is moving quickly
        float positionDelta = Vector3.Distance(transform.position, lastPosition);
        float rotationDelta = Quaternion.Angle(transform.rotation, lastRotation);
        
        if (positionDelta > 0.1f || rotationDelta > 5f)
            return true;
            
        return false;
    }
    
    private void UpdateBuoyancyPhysics()
    {
        lastUpdateTime = Time.time;
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        
        // Reset physics data
        totalSubmergedVolume = 0f;
        totalBuoyancyForce = Vector3.zero;
        totalDragForce = Vector3.zero;
        centerOfBuoyancy = Vector3.zero;
        
        // Update buoyancy points
        int pointsToUpdate = settings.useAsyncSampling ? 
            Mathf.Min(settings.maxSamplesPerFrame, buoyancyPoints.Count) : 
            buoyancyPoints.Count;
            
        for (int i = 0; i < pointsToUpdate; i++)
        {
            int index = settings.useAsyncSampling ? 
                (currentSampleIndex + i) % buoyancyPoints.Count : i;
                
            UpdateBuoyancyPoint(buoyancyPoints[index]);
        }
        
        if (settings.useAsyncSampling)
            currentSampleIndex = (currentSampleIndex + pointsToUpdate) % buoyancyPoints.Count;
        
        // Calculate center of buoyancy
        if (totalSubmergedVolume > 0f)
        {
            centerOfBuoyancy /= totalSubmergedVolume;
        }
    }
    
    private void UpdateBuoyancyPoint(BuoyancyPoint point)
    {
        // Transform to world space
        point.worldPosition = transform.TransformPoint(point.localPosition);
        
        // Sample ocean height and normal at this position
        SampleOceanData(point.worldPosition, out point.waterHeight, out point.waterNormal);
        
        // Calculate submersion
        float submersion = point.waterHeight - point.worldPosition.y;
        
        if (submersion > 0f)
        {
            // Calculate submerged volume (sphere cap approximation)
            float sphereVolume = (4f/3f) * Mathf.PI * point.radius * point.radius * point.radius;
            float submersionRatio = Mathf.Clamp01(submersion / (point.radius * 2f));
            point.submergedVolume = sphereVolume * submersionRatio;
            
            // Update totals
            totalSubmergedVolume += point.submergedVolume;
            centerOfBuoyancy += point.worldPosition * point.submergedVolume;
            
            // Calculate buoyancy force
            Vector3 buoyancyForce = point.waterNormal * (point.submergedVolume * settings.waterDensity * 9.81f);
            point.force = buoyancyForce;
            totalBuoyancyForce += buoyancyForce;
            
            // Calculate velocity and drag
            point.velocity = rigidBody.GetPointVelocity(point.worldPosition);
            
            if (settings.enableWaveForces)
            {
                // Add drag force
                Vector3 dragForce = -point.velocity * settings.dragCoefficient * point.submergedVolume * settings.waterDensity;
                point.force += dragForce;
                totalDragForce += dragForce;
                
                // Add viscosity effects
                Vector3 viscosityForce = -point.velocity * settings.viscosityCoefficient * submersionRatio;
                point.force += viscosityForce;
            }
        }
        else
        {
            point.submergedVolume = 0f;
            point.force = Vector3.zero;
        }
    }
    
    private void SampleOceanData(Vector3 worldPosition, out float height, out Vector3 normal)
    {
        // Use the proper ocean data reader
        Vector3 displacement = OceanDataReader.SampleOceanHeight(worldPosition, oceanGenerator);
        height = displacement.y * settings.waveHeightMultiplier;
        normal = OceanDataReader.SampleOceanNormal(worldPosition, oceanGenerator);
    }
    
    private void ApplyForces()
    {
        if (totalSubmergedVolume <= 0f)
            return;
            
        // Apply buoyancy force at center of buoyancy
        rigidBody.AddForceAtPosition(totalBuoyancyForce, centerOfBuoyancy);
        
        // Apply drag forces
        rigidBody.AddForce(totalDragForce);
        
        // Apply angular drag
        Vector3 angularDrag = -rigidBody.angularVelocity * settings.angularDragCoefficient * totalSubmergedVolume;
        rigidBody.AddTorque(angularDrag);
        
        // Apply individual point forces for more detailed interaction
        foreach (var point in buoyancyPoints)
        {
            if (point.submergedVolume > 0f)
            {
                rigidBody.AddForceAtPosition(point.force * 0.1f, point.worldPosition); // Scaled down since already applied globally
            }
        }
    }
    
    // Public API for external systems
    public float GetWaterHeightAtPosition(Vector3 worldPosition)
    {
        SampleOceanData(worldPosition, out float height, out Vector3 normal);
        return height;
    }
    
    public Vector3 GetWaterNormalAtPosition(Vector3 worldPosition)
    {
        SampleOceanData(worldPosition, out float height, out Vector3 normal);
        return normal;
    }
    
    public bool IsPointSubmerged(Vector3 worldPosition)
    {
        float waterHeight = GetWaterHeightAtPosition(worldPosition);
        return worldPosition.y < waterHeight;
    }
    
    public float GetSubmergedPercentage()
    {
        if (buoyancyPoints.Count == 0) return 0f;
        
        int submergedCount = 0;
        foreach (var point in buoyancyPoints)
        {
            if (point.submergedVolume > 0f)
                submergedCount++;
        }
        
        return (float)submergedCount / buoyancyPoints.Count;
    }
    
    // Gizmos for debugging
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        foreach (var point in buoyancyPoints)
        {
            Vector3 worldPos = Application.isPlaying ? point.worldPosition : transform.TransformPoint(point.localPosition);
            
            Gizmos.color = point.submergedVolume > 0f ? submergedColor : emergentColor;
            Gizmos.DrawWireSphere(worldPos, point.radius);
            
            if (Application.isPlaying && point.submergedVolume > 0f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(worldPos, worldPos + point.waterNormal);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(worldPos, worldPos + point.force.normalized * 2f);
            }
        }
        
        if (Application.isPlaying && totalSubmergedVolume > 0f)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(centerOfBuoyancy, 0.5f);
        }
    }
} 