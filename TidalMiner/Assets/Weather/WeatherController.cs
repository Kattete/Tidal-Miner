using UnityEngine;
using UnityEngine.UI;

public class WeatherController : MonoBehaviour
{
    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem rainParticleSystem;

    [Header("Light")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private float minLightIntensity = 0.2f;
    [SerializeField] private float maxLightIntensity = 1.0f;

    [Header("Weather Controls")]
    [Range(0, 10)]
    [SerializeField] private float weatherIntensity = 0;

    // Store initial emission rates and particle counts
    private float initialRainEmissionRate;
    private int initialRainMaxParticles;

    // Maximum multiplier for max particles (at intensity 10)
    [SerializeField] private float maxParticleMultiplier = 3.0f;

    private void Start()
    {
        // Store initial emission rates
        if (rainParticleSystem != null)
        {
            var rainEmission = rainParticleSystem.emission;
            initialRainEmissionRate = rainEmission.rateOverTime.constant;
            initialRainMaxParticles = rainParticleSystem.main.maxParticles;
        }

        // Apply initial settings
        UpdateWeatherEffects();
    }

    private void UpdateWeatherEffects()
    {
        // Update rain particle system
        if (rainParticleSystem != null)
        {
            // Update emission rate
            var rainEmission = rainParticleSystem.emission;
            rainEmission.rateOverTime = initialRainEmissionRate * weatherIntensity;

            // Update max particles
            var rainMain = rainParticleSystem.main;
            float particleMultiplier = 1 + ((maxParticleMultiplier - 1) * (weatherIntensity / 10f));
            rainMain.maxParticles = Mathf.RoundToInt(initialRainMaxParticles * particleMultiplier);
        }
        // Update directional light - darker as intensity increases
        if (directionalLight != null)
        {
            // Calculate light intensity (inverse relationship with rain intensity)
            // When weatherIntensity = 0, light is at max; when weatherIntensity = 10, light is at min
            float lightIntensityRange = maxLightIntensity - minLightIntensity;
            float normalizedWeatherIntensity = weatherIntensity / 10f;
            directionalLight.intensity = maxLightIntensity - (lightIntensityRange * normalizedWeatherIntensity);
        }
    }

    public float GetWeatherIntensity()
    {
        return weatherIntensity;
    }

    private void Update()
    {
        UpdateWeatherEffects();
    }
}
