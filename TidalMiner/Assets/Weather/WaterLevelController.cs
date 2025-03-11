using UnityEngine;

public class WaterLevelController : MonoBehaviour
{
    [Header("Water Level Settings")]
    [SerializeField] private float minWaterHeight = -10f; // Water height when dry
    [SerializeField] private float maxWaterHeight = 0f; // Water height when fully flooded
    [SerializeField] private float waterRiseSpeed = 0.2f; // How quickly water rises
    [SerializeField] private float waterLowerSpeed = 0.1f; // How quickly water lowers

    [Header("Weather Integration")]
    [SerializeField] private WeatherController weatherController;
    [SerializeField] private float minRainIntensityForRising = 2f; // Min rain intensity to start rising

    [Header("Visual Effects")]
    [SerializeField] private float maxWaveHeight = 0.3f; // Maximum wave height at highest rain intensity
    [SerializeField] private float maxRippleStrength = 0.5f; // Maximum ripple strength at highest rain intensity
    [SerializeField] private float maxFlowSpeed = 1.0f; // Maximum flow speed at highest rain intensity
    [SerializeField] private Material waterMaterial; // Material for the water plane

    // Private variables
    private float currentWaterHeight;
    private float targetWaterHeight;

    private void Start()
    {
        // Find the weather controller if not assigned
        if (weatherController == null)
        {
            weatherController = FindObjectOfType<WeatherController>();
        }

        // Set initial water height
        currentWaterHeight = minWaterHeight;
        transform.position = new Vector3(transform.position.x, currentWaterHeight, transform.position.z);

        // Initialize water material properties
        if (waterMaterial == null && GetComponent<Renderer>() != null)
        {
            waterMaterial = GetComponent<Renderer>().material;
        }

        if (waterMaterial != null)
        {
            // Set initial shader properties to calm water
            waterMaterial.SetFloat("_WaveHeight", 0.01f);
            waterMaterial.SetFloat("_FlowSpeed", 0.1f);
            waterMaterial.SetFloat("_RippleStrength", 0f);
        }
    }

    private void Update()
    {
        // Calculate target water height based on rain intensity
        UpdateTargetWaterHeight();

        // Smoothly move water level toward target
        UpdateWaterHeight();

        // Update water visual effects based on rain intensity
        UpdateWaterVisuals();
    }

    private void UpdateTargetWaterHeight()
    {
        if (weatherController != null)
        {
            float rainIntensity = weatherController.GetWeatherIntensity();

            // Only start rising water when rain is significant
            if (rainIntensity > minRainIntensityForRising)
            {
                // Calculate how much of the way from min to max the water should rise
                float normalizedRain = Mathf.Clamp01((rainIntensity - minRainIntensityForRising) /
                                                    (10f - minRainIntensityForRising));

                // Set target height
                targetWaterHeight = Mathf.Lerp(minWaterHeight, maxWaterHeight, normalizedRain);
            }
            else
            {
                // When not raining enough, water should recede
                targetWaterHeight = minWaterHeight;
            }
        }
    }

    private void UpdateWaterHeight()
    {
        // Determine whether water should rise or lower
        if (currentWaterHeight < targetWaterHeight)
        {
            // Water is rising
            currentWaterHeight += waterRiseSpeed * Time.deltaTime;
            if (currentWaterHeight > targetWaterHeight)
            {
                currentWaterHeight = targetWaterHeight;
            }
        }
        else if (currentWaterHeight > targetWaterHeight)
        {
            // Water is lowering
            currentWaterHeight -= waterLowerSpeed * Time.deltaTime;
            if (currentWaterHeight < targetWaterHeight)
            {
                currentWaterHeight = targetWaterHeight;
            }
        }

        // Update water plane position
        transform.position = new Vector3(transform.position.x, currentWaterHeight, transform.position.z);
    }

    private void UpdateWaterVisuals()
    {
        if (waterMaterial != null && weatherController != null)
        {
            float rainIntensity = weatherController.GetWeatherIntensity();
            float normalizedIntensity = rainIntensity / 10f;

            // Update wave height based on rain intensity
            float waveHeight = Mathf.Lerp(0.05f, maxWaveHeight, normalizedIntensity);
            waterMaterial.SetFloat("_WaveHeight", waveHeight);

            // Update water flow speed based on rain intensity
            float flowSpeed = Mathf.Lerp(0.1f, maxFlowSpeed, normalizedIntensity);
            waterMaterial.SetFloat("_FlowSpeed", flowSpeed);

            // Update ripple strength based on rain intensity
            float rippleStrength = normalizedIntensity * maxRippleStrength;
            waterMaterial.SetFloat("_RippleStrength", rippleStrength);
        }
    }

    // Optional method to force water level for game events
    public void SetWaterLevel(float normalizedLevel)
    {
        normalizedLevel = Mathf.Clamp01(normalizedLevel);
        targetWaterHeight = Mathf.Lerp(minWaterHeight, maxWaterHeight, normalizedLevel);
    }

    // Method to get the current water height
    public float GetCurrentWaterHeight()
    {
        return currentWaterHeight;
    }
}