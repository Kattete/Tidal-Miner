using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SnowController : MonoBehaviour
{
    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem snowParticleSystem;

    [Header("Light")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private float minLightIntensity = 0.2f;
    [SerializeField] private float maxLightIntensity = 1.0f;

    [Header("Weather Controls")]
    [Range(0, 10)]
    [SerializeField] private float snowIntensity = 0;
    [SerializeField] private float maxParticleMultiplier = 3.0f;

    [Header("Snow Accumulation")]
    [SerializeField] private float snowAccumulationRate = 0.01f; // How quickly snow builds up
    [SerializeField] private float snowMeltRate = 0.02f; // How quickly snow melts
    [SerializeField] private float maxSnowHeight = 1.0f; // Maximum snow height
    [SerializeField] private Material[] snowableMaterials; // Materials that can receive snow
    [SerializeField] private string snowHeightProperty = "_SnowAmount"; // Property in shader for snow height

    // Store initial emission rates and particle counts
    private float initialSnowEmissionRate;
    private int initialSnowMaxParticles;
    private float currentSnowAmount = 0f;
    private float targetSnowAmount = 0f;

    private void Start()
    {
        // Store initial emission rates
        if (snowParticleSystem != null)
        {
            var snowEmission = snowParticleSystem.emission;
            initialSnowEmissionRate = snowEmission.rateOverTime.constant;
            initialSnowMaxParticles = snowParticleSystem.main.maxParticles;
        }

        // Apply initial settings
        UpdateSnowEffects();
    }

    public void SetSnowIntensity(float intensity)
    {
        snowIntensity = Mathf.Clamp(intensity, 0, 10);
    }

    private void UpdateSnowEffects()
    {
        // Update snow particle system
        if (snowParticleSystem != null)
        {
            // Update emission rate
            var snowEmission = snowParticleSystem.emission;
            snowEmission.rateOverTime = initialSnowEmissionRate * snowIntensity;

            // Update max particles
            var snowMain = snowParticleSystem.main;
            float particleMultiplier = 1 + ((maxParticleMultiplier - 1) * (snowIntensity / 10f));
            snowMain.maxParticles = Mathf.RoundToInt(initialSnowMaxParticles * particleMultiplier);
        }

        // Update directional light - darker as intensity increases (if not controlled by another system)
        if (directionalLight != null)
        {
            // Check if another weather controller is affecting the light
            WeatherController rainController = FindObjectOfType<WeatherController>();
            if (rainController == null || rainController.GetRainIntensity() == 0)
            {
                // Calculate light intensity (inverse relationship with snow intensity)
                float lightIntensityRange = maxLightIntensity - minLightIntensity;
                float normalizedSnowIntensity = snowIntensity / 10f;
                directionalLight.intensity = maxLightIntensity - (lightIntensityRange * normalizedSnowIntensity);
            }
        }

        // Calculate target snow amount based on current snow intensity
        targetSnowAmount = (snowIntensity / 10f) * maxSnowHeight;
    }

    private void UpdateSnowAccumulation()
    {
        // Gradually change current snow amount toward target amount
        if (currentSnowAmount < targetSnowAmount)
        {
            // Snow is accumulating
            currentSnowAmount += snowAccumulationRate * Time.deltaTime * snowIntensity;
            if (currentSnowAmount > targetSnowAmount)
                currentSnowAmount = targetSnowAmount;
        }
        else if (currentSnowAmount > targetSnowAmount)
        {
            // Snow is melting
            currentSnowAmount -= snowMeltRate * Time.deltaTime;
            if (currentSnowAmount < targetSnowAmount)
                currentSnowAmount = targetSnowAmount;
        }

        // Apply snow amount to all snow-compatible materials
        foreach (Material mat in snowableMaterials)
        {
            if (mat != null && mat.HasProperty(snowHeightProperty))
            {
                mat.SetFloat(snowHeightProperty, currentSnowAmount);
            }
        }
    }

    public float GetSnowIntensity()
    {
        return snowIntensity;
    }

    public float GetSnowAmount()
    {
        return currentSnowAmount;
    }

    public float GetMaxSnowHeight()
    {
        return maxSnowHeight;
    }

    private void Update()
    {
        UpdateSnowEffects();
        UpdateSnowAccumulation();
    }

    // Optional: Method to generate snow footprints (can be called by player movement)
    public void CreateFootprint(Vector3 position, Quaternion rotation, float depth)
    {
        // Only create footprints if there's enough snow
        if (currentSnowAmount > 0.3f)
        {
            // Implementation depends on your footprint system
            // Could instantiate a footprint prefab, modify a terrain, etc.
            Debug.Log("Snow footprint created at " + position);
        }
    }
}