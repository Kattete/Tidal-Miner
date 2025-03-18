using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CraftingRecipe
{
    public string itemName; // The name of the crafted item
    public Dictionary<string, int> requiredItems; // A dictionary of required materials and their amounts
    public GameObject itemPrefab; // The prefab of the crafted item

    // Constructor to initialize a CraftingRecipe
    public CraftingRecipe(string name, Dictionary<string, int> required)
    {
        itemName = name;
        requiredItems = required;
    }

    // Method to check if the player has enough materials for this recipe
    public bool CanCraft(Dictionary<string, int> inventory)
    {
        foreach (var item in requiredItems)
        {
            if (!inventory.ContainsKey(item.Key) || inventory[item.Key] < item.Value)
            {
                return false; // Not enough of the required item
            }
        }
        return true; // All required items are available
    }
}