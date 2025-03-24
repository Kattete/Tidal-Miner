using UnityEngine;

[CreateAssetMenu(fileName = "New Crafting Recipe", menuName = "Crafting/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    public string recipeName;
    public CollectibleItem craftedItem;  // The item this recipe produces
    public float craftTime = 3f; // Time to craft
    public ResourceRequirement[] requiredResources;
}

[System.Serializable]
public class ResourceRequirement
{
    public CollectibleItem resource;  // The Item required for crafting
    public int amount;     // The amount of that resource needed
}