using UnityEngine;

[System.Serializable]
public class ShipEngineSettings
{
    [Header("Engine Power")]
    public float maxForwardSpeed = 20f;
    public float maxReverseSpeed = 8f;
    public float acceleration = 5f;
    public float deceleration = 10f;
    
    [Header("Steering")]
    public float maxTurnSpeed = 45f;
    public float turnAcceleration = 30f;
    public AnimationCurve turnEfficiencyBySpeed = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);
    
    [Header("Physics")]
    public Vector3 centerOfMass = Vector3.zero;
    public float engineEfficiencyUnderwater = 0.1f;
}

[System.Serializable]
public class ShipHullSettings
{
    [Header("Stability")]
    public float stabilityStrength = 1000f;
    public float rollDamping = 500f;
    public float pitchDamping = 300f;
    public float yawDamping = 200f;
    
    [Header("Hydrodynamics")]
    public float hullDragCoefficient = 0.1f;
    public float lateralDragMultiplier = 2f;
    public float verticalDragMultiplier = 5f;
}

public class ShipController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody shipRigidbody;
    [SerializeField] private OceanBuoyancyGPU buoyancySystem;
    
    [Header("Ship Configuration")]
    [SerializeField] private ShipEngineSettings engineSettings;
    [SerializeField] private ShipHullSettings hullSettings;
    
    [Header("Control Input")]
    [SerializeField] private bool useKeyboardInput = true;
    [SerializeField] private string thrustAxis = "Vertical";
    [SerializeField] private string steerAxis = "Horizontal";
    
    [Header("Audio & Effects")]
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private ParticleSystem wakeEffect;
    [SerializeField] private Transform propellerTransform;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    // Runtime state
    private float currentThrust = 0f;
    private float currentSteering = 0f;
    private float enginePower = 0f;
    private float currentSpeed = 0f;
    private float submergedPercentage = 0f;
    
    // Input handling
    private float thrustInput = 0f;
    private float steerInput = 0f;
    
    // Wake effect
    private Vector3 lastPosition;
    private float wakeIntensity = 0f;
    
    private void Start()
    {
        InitializeShip();
    }
    
    private void InitializeShip()
    {
        if (shipRigidbody == null)
            shipRigidbody = GetComponent<Rigidbody>();
            
        if (buoyancySystem == null)
            buoyancySystem = GetComponent<OceanBuoyancyGPU>();
        
        // Set center of mass for realistic ship physics
        if (shipRigidbody != null)
        {
            shipRigidbody.centerOfMass = engineSettings.centerOfMass;
        }
        
        lastPosition = transform.position;
    }
    
    private void Update()
    {
        HandleInput();
        UpdateEngineAudio();
        UpdateVisualEffects();
        
        if (showDebugInfo)
        {
            UpdateDebugInfo();
        }
    }
    
    private void FixedUpdate()
    {
        if (shipRigidbody == null)
            return;
            
        UpdateShipPhysics();
        ApplyEngineForces();
        ApplyHullForces();
        ApplyStabilization();
    }
    
    private void HandleInput()
    {
        if (useKeyboardInput)
        {
            thrustInput = Input.GetAxis(thrustAxis);
            steerInput = Input.GetAxis(steerAxis);
        }
        
        // Smooth input interpolation
        float targetThrust = thrustInput;
        float targetSteering = steerInput;
        
        // Apply thrust acceleration/deceleration
        float thrustRate = targetThrust > currentThrust ? engineSettings.acceleration : engineSettings.deceleration;
        currentThrust = Mathf.MoveTowards(currentThrust, targetThrust, thrustRate * Time.deltaTime);
        
        // Apply steering acceleration
        currentSteering = Mathf.MoveTowards(currentSteering, targetSteering, engineSettings.turnAcceleration * Time.deltaTime);
    }
    
    private void UpdateShipPhysics()
    {
        currentSpeed = Vector3.Dot(shipRigidbody.linearVelocity, transform.forward);
        
        // Get submersion percentage from buoyancy system
        if (buoyancySystem != null)
        {
            submergedPercentage = buoyancySystem.GetSubmergedPercentage();
        }
        
        // Calculate effective engine power based on submersion
        float engineEfficiency = Mathf.Lerp(engineSettings.engineEfficiencyUnderwater, 1f, submergedPercentage);
        enginePower = currentThrust * engineEfficiency;
    }
    
    private void ApplyEngineForces()
    {
        if (Mathf.Abs(enginePower) < 0.01f)
            return;
            
        // Calculate maximum speed based on direction
        float maxSpeed = enginePower > 0 ? engineSettings.maxForwardSpeed : engineSettings.maxReverseSpeed;
        
        // Engine force calculation with speed limitation
        float speedRatio = Mathf.Abs(currentSpeed) / maxSpeed;
        float engineForce = enginePower * (1f - speedRatio) * 10000f; // Scale factor for force
        
        // Apply thrust force
        Vector3 thrustForce = transform.forward * engineForce;
        shipRigidbody.AddForce(thrustForce);
        
        // Apply steering (rudder effect) - only effective when moving
        if (Mathf.Abs(currentSpeed) > 0.5f && Mathf.Abs(currentSteering) > 0.01f)
        {
            float turnEfficiency = engineSettings.turnEfficiencyBySpeed.Evaluate(Mathf.Abs(currentSpeed) / engineSettings.maxForwardSpeed);
            float rudderForce = currentSteering * engineSettings.maxTurnSpeed * turnEfficiency * submergedPercentage;
            
            // Apply torque around Y-axis
            shipRigidbody.AddTorque(Vector3.up * rudderForce * 1000f);
        }
    }
    
    private void ApplyHullForces()
    {
        if (submergedPercentage <= 0f)
            return;
            
        Vector3 velocity = shipRigidbody.linearVelocity;
        
        // Calculate drag forces
        Vector3 forwardDrag = -Vector3.Project(velocity, transform.forward) * hullSettings.hullDragCoefficient;
        Vector3 lateralDrag = -Vector3.Project(velocity, transform.right) * hullSettings.lateralDragMultiplier;
        Vector3 verticalDrag = -Vector3.Project(velocity, transform.up) * hullSettings.verticalDragMultiplier;
        
        Vector3 totalDrag = (forwardDrag + lateralDrag + verticalDrag) * submergedPercentage * 100f;
        shipRigidbody.AddForce(totalDrag);
        
        // Angular drag
        Vector3 angularVelocity = shipRigidbody.angularVelocity;
        Vector3 angularDrag = new Vector3(
            -angularVelocity.x * hullSettings.rollDamping,
            -angularVelocity.y * hullSettings.yawDamping,
            -angularVelocity.z * hullSettings.pitchDamping
        ) * submergedPercentage;
        
        shipRigidbody.AddTorque(angularDrag);
    }
    
    private void ApplyStabilization()
    {
        if (submergedPercentage <= 0f)
            return;
            
        // Anti-roll stabilization
        float rollAngle = Vector3.SignedAngle(Vector3.up, transform.up, transform.forward);
        Vector3 stabilizingTorque = -transform.forward * rollAngle * hullSettings.stabilityStrength * submergedPercentage;
        shipRigidbody.AddTorque(stabilizingTorque);
        
        // Pitch stabilization
        float pitchAngle = Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up);
        Vector3 pitchCorrection = -transform.right * pitchAngle * hullSettings.stabilityStrength * 0.5f * submergedPercentage;
        shipRigidbody.AddTorque(pitchCorrection);
    }
    
    private void UpdateEngineAudio()
    {
        if (engineAudioSource == null)
            return;
            
        // Update engine audio based on power and speed
        float audioIntensity = Mathf.Abs(enginePower) * 0.5f + Mathf.Abs(currentSpeed) / engineSettings.maxForwardSpeed * 0.5f;
        engineAudioSource.volume = audioIntensity * 0.8f;
        engineAudioSource.pitch = 0.8f + audioIntensity * 0.4f;
    }
    
    private void UpdateVisualEffects()
    {
        // Update propeller rotation
        if (propellerTransform != null)
        {
            float propellerSpeed = enginePower * 360f * 5f; // RPM simulation
            propellerTransform.Rotate(0, 0, propellerSpeed * Time.deltaTime);
        }
        
        // Update wake effect
        if (wakeEffect != null)
        {
            Vector3 velocityChange = transform.position - lastPosition;
            wakeIntensity = velocityChange.magnitude / Time.deltaTime;
            
            var emission = wakeEffect.emission;
            emission.rateOverTime = wakeIntensity * 10f * submergedPercentage;
            
            var velocityModule = wakeEffect.velocityOverLifetime;
            velocityModule.space = ParticleSystemSimulationSpace.World;
            
            lastPosition = transform.position;
        }
    }
    
    private void UpdateDebugInfo()
    {
        // This would typically update UI elements showing ship status
        // For now, just log key information
        if (Time.frameCount % 60 == 0) // Every second at 60 FPS
        {
            Debug.Log($"Ship Status - Speed: {currentSpeed:F1} m/s, Submerged: {submergedPercentage:P0}, Engine: {enginePower:F2}");
        }
    }
    
    // Public API for external systems
    public float GetCurrentSpeed()
    {
        return currentSpeed;
    }
    
    public float GetEngineThrust()
    {
        return currentThrust;
    }
    
    public float GetSteering()
    {
        return currentSteering;
    }
    
    public float GetSubmergedPercentage()
    {
        return submergedPercentage;
    }
    
    public bool IsMoving()
    {
        return Mathf.Abs(currentSpeed) > 0.1f;
    }
    
    public bool IsUnderwater()
    {
        return submergedPercentage > 0.9f;
    }
    
    public void SetEngineInput(float thrust, float steering)
    {
        thrustInput = Mathf.Clamp(thrust, -1f, 1f);
        steerInput = Mathf.Clamp(steering, -1f, 1f);
    }
    
    // Emergency stop
    public void EmergencyStop()
    {
        thrustInput = 0f;
        steerInput = 0f;
        currentThrust = 0f;
        currentSteering = 0f;
        shipRigidbody.linearDamping = 5f; // Temporarily increase drag
        
        Invoke(nameof(ResetDrag), 2f);
    }
    
    private void ResetDrag()
    {
        shipRigidbody.linearDamping = 0.1f;
    }
    
    // Gizmos for debugging
    private void OnDrawGizmosSelected()
    {
        // Draw center of mass
        Gizmos.color = Color.red;
        Vector3 comWorld = transform.TransformPoint(engineSettings.centerOfMass);
        Gizmos.DrawWireSphere(comWorld, 0.2f);
        
        // Draw velocity vector
        if (Application.isPlaying && shipRigidbody != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + shipRigidbody.linearVelocity);
            
            // Draw force indicators
            Gizmos.color = Color.green;
            Vector3 thrustDirection = transform.forward * currentThrust * 5f;
            Gizmos.DrawLine(transform.position, transform.position + thrustDirection);
        }
    }
} 