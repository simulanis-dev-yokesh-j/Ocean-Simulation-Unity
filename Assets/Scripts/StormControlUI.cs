using UnityEngine;
using UnityEngine.UI;

public class StormControlUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OceanStormController stormController;
    
    [Header("UI Elements")]
    [SerializeField] private Slider stormIntensitySlider;
    [SerializeField] private Slider waveSizeSlider;
    [SerializeField] private Slider windStrengthSlider;
    [SerializeField] private Slider windDirectionSlider;
    [SerializeField] private Slider choppinessSlider;
    [SerializeField] private Slider foamSlider;
    
    [Header("Value Labels")]
    [SerializeField] private Text stormIntensityLabel;
    [SerializeField] private Text waveSizeLabel;
    [SerializeField] private Text windStrengthLabel;
    [SerializeField] private Text windDirectionLabel;
    [SerializeField] private Text choppinessLabel;
    [SerializeField] private Text foamLabel;
    
    [Header("Preset Buttons")]
    [SerializeField] private Button calmButton;
    [SerializeField] private Button lightBreezeButton;
    [SerializeField] private Button moderateButton;
    [SerializeField] private Button roughSeasButton;
    [SerializeField] private Button stormButton;
    [SerializeField] private Button hurricaneButton;
    
    [Header("Auto-Find UI")]
    [SerializeField] private bool autoFindUIElements = true;
    
    private void Start()
    {
        if (stormController == null)
            stormController = FindObjectOfType<OceanStormController>();
            
        if (autoFindUIElements)
        {
            AutoFindUIElements();
        }
        
        SetupSliderListeners();
        SetupButtonListeners();
        UpdateUI();
    }
    
    private void AutoFindUIElements()
    {
        // Try to find sliders by name
        if (stormIntensitySlider == null) stormIntensitySlider = GameObject.Find("StormIntensitySlider")?.GetComponent<Slider>();
        if (waveSizeSlider == null) waveSizeSlider = GameObject.Find("WaveSizeSlider")?.GetComponent<Slider>();
        if (windStrengthSlider == null) windStrengthSlider = GameObject.Find("WindStrengthSlider")?.GetComponent<Slider>();
        if (windDirectionSlider == null) windDirectionSlider = GameObject.Find("WindDirectionSlider")?.GetComponent<Slider>();
        if (choppinessSlider == null) choppinessSlider = GameObject.Find("ChoppinessSlider")?.GetComponent<Slider>();
        if (foamSlider == null) foamSlider = GameObject.Find("FoamSlider")?.GetComponent<Slider>();
        
        // Try to find labels
        if (stormIntensityLabel == null) stormIntensityLabel = GameObject.Find("StormIntensityLabel")?.GetComponent<Text>();
        if (waveSizeLabel == null) waveSizeLabel = GameObject.Find("WaveSizeLabel")?.GetComponent<Text>();
        if (windStrengthLabel == null) windStrengthLabel = GameObject.Find("WindStrengthLabel")?.GetComponent<Text>();
        if (windDirectionLabel == null) windDirectionLabel = GameObject.Find("WindDirectionLabel")?.GetComponent<Text>();
        if (choppinessLabel == null) choppinessLabel = GameObject.Find("ChoppinessLabel")?.GetComponent<Text>();
        if (foamLabel == null) foamLabel = GameObject.Find("FoamLabel")?.GetComponent<Text>();
        
        // Try to find buttons
        if (calmButton == null) calmButton = GameObject.Find("CalmButton")?.GetComponent<Button>();
        if (lightBreezeButton == null) lightBreezeButton = GameObject.Find("LightBreezeButton")?.GetComponent<Button>();
        if (moderateButton == null) moderateButton = GameObject.Find("ModerateButton")?.GetComponent<Button>();
        if (roughSeasButton == null) roughSeasButton = GameObject.Find("RoughSeasButton")?.GetComponent<Button>();
        if (stormButton == null) stormButton = GameObject.Find("StormButton")?.GetComponent<Button>();
        if (hurricaneButton == null) hurricaneButton = GameObject.Find("HurricaneButton")?.GetComponent<Button>();
    }
    
    private void SetupSliderListeners()
    {
        if (stormIntensitySlider != null)
        {
            stormIntensitySlider.minValue = 0f;
            stormIntensitySlider.maxValue = 100f;
            stormIntensitySlider.onValueChanged.AddListener(OnStormIntensityChanged);
        }
        
        if (waveSizeSlider != null)
        {
            waveSizeSlider.minValue = 0f;
            waveSizeSlider.maxValue = 100f;
            waveSizeSlider.onValueChanged.AddListener(OnWaveSizeChanged);
        }
        
        if (windStrengthSlider != null)
        {
            windStrengthSlider.minValue = 0f;
            windStrengthSlider.maxValue = 100f;
            windStrengthSlider.onValueChanged.AddListener(OnWindStrengthChanged);
        }
        
        if (windDirectionSlider != null)
        {
            windDirectionSlider.minValue = 0f;
            windDirectionSlider.maxValue = 360f;
            windDirectionSlider.onValueChanged.AddListener(OnWindDirectionChanged);
        }
        
        if (choppinessSlider != null)
        {
            choppinessSlider.minValue = 0f;
            choppinessSlider.maxValue = 100f;
            choppinessSlider.onValueChanged.AddListener(OnChoppinessChanged);
        }
        
        if (foamSlider != null)
        {
            foamSlider.minValue = 0f;
            foamSlider.maxValue = 100f;
            foamSlider.onValueChanged.AddListener(OnFoamChanged);
        }
    }
    
    private void SetupButtonListeners()
    {
        if (calmButton != null) calmButton.onClick.AddListener(() => ApplyPreset(0));
        if (lightBreezeButton != null) lightBreezeButton.onClick.AddListener(() => ApplyPreset(1));
        if (moderateButton != null) moderateButton.onClick.AddListener(() => ApplyPreset(2));
        if (roughSeasButton != null) roughSeasButton.onClick.AddListener(() => ApplyPreset(3));
        if (stormButton != null) stormButton.onClick.AddListener(() => ApplyPreset(4));
        if (hurricaneButton != null) hurricaneButton.onClick.AddListener(() => ApplyPreset(5));
    }
    
    private void OnStormIntensityChanged(float value)
    {
        if (stormController != null) stormController.SetStormIntensity(value);
        UpdateLabel(stormIntensityLabel, value, "%");
    }
    
    private void OnWaveSizeChanged(float value)
    {
        if (stormController != null) stormController.SetWaveSize(value);
        UpdateLabel(waveSizeLabel, value, "%");
    }
    
    private void OnWindStrengthChanged(float value)
    {
        if (stormController != null) stormController.SetWindStrength(value);
        UpdateLabel(windStrengthLabel, value, "%");
    }
    
    private void OnWindDirectionChanged(float value)
    {
        if (stormController != null) stormController.SetWindDirection(value);
        UpdateLabel(windDirectionLabel, value, "°");
    }
    
    private void OnChoppinessChanged(float value)
    {
        if (stormController != null) stormController.SetStormIntensity(value);
        UpdateLabel(choppinessLabel, value, "%");
    }
    
    private void OnFoamChanged(float value)
    {
        UpdateLabel(foamLabel, value, "%");
    }
    
    private void UpdateLabel(Text label, float value, string suffix)
    {
        if (label != null)
        {
            label.text = $"{value:F0}{suffix}";
        }
    }
    
    private void ApplyPreset(int presetIndex)
    {
        if (stormController != null)
        {
            stormController.ApplyPresetByIndex(presetIndex);
            UpdateUI();
        }
    }
    
    public void UpdateUI()
    {
        if (stormController == null) return;
        
        // Update sliders to match current storm controller values
        // Note: This uses reflection or you'd need to add getters to StormController
        // For now, we'll just update labels
        
        UpdateLabel(stormIntensityLabel, stormIntensitySlider?.value ?? 0f, "%");
        UpdateLabel(waveSizeLabel, waveSizeSlider?.value ?? 0f, "%");
        UpdateLabel(windStrengthLabel, windStrengthSlider?.value ?? 0f, "%");
        UpdateLabel(windDirectionLabel, windDirectionSlider?.value ?? 0f, "°");
        UpdateLabel(choppinessLabel, choppinessSlider?.value ?? 0f, "%");
        UpdateLabel(foamLabel, foamSlider?.value ?? 0f, "%");
    }
    
    // Keyboard shortcuts for quick testing
    private void Update()
    {
        if (stormController == null) return;
        
        // Number keys for quick presets
        if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyPreset(0); // Calm
        if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyPreset(1); // Light Breeze
        if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyPreset(2); // Moderate
        if (Input.GetKeyDown(KeyCode.Alpha4)) ApplyPreset(3); // Rough Seas
        if (Input.GetKeyDown(KeyCode.Alpha5)) ApplyPreset(4); // Storm
        if (Input.GetKeyDown(KeyCode.Alpha6)) ApplyPreset(5); // Hurricane
        
        // Arrow keys for quick adjustments
        if (Input.GetKey(KeyCode.UpArrow))
        {
            float current = stormIntensitySlider?.value ?? 0f;
            OnStormIntensityChanged(Mathf.Min(current + Time.deltaTime * 20f, 100f));
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            float current = stormIntensitySlider?.value ?? 0f;
            OnStormIntensityChanged(Mathf.Max(current - Time.deltaTime * 20f, 0f));
        }
    }
} 