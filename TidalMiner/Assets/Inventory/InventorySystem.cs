using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class InventorySystem : MonoBehaviour
{
    [System.Serializable]
    public class InventorySlot
    {
        public string itemID = "";
        public GameObject itemInstance; // The actual equipped item
        public RectTransform slotTransform;
        public bool isOccupied;
        public int quantity = 0;
        public Image itemIcon; // 2D sprite image instead of 3D model
        public TextMeshProUGUI quantityText; // Text showing stack count
    }

    [System.Serializable]
    public class ItemSpriteMapping
    {
        public string itemID;
        public Sprite itemSprite; // 2D sprite for inventory display
        [Tooltip("Adjust the icon's size within the slot (0-1 for percentage of slot size)")]
        [Range(0.1f, 1.0f)]
        public float sizeFactor = 0.8f; // How much of the slot the icon should fill
        [Tooltip("Color tint for the sprite (default is white = no tint)")]
        public Color tint = Color.white;
    }

    [Header("Inventory Settings")]
    [SerializeField] private int maxSlots = 4;
    [SerializeField] private GameObject mapDevicePrefab;
    [SerializeField] public Transform itemHoldPosition;

    [Header("UI References")]
    [SerializeField] private RectTransform[] slotTransforms;
    [SerializeField] private GameObject slotHighlight;
    [SerializeField] private Vector3 highlightedScale = new Vector3(1.1f, 1.1f, 1.1f);
    [SerializeField] private GameObject quantityTextPrefab; // TextMesh Pro text prefab

    [Header("Item Sprites")]
    [SerializeField] private List<ItemSpriteMapping> itemSprites = new List<ItemSpriteMapping>();
    [SerializeField] private Sprite defaultSprite; // Fallback sprite if no specific one is found
    [SerializeField] private Color defaultTint = Color.white;
    [Range(0.1f, 1.0f)]
    [SerializeField] private float defaultSizeFactor = 0.8f;

    [Header("Input")]
    [SerializeField] private InputActionReference slot1Action;
    [SerializeField] private InputActionReference slot2Action;
    [SerializeField] private InputActionReference slot3Action;
    [SerializeField] private InputActionReference slot4Action;

    // Private variables
    public InventorySlot[] inventorySlots;
    public int currentActiveSlot = 0;
    public GameObject currentEquippedItem;
    private MapDevice mapDeviceComponent;

    private void Awake()
    {
        // Initialize inventory slots
        inventorySlots = new InventorySlot[maxSlots];
        for (int i = 0; i < maxSlots; i++)
        {
            inventorySlots[i] = new InventorySlot
            {
                itemID = "",
                itemInstance = null,
                slotTransform = (i < slotTransforms.Length) ? slotTransforms[i] : null,
                isOccupied = false,
                quantity = 0,
                itemIcon = null,
                quantityText = null
            };

            // Create UI elements for each slot
            if (slotTransforms[i] != null)
            {
                // Create item icon (sprite)
                GameObject iconObj = new GameObject("ItemIcon");
                iconObj.transform.SetParent(slotTransforms[i], false);

                // Set up RectTransform to position within slot
                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.1f, 0.1f); // Small margin from edges
                iconRect.anchorMax = new Vector2(0.9f, 0.9f); // Small margin from edges
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;

                // Add Image component for the sprite
                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.preserveAspect = true; // Maintain aspect ratio
                iconImage.raycastTarget = false; // Don't block raycasts

                // Hide initially and store reference
                iconImage.gameObject.SetActive(false);
                inventorySlots[i].itemIcon = iconImage;

                // Create quantity text if prefab exists
                if (quantityTextPrefab != null)
                {
                    GameObject textObj = Instantiate(quantityTextPrefab, slotTransforms[i]);
                    textObj.transform.SetAsLastSibling(); // Ensure it's on top
                    inventorySlots[i].quantityText = textObj.GetComponent<TextMeshProUGUI>();

                    if (inventorySlots[i].quantityText != null)
                    {
                        inventorySlots[i].quantityText.text = "";
                        inventorySlots[i].quantityText.gameObject.SetActive(false);
                    }
                }
            }
        }

        // Add map device to first slot
        if (mapDevicePrefab != null)
        {
            AddItem(mapDevicePrefab, "MapDevice");
        }
    }

    private void OnEnable()
    {
        // Enable input actions
        EnableInputActions(true);
    }

    private void OnDisable()
    {
        // Disable input actions
        EnableInputActions(false);
    }

    private void EnableInputActions(bool enable)
    {
        InputActionReference[] actions = { slot1Action, slot2Action, slot3Action, slot4Action };

        foreach (var action in actions)
        {
            if (action != null)
            {
                if (enable)
                {
                    action.action.Enable();
                    if (action == slot1Action) action.action.performed += OnSlot1;
                    else if (action == slot2Action) action.action.performed += OnSlot2;
                    else if (action == slot3Action) action.action.performed += OnSlot3;
                    else if (action == slot4Action) action.action.performed += OnSlot4;
                }
                else
                {
                    if (action == slot1Action) action.action.performed -= OnSlot1;
                    else if (action == slot2Action) action.action.performed -= OnSlot2;
                    else if (action == slot3Action) action.action.performed -= OnSlot3;
                    else if (action == slot4Action) action.action.performed -= OnSlot4;
                    action.action.Disable();
                }
            }
        }
    }

    private void OnSlot1(InputAction.CallbackContext context) { EquipSlot(0); }
    private void OnSlot2(InputAction.CallbackContext context) { EquipSlot(1); }
    private void OnSlot3(InputAction.CallbackContext context) { EquipSlot(2); }
    private void OnSlot4(InputAction.CallbackContext context) { EquipSlot(3); }

    private void EquipSlot(int slotIndex)
    {
        // Check if slot is valid and occupied
        if (slotIndex < 0 || slotIndex >= maxSlots || !inventorySlots[slotIndex].isOccupied)
            return;

        // First, tell the current map device it's no longer equipped (if it exists)
        UpdateMapDeviceState(false, false);

        // Unequip current item if any
        if (currentEquippedItem != null)
        {
            currentEquippedItem.SetActive(false);
        }

        // Update active slot
        currentActiveSlot = slotIndex;
        currentEquippedItem = inventorySlots[slotIndex].itemInstance;

        if (currentEquippedItem != null)
        {
            // Position in hand
            currentEquippedItem.transform.position = itemHoldPosition.position;
            currentEquippedItem.transform.rotation = itemHoldPosition.rotation;
            currentEquippedItem.transform.SetParent(itemHoldPosition);

            // Activate the item
            currentEquippedItem.SetActive(true);

            // Tell the map device about its state
            bool isMapDeviceNowEquipped = (slotIndex == 0) && (currentEquippedItem.GetComponent<MapDevice>() != null);
            if (isMapDeviceNowEquipped)
            {
                UpdateMapDeviceState(true, true);
                Debug.Log("Map Device equipped and active in slot 0");
            }
        }

        // Update UI
        UpdateSlotHighlight();
    }

    // Method to update map device state
    private void UpdateMapDeviceState(bool isEquipped, bool isInFirstSlot)
    {
        // Find the map device in slot 0
        if (inventorySlots[0].isOccupied && inventorySlots[0].itemInstance != null)
        {
            MapDevice mapDevice = inventorySlots[0].itemInstance.GetComponent<MapDevice>();
            if (mapDevice != null)
            {
                mapDevice.SetEquipmentState(isEquipped, isInFirstSlot);
                Debug.Log($"Updated map device state: equipped={isEquipped}, inSlot1={isInFirstSlot}");
            }
        }
    }

    private void UpdateSlotHighlight()
    {
        // Reset all slots to normal appearance
        for (int i = 0; i < slotTransforms.Length; i++)
        {
            if (slotTransforms[i] != null)
            {
                slotTransforms[i].localScale = Vector3.one;
            }
        }

        // Highlight active slot
        if (currentActiveSlot < slotTransforms.Length && slotTransforms[currentActiveSlot] != null)
        {
            slotTransforms[currentActiveSlot].localScale = highlightedScale;

            if (slotHighlight != null)
            {
                slotHighlight.transform.position = slotTransforms[currentActiveSlot].position;
                slotHighlight.SetActive(true);
            }
        }
    }

    // Method to add an item to inventory
    public bool AddItem(GameObject item, string itemID)
    {
        if (item == null || string.IsNullOrEmpty(itemID)) return false;

        // Check if we already have this stackable item
        for (int i = 0; i < maxSlots; i++)
        {
            if (inventorySlots[i].isOccupied && inventorySlots[i].itemID == itemID)
            {
                // Increase stack count
                inventorySlots[i].quantity++;

                // Update quantity text
                UpdateQuantityText(i);

                // Disable colliders to prevent further interaction
                DisableColliders(item);

                // If it's not the map device, deactivate the original
                if (item.GetComponent<MapDevice>() == null)
                {
                    item.SetActive(false);
                }

                return true;
            }
        }

        // Find first empty slot
        for (int i = 0; i < maxSlots; i++)
        {
            if (!inventorySlots[i].isOccupied)
            {
                // Prepare the item
                GameObject itemInstance;
                if (item.scene.name == null) // It's a prefab
                {
                    itemInstance = Instantiate(item);
                }
                else
                {
                    itemInstance = item;
                    // Disable colliders to prevent further interaction
                    DisableColliders(itemInstance);
                }

                // Setup the inventory slot
                inventorySlots[i].itemID = itemID;
                inventorySlots[i].itemInstance = itemInstance;
                inventorySlots[i].isOccupied = true;
                inventorySlots[i].quantity = 1;

                // Set up the sprite icon
                SetItemSprite(i, itemID);

                // Update quantity text
                UpdateQuantityText(i);

                // Deactivate initially
                itemInstance.SetActive(false);

                // If this is the first item and none equipped, equip it
                if (currentEquippedItem == null)
                {
                    EquipSlot(i);
                }

                if (i == 0)
                {
                    MapDevice mapDevice = itemInstance.GetComponent<MapDevice>();
                    if (mapDevice != null)
                    {
                        mapDevice.SetEquipmentState(true, true);
                        Debug.Log("Map device state initialized during addition to inventory");
                    }
                }

                return true;
            }
        }

        // Inventory full
        return false;
    }

    // Get sprite mapping for an item ID
    private ItemSpriteMapping GetSpriteMappingForItem(string itemID)
    {
        // Find the matching sprite mapping
        foreach (var mapping in itemSprites)
        {
            if (mapping.itemID == itemID && mapping.itemSprite != null)
            {
                return mapping;
            }
        }

        // No matching sprite found
        return null;
    }

    // Set sprite for an inventory slot
    private void SetItemSprite(int slotIndex, string itemID)
    {
        InventorySlot slot = inventorySlots[slotIndex];
        if (slot.slotTransform == null || slot.itemIcon == null) return;

        // Get the appropriate sprite for this item
        ItemSpriteMapping spriteMapping = GetSpriteMappingForItem(itemID);

        // If we have a specific mapping, use it
        if (spriteMapping != null)
        {
            slot.itemIcon.sprite = spriteMapping.itemSprite;
            slot.itemIcon.color = spriteMapping.tint;

            // Adjust size based on the size factor
            RectTransform iconRect = slot.itemIcon.GetComponent<RectTransform>();
            float sizeFactor = spriteMapping.sizeFactor;
            iconRect.anchorMin = new Vector2(0.5f - sizeFactor / 2, 0.5f - sizeFactor / 2);
            iconRect.anchorMax = new Vector2(0.5f + sizeFactor / 2, 0.5f + sizeFactor / 2);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
        }
        // Otherwise use the default sprite
        else if (defaultSprite != null)
        {
            slot.itemIcon.sprite = defaultSprite;
            slot.itemIcon.color = defaultTint;

            // Use default size factor
            RectTransform iconRect = slot.itemIcon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f - defaultSizeFactor / 2, 0.5f - defaultSizeFactor / 2);
            iconRect.anchorMax = new Vector2(0.5f + defaultSizeFactor / 2, 0.5f + defaultSizeFactor / 2);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
        }
        else
        {
            // No sprite available
            slot.itemIcon.gameObject.SetActive(false);
            Debug.LogWarning($"No sprite found for item: {itemID}");
            return;
        }

        // Show the sprite
        slot.itemIcon.gameObject.SetActive(true);

        Debug.Log($"Set sprite for {itemID} in slot {slotIndex}");
    }

    // Update quantity text
    private void UpdateQuantityText(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots) return;

        InventorySlot slot = inventorySlots[slotIndex];

        if (slot.quantityText != null)
        {
            if (slot.quantity > 1)
            {
                slot.quantityText.text = slot.quantity.ToString();
                slot.quantityText.gameObject.SetActive(true);
            }
            else
            {
                slot.quantityText.text = "";
                slot.quantityText.gameObject.SetActive(false);
            }
        }
    }

    // Helper method to disable colliders
    private void DisableColliders(GameObject item)
    {
        Collider[] colliders = item.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }

    // Check if we have a specific item ID in inventory
    public bool HasItem(string itemID)
    {
        if (string.IsNullOrEmpty(itemID)) return false;

        for (int i = 0; i < maxSlots; i++)
        {
            if (inventorySlots[i].isOccupied && inventorySlots[i].itemID == itemID)
            {
                return true;
            }
        }

        return false;
    }

    // Get quantity of a specific item
    public int GetItemQuantity(string itemID)
    {
        if (string.IsNullOrEmpty(itemID)) return 0;

        for (int i = 0; i < maxSlots; i++)
        {
            if (inventorySlots[i].isOccupied && inventorySlots[i].itemID == itemID)
            {
                return inventorySlots[i].quantity;
            }
        }

        return 0;
    }
}