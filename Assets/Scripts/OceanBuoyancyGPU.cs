using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct GPUSamplePoint
{
    public Vector3 worldPosition;
    public float waterHeight;
    public Vector3 waterNormal;
    public Vector3 waterVelocity;
    public Vector4 additionalData; // foam, displacement magnitude, etc.
}

public class OceanBuoyancyGPU : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OceanGenerator oceanGenerator;
    [SerializeField] private Rigidbody rigidBody;
    [SerializeField] private ComputeShader oceanSamplerShader;
    
    [Header("Buoyancy Configuration")]
    [SerializeField] private BuoyancySettings settings;
    [SerializeField] private List<BuoyancyPoint> buoyancyPoints = new List<BuoyancyPoint>();
    
    [Header("GPU Sampling")]
    [SerializeField] private bool useGPUSampling = true;
    [SerializeField] private int maxGPUSamples = 64;
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color submergedColor = Color.blue;
    [SerializeField] private Color emergentColor = Color.red;
    
    // GPU compute resources
    private ComputeBuffer samplePointsBuffer;
    private GPUSamplePoint[] samplePointsArray;
    private int oceanHeightsKernel;
    private int oceanDataKernel;
    
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
    
    // Wave force calculation
    private Vector3[] previousWaterVelocities;
    private float waveForceAccumulator;
    
    private void Start()
    {
        InitializeBuoyancy();
        InitializeGPUResources();
    }
    
    private void InitializeBuoyancy()
    {
        if (rigidBody == null)
            rigidBody = GetComponent<Rigidbody>();
            
        if (oceanGenerator == null)
            oceanGenerator = FindObjectOfType<OceanGenerator>();
        
        // Generate default buoyancy points if none exist
        if (buoyancyPoints.Count == 0)
        {
            GenerateDefaultBuoyancyPoints();
        }
        
        // Initialize wave force tracking
        previousWaterVelocities = new Vector3[buoyancyPoints.Count];
        
        lastUpdateTime = Time.time;
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }
    
    private void InitializeGPUResources()
    {
        if (!useGPUSampling || oceanSamplerShader == null)
            return;
            
        // Find compute kernels
        oceanHeightsKernel = oceanSamplerShader.FindKernel("SampleOceanHeights");
        oceanDataKernel = oceanSamplerShader.FindKernel("SampleOceanData");
        
        // Create compute buffer for sample points
        int bufferSize = Mathf.Min(maxGPUSamples, buoyancyPoints.Count);
        samplePointsBuffer = new ComputeBuffer(bufferSize, System.Runtime.InteropServices.Marshal.SizeOf<GPUSamplePoint>());
        samplePointsArray = new GPUSamplePoint[bufferSize];
        
        // Bind buffer to compute shader
        oceanSamplerShader.SetBuffer(oceanHeightsKernel, "SamplePoints", samplePointsBuffer);
        oceanSamplerShader.SetBuffer(oceanDataKernel, "SamplePoints", samplePointsBuffer);
    }
    
    private void GenerateDefaultBuoyancyPoints()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Bounds bounds = col.bounds;
            Vector3 localBounds = transform.InverseTransformVector(bounds.size);
            
            // Create hull-based buoyancy points for better ship simulation
            GenerateHullBuoyancyPoints(localBounds);
        }
    }
    
    private void GenerateHullBuoyancyPoints(Vector3 localBounds)
    {
        // Create points along the hull bottom and sides for realistic ship physics
        int pointsLength = Mathf.Max(3, Mathf.RoundToInt(localBounds.z / 3f));
        int pointsWidth = Mathf.Max(3, Mathf.RoundToInt(localBounds.x / 3f));
        int pointsHeight = 2; // Hull bottom and sides
        
        for (int z = 0; z < pointsLength; z++)
        {
            for (int x = 0; x < pointsWidth; x++)
            {
                for (int y = 0; y < pointsHeight; y++)
                {
                    Vector3 localPos = new Vector3(
                        (x / (float)(pointsWidth - 1) - 0.5f) * localBounds.x,
                        -localBounds.y * 0.5f + y * localBounds.y * 0.3f,
                        (z / (float)(pointsLength - 1) - 0.5f) * localBounds.z
                    );
                    
                    float radius = Mathf.Min(localBounds.x, localBounds.z) / (pointsWidth + pointsLength) * 0.7f;
                    
                    buoyancyPoints.Add(new BuoyancyPoint
                    {
                        localPosition = localPos,
                        radius = radius
                    });
                }
            }
        }
    }
    
    private void FixedUpdate()
    {
        if (oceanGenerator == null || rigidBody == null)
            return;
            
        if (ShouldUpdateThisFrame())
        {
            if (useGPUSampling && samplePointsBuffer != null)
            {
                UpdateBuoyancyPhysicsGPU();
            }
            else
            {
                UpdateBuoyancyPhysicsCPU();
            }
            
            ApplyForces();
        }
    }
    
    private bool ShouldUpdateThisFrame()
    {
        float timeSinceLastUpdate = Time.time - lastUpdateTime;
        
        if (timeSinceLastUpdate >= 1f / settings.updateFrequency)
            return true;
            
        float positionDelta = Vector3.Distance(transform.position, lastPosition);
        float rotationDelta = Quaternion.Angle(transform.rotation, lastRotation);
        
        return positionDelta > 0.1f || rotationDelta > 5f;
    }
    
    private void UpdateBuoyancyPhysicsGPU()
    {
        lastUpdateTime = Time.time;
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        
        ResetPhysicsData();
        
        // Prepare sample points for GPU
        int samplesToProcess = Mathf.Min(samplePointsArray.Length, buoyancyPoints.Count);
        
        for (int i = 0; i < samplesToProcess; i++)
        {
            int pointIndex = (currentSampleIndex + i) % buoyancyPoints.Count;
            var point = buoyancyPoints[pointIndex];
            
            samplePointsArray[i] = new GPUSamplePoint
            {
                worldPosition = transform.TransformPoint(point.localPosition),
                waterHeight = 0f,
                waterNormal = Vector3.up,
                waterVelocity = Vector3.zero,
                additionalData = Vector4.zero
            };
        }
        
        // Upload to GPU
        samplePointsBuffer.SetData(samplePointsArray, 0, 0, samplesToProcess);
        
        // Set ocean data to compute shader
        SetOceanDataToShader();
        
        // Dispatch compute shader
        oceanSamplerShader.SetInt("SampleCount", samplesToProcess);
        int threadGroups = Mathf.CeilToInt(samplesToProcess / 64f);
        oceanSamplerShader.Dispatch(oceanDataKernel, threadGroups, 1, 1);
        
        // Read back results
        samplePointsBuffer.GetData(samplePointsArray, 0, 0, samplesToProcess);
        
        // Process results and update buoyancy points
        for (int i = 0; i < samplesToProcess; i++)
        {
            int pointIndex = (currentSampleIndex + i) % buoyancyPoints.Count;
            ProcessGPUSampleResult(pointIndex, samplePointsArray[i]);
        }
        
        currentSampleIndex = (currentSampleIndex + samplesToProcess) % buoyancyPoints.Count;
        CalculateCenterOfBuoyancy();
    }
    
    private void SetOceanDataToShader()
    {
        // Set displacement maps
        for (int i = 0; i < 3; i++)
        {
            var displacementMap = oceanGenerator.GetDisplacementMap(i);
            var normalMap = oceanGenerator.GetNormalMap(i);
            float lengthScale = oceanGenerator.GetLengthScale(i);
            
            if (displacementMap != null)
            {
                oceanSamplerShader.SetTexture(oceanDataKernel, $"DisplacementMap{i + 1}", displacementMap);
            }
            
            if (normalMap != null)
            {
                oceanSamplerShader.SetTexture(oceanDataKernel, $"NormalMap{i + 1}", normalMap);
            }
            
            oceanSamplerShader.SetFloat($"LengthScale{i + 1}", lengthScale);
        }
        
        oceanSamplerShader.SetFloat("WaveHeightMultiplier", settings.waveHeightMultiplier);
    }
    
    private void ProcessGPUSampleResult(int pointIndex, GPUSamplePoint sampleResult)
    {
        var point = buoyancyPoints[pointIndex];
        
        point.worldPosition = sampleResult.worldPosition;
        point.waterHeight = sampleResult.waterHeight;
        point.waterNormal = sampleResult.waterNormal;
        
        // Calculate submersion
        float submersion = point.waterHeight - point.worldPosition.y;
        
        if (submersion > 0f)
        {
            CalculateBuoyancyForPoint(point, submersion);
            
            // Advanced wave force calculation using GPU-computed water velocity
            if (settings.enableWaveForces)
            {
                Vector3 waterVelocity = sampleResult.waterVelocity;
                Vector3 relativeVelocity = rigidBody.GetPointVelocity(point.worldPosition) - waterVelocity;
                
                // Wave impact force
                float waveHeight = sampleResult.additionalData.z;
                float waveSlope = sampleResult.additionalData.y;
                Vector3 waveForce = CalculateWaveImpactForce(point, relativeVelocity, waveHeight, waveSlope);
                
                point.force += waveForce;
                totalBuoyancyForce += waveForce;
                
                // Store for next frame velocity calculation
                if (pointIndex < previousWaterVelocities.Length)
                {
                    previousWaterVelocities[pointIndex] = waterVelocity;
                }
            }
        }
        else
        {
            point.submergedVolume = 0f;
            point.force = Vector3.zero;
        }
        
        buoyancyPoints[pointIndex] = point;
    }
    
    private Vector3 CalculateWaveImpactForce(BuoyancyPoint point, Vector3 relativeVelocity, float waveHeight, float waveSlope)
    {
        // Advanced wave force calculation
        Vector3 waveForce = Vector3.zero;
        
        // Slamming force (when hull hits waves)
        float slammingIntensity = Mathf.Max(0f, -relativeVelocity.y) * waveSlope;
        if (slammingIntensity > 0.1f)
        {
            Vector3 slammingForce = Vector3.up * slammingIntensity * point.submergedVolume * settings.waterDensity * 50f;
            waveForce += slammingForce;
        }
        
        // Wave drift force
        Vector3 waveDrift = point.waterNormal * waveHeight * point.submergedVolume * settings.waterDensity * 0.5f;
        waveForce += waveDrift;
        
        // Added mass effect
        Vector3 addedMassForce = -relativeVelocity * point.submergedVolume * settings.waterDensity * 0.3f;
        waveForce += addedMassForce;
        
        return waveForce;
    }
    
    private void UpdateBuoyancyPhysicsCPU()
    {
        // Fallback CPU implementation (simplified version of original)
        lastUpdateTime = Time.time;
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        
        ResetPhysicsData();
        
        int pointsToUpdate = settings.useAsyncSampling ? 
            Mathf.Min(settings.maxSamplesPerFrame, buoyancyPoints.Count) : 
            buoyancyPoints.Count;
            
        for (int i = 0; i < pointsToUpdate; i++)
        {
            int index = settings.useAsyncSampling ? 
                (currentSampleIndex + i) % buoyancyPoints.Count : i;
                
            UpdateBuoyancyPointCPU(buoyancyPoints[index]);
        }
        
        if (settings.useAsyncSampling)
            currentSampleIndex = (currentSampleIndex + pointsToUpdate) % buoyancyPoints.Count;
            
        CalculateCenterOfBuoyancy();
    }
    
    private void UpdateBuoyancyPointCPU(BuoyancyPoint point)
    {
        point.worldPosition = transform.TransformPoint(point.localPosition);
        
        // Use proper ocean sampling
        Vector3 displacement = OceanDataReader.SampleOceanHeight(point.worldPosition, oceanGenerator);
        point.waterHeight = displacement.y * settings.waveHeightMultiplier;
        point.waterNormal = OceanDataReader.SampleOceanNormal(point.worldPosition, oceanGenerator);
        
        float submersion = point.waterHeight - point.worldPosition.y;
        
        if (submersion > 0f)
        {
            CalculateBuoyancyForPoint(point, submersion);
        }
        else
        {
            point.submergedVolume = 0f;
            point.force = Vector3.zero;
        }
    }
    
    private void CalculateBuoyancyForPoint(BuoyancyPoint point, float submersion)
    {
        // Calculate submerged volume
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
        
        // Calculate drag
        if (settings.enableWaveForces)
        {
            point.velocity = rigidBody.GetPointVelocity(point.worldPosition);
            Vector3 dragForce = -point.velocity * settings.dragCoefficient * point.submergedVolume * settings.waterDensity;
            point.force += dragForce;
            totalDragForce += dragForce;
        }
    }
    
    private void ResetPhysicsData()
    {
        totalSubmergedVolume = 0f;
        totalBuoyancyForce = Vector3.zero;
        totalDragForce = Vector3.zero;
        centerOfBuoyancy = Vector3.zero;
    }
    
    private void CalculateCenterOfBuoyancy()
    {
        if (totalSubmergedVolume > 0f)
        {
            centerOfBuoyancy /= totalSubmergedVolume;
        }
    }
    
    private void ApplyForces()
    {
        if (totalSubmergedVolume <= 0f)
            return;
            
        // Apply buoyancy force at center of buoyancy
        rigidBody.AddForceAtPosition(totalBuoyancyForce, centerOfBuoyancy);
        
        // Apply drag forces
        rigidBody.AddForce(totalDragForce);
        
        // Apply angular drag based on submerged volume
        Vector3 angularDrag = -rigidBody.angularVelocity * settings.angularDragCoefficient * totalSubmergedVolume;
        rigidBody.AddTorque(angularDrag);
        
        // Apply stability enhancement for ships
        ApplyStabilityForces();
    }
    
    private void ApplyStabilityForces()
    {
        // Anti-roll stabilization
        float rollAngle = Vector3.Angle(transform.up, Vector3.up);
        if (rollAngle > 5f)
        {
            Vector3 stabilizingTorque = Vector3.Cross(transform.up, Vector3.up) * rollAngle * totalSubmergedVolume * 10f;
            rigidBody.AddTorque(stabilizingTorque);
        }
        
        // Pitch damping
        float pitchVelocity = Vector3.Dot(rigidBody.angularVelocity, transform.right);
        Vector3 pitchDamping = -transform.right * pitchVelocity * totalSubmergedVolume * 5f;
        rigidBody.AddTorque(pitchDamping);
    }
    
    // Public API
    public float GetWaterHeightAtPosition(Vector3 worldPosition)
    {
        if (useGPUSampling && samplePointsBuffer != null)
        {
            // Use GPU sampling for single point (can be optimized)
            var tempArray = new GPUSamplePoint[1];
            tempArray[0] = new GPUSamplePoint { worldPosition = worldPosition };
            
            var tempBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf<GPUSamplePoint>());
            tempBuffer.SetData(tempArray);
            
            oceanSamplerShader.SetBuffer(oceanHeightsKernel, "SamplePoints", tempBuffer);
            SetOceanDataToShader();
            oceanSamplerShader.SetInt("SampleCount", 1);
            oceanSamplerShader.Dispatch(oceanHeightsKernel, 1, 1, 1);
            
            tempBuffer.GetData(tempArray);
            tempBuffer.Release();
            
            return tempArray[0].waterHeight;
        }
        
        return 0f; // Fallback CPU implementation
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
    
    private void OnDestroy()
    {
        if (samplePointsBuffer != null)
        {
            samplePointsBuffer.Release();
            samplePointsBuffer = null;
        }
    }
    
    // Debug visualization
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