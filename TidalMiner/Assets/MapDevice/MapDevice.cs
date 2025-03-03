using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MapDevice : MonoBehaviour
{
    [Header("Scanner Settings")]
    [SerializeField] private float scanRadius = 5f;
    [SerializeField] private float scanFrequency = 0.5f; // How often to scan (in seconds)
    [SerializeField] private float hologramShowAngle = 30f; // Angle within which to show hologram
    [SerializeField] private LayerMask scannableLayer; // Layer for scannable objects

    [Header("Visual Elements")]
    [SerializeField] private GameObject mapDeviceUI; // The UI panel that contains the radar
    [SerializeField] private RectTransform radarDisplay; // The circular radar display
    [SerializeField] private GameObject dotPrefab; // Red dot prefab to instantiate on radar
    [SerializeField] private RectTransform radarSweeperImage;

    [Header("Audio")]
    [SerializeField] private AudioClip scanSound;
    [SerializeField] private AudioClip objectFoundSound;
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Input")]
    [SerializeField] private InputActionReference activateScannerAction;

    // Private variables
    private bool isScanning = false;
    private float scanTimer = 0f;
    private List<ScannableObjectScript> detectedObjects = new List<ScannableObjectScript>();
    private Dictionary<ScannableObjectScript, GameObject> radarDots = new Dictionary<ScannableObjectScript, GameObject>();
    private Dictionary<ScannableObjectScript, GameObject> hologramEffects = new Dictionary<ScannableObjectScript, GameObject>();
    private Transform playerCamera;

    // Sweep animation variables;
    private float sweepRoation = 0f;
    private float currentAngle = 0f;
    [SerializeField] private float sweepSpeed = 120f;
    [SerializeField] private float sweepDetectionAngle = 20f;
    [SerializeField] private float sweepAngleOffset = 180f;
    [SerializeField] private float dotFadeTime = 5f;
    [SerializeField] private float dotFadeDuration = 2f;
    private Dictionary<ScannableObjectScript, float> objectAngles = new Dictionary<ScannableObjectScript, float>(); // track radar angles
    private Dictionary<ScannableObjectScript, float> dotVisibilityTimer = new Dictionary<ScannableObjectScript, float>();
    private Dictionary<ScannableObjectScript, bool> hasBeenSwept = new Dictionary<ScannableObjectScript, bool>();

    // Cache
    private List<ScannableObjectScript> objectsToRemove = new List<ScannableObjectScript>();

    private void Awake()
    {
        // Find the main camera if not directly assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main.transform;
        }

        // Set up scanner UI initially hidden
        if (mapDeviceUI != null)
        {
            mapDeviceUI.SetActive(false);
        }

        // Make sure we have an audio source
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        // Enable input action
        if (activateScannerAction != null)
        {
            activateScannerAction.action.Enable();
            activateScannerAction.action.performed += OnToggleScanner;
        }
    }

    private void OnDisable()
    {
        // Disable input action
        if (activateScannerAction != null)
        {
            activateScannerAction.action.performed -= OnToggleScanner;
            activateScannerAction.action.Disable();
        }

        // Clean up hologram effects when disabled
        CleanupHolograms();
    }

    private void OnToggleScanner(InputAction.CallbackContext context)
    {
        isScanning = !isScanning;

        // Toggle the UI
        if (mapDeviceUI != null)
        {
            mapDeviceUI.SetActive(isScanning);
        }

        // Play activation sound
        if (audioSource != null && activationSound != null && isScanning)
        {
            audioSource.PlayOneShot(activationSound);
        }

        // Clear radar when deactivated
        if (!isScanning)
        {
            ClearRadar();
            CleanupHolograms();
        }
    }

    private void Update()
    {
        if (!isScanning) return;

        // Increment scan timer
        scanTimer += Time.deltaTime;

        sweepRoation = (sweepRoation + sweepSpeed * Time.deltaTime) % 360;
        if(radarSweeperImage != null)
        {
            radarSweeperImage.localRotation = Quaternion.Euler(0, 0, -sweepRoation);
        }

        // Perform scan at regular intervals
        if (scanTimer >= scanFrequency)
        {
            PerformScan();
            scanTimer = 0f;
        }

        // Update radar dots positions
        UpdateRadarDisplay();

        // Check which objects are in view to show holograms
        UpdateHolograms();

        // Update dot visibility based on current sweep position;
        UpdateDotVisibility(sweepRoation);
    }

    private void PerformScan()
    {
        // Find all scannable objects within radius
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, scanRadius, scannableLayer);

        // Play scan sound
        if (audioSource != null && scanSound != null)
        {
            audioSource.PlayOneShot(scanSound, 0.5f);
        }

        // Track which objects we need to remove (no longer in range)
        objectsToRemove.Clear();
        objectsToRemove.AddRange(detectedObjects);

        foreach (var hitCollider in hitColliders)
        {
            // Get the ScannableObject component
            ScannableObjectScript scannableObject = hitCollider.GetComponent<ScannableObjectScript>();

            if (scannableObject != null)
            {
                // Check if this is a new object
                if (!detectedObjects.Contains(scannableObject))
                {
                    // Add new object to detected list
                    detectedObjects.Add(scannableObject);

                    // Create radar dot for this object
                    CreateRadarDot(scannableObject, false);

                    // Initialize its sweep state
                    hasBeenSwept[scannableObject] = false;

                    // Initialize the object
                    scannableObject.OnDetected();
                }
                else
                {
                    // Object was already detected, remove from the to-remove list
                    objectsToRemove.Remove(scannableObject);
                }
            }
        }

        // Remove objects that are no longer in range
        foreach (var obj in objectsToRemove)
        {
            RemoveDetectedObject(obj);
        }
    }

    private void CreateRadarDot(ScannableObjectScript scannableObject, bool initiallyVisible)
    {
        if (radarDisplay == null || dotPrefab == null) return;

        // Instantiate dot prefab as child of radar display
        GameObject dot = Instantiate(dotPrefab, radarDisplay);

        dot.SetActive(initiallyVisible);

        Image dotImage = dot.GetComponent<Image>();
        if(dotImage != null)
        {
            Color color = dotImage.color;
            dotImage.color = new Color(color.r, color.g, color.b, initiallyVisible ? 1f : 0f);
        }

        // Store reference to the dot
        radarDots[scannableObject] = dot;
    }

    private void UpdateRadarDisplay()
    {
        if (radarDisplay == null) return;

        // Get radar display dimensions
        float radarRadius = radarDisplay.rect.width * 0.5f;

        Vector3 playerForward = playerCamera.forward;
        playerForward.y = 0;
        playerForward.Normalize();

        float playerAngle = Vector3.SignedAngle(Vector3.forward, playerForward, Vector3.up);

        foreach (var obj in detectedObjects)
        {
            if (obj == null) continue;

            // Get radar dot for this object
            if (radarDots.TryGetValue(obj, out GameObject dot))
            {
                if (dot == null) continue;

                // Calculate relative position from player to object
                Vector3 relativePos = obj.transform.position - transform.position;

                // Calculate 2D position on radar (normalized by scan radius)
                float distance = relativePos.magnitude;
                float normalizedDist = Mathf.Clamp01(distance / scanRadius);

                // Convert to radar space coordinates
                Vector3 flatRelative = new Vector3(relativePos.x, 0, relativePos.z);
                float objectAngle = Vector3.SignedAngle(Vector3.forward, flatRelative, Vector3.up);

                float radarAngle = objectAngle - playerAngle;
                radarAngle = (radarAngle + 360) % 360;
                objectAngles[obj] = radarAngle;
                float radarAngleRad = radarAngle * Mathf.Deg2Rad;
                

                // Calculate position on radar
                float x = Mathf.Sin(radarAngleRad) * normalizedDist * radarRadius;
                float y = Mathf.Cos(radarAngleRad) * normalizedDist * radarRadius;

                // Apply position to dot's RectTransform
                RectTransform dotRect = dot.GetComponent<RectTransform>();
                if (dotRect != null)
                {
                    dotRect.anchoredPosition = new Vector2(x,y);
                }
            }
        }
    }

    private void UpdateDotVisibility(float sweepAngle)
    {
        // Normalize sweep angle to 0-360 range
        sweepAngle = (sweepAngle + 360) % 360;

        // Update timers and fade for all objects
        float deltaTime = Time.deltaTime;

        foreach (var obj in detectedObjects)
        {
            if (obj == null || !objectAngles.ContainsKey(obj) || !radarDots.TryGetValue(obj, out GameObject dot))
                continue;

            // Get the radar angle for this object
            float objAngle = objectAngles[obj];

            float correctedSweepAngle = (sweepAngle + sweepAngleOffset) % 360;

            // Calculate angular distance (accounting for 0/360 boundary)
            float angleDiff = Mathf.Abs(correctedSweepAngle - objAngle);
            if (angleDiff > 180) angleDiff = 360 - angleDiff;

            // Check if sweeper beam is passing over this object
            bool isInSweep = angleDiff <= sweepDetectionAngle;

            // Get the dot's Image component
            Image dotImage = dot.GetComponent<Image>();
            if (dotImage == null) continue;

            // Is the dot currently visible?
            bool isVisible = dot.activeSelf;

            bool isFirstSweep = isInSweep && hasBeenSwept.ContainsKey(obj) && !hasBeenSwept[obj];
            if (isInSweep)
            {
                // Sweeper is over the object - refresh its visibility
                dotVisibilityTimer[obj] = 0f; // Reset the timer

                if (isFirstSweep || !isVisible)
                {
                    // Object wasn't visible before - "ping" it
                    dot.SetActive(true);
                    dotImage.color = new Color(dotImage.color.r, dotImage.color.g, dotImage.color.b, 1f); // Full opacity

                    // Play ping sound
                    if (audioSource != null && objectFoundSound != null)
                    {
                        audioSource.PlayOneShot(objectFoundSound, 0.3f);
                    }
                    hasBeenSwept[obj] = true;
                }
            }
            else
            {
                if(hasBeenSwept.ContainsKey(obj) && hasBeenSwept[obj])
                {
                    // Object not under sweeper - update its fade timer
                    if (dotVisibilityTimer.ContainsKey(obj))
                    {
                        dotVisibilityTimer[obj] += deltaTime;

                        // Calculate fade based on timer
                        float timeSinceDetection = dotVisibilityTimer[obj];

                        if (timeSinceDetection > dotFadeTime)
                        {
                            // Start fading the dot
                            float fadeProgress = (timeSinceDetection - dotFadeTime) / dotFadeDuration;
                            fadeProgress = Mathf.Clamp01(fadeProgress);

                            // Apply fade to the dot image
                            float alpha = 1.0f - fadeProgress;
                            dotImage.color = new Color(dotImage.color.r, dotImage.color.g, dotImage.color.b, alpha);

                            // Don't completely hide the dot - keep it at very low opacity
                            if (alpha <= 0.1f)
                            {
                                dotImage.color = new Color(dotImage.color.r, dotImage.color.g, dotImage.color.b, 0.1f);
                            }
                        }
                    }
                    else
                    {
                        // Initialize timer for objects we haven't tracked yet
                        dotVisibilityTimer[obj] = 0f;
                    }
                }
                
            }
        }
    }

    public float GetCurrentSweepAngle()
    {
        return currentAngle;
    }

    private void UpdateHolograms()
    {
        foreach (var obj in detectedObjects)
        {
            if (obj == null) continue;

            // Calculate direction to object
            Vector3 directionToObject = (obj.transform.position - playerCamera.position).normalized;

            // Check if object is in front of the player (dot product with camera forward)
            float dotProduct = Vector3.Dot(playerCamera.forward, directionToObject);

            // Calculate angle between camera forward and direction to object
            float angleToObject = Vector3.Angle(playerCamera.forward, directionToObject);

            // Check if object is within the show angle
            bool shouldShowHologram = (angleToObject <= hologramShowAngle) && (dotProduct > 0);

            // Get the current hologram status
            bool hasHologram = hologramEffects.ContainsKey(obj);

            // Update hologram visibility only when there's a change in status
            if (shouldShowHologram && !hasHologram)
            {
                // Only show the hologram if it's not already showing
                ShowHologram(obj);
            }
            else if (!shouldShowHologram && hasHologram)
            {
                // Only hide the hologram if it's currently showing
                HideHologram(obj);
            }
            // Don't do anything if the status hasn't changed
        }
    }

    private void ShowHologram(ScannableObjectScript obj)
    {
        // Only notify once and keep track that we've shown this hologram
        hologramEffects[obj] = null; // We're just using this as a status tracker

        // This will create the hologram through the HologramEffect component
        obj.OnHologramDisplayed();
    }

    private void HideHologram(ScannableObjectScript obj)
    {
        // Only notify the object if we previously told it to show a hologram
        if (hologramEffects.ContainsKey(obj))
        {
            // Notify the object that its hologram should be hidden
            obj.OnHologramHidden();

            // Remove from our tracking dictionary
            hologramEffects.Remove(obj);
        }
    }

    private void RemoveDetectedObject(ScannableObjectScript obj)
    {
        detectedObjects.Remove(obj);

        // Remove radar dot
        if (radarDots.TryGetValue(obj, out GameObject dot) && dot != null)
        {
            Destroy(dot);
        }
        radarDots.Remove(obj);

        // Remove hologram if it exists
        HideHologram(obj);

        // Notify the object it's no longer detected
        if (obj != null)
        {
            obj.OnLostDetection();
        }
    }

    private void ClearRadar()
    {
        // Destroy all dots on the radar
        foreach (var dot in radarDots.Values)
        {
            if (dot != null)
            {
                Destroy(dot);
            }
        }

        radarDots.Clear();
        detectedObjects.Clear();
    }

    private void CleanupHolograms()
    {
        // Properly notify each object to hide its hologram
        foreach (var entry in hologramEffects)
        {
            ScannableObjectScript obj = entry.Key;
            if (obj != null)
            {
                // Tell the object to hide its hologram
                obj.OnHologramHidden();
            }
        }

        // Clear the tracking dictionary
        hologramEffects.Clear();
    }

    // For debugging
    private void OnDrawGizmosSelected()
    {
        // Draw scan radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, scanRadius);

        // Draw hologram view angle
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 forward = playerCamera.forward;
            Vector3 right = Quaternion.Euler(0, hologramShowAngle, 0) * forward;
            Vector3 left = Quaternion.Euler(0, -hologramShowAngle, 0) * forward;

            Gizmos.DrawRay(playerCamera.position, forward * scanRadius);
            Gizmos.DrawRay(playerCamera.position, right * scanRadius);
            Gizmos.DrawRay(playerCamera.position, left * scanRadius);
        }
    }
}
