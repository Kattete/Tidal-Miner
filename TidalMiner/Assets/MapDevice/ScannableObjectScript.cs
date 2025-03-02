using NUnit.Framework;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.Events;

public class ScannableObjectScript : MonoBehaviour
{
    [Header("Scannable settings")]
    [SerializeField] private string objectID;
    [SerializeField] private string objectName;
    [TextArea(3,5)]
    [SerializeField] private string objectDescription;
    [SerializeField] private Transform hologramAttachPoint;
    [SerializeField] private bool isCollectible = false;

    [Header("Visual Feedback")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private GameObject highlighEffect;
    [SerializeField] private float pulseFrequency = 1.5f;
    [SerializeField] private float pulseIntensity = 0.2f;

    [Header("Events")]
    [SerializeField] private UnityEvent onDetected;
    [SerializeField] private UnityEvent onHologramDisplayed;
    [SerializeField] private UnityEvent onHOlogramHidden;
    [SerializeField] private UnityEvent onCollected;

    private bool isDetected = false;
    private bool isHologramDisplayed = false;
    private Renderer[] renderers;
    private float pulseTimer = 0f;

    private List<GameObject> activeEffects = new List<GameObject>();

    private void Awake()
    {
        // Cache renderers
        renderers = GetComponentsInChildren<Renderer>();

        if (hologramAttachPoint == null)
        {
            hologramAttachPoint = transform;
        }

        if(highlighEffect != null)
        {
            highlighEffect.SetActive(false);
        }
    }

    private void Update()
    {
        if (isHologramDisplayed)
        {
            // Pulse effect for hologram display
            pulseTimer += Time.deltaTime * pulseFrequency;
            UpdatePulseEffect();
        }
    }

    public void OnDetected()
    {
        isDetected = true;
        onDetected?.Invoke();

        Debug.Log($"Scanner detected: {objectName} ({objectID})");
    }

    public void OnHologramDisplayed()
    {
        isHologramDisplayed = true;
        onHologramDisplayed?.Invoke();

        if(highlighEffect != null)
        {
            highlighEffect.SetActive(true);
        }

        if(highlightMaterial != null)
        {
            foreach (var renderer in renderers)
            {
                // Store original materials to restore later
                Material[] originalMaterials = renderer.materials;
                Material[] newMaterials = new Material[originalMaterials.Length];

                for(int i = 0; i<originalMaterials.Length; i++)
                {
                    newMaterials[i] = highlightMaterial;
                }

                renderer.materials = newMaterials;
            }
        }
        // Reset pulse timer
        pulseTimer = 0f;
    }

    public void OnHologramHidden()
    {
        isHologramDisplayed = false;
        onHologramDisplayed?.Invoke();

        if(highlighEffect != null)
        {
            highlighEffect.SetActive(false);
        }

        if(normalMaterial != null && highlightMaterial != null)
        {
            foreach(var renderer in renderers){
                Material[] currentMaterials = renderer.materials;
                Material[] restoreMaterials = new Material[currentMaterials.Length];

                for(int i =0; i<currentMaterials.Length; i++)
                {
                    restoreMaterials[i] = normalMaterial;
                }
                renderer.materials = restoreMaterials;
            }
        }

        // Clear any active effects
        foreach (GameObject effect in activeEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }
        activeEffects.Clear();
    }

    public void OnLostDetection()
    {
        isDetected = false;

        if (isHologramDisplayed)
        {
            OnHologramHidden();
        }
    }

    public void OnCollect()
    {
        if (!isCollectible) return;
        onCollected?.Invoke();

        // GAME INVENTORY SYSTEM HERE
        Debug.Log($"Collected item: {objectName}");
        gameObject.SetActive(false);
    }

    public Transform GetHologramAttachPoint()
    {
        return hologramAttachPoint;
    }

    public(string name, string description) GetObjectInfo()
    {
        return (objectName, objectDescription);
    }

    private void UpdatePulseEffect()
    {
        if (renderers.Length == 0) return;
        //  calculate pulse intensity
        float pulseValue = Mathf.Sin(pulseTimer) * 0.5f + 0.5f;

        // Apply emission
        if (highlightMaterial != null && highlightMaterial.HasProperty("_EmissionColor"))
        {
            Color baseEmission = highlightMaterial.GetColor("_EmissionColor");
            Color pulsedEmission = baseEmission * (1f + pulseValue * pulseIntensity);

            foreach (var renderer in renderers)
            {
                foreach (Material mat in renderer.materials)
                {
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", pulsedEmission);
                    }
                }
            }
        }

        if(highlighEffect != null)
        {
            float scale = 1f + pulseValue * pulseIntensity * 0.1f;
            highlighEffect.transform.localScale = new Vector3(scale, scale, scale);
        }
    }

    public void AddEffect(GameObject effectPrefab)
    {
        if (effectPrefab == null) return;
        GameObject effect = Instantiate(effectPrefab, hologramAttachPoint.position, Quaternion.identity);
        effect.transform.SetParent(hologramAttachPoint);
        activeEffects.Add(effect);
    }
}
