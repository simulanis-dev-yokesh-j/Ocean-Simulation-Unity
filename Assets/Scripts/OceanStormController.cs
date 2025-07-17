using UnityEngine;
using Sirenix.OdinInspector;

[System.Serializable]
public class WeatherPreset
{
    [HideLabel, DisplayAsString]
    public string name;
    
    [Range(0f, 100f), SuffixLabel("%"), ShowInInspector]
    [ProgressBar(0, 100, ColorMember = "GetIntensityColor")]
    public float stormIntensity;
    
    [Range(0f, 100f), SuffixLabel("%")]
    [ProgressBar(0, 100, 0.3f, 0.8f, 1f)]
    public float waveSize;
    
    [Range(0f, 100f), SuffixLabel("%")]
    [ProgressBar(0, 100, 0.6f, 0.9f, 0.3f)]
    public float windStrength;
    
    [Range(0f, 360f), SuffixLabel("°")]
    public float windDirection;
    
    [Range(0f, 100f), SuffixLabel("%")]
    [ProgressBar(0, 100, 0.8f, 0.4f, 0.2f)]
    public float choppiness;
    
    [Range(0f, 100f), SuffixLabel("%")]
    [ProgressBar(0, 100, 1f, 1f, 1f)]
    public float foamAmount;
    
    private Color GetIntensityColor()
    {
        if (stormIntensity < 30f) return Color.green;
        if (stormIntensity < 60f) return Color.yellow;
        if (stormIntensity < 85f) return Color.red;
        return Color.magenta;
    }
}

public class OceanStormController : MonoBehaviour
{
    [TabGroup("Setup")]
    [Required]
    [Tooltip("The ocean generator to control")]
    [SerializeField] private OceanGenerator oceanGenerator;
    
    [TabGroup("Setup")]
    [InfoBox("Automatically finds Ocean Generator if not assigned", InfoMessageType.Info)]
    [SerializeField] private bool autoFindOceanGenerator = true;
    
    // Weather Control Tab
    [TabGroup("Weather Control")]
    [InfoBox("🌊 Drag sliders or use preset buttons below to control the ocean", InfoMessageType.None)]
    
    [TabGroup("Weather Control")]
    [BoxGroup("Current Weather", ShowLabel = false)]
    [Range(0f, 100f), SuffixLabel("%")]
    [ProgressBar(0, 100, ColorMember = "GetStormIntensityColor")]
    [Tooltip("Overall storm power - affects all other parameters")]
    [OnValueChanged("OnStormParameterChanged")]
    [SerializeField] private float stormIntensity = 30f;
    
    [BoxGroup("Current Weather")]
    [Range(0f, 100f), SuffixLabel("%")]
    [ProgressBar(0, 100, 0.3f, 0.8f, 1f)]
    [Tooltip("How large the waves are - from ripples to massive swells")]
    [OnValueChanged("OnStormParameterChanged")]
    [SerializeField] private float waveSize = 50f;
    
    [BoxGroup("Current Weather")]
    [Range(0f, 100f), SuffixLabel("%")]
    [ProgressBar(0, 100, 0.6f, 0.9f, 0.3f)]
    [Tooltip("Wind power affecting wave generation")]
    [OnValueChanged("OnStormParameterChanged")]
    [SerializeField] private float windStrength = 40f;
    
    [BoxGroup("Current Weather")]
    [Range(0f, 360f), SuffixLabel("°")]
    [Tooltip("Wind direction in degrees (0=North, 90=East, 180=South, 270=West)")]
    [OnValueChanged("OnStormParameterChanged")]
    [SerializeField] private float windDirection = 45f;
    
    [BoxGroup("Current Weather")]
    [Range(0f, 100f), SuffixLabel("%")]
    [ProgressBar(0, 100, 0.8f, 0.4f, 0.2f)]
    [Tooltip("How choppy vs smooth - high values create sharp, choppy waves")]
    [OnValueChanged("OnStormParameterChanged")]
    [SerializeField] private float choppiness = 50f;
    
    [BoxGroup("Current Weather")]
    [Range(0f, 100f), SuffixLabel("%")]
    [ProgressBar(0, 100, 1f, 1f, 1f)]
    [Tooltip("Amount of whitecaps and sea foam")]
    [OnValueChanged("OnStormParameterChanged")]
    [SerializeField] private float foamAmount = 40f;
    
    [TabGroup("Weather Control")]
    [BoxGroup("Transition Settings")]
    [Range(0.1f, 5f), SuffixLabel("x")]
    [Tooltip("How fast weather changes happen")]
    [SerializeField] private float transitionSpeed = 1f;
    
    [BoxGroup("Transition Settings")]
    [Tooltip("Enable smooth transitions between weather states")]
    [SerializeField] private bool smoothTransitions = true;
    
    // Weather Presets
    [TabGroup("Weather Control")]
    [BoxGroup("Quick Weather Presets")]
    [InfoBox("Click any button to instantly apply a weather preset", InfoMessageType.Info)]

    [BoxGroup("Quick Weather Presets")]
    [ButtonGroup("PresetsRow1")]
    [Button("Calm", ButtonSizes.Large), GUIColor(0.7f, 1f, 0.7f)]
    private void ApplyCalm() => ApplyPresetByIndex(0);

    [ButtonGroup("PresetsRow1")]
    [Button("Light Breeze", ButtonSizes.Large), GUIColor(1f, 1f, 0.7f)]
    private void ApplyLightBreeze() => ApplyPresetByIndex(1);

    [ButtonGroup("PresetsRow1")]
    [Button("Moderate", ButtonSizes.Large), GUIColor(1f, 0.9f, 0.6f)]
    private void ApplyModerate() => ApplyPresetByIndex(2);

    [BoxGroup("Quick Weather Presets")]
    [ButtonGroup("PresetsRow2")]
    [Button("Rough Seas", ButtonSizes.Large), GUIColor(1f, 0.7f, 0.6f)]
    private void ApplyRoughSeas() => ApplyPresetByIndex(3);

    [ButtonGroup("PresetsRow2")]
    [Button("Storm", ButtonSizes.Large), GUIColor(0.9f, 0.6f, 0.9f)]
    private void ApplyStormPreset() => ApplyPresetByIndex(4);

    [ButtonGroup("PresetsRow2")]
    [Button("Hurricane", ButtonSizes.Large), GUIColor(0.8f, 0.5f, 0.8f)]
    private void ApplyHurricane() => ApplyPresetByIndex(5);
    
    // Presets Tab
    [TabGroup("Presets")]
    [InfoBox("🎛️ Manage and customize weather presets", InfoMessageType.None)]
    [ListDrawerSettings(Expanded = true, DraggableItems = false, HideAddButton = false)]
    [SerializeField] private WeatherPreset[] weatherPresets = new WeatherPreset[]
    {
        new WeatherPreset { name = "Calm", stormIntensity = 10f, waveSize = 20f, windStrength = 15f, windDirection = 0f, choppiness = 20f, foamAmount = 10f },
        new WeatherPreset { name = "Light Breeze", stormIntensity = 25f, waveSize = 35f, windStrength = 30f, windDirection = 45f, choppiness = 40f, foamAmount = 25f },
        new WeatherPreset { name = "Moderate", stormIntensity = 45f, waveSize = 55f, windStrength = 50f, windDirection = 90f, choppiness = 60f, foamAmount = 45f },
        new WeatherPreset { name = "Rough Seas", stormIntensity = 70f, waveSize = 75f, windStrength = 70f, windDirection = 135f, choppiness = 80f, foamAmount = 70f },
        new WeatherPreset { name = "Storm", stormIntensity = 90f, waveSize = 90f, windStrength = 85f, windDirection = 180f, choppiness = 90f, foamAmount = 85f },
        new WeatherPreset { name = "Hurricane", stormIntensity = 100f, waveSize = 100f, windStrength = 100f, windDirection = 225f, choppiness = 100f, foamAmount = 100f }
    };
    
    // Debug Tab
    [TabGroup("Debug")]
    [InfoBox("🔍 Debug information and advanced controls", InfoMessageType.None)]
    
    [TabGroup("Debug")]
    [ShowInInspector, ReadOnly]
    [DisplayAsString, LabelText("Current Weather State")]
    private string CurrentWeatherDescription => GetCurrentWeatherDescription();
    
    [TabGroup("Debug")]
    [ShowInInspector, ReadOnly]
    [ProgressBar(0, 100, ColorMember = "GetStormIntensityColor")]
    [LabelText("Overall Intensity")]
    private float DebugStormIntensity => targetStormIntensity;
    
    [TabGroup("Debug")]
    [BoxGroup("Ocean Values", ShowLabel = true)]
    [ShowInInspector, ReadOnly, LabelText("Wind Speed")]
    private string DebugWindSpeed => oceanGenerator?.GetOceanSettings()?.WindSpeed.ToString("F1") + " m/s" ?? "N/A";
    
    [BoxGroup("Ocean Values")]
    [ShowInInspector, ReadOnly, LabelText("Wave Factor")]
    private string DebugWaveFactor => oceanGenerator?.GetOceanSettings()?.WaveChopyFactor.ToString("F2") ?? "N/A";
    
    [BoxGroup("Ocean Values")]
    [ShowInInspector, ReadOnly, LabelText("Foam Intensity")]
    private string DebugFoamIntensity => oceanGenerator?.GetOceanSettings()?.FoamIntensity.ToString("F2") ?? "N/A";
    
    [TabGroup("Debug")]
    [Button("🔄 Force Ocean Update", ButtonSizes.Medium)]
    [Tooltip("Manually force the ocean to update with current settings")]
    private void DebugForceUpdate() => ForceOceanUpdate();
    
    [TabGroup("Debug")]
    [Button("📊 Log Current Settings", ButtonSizes.Medium)]
    [Tooltip("Print all current settings to console")]
    private void DebugLogSettings() => LogCurrentSettings();
    
    // Internal values for smooth transitions
    private float targetStormIntensity;
    private float targetWaveSize;
    private float targetWindStrength;
    private float targetWindDirection;
    private float targetChoppiness;
    private float targetFoamAmount;
    
    // Add these fields at the top of the class
    private float lastStormIntensity, lastWaveSize, lastWindStrength, lastWindDirection, lastChoppiness, lastFoamAmount;
    
    private void Start()
    {
        if (autoFindOceanGenerator && oceanGenerator == null)
            oceanGenerator = FindObjectOfType<OceanGenerator>();
            
        // Initialize targets
        targetStormIntensity = stormIntensity;
        targetWaveSize = waveSize;
        targetWindStrength = windStrength;
        targetWindDirection = windDirection;
        targetChoppiness = choppiness;
        targetFoamAmount = foamAmount;
        
        // Apply initial settings
        ApplyStormSettings();
    }
    
    private void Update()
    {
        // Smooth transitions (if enabled)
        if (smoothTransitions)
        {
            UpdateTransitions();
        }
        else
        {
            targetStormIntensity = stormIntensity;
            targetWaveSize = waveSize;
            targetWindStrength = windStrength;
            targetWindDirection = windDirection;
            targetChoppiness = choppiness;
            targetFoamAmount = foamAmount;
        }

        // Only apply if something changed
        if (HasStormSettingsChanged())
        {
            ApplyStormSettings();
            CacheLastValues();
        }
    }
    
    private void UpdateTransitions()
    {
        float deltaTime = Time.deltaTime * transitionSpeed;
        
        targetStormIntensity = Mathf.Lerp(targetStormIntensity, stormIntensity, deltaTime);
        targetWaveSize = Mathf.Lerp(targetWaveSize, waveSize, deltaTime);
        targetWindStrength = Mathf.Lerp(targetWindStrength, windStrength, deltaTime);
        targetWindDirection = Mathf.LerpAngle(targetWindDirection, windDirection, deltaTime);
        targetChoppiness = Mathf.Lerp(targetChoppiness, choppiness, deltaTime);
        targetFoamAmount = Mathf.Lerp(targetFoamAmount, foamAmount, deltaTime);
    }
    
    private void ApplyStormSettings()
    {
        if (oceanGenerator == null) return;
        
        var oceanSettings = oceanGenerator.GetOceanSettings();
        if (oceanSettings == null) return;
        
        // Map simple controls to complex ocean parameters
        
        // Wind Speed: 0-100% maps to 5-50 m/s (realistic wind speeds)
        oceanSettings.WindSpeed = Mathf.Lerp(5f, 50f, targetWindStrength / 100f);
        
        // Wind Direction: Direct mapping
        oceanSettings.WindAngle = targetWindDirection;
        
        // Wave Choppiness: 0-100% maps to 0.1-2.0
        oceanSettings.WaveChopyFactor = Mathf.Lerp(0.1f, 2f, targetChoppiness / 100f);
        
        // Foam settings
        oceanSettings.FoamIntensity = Mathf.Lerp(0f, 2f, targetFoamAmount / 100f);
        oceanSettings.FoamDecay = Mathf.Lerp(0.2f, 0.05f, targetFoamAmount / 100f); // More foam = slower decay
        
        // Swell: Storm intensity affects the ratio of swell to wind waves
        oceanSettings.Swell = Mathf.Lerp(0.8f, 0.2f, targetStormIntensity / 100f); // More storm = less swell, more choppy
        
        // Wave Scale: Affects all three cascades based on wave size
        UpdateCascadeScales();
        
        // Force ocean to update with new settings
        ForceOceanUpdate();
    }
    
    private void UpdateCascadeScales()
    {
        // Base scales for different wave sizes
        float smallWaveScale = Mathf.Lerp(20f, 100f, targetWaveSize / 100f);   // Small details
        float mediumWaveScale = Mathf.Lerp(100f, 400f, targetWaveSize / 100f); // Medium waves  
        float largeWaveScale = Mathf.Lerp(300f, 800f, targetWaveSize / 100f);  // Large swells
        
        // Storm intensity affects the scale distribution
        float stormFactor = targetStormIntensity / 100f;
        smallWaveScale *= (1f + stormFactor * 0.5f);   // Storm makes small waves bigger
        mediumWaveScale *= (1f + stormFactor * 0.7f);  // Medium effect
        largeWaveScale *= (1f + stormFactor * 0.3f);   // Less effect on large swells
        
        // Apply to cascades
        var cascade1 = oceanGenerator.GetCascadeSettings(0); // Small waves
        var cascade2 = oceanGenerator.GetCascadeSettings(1); // Medium waves
        var cascade3 = oceanGenerator.GetCascadeSettings(2); // Large waves
        
        if (cascade1 != null) cascade1.LengthScale = Mathf.RoundToInt(smallWaveScale);
        if (cascade2 != null) cascade2.LengthScale = Mathf.RoundToInt(mediumWaveScale);
        if (cascade3 != null) cascade3.LengthScale = Mathf.RoundToInt(largeWaveScale);
    }
    
    private void ForceOceanUpdate()
    {
        // Trigger the ocean to regenerate with new settings
        if (oceanGenerator != null)
        {
            // This will recreate the wave cascades with new settings
            oceanGenerator.SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
        }
    }
    
    // Public API for easy control
    public void SetStormIntensity(float intensity)
    {
        stormIntensity = Mathf.Clamp(intensity, 0f, 100f);
    }
    
    public void SetWaveSize(float size)
    {
        waveSize = Mathf.Clamp(size, 0f, 100f);
    }
    
    public void SetWindStrength(float strength)
    {
        windStrength = Mathf.Clamp(strength, 0f, 100f);
    }
    
    public void SetWindDirection(float direction)
    {
        windDirection = direction % 360f;
    }
    
    public void ApplyPreset(string presetName)
    {
        foreach (var preset in weatherPresets)
        {
            if (preset.name == presetName)
            {
                ApplyPreset(preset);
                break;
            }
        }
    }
    
    public void ApplyPreset(WeatherPreset preset)
    {
        stormIntensity = preset.stormIntensity;
        waveSize = preset.waveSize;
        windStrength = preset.windStrength;
        windDirection = preset.windDirection;
        choppiness = preset.choppiness;
        foamAmount = preset.foamAmount;
    }
    
    public void ApplyPresetByIndex(int index)
    {
        if (index >= 0 && index < weatherPresets.Length)
        {
            ApplyPreset(weatherPresets[index]);
        }
    }
    
    // Quick preset methods
    public void SetCalm() => ApplyPreset("Calm");
    public void SetModerate() => ApplyPreset("Moderate");
    public void SetStorm() => ApplyPreset("Storm");
    public void SetHurricane() => ApplyPreset("Hurricane");
    
    // Odin Inspector Support Methods
    private Color GetStormIntensityColor()
    {
        float intensity = targetStormIntensity;
        if (intensity < 20f) return new Color(0.4f, 0.8f, 1f);      // Light blue - calm
        if (intensity < 40f) return new Color(0.2f, 1f, 0.2f);      // Green - mild
        if (intensity < 60f) return new Color(1f, 1f, 0.2f);        // Yellow - moderate
        if (intensity < 80f) return new Color(1f, 0.6f, 0.2f);      // Orange - rough
        if (intensity < 95f) return new Color(1f, 0.2f, 0.2f);      // Red - storm
        return new Color(0.8f, 0.2f, 0.8f);                         // Magenta - hurricane
    }
    
    private string GetCurrentWeatherDescription()
    {
        float intensity = targetStormIntensity;
        if (intensity < 20f) return "🌅 Peaceful Waters - Mirror-like surface";
        if (intensity < 40f) return "🌊 Light Breeze - Gentle ripples";
        if (intensity < 60f) return "⛵ Moderate Seas - Good sailing conditions";
        if (intensity < 80f) return "🌪️ Rough Waters - Challenging conditions";
        if (intensity < 95f) return "⛈️ Storm - Dangerous seas";
        return "🌀 Hurricane - Extreme conditions!";
    }
    
    private void OnStormParameterChanged()
    {
        // This gets called whenever any storm parameter changes in the inspector
        // Forces immediate update for real-time feedback
        if (!smoothTransitions)
        {
            targetStormIntensity = stormIntensity;
            targetWaveSize = waveSize;
            targetWindStrength = windStrength;
            targetWindDirection = windDirection;
            targetChoppiness = choppiness;
            targetFoamAmount = foamAmount;
            ApplyStormSettings();
        }
    }
    
    private void LogCurrentSettings()
    {
        Debug.Log("=== OCEAN STORM CONTROLLER SETTINGS ===");
        Debug.Log($"Storm Intensity: {stormIntensity:F1}% (Target: {targetStormIntensity:F1}%)");
        Debug.Log($"Wave Size: {waveSize:F1}%");
        Debug.Log($"Wind: {windStrength:F1}% @ {windDirection:F0}°");
        Debug.Log($"Choppiness: {choppiness:F1}%");
        Debug.Log($"Foam: {foamAmount:F1}%");
        Debug.Log($"Weather State: {GetCurrentWeatherDescription()}");
        
        if (oceanGenerator?.GetOceanSettings() != null)
        {
            var settings = oceanGenerator.GetOceanSettings();
            Debug.Log("--- Ocean Settings ---");
            Debug.Log($"Wind Speed: {settings.WindSpeed:F1} m/s");
            Debug.Log($"Wind Angle: {settings.WindAngle:F0}°");
            Debug.Log($"Wave Chopy Factor: {settings.WaveChopyFactor:F2}");
            Debug.Log($"Foam Intensity: {settings.FoamIntensity:F2}");
            Debug.Log($"Swell: {settings.Swell:F2}");
        }
        Debug.Log("================================");
    }
    
    // Optional: Runtime GUI for quick testing (Odin Inspector provides better debugging)
    [TabGroup("Debug")]
    [SerializeField, Tooltip("Show runtime GUI overlay for quick testing")]
    private bool showRuntimeGUI = false;
    
    private void OnGUI()
    {
        if (!showRuntimeGUI) return;
        
        GUILayout.BeginArea(new Rect(10, 120, 300, 140));
        GUILayout.Label("🌊 Ocean Storm Controller", GUI.skin.box);
        GUILayout.Label($"{GetCurrentWeatherDescription()}");
        GUILayout.Label($"Intensity: {targetStormIntensity:F0}% | Waves: {targetWaveSize:F0}%");
        
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Calm")) SetCalm();
        if (GUILayout.Button("Storm")) SetStorm();
        if (GUILayout.Button("Hurricane")) SetHurricane();
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
    
    // Add these helper methods
    private bool HasStormSettingsChanged()
    {
        return !Mathf.Approximately(targetStormIntensity, lastStormIntensity)
            || !Mathf.Approximately(targetWaveSize, lastWaveSize)
            || !Mathf.Approximately(targetWindStrength, lastWindStrength)
            || !Mathf.Approximately(targetWindDirection, lastWindDirection)
            || !Mathf.Approximately(targetChoppiness, lastChoppiness)
            || !Mathf.Approximately(targetFoamAmount, lastFoamAmount);
    }

    private void CacheLastValues()
    {
        lastStormIntensity = targetStormIntensity;
        lastWaveSize = targetWaveSize;
        lastWindStrength = targetWindStrength;
        lastWindDirection = targetWindDirection;
        lastChoppiness = targetChoppiness;
        lastFoamAmount = targetFoamAmount;
    }
} 