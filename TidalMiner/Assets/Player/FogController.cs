using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FogController : MonoBehaviour
{
    [Header("Fog Configuration")]
    [SerializeField] private bool useDistanceFog = true;
    [SerializeField] private Color fogColor = new Color(0.5f, 0.5f, 0.8f, 1.0f); // Blueish fog color

    [Header("Distance Settings")]
    [Tooltip("Distance at which fog begins to appear")]
    [SerializeField] private float startDistance = 10f;

    [Tooltip("Distance at which objects are completely obscured")]
    [SerializeField] private float endDistance = 30f;

    [Tooltip("Controls how quickly fog density increases with distance")]
    [Range(1f, 5f)]
    [SerializeField] private float fogDensityCurve = 2f;

    [Header("Weather Effects")]
    [SerializeField] private bool simulateWeather = false;
    [SerializeField] private float rainFogMultiplier = 0.5f; // More dense fog during rain
    [Range(0f, 1f)]
    [SerializeField] private float rainIntensity = 0f;

    // For weather transition
    private float targetRainIntensity = 0f;
    private float weatherTransitionSpeed = 0.5f;

    // Cache original fog settings to restore if needed
    private bool originalFogEnabled;
    private Color originalFogColor;
    private float originalFogDensity;
    private float originalFogStartDistance;
    private float originalFogEndDistance;
    private FogMode originalFogMode;

    private void Start()
    {
        // Cache original fog settings
        CacheOriginalFogSettings();

        // Initialize fog
        if (useDistanceFog)
        {
            ApplyFogSettings();
        }
    }

    private void CacheOriginalFogSettings()
    {
        originalFogEnabled = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogStartDistance = RenderSettings.fogStartDistance;
        originalFogEndDistance = RenderSettings.fogEndDistance;
        originalFogMode = RenderSettings.fogMode;
    }

    private void Update()
    {
        if (!useDistanceFog) return;

        // Update weather if enabled
        if (simulateWeather)
        {
            UpdateWeather();

            // Apply fog settings (only if needed, for performance)
            ApplyFogSettings();
        }
    }

    private void ApplyFogSettings()
    {
        // Enable fog in the render settings
        RenderSettings.fog = true;

        // Set fog mode to linear for precise distance control
        RenderSettings.fogMode = FogMode.Linear;

        // Adjust start and end distances based on weather if enabled
        float currentStartDistance = startDistance;
        float currentEndDistance = endDistance;

        if (simulateWeather && rainIntensity > 0)
        {
            float weatherMultiplier = Mathf.Lerp(1f, rainFogMultiplier, rainIntensity);
            currentStartDistance /= weatherMultiplier;
            currentEndDistance /= weatherMultiplier;
        }

        // Apply fog distances
        RenderSettings.fogStartDistance = currentStartDistance;
        RenderSettings.fogEndDistance = currentEndDistance;

        // Apply color
        RenderSettings.fogColor = fogColor;
    }

    private void UpdateWeather()
    {
        // Gradually transition rain intensity for smooth fog changes
        rainIntensity = Mathf.Lerp(rainIntensity, targetRainIntensity, Time.deltaTime * weatherTransitionSpeed);
    }

    private void OnDisable()
    {
        // Restore original fog settings if this component is disabled
        RestoreOriginalFogSettings();
    }

    private void RestoreOriginalFogSettings()
    {
        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;
        RenderSettings.fogStartDistance = originalFogStartDistance;
        RenderSettings.fogEndDistance = originalFogEndDistance;
        RenderSettings.fogMode = originalFogMode;
    }

    // Public methods for interacting with the fog system
    /// Adjusts the fog distance settings at runtime
    public void SetFogDistances(float start, float end)
    {
        startDistance = start;
        endDistance = end;
        ApplyFogSettings();
    }

    /// Sets fog color manually
    public void SetFogColor(Color color)
    {
        fogColor = color;
        ApplyFogSettings();
    }

    /// Triggers a weather change
    public void SetWeather(float rain, float transitionTime = 2f)
    {
        simulateWeather = true;
        targetRainIntensity = Mathf.Clamp01(rain);
        weatherTransitionSpeed = 1f / Mathf.Max(0.1f, transitionTime);
    }

    /// Enable or disable fog
    public void SetFogEnabled(bool enabled)
    {
        useDistanceFog = enabled;

        if (enabled)
        {
            ApplyFogSettings();
        }
        else
        {
            RestoreOriginalFogSettings();
        }
    }
}
