using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CollectibleItem : MonoBehaviour
{
    [Header("Item Settings")]
    [SerializeField] private string itemName = "Item";
    [SerializeField] private string itemDescription = "A collectible item";
    [SerializeField] private string itemID;
    [SerializeField] private bool isAutoCollect = false;
    [SerializeField] private float interactionDistance = 3f;

    [Header("UI Prompt")]
    [SerializeField] private GameObject interactionPromptPrefab;
    [SerializeField] private Vector3 promptOffset = new Vector3(0, 0.5f, 0);
    [SerializeField] private string promptText = "Press E to collect";
    [SerializeField] private float promptScale = 0.2f; // Control the scale of the prompt

    // References
    private GameObject promptInstance;
    private TextMeshProUGUI promptTextComponent;
    private Camera mainCamera;
    private ScannableObjectScript scannableScript;

    // States
    private bool isInRange = false;
    private bool isLookingAt = false;
    private Transform player;
    private InventorySystem playerInventory;

    private void Awake()
    {
        // Find the main camera
        mainCamera = Camera.main;

        // Get or add ScannableObjectScript component
        scannableScript = GetComponent<ScannableObjectScript>();
        if (scannableScript == null)
        {
            scannableScript = gameObject.AddComponent<ScannableObjectScript>();
        }

        // Mark this object as collectible
        MarkAsCollectible();
    }

    private void Start()
    {
        // Set the layer to Scannable
        gameObject.layer = LayerMask.NameToLayer("Scannable");

        // Create the interaction prompt
        CreatePrompt();

        // Find the player
        FindPlayer();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerInventory = playerObj.GetComponent<InventorySystem>();
            if (playerInventory == null)
            {
                playerInventory = playerObj.GetComponentInChildren<InventorySystem>();
            }
        }
        else
        {
            Debug.LogWarning("No GameObject with tag 'Player' found. Make sure your player has this tag.");
        }
    }

    private void CreatePrompt()
    {
        if (interactionPromptPrefab != null)
        {
            // Calculate position
            Vector3 promptPosition = transform.position + promptOffset;

            // Instantiate at the correct position
            promptInstance = Instantiate(interactionPromptPrefab, promptPosition, Quaternion.identity);

            // Scale the prompt to be smaller
            promptInstance.transform.localScale = Vector3.one * promptScale;

            // Set the text if it exists
            promptTextComponent = promptInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (promptTextComponent != null)
            {
                promptTextComponent.text = promptText;
            }
            else
            {
                // Try with regular Text component
                Text regularText = promptInstance.GetComponentInChildren<Text>();
                if (regularText != null)
                {
                    regularText.text = promptText;
                }
            }

            // Hide prompt initially
            promptInstance.SetActive(false);
        }
    }

    private void Update()
    {
        // Make sure we have found the player
        if (player == null)
        {
            FindPlayer();
            return;
        }

        // Check if player is in range
        CheckPlayerInRange();

        // Check if player is looking at this object
        CheckPlayerLookingAt();

        // Update prompt visibility and position
        UpdatePrompt();

        // Check for input to collect
        if (isInRange && isLookingAt && Input.GetKeyDown(KeyCode.E))
        {
            AttemptCollect();
        }
    }

    private void CheckPlayerInRange()
    {
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            isInRange = distance <= interactionDistance;
        }
        else
        {
            isInRange = false;
        }
    }

    private void CheckPlayerLookingAt()
    {
        if (mainCamera == null || !isInRange)
        {
            isLookingAt = false;
            return;
        }

        // Cast a ray from center of screen
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        // Raycast and check if it hits this object
        if (Physics.Raycast(ray, out hit, interactionDistance))
        {
            isLookingAt = hit.transform == transform || hit.transform.IsChildOf(transform);
        }
        else
        {
            isLookingAt = false;
        }
    }

    private void UpdatePrompt()
    {
        if (promptInstance != null)
        {
            // Determine if prompt should be visible
            bool shouldShowPrompt = isInRange && isLookingAt;

            // Update visibility
            if (promptInstance.activeSelf != shouldShowPrompt)
            {
                promptInstance.SetActive(shouldShowPrompt);
            }

            // If visible, update position and rotation
            if (shouldShowPrompt)
            {
                // Update position to stay above object
                promptInstance.transform.position = transform.position + promptOffset;

                // Make it face the camera (billboard effect)
                if (mainCamera != null)
                {
                    // This makes the prompt always face the camera
                    promptInstance.transform.LookAt(
                        promptInstance.transform.position + mainCamera.transform.rotation * Vector3.forward,
                        mainCamera.transform.rotation * Vector3.up
                    );
                }
            }
        }
    }

    private void AttemptCollect()
    {
        if (playerInventory != null)
        {
            bool collected = playerInventory.AddItem(gameObject, itemID);

            if (collected)
            {
                // Clean up the prompt
                if (promptInstance != null)
                {
                    Destroy(promptInstance);
                }

                Collider[] colliders = GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders)
                {
                    collider.enabled = false;
                }

                ScannableObjectScript scannable = GetComponent<ScannableObjectScript>();
                if(scannable != null)
                {
                    scannable.OnCollect();
                }
            }
        }
    }

    private void MarkAsCollectible()
    {
        // Use reflection to set isCollectible field
        var field = typeof(ScannableObjectScript).GetField("isCollectible",
                                                         System.Reflection.BindingFlags.Instance |
                                                         System.Reflection.BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(scannableScript, true);
        }
    }

    private void OnDestroy()
    {
        if (promptInstance != null)
        {
            Destroy(promptInstance);
        }
    }
}