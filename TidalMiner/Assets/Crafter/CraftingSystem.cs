using System.Collections.Generic;
using UnityEngine;

public class CraftingSystem : MonoBehaviour
{
    // The dictionary holding all crafting recipes
    private Dictionary<string, CraftingRecipe> recipes;

    private void Start()
    {
        // Initialize recipes dictionary
        recipes = new Dictionary<string, CraftingRecipe>();

        // Hardcoded crafting recipes
        recipes.Add("Plate", new CraftingRecipe(
            "Plate",
            new Dictionary<string, int>
            {
                { "nails", 2 },
                { "cogs", 2 },
            }
        ));

        recipes.Add("Rod", new CraftingRecipe(
            "Rod",
            new Dictionary<string, int>
            {
                { "nails", 1 },
                { "cogs", 1 }
            }
        ));
    }

    // Check if the player can craft an item
    public bool CanCraft(string recipeName, Dictionary<string, int> playerInventory)
    {
        if (recipes.ContainsKey(recipeName))
        {
            return recipes[recipeName].CanCraft(playerInventory);
        }
        else
        {
            Debug.Log("Recipe not found.");
            return false;
        }
    }

    // Craft an item if possible
    public void CraftItem(string recipeName, Dictionary<string, int> playerInventory)
    {
        if (CanCraft(recipeName, playerInventory))
        {
            CraftingRecipe recipe = recipes[recipeName];

            // Remove required items from inventory
            foreach (var item in recipe.requiredItems)
            {
                playerInventory[item.Key] -= item.Value;
            }

            // Add crafted item to inventory
            Debug.Log("Crafted: " + recipe.itemName);
        }
        else
        {
            Debug.Log("Cannot craft: Insufficient materials.");
        }
    }
}