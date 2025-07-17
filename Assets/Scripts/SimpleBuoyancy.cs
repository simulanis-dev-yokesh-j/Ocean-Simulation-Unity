using UnityEngine;

public class SimpleBuoyancy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OceanGenerator oceanGenerator;
    [SerializeField] private Rigidbody rigidBody;
    
    [Header("Buoyancy Settings")]
    [SerializeField] private float waterDensity = 1000f;
    [SerializeField] private float objectVolume = 10f; // Estimated volume of the object
    [SerializeField] private float submergedVolumeMultiplier = 5f; // Multiplier for stronger buoyancy
    [SerializeField] private float maxBuoyancyForce = 500000f; // Much higher limit
    
    [Header("Damping")]
    [SerializeField] private float linearDrag = 2f; // Increased to reduce bouncing
    [SerializeField] private float angularDrag = 5f; // Increased for more stability
    
    [Header("Multi-Point Buoyancy")]
    [SerializeField] private bool useMultiplePoints = true;
    [SerializeField] private Vector3[] buoyancyPoints = new Vector3[]
    {
        new Vector3(-1f, 0f, 1f),   // Front left
        new Vector3(1f, 0f, 1f),    // Front right
        new Vector3(-1f, 0f, -1f),  // Back left
        new Vector3(1f, 0f, -1f),   // Back right
        new Vector3(0f, 0f, 0f),    // Center
    };
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showGizmos = true;
    
    // Runtime variables
    private float currentWaterHeight = 0f;
    private Vector3 currentWaterNormal = Vector3.up;
    private float submergedPercentage = 0f;
    private Vector3 buoyancyForce = Vector3.zero;
    private Vector3 totalTorque = Vector3.zero;
    
    private int sampleFrameCounter = 0;
    private const int sampleFrameInterval = 2; // Only sample every 2 FixedUpdates
    
    private void Start()
    {
        // Get components if not assigned
        if (rigidBody == null)
            rigidBody = GetComponent<Rigidbody>();
            
        if (oceanGenerator == null)
            oceanGenerator = FindObjectOfType<OceanGenerator>();
            
        // Set reasonable rigidbody defaults
        if (rigidBody != null)
        {
            // Don't force a minimum mass - let user configure it
            if (rigidBody.mass > 2000f)
            {
                Debug.LogWarning($"Rigidbody mass ({rigidBody.mass}) might be too high for buoyancy. Consider reducing to 100-1000 range.");
            }
            rigidBody.linearDamping = linearDrag;
            rigidBody.angularDamping = angularDrag;
        }
    }
    
    private void FixedUpdate()
    {
        if (rigidBody == null || oceanGenerator == null)
            return;
            
        sampleFrameCounter++;
        bool shouldSample = (sampleFrameCounter % sampleFrameInterval == 0);

        UpdateWaterData(shouldSample);
        CalculateBuoyancy();
        ApplyBuoyancyForce();
        
        if (showDebugInfo)
        {
            LogDebugInfo();
        }
    }
    
    private void UpdateWaterData(bool shouldSample)
    {
        if (!useMultiplePoints)
        {
            if (shouldSample)
            {
                Vector3 objectPosition = transform.position;
                Vector3 singlePointDisplacement = OceanDataReader.SampleOceanHeight(objectPosition, oceanGenerator);
                currentWaterHeight = singlePointDisplacement.y;
                currentWaterNormal = OceanDataReader.SampleOceanNormal(objectPosition, oceanGenerator);
                CalculateSubmersionSinglePoint(objectPosition);
            }
        }
        else
        {
            if (shouldSample)
            {
                CalculateMultiPointBuoyancy();
            }
        }
    }
    
    private void CalculateSubmersionSinglePoint(Vector3 objectPosition)
    {
        float objectBottom = objectPosition.y - GetObjectHeight() * 0.5f;
        float objectTop = objectPosition.y + GetObjectHeight() * 0.5f;
        
        if (objectTop < currentWaterHeight)
        {
            submergedPercentage = 1f;
        }
        else if (objectBottom > currentWaterHeight)
        {
            submergedPercentage = 0f;
        }
        else
        {
            float submergedHeight = currentWaterHeight - objectBottom;
            submergedPercentage = Mathf.Clamp01(submergedHeight / GetObjectHeight());
        }
    }
    
    private void CalculateMultiPointBuoyancy()
    {
        buoyancyForce = Vector3.zero;
        totalTorque = Vector3.zero;
        float totalSubmergedVolume = 0f;
        
        Vector3 objectPosition = transform.position;
        float objectHeight = GetObjectHeight();
        
        foreach (Vector3 localPoint in buoyancyPoints)
        {
            // Transform local point to world space
            Vector3 worldPoint = transform.TransformPoint(localPoint);
            
            // Sample water at this point
            Vector3 pointDisplacement = OceanDataReader.SampleOceanHeight(worldPoint, oceanGenerator);
            float waterHeight = pointDisplacement.y;
            Vector3 waterNormal = OceanDataReader.SampleOceanNormal(worldPoint, oceanGenerator);
            
            // Calculate submersion at this point
            float pointBottom = worldPoint.y - objectHeight * 0.25f; // Quarter height for each point
            float pointTop = worldPoint.y + objectHeight * 0.25f;
            
            float pointSubmersion = 0f;
            if (pointTop < waterHeight)
            {
                pointSubmersion = 1f;
            }
            else if (pointBottom < waterHeight)
            {
                pointSubmersion = (waterHeight - pointBottom) / (objectHeight * 0.5f);
                pointSubmersion = Mathf.Clamp01(pointSubmersion);
            }
            
            if (pointSubmersion > 0f)
            {
                // Calculate force at this point
                float pointVolume = (objectVolume / buoyancyPoints.Length) * pointSubmersion * submergedVolumeMultiplier;
                float pointForceMagnitude = waterDensity * pointVolume * 9.81f;
                pointForceMagnitude = Mathf.Min(pointForceMagnitude, maxBuoyancyForce / buoyancyPoints.Length);
                
                Vector3 pointForce = waterNormal * pointForceMagnitude;
                buoyancyForce += pointForce;
                
                // Calculate torque around center of mass
                Vector3 forcePosition = worldPoint - objectPosition;
                Vector3 torque = Vector3.Cross(forcePosition, pointForce);
                totalTorque += torque;
                
                totalSubmergedVolume += pointVolume;
            }
        }
        
        // Update overall submersion percentage for display
        submergedPercentage = totalSubmergedVolume / objectVolume;
        submergedPercentage = Mathf.Clamp01(submergedPercentage);
        
        // Store average water data for debugging
        Vector3 centerDisplacement = OceanDataReader.SampleOceanHeight(objectPosition, oceanGenerator);
        currentWaterHeight = centerDisplacement.y;
        currentWaterNormal = OceanDataReader.SampleOceanNormal(objectPosition, oceanGenerator);
    }
    
    private void CalculateBuoyancy()
    {
        if (!useMultiplePoints)
        {
            // Single point calculation (original)
            if (submergedPercentage <= 0f)
            {
                buoyancyForce = Vector3.zero;
                return;
            }
            
            float submergedVolume = objectVolume * submergedPercentage * submergedVolumeMultiplier;
            float buoyantForceMagnitude = waterDensity * submergedVolume * 9.81f;
            buoyantForceMagnitude = Mathf.Min(buoyantForceMagnitude, maxBuoyancyForce);
            buoyancyForce = currentWaterNormal * buoyantForceMagnitude;
            totalTorque = Vector3.zero;
        }
        // Multi-point calculation is done in CalculateMultiPointBuoyancy()
    }
    
    private void ApplyBuoyancyForce()
    {
        if (buoyancyForce.magnitude > 0.1f)
        {
            // Apply main buoyancy force
            rigidBody.AddForce(buoyancyForce);
            
            // Apply rotational torque (only for multi-point)
            if (useMultiplePoints && totalTorque.magnitude > 0.1f)
            {
                rigidBody.AddTorque(totalTorque);
            }
            
            // Enhanced damping to prevent bouncing
            Vector3 dampingForce = -rigidBody.linearVelocity * linearDrag * submergedPercentage;
            rigidBody.AddForce(dampingForce);
            
            // Angular damping with extra stability
            Vector3 angularDampingTorque = -rigidBody.angularVelocity * angularDrag * submergedPercentage;
            rigidBody.AddTorque(angularDampingTorque);
            
            // Additional stability: prevent excessive rotation
            if (useMultiplePoints)
            {
                // Stabilizing torque to keep ship upright
                Vector3 upDirection = transform.up;
                Vector3 worldUp = Vector3.up;
                float uprightness = Vector3.Dot(upDirection, worldUp);
                
                if (uprightness < 0.9f) // If ship is tilted
                {
                    Vector3 correctiveAxis = Vector3.Cross(upDirection, worldUp);
                    float correctionMagnitude = (1f - uprightness) * 1000f * submergedPercentage;
                    Vector3 correctiveTorque = correctiveAxis * correctionMagnitude;
                    rigidBody.AddTorque(correctiveTorque);
                }
            }
        }
    }
    
    private float GetObjectHeight()
    {
        // Try to get height from collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            return col.bounds.size.y;
        }
        
        // Try to get height from renderer
        Renderer ren = GetComponent<Renderer>();
        if (ren != null)
        {
            return ren.bounds.size.y;
        }
        
        // Default height
        return 2f;
    }
    
    private void LogDebugInfo()
    {
        if (Time.fixedTime % 1f < Time.fixedDeltaTime) // Log every second
        {
            float submergedVolume = objectVolume * submergedPercentage * submergedVolumeMultiplier;
            float expectedForce = waterDensity * submergedVolume * 9.81f;
            float actualForce = Mathf.Min(expectedForce, maxBuoyancyForce);
            
            string modeText = useMultiplePoints ? "Multi-Point" : "Single-Point";
            Debug.Log($"Buoyancy Debug ({modeText}) - Water Height: {currentWaterHeight:F2}, Object Y: {transform.position.y:F2}, " +
                     $"Submerged: {submergedPercentage:P0}, Mass: {rigidBody.mass:F0}kg");
            Debug.Log($"Force Calculation - Expected: {expectedForce:F0}N, Actual: {actualForce:F0}N, " +
                     $"Weight: {rigidBody.mass * 9.81f:F0}N, Net: {(actualForce - rigidBody.mass * 9.81f):F0}N");
            
            if (useMultiplePoints)
            {
                Debug.Log($"Torque: {totalTorque.magnitude:F0}Nm, Angular Velocity: {rigidBody.angularVelocity.magnitude:F2}rad/s");
            }
        }
    }
    
    // Public API
    public float GetWaterHeight()
    {
        return currentWaterHeight;
    }
    
    public float GetSubmergedPercentage()
    {
        return submergedPercentage;
    }
    
    public Vector3 GetBuoyancyForce()
    {
        return buoyancyForce;
    }
    
    public bool IsFloating()
    {
        return submergedPercentage > 0f && submergedPercentage < 1f;
    }
    
    public bool IsUnderwater()
    {
        return submergedPercentage >= 1f;
    }
    
    // Gizmos for debugging
    private void OnDrawGizmos()
    {
        if (!showGizmos || !Application.isPlaying)
            return;
            
        Vector3 pos = transform.position;
        
        if (useMultiplePoints && buoyancyPoints != null)
        {
            DrawMultiPointGizmos();
        }
        else
        {
            DrawSinglePointGizmos(pos);
        }
    }
    
    private void DrawMultiPointGizmos()
    {
        if (!Application.isPlaying)
        {
            // Show buoyancy points in edit mode
            Gizmos.color = Color.yellow;
            foreach (Vector3 localPoint in buoyancyPoints)
            {
                Vector3 worldPoint = transform.TransformPoint(localPoint);
                Gizmos.DrawWireSphere(worldPoint, 0.3f);
            }
            return;
        }
        
        // Runtime multi-point visualization
        foreach (Vector3 localPoint in buoyancyPoints)
        {
            Vector3 worldPoint = transform.TransformPoint(localPoint);
            Vector3 gizmoDisplacement = OceanDataReader.SampleOceanHeight(worldPoint, oceanGenerator);
            float waterHeight = gizmoDisplacement.y;
            
            // Draw water level at each point
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(new Vector3(worldPoint.x, waterHeight, worldPoint.z), new Vector3(0.5f, 0.05f, 0.5f));
            
            // Draw buoyancy point - color based on submersion
            bool isSubmerged = worldPoint.y < waterHeight;
            Gizmos.color = isSubmerged ? Color.green : Color.red;
            Gizmos.DrawWireSphere(worldPoint, 0.2f);
            
            // Draw line from point to water surface
            if (isSubmerged)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(worldPoint, new Vector3(worldPoint.x, waterHeight, worldPoint.z));
            }
        }
        
        // Draw overall buoyancy force
        if (buoyancyForce.magnitude > 0.1f)
        {
            Gizmos.color = Color.yellow;
            Vector3 forceVector = buoyancyForce.normalized * 3f;
            Gizmos.DrawLine(transform.position, transform.position + forceVector);
            Gizmos.DrawWireSphere(transform.position + forceVector, 0.3f);
        }
        
        // Draw torque (simplified visualization)
        if (totalTorque.magnitude > 0.1f)
        {
            Gizmos.color = Color.magenta;
            Vector3 torqueVector = totalTorque.normalized * 2f;
            Gizmos.DrawLine(transform.position, transform.position + torqueVector);
        }
    }
    
    private void DrawSinglePointGizmos(Vector3 pos)
    {
        if (!Application.isPlaying)
            return;
            
        // Draw water level
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(new Vector3(pos.x, currentWaterHeight, pos.z), new Vector3(4f, 0.1f, 4f));
        
        // Draw object bounds
        float objectHeight = GetObjectHeight();
        Gizmos.color = submergedPercentage > 0f ? Color.green : Color.red;
        Gizmos.DrawWireCube(pos, new Vector3(2f, objectHeight, 2f));
        
        // Draw submerged portion
        if (submergedPercentage > 0f)
        {
            Gizmos.color = new Color(0f, 0f, 1f, 0.3f);
            float submergedHeight = objectHeight * submergedPercentage;
            Vector3 submergedCenter = new Vector3(pos.x, pos.y - (objectHeight - submergedHeight) * 0.5f, pos.z);
            Gizmos.DrawCube(submergedCenter, new Vector3(2f, submergedHeight, 2f));
        }
        
        // Draw buoyancy force
        if (buoyancyForce.magnitude > 0.1f)
        {
            Gizmos.color = Color.yellow;
            Vector3 forceVector = buoyancyForce.normalized * 3f;
            Gizmos.DrawLine(pos, pos + forceVector);
            Gizmos.DrawWireSphere(pos + forceVector, 0.2f);
        }
        
        // Draw water normal
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pos, pos + currentWaterNormal * 2f);
    }
} 