using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class ScannableObjectScript : MonoBehaviour
{
    [Header("Scannable Settings")]
    [SerializeField] private string objectID;
    [SerializeField] private string objectName;
    [TextArea(3, 5)]
    [SerializeField] private string objectDescription;
    [SerializeField] private Transform hologramAttachPoint;
    [SerializeField] private bool isCollectible = false;

    [Header("Events")]
    [SerializeField] private UnityEvent onDetected;
    [SerializeField] private UnityEvent onHologramDisplayed;
    [SerializeField] private UnityEvent onHologramHidden;
    [SerializeField] private UnityEvent onCollected;

    // Internal state
    private bool isDetected = false;
    private HologramEffect hologramEffect;

    // For any additional effects that might be added
    private List<GameObject> activeEffects = new List<GameObject>();

    private void Awake()
    {
        // Add and cache the hologram effect component
        hologramEffect = gameObject.AddComponent<HologramEffect>();

        // Set default hologram attach point if not specified
        if (hologramAttachPoint == null)
        {
            hologramAttachPoint = transform;
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
        if (hologramEffect != null)
        {
            hologramEffect.CreateHologram();
        }

        onHologramDisplayed?.Invoke();
    }

    public void OnHologramHidden()
    {
        if (hologramEffect != null)
        {
            hologramEffect.DestroyHologram();
        }

        onHologramHidden?.Invoke();
    }

    public void OnLostDetection()
    {
        isDetected = false;

        // Make sure hologram is hidden
        OnHologramHidden();
    }

    public void OnCollect()
    {
        if (!isCollectible) return;

        onCollected?.Invoke();

        // Hook for game inventory system
        Debug.Log($"Collected item: {objectName}");

        // Disable the object
        gameObject.SetActive(false);
    }

    public Transform GetHologramAttachPoint()
    {
        return hologramAttachPoint;
    }

    public (string name, string description) GetObjectInfo()
    {
        return (objectName, objectDescription);
    }

    public void AddEffect(GameObject effectPrefab)
    {
        if (effectPrefab == null) return;

        GameObject effect = Instantiate(effectPrefab, hologramAttachPoint.position, Quaternion.identity);
        effect.transform.SetParent(hologramAttachPoint);
        activeEffects.Add(effect);
    }

    public bool IsCollectible()
    {
        return isCollectible;
    }

    private void OnDestroy()
    {
        foreach (var effect in activeEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }
        activeEffects.Clear();
    }
}