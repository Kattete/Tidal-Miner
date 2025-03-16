using UnityEngine;
using UnityEngine.UI;

public class OxygenManager : MonoBehaviour
{
    /* public Image oxygenBarImage;
     * 
     */
    public Slider oxygenBarSlider;
    public Slider healthBarSlider;
    private CharacterStats character;
    private float currentOxygen;
    private float currentHealth;
    private bool isCarryingItem = false;
    private bool isSwimming = false;

    // Default no weight carrying
    private float carryingItemWeight = 0f;

    private void Awake()
    {
        // Find the character script in the scene (works only if there is one character script in the scene)
        character = FindAnyObjectByType<CharacterStats>();

        if (character == null)
        {
            Debug.LogError("Character script NOT found in the scene!");
        }

        currentOxygen = character.oxygenMax;
        currentHealth = character.maxHealth;

        oxygenBarSlider.maxValue = character.oxygenMax;
        oxygenBarSlider.value = currentOxygen;

        healthBarSlider.maxValue = character.maxHealth;
        healthBarSlider.value = currentHealth;
    }

    void Update()
    {
        if (character == null) return;
        DrainOxygen();
        UpdateUI();
        Debug.Log("Oxygen: " + currentOxygen);
    }

    private void DrainOxygen()
    {
        float drainRate = character.oxygenDrainAlways;

        // If running then drain faster 
        if (Input.GetKey(KeyCode.LeftShift))
            drainRate = character.oxygenDrainSprint;

        // If swimming then drain faster
        if (isSwimming)
            drainRate = character.oxygenDrainSwim;

        //Multiply the drain rate by the weight of the item
        if (carryingItemWeight > 0)
            drainRate += character.oxygenDrainCarryBase * carryingItemWeight;

        // If jumping remove flat amount of oxygen
        if (Input.GetKeyDown(KeyCode.Space))
            drainRate = character.oxygenDrainJump;

        currentOxygen -= drainRate * Time.deltaTime;
        currentOxygen = Mathf.Clamp(currentOxygen, 0, character.oxygenMax);

        if (currentOxygen <= 0)
        {
            Debug.Log("Oxygen is depleted, draining health.");
            // Drain health if oxygen is zero
            currentHealth -= character.healthDrainNoOxygen * Time.deltaTime;
            currentHealth = Mathf.Clamp(currentHealth, 0, character.maxHealth);

            Debug.Log("Current Health: " + currentHealth);

        if (currentHealth <= 0)
            {
                Debug.Log("Player is dead!");
                // Death logic TODO
            }
        }
    }
    
    private void UpdateUI()
    {
        oxygenBarSlider.value = currentOxygen;
        healthBarSlider.value = currentHealth;
    }

    public void SetCarryingItem(float itemWeight)
    {
        carryingItemWeight = itemWeight;
    }

    public void SetSwimming(bool swimming)
    {
        isSwimming = swimming;
    }
}
