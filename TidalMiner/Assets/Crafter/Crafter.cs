using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class Crafter : MonoBehaviour
{
    [Header("Crafting Settings")]
    [SerializeField] public CraftingRecipe currentRecipe; // The selected recipe
    [SerializeField] public Slider craftProgressBar;
    [SerializeField] public InventorySystem playerInventory; // Reference to player's inventory
    [SerializeField] public Transform spawnPoint; // Where the crafted item will drop
    [SerializeField] public GameObject craftingUI; // Reference to the crafting UI

    [Header("UI Prompt")]
    [SerializeField] public GameObject interactionPromptPrefab;
    [SerializeField] Vector3 promptOffset = new Vector3(0, 0.5f, 0);
    [SerializeField] string promptText = "Press E to craft";
    [SerializeField] float promptScale = 0.2f;

    [Header("Crafting UI Elements")]
    [SerializeField] private Button craftButton;

    // States
    private bool isCrafting = false;
    private float holdTime = 1f;
    private bool isLookingAt = false;
    private bool isInRange = false;
    private bool isCraftingUIOpen = false;

    // References
    private GameObject promptInstance;
    private TextMeshProUGUI promptTextComponent;
    private Transform player;
    private Camera mainCamera;
    private CollectibleItem collectibleItem;
    public Text interactionText;
    private PlayerInput playerInput;
    public GameObject recipeButtonPrefab;

    public GameObject recipeUIPrefab;
    public List<CraftingRecipe> availableRecipes;
    public GameObject recipeItemPrefab;
    public Transform recipeListParent;

    // TODO Sounds and animations

    private void Awake()
    {
        StartCoroutine(InitializeCraftComponents());
        CreatePrompt();
        mainCamera = Camera.main;

        if (craftingUI != null)
        {
            craftingUI.SetActive(false); // Hide crafting UI initially
        }

        if (craftProgressBar != null)
        {
            craftProgressBar.gameObject.SetActive(false); // Hide crafting progress bar
        }

        if (promptInstance != null)
        {
            promptInstance.SetActive(false); // Ensure prompt is off initially
        }

        if (craftButton != null)
        {
            craftButton.onClick.AddListener(StartCrafting); // Listen for button click
        }
        else
        {
            Debug.LogError("Craft Button is not assigned!");
        }
    }

    private void Start()
    {
        playerInput = FindAnyObjectByType<PlayerInput>(); // Ensure we reference PlayerInput
    }

    private IEnumerator InitializeCraftComponents()
    {
        yield return null;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerInventory = playerObj.GetComponent<InventorySystem>();
        }
    }

    private void Update()
    {
        if (player == null) return;

        CheckPlayerInRange();
        CheckPlayerLookingAt();
        UpdatePrompt();

        if (isInRange && isLookingAt && Input.GetKeyDown(KeyCode.E))
        {
            ToggleCraftingUI();
        }

        if (isCrafting)
        {
            HandleCraftingProgress();
        }
    }

    private void UpdateCraftingUI()
    {
        // Clear existing UI elements to avoid duplicates
        foreach (Transform child in recipeListParent)
        {
            Destroy(child.gameObject);
        }

        // Check if the recipeItemPrefab and recipeListParent are assigned
        if (recipeItemPrefab == null || recipeListParent == null)
        {
            Debug.LogError("Recipe Item Prefab or Recipe List Parent is not assigned!");
            return;
        }

        // Populate the UI with available recipes
        foreach (CraftingRecipe recipe in availableRecipes)
        {
            // Instantiate the recipe item UI element
            GameObject recipeItem = Instantiate(recipeItemPrefab, recipeListParent);

            // Get the TextMeshProUGUI component for the recipe name
            TextMeshProUGUI recipeText = recipeItem.GetComponentInChildren<TextMeshProUGUI>();
            if (recipeText != null)
            {
                recipeText.text = recipe.recipeName; // Set the recipe name
            }
            else
            {
                Debug.LogError("TextMeshProUGUI component not found in Recipe Item Prefab!");
            }

            // Get the Button component for selecting the recipe
            Button recipeButton = recipeItem.GetComponent<Button>();
            if (recipeButton != null)
            {
                // Add a listener to the button to select the recipe when clicked
                recipeButton.onClick.AddListener(() => SelectRecipe(recipe));
            }
            else
            {
                Debug.LogError("Button component not found in Recipe Item Prefab!");
            }
        }
    }

    // Select a recipe from the list
    private void SelectRecipe(CraftingRecipe recipe)
    {
        currentRecipe = recipe;
        Debug.Log("Selected Recipe: " + recipe.recipeName);
    }


    public void StartCrafting()
    {
        Debug.Log("Crafting started for: " + currentRecipe.recipeName);
        if (currentRecipe == null)
        {
            Debug.LogWarning("No recipe selected!");
            return;
        }
        // Prevent crafting if resources are missing
        if (!HasRequiredResources())
        {
            Debug.Log("Missing resources!");
            return;
        }

        isCrafting = true;
        holdTime = 1f;
        craftProgressBar.maxValue = currentRecipe.craftTime;
        craftProgressBar.gameObject.SetActive(true);
    }


    private void HandleCraftingProgress()
    {
        holdTime += Time.deltaTime;
        craftProgressBar.value = holdTime;

        if (holdTime >= currentRecipe.craftTime)
        {
            CompleteCrafting();
        }
    }

    // Stop the crafting process
    public void StopCrafting()
    {
        isCrafting = false;
        holdTime = 0f;
        craftProgressBar.value = 0;
        craftProgressBar.gameObject.SetActive(false);
    }

    private bool HasRequiredResources()
    {
        foreach (var req in currentRecipe.requiredResources)
        {
            if (!playerInventory.HasItem(req.resource.itemID, req.amount))
                return false;
        }
        return true;
    }

    private void CompleteCrafting()
    {
        // Deduct resources
        foreach (var req in currentRecipe.requiredResources)
        {
            playerInventory.RemoveItem(req.resource.itemID, req.amount);
        }

        // Spawn the crafted item in the world
        Instantiate(currentRecipe.craftedItem.itemPrefab, spawnPoint.position, Quaternion.identity);

        // Play crafting animation/sound here
        Debug.Log(currentRecipe.recipeName + " crafted!");

        // Reset UI
        StopCrafting();
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


    private void CheckPlayerInRange()
    {
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            isInRange = distance <= 3f;
        }
    }

    private void CheckPlayerLookingAt()
    {
        if (mainCamera == null || !isInRange)
        {
            isLookingAt = false;
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, 3f))
        {
            isLookingAt = hit.transform == transform;
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

    private void ToggleCraftingUI()
    {
        if (craftingUI == null) return;

        bool isCraftingUIOpen = !craftingUI.activeSelf;
        craftingUI.SetActive(isCraftingUIOpen);

        LockPlayerMovement(isCraftingUIOpen); // Call movement lock function

        if (isCraftingUIOpen)
        {
            UpdateCraftingUI(); // Ensure UI updates when opened
            
            // Reset the scroll position to the top
            ScrollRect scrollRect = craftingUI.GetComponentInChildren<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1; // 1 = top, 0 = bottom
            }
        }
    }

    private void LockPlayerMovement(bool lockMovement)
    {
        if (player == null) return;

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            if (lockMovement)
            {
                playerInput.DeactivateInput(); // Disable movement
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                playerInput.ActivateInput(); // Enable movement
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

}