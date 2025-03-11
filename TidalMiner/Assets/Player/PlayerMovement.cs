using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.VisualScripting;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    // Input Action References
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference crouchAction;

    // Movement Parameters
    [Header("Basic Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -20f;

    // Acceleration and Deceleration
    [Header("Movement Handling")]
    [SerializeField] private float accelerationTime = 0.1f;
    [SerializeField] private float decelerationTime = 0.2f;
    [SerializeField] private float airControlFactor = 0.5f;

    // Slope Handling
    [Header("Slope Parameters")]
    [SerializeField] private float maxSlopeAngle = 45f;
    [SerializeField] private float slopeSlideSpeed = 8f;
    [SerializeField] private float slopeForce = 10f;
    [SerializeField] private float snapForce = 5f;
    [SerializeField] private LayerMask groundMask;

    // Crouching
    [Header("Crouch Parameters")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchingHeight = 1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;

    [Header("Swimming Handling")]
    [SerializeField] private WaterLevelController waterLevelController;
    [SerializeField] private float swimSpeed = 3f; // Single swim speed for all directions
    [SerializeField] private float swimUpSpeed = 3f; // Speed when pressing space underwater
    [SerializeField] private float waterMovementSmoothing = 0.1f; // Lower = more responsive
    [SerializeField] private float underwaterThreshold = 0.2f; // How deep before considered underwater

    [Header("Underwater Effects")]
    [SerializeField] private Color normalFogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color underwaterFogColor = new Color(0.15f, 0.22f, 0.4f, 1f);
    [SerializeField] private float underwaterFogDensity = 0.04f;
    [SerializeField] private AudioSource underwaterAudioSource;
    [SerializeField] private AudioSource swimmingAudioSource;
    [SerializeField] private ParticleSystem bubbleEffect;

    // Component References
    private CharacterController controller;
    private Camera playerCamera;
    private Transform cameraHolder;
    private PlayerInput playerInput;

    // State Management
    private Vector3 moveDirection;
    private Vector3 impact = Vector3.zero;
    private Vector3 velocity;
    private float verticalVelocity;
    private float targetSpeed;
    private float currentSpeed;
    private float originalStepOffset;
    private bool isGrounded;
    private bool isJumping;
    private bool isSprinting;
    private bool isCrouching;
    private bool isOnSlope;
    private bool isSlidingDownSlope;
    private RaycastHit slopeHit;

    // Swimming State Management
    private bool isInWater = false;
    private bool isUnderwater = false;
    private bool wasUnderwater = false;
    private float waterSurfaceHeight = 0f;
    private float defaultFogDensity;

    // Input Values
    private Vector2 currentMovementInput;
    private bool jumpPressed;
    private bool sprintHeld;
    private bool crouchToggled;

    // Cache
    private Vector3 normalVector = Vector3.up;
    private Vector3 lastMoveDirection;
    private float targetHeight;

    private void Awake()
    {
        // Get references
        controller = GetComponent<CharacterController>();
        playerCamera = Camera.main;
        playerInput = GetComponent<PlayerInput>();

        // Create a camera holder if it doesn't exist
        if (transform.Find("CameraHolder") == null)
        {
            GameObject cameraHolderObj = new GameObject("CameraHolder");
            cameraHolderObj.transform.parent = transform;
            cameraHolderObj.transform.localPosition = new Vector3(0, 1.6f, 0);
            cameraHolder = cameraHolderObj.transform;

            if (playerCamera != null)
            {
                playerCamera.transform.parent = cameraHolder;
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;
            }
        }
        else
        {
            cameraHolder = transform.Find("CameraHolder");
        }

        // Initialize variables
        originalStepOffset = controller.stepOffset;
        targetHeight = standingHeight;

        // Store default fog settings
        defaultFogDensity = RenderSettings.fogDensity;

        // Find water level controller if not assigned
        if (waterLevelController == null)
        {
            waterLevelController = FindObjectOfType<WaterLevelController>();
        }
    }

    private void OnEnable()
    {
        // Enable input actions and subscribe to events
        if (moveAction != null)
        {
            moveAction.action.Enable();
            moveAction.action.performed += OnMove;
            moveAction.action.canceled += OnMove;
        }

        if (jumpAction != null)
        {
            jumpAction.action.Enable();
            jumpAction.action.performed += OnJump;
            jumpAction.action.canceled += OnJump;
        }

        if (sprintAction != null)
        {
            sprintAction.action.Enable();
            sprintAction.action.performed += OnSprint;
            sprintAction.action.canceled += OnSprint;
        }

        if (crouchAction != null)
        {
            crouchAction.action.Enable();
            crouchAction.action.performed += OnCrouch;
        }
    }

    private void OnDisable()
    {
        // Disable input actions and unsubscribe from events
        if (moveAction != null)
        {
            moveAction.action.Disable();
            moveAction.action.performed -= OnMove;
            moveAction.action.canceled -= OnMove;
        }

        if (jumpAction != null)
        {
            jumpAction.action.Disable();
            jumpAction.action.performed -= OnJump;
            jumpAction.action.canceled -= OnJump;
        }

        if (sprintAction != null)
        {
            sprintAction.action.Disable();
            sprintAction.action.performed -= OnSprint;
            sprintAction.action.canceled -= OnSprint;
        }

        if (crouchAction != null)
        {
            crouchAction.action.Disable();
            crouchAction.action.performed -= OnCrouch;
        }
    }

    // Input Action Event Handlers
    private void OnMove(InputAction.CallbackContext context)
    {
        currentMovementInput = context.ReadValue<Vector2>();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        // For continuous upward movement underwater
        jumpPressed = context.ReadValueAsButton();
    }

    private void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.performed;
    }

    private void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.performed && !isInWater) // Disable crouching while in water
        {
            crouchToggled = !crouchToggled;
            targetHeight = crouchToggled ? crouchingHeight : standingHeight;

            // Adjust camera position
            float cameraYOffset = crouchToggled ? 0.9f : 1.6f;
            cameraHolder.localPosition = new Vector3(0, cameraYOffset, 0);
        }
    }

    private void Update()
    {
        // Check if player is in water
        CheckWaterState();

        ProcessInputs();

        if (isInWater)
        {
            HandleSwimming();
        }
        else
        {
            CheckGroundStatus();
            HandleCrouching();
            HandleMovement();
            HandleGravity();
            HandleJump();
        }

        ApplyFinalMovement();
        UpdateUnderwaterEffects();
    }

    private void CheckWaterState()
    {
        if (waterLevelController != null)
        {
            // Get current water height from the water level controller
            waterSurfaceHeight = waterLevelController.GetCurrentWaterHeight();

            // Player's position
            float playerBottomHeight = transform.position.y;
            float playerHeadHeight = transform.position.y + controller.height - controller.skinWidth;

            // Check if player is touching water
            bool wasTouchingWater = isInWater;
            isInWater = playerBottomHeight <= waterSurfaceHeight;

            // Check if player's head is underwater
            wasUnderwater = isUnderwater;
            isUnderwater = playerHeadHeight < (waterSurfaceHeight - underwaterThreshold);

            // Trigger state change events
            if (isInWater != wasTouchingWater)
            {
                if (isInWater)
                    OnEnterWater();
                else
                    OnExitWater();
            }

            if (isUnderwater != wasUnderwater)
            {
                if (isUnderwater)
                    OnSubmerge();
                else
                    OnSurface();
            }
        }
    }

    private void OnEnterWater()
    {
        // Reset crouching when entering water
        if (crouchToggled)
        {
            crouchToggled = false;
            targetHeight = standingHeight;
            float cameraYOffset = 1.6f;
            cameraHolder.localPosition = new Vector3(0, cameraYOffset, 0);
        }

        // Play water splash sound
        if (swimmingAudioSource != null && !swimmingAudioSource.isPlaying)
        {
            swimmingAudioSource.pitch = Random.Range(0.8f, 1.2f);
            swimmingAudioSource.Play();
        }

        // Reduce vertical velocity to prevent sinking too fast
        velocity.y = Mathf.Max(velocity.y, -2f);
    }

    private void OnExitWater()
    {
        // Stop swimming sounds
        if (swimmingAudioSource != null)
            swimmingAudioSource.Stop();

        if (underwaterAudioSource != null)
            underwaterAudioSource.Stop();

        // Stop bubble effects
        if (bubbleEffect != null)
            bubbleEffect.Stop();
    }

    private void OnSubmerge()
    {
        // Start underwater audio
        if (underwaterAudioSource != null)
            underwaterAudioSource.Play();

        // Start bubble effect
        if (bubbleEffect != null)
            bubbleEffect.Play();
    }

    private void OnSurface()
    {
        // Stop underwater audio
        if (underwaterAudioSource != null)
            underwaterAudioSource.Stop();

        // Stop bubble effect
        if (bubbleEffect != null)
            bubbleEffect.Stop();
    }

    private void ProcessInputs()
    {
        // Calculate move direction relative to camera orientation
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        // Project vectors onto the horizontal plane (only for land movement)
        if (!isInWater)
        {
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        // Create the movement vector based on input system values
        moveDirection = (forward * currentMovementInput.y + right * currentMovementInput.x).normalized;

        // Process different movement speeds based on state
        if (isInWater)
        {
            // Single swimming speed - simplified
            targetSpeed = swimSpeed;
        }
        else
        {
            // Land movement speed logic
            if (sprintHeld && !crouchToggled && moveDirection.magnitude > 0.1f)
            {
                isSprinting = true;
                targetSpeed = sprintSpeed;
            }
            else if (crouchToggled)
            {
                targetSpeed = crouchSpeed;
                isSprinting = false;
            }
            else
            {
                targetSpeed = walkSpeed;
                isSprinting = false;
            }
        }

        // Handle jump
        if (jumpPressed)
        {
            if (isInWater)
            {
                // In water, jump is used to swim up
                // We don't need to set anything here as the swimming logic uses jumpPressed directly
            }
            else if (isGrounded && !crouchToggled)
            {
                // On land, normal jump
                isJumping = true;
            }

            // Don't reset jumpPressed here as we want continuous upward movement while holding jump
        }

        // Handle dive (swim down) - divePressed is set directly from input events
    }

    private void HandleSwimming()
    {
        // Free underwater movement like flying
        if (isInWater)
        {
            // Get camera forward and right for movement direction
            Vector3 forward = playerCamera.transform.forward;
            Vector3 right = playerCamera.transform.right;

            // Calculate the base movement direction including vertical component from camera
            Vector3 targetMoveDirection = (forward * currentMovementInput.y + right * currentMovementInput.x).normalized;

            // Apply the horizontal and forward/backward movement
            velocity = Vector3.Lerp(velocity, targetMoveDirection * swimSpeed, (1f / waterMovementSmoothing) * Time.deltaTime);

            // Handle vertical movement separately
            float verticalVelocity = 0f;

            // Use jump to swim up
            if (jumpPressed)
            {
                verticalVelocity = swimUpSpeed;
            }
            else
            {
                // If no vertical input, gradually slow down vertical movement
                verticalVelocity = Mathf.Lerp(velocity.y, 0, Time.deltaTime * 5f);
            }

            // Apply vertical velocity
            velocity.y = verticalVelocity;

            // If at surface and jump is pressed with upward momentum, try to jump out of water
            if (!isUnderwater && isJumping && velocity.y > 0)
            {
                velocity.y = jumpHeight * 1.5f; // Jump with extra force to exit water
                isJumping = false;
            }
        }
    }

    private void CheckGroundStatus()
    {
        // Only check ground status when not in water
        if (!isInWater)
        {
            // Check if grounded based on the CharacterController
            isGrounded = controller.isGrounded;

            // Reset step offset if we're grounded
            controller.stepOffset = isGrounded ? originalStepOffset : 0f;

            // Check for slopes
            if (isGrounded && Physics.Raycast(transform.position, Vector3.down, out slopeHit, controller.height / 2 + 0.3f, groundMask))
            {
                normalVector = slopeHit.normal;
                isOnSlope = Vector3.Angle(normalVector, Vector3.up) > 0.1f && Vector3.Angle(normalVector, Vector3.up) <= maxSlopeAngle;
                isSlidingDownSlope = Vector3.Angle(normalVector, Vector3.up) > maxSlopeAngle;
            }
            else
            {
                isOnSlope = false;
                isSlidingDownSlope = false;
                normalVector = Vector3.up;
            }
        }
    }

    private void HandleCrouching()
    {
        // Don't handle crouching while in water
        if (isInWater) return;

        // Update crouching state based on toggle
        isCrouching = crouchToggled;

        // Smoothly transition between standing and crouching heights
        float currentHeight = controller.height;
        if (Mathf.Abs(currentHeight - targetHeight) > 0.01f)
        {
            controller.height = Mathf.Lerp(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);

            // Adjust controller center position to keep feet at the same level
            float centerY = controller.height / 2;
            controller.center = new Vector3(0, centerY, 0);
        }
    }

    private void HandleMovement()
    {
        // Skip this method when in water
        if (isInWater) return;

        // Smooth acceleration and deceleration
        float accelerationRate = isGrounded ? (moveDirection.magnitude > 0.1f ? accelerationTime : decelerationTime) : accelerationTime * airControlFactor;
        currentSpeed = Mathf.Lerp(currentSpeed, moveDirection.magnitude > 0.1f ? targetSpeed : 0f, (1f / accelerationRate) * Time.deltaTime);

        // Handle slope movement
        if (isGrounded)
        {
            // If on a slope, use the normal to calculate the movement direction
            if (isOnSlope)
            {
                velocity = Vector3.ProjectOnPlane(moveDirection * currentSpeed, normalVector);

                // Apply extra force when moving uphill
                if (Vector3.Dot(velocity.normalized, Vector3.up) < 0 && currentSpeed > 0.1f)
                {
                    velocity += Vector3.down * slopeForce * Time.deltaTime;
                }
            }
            // If on too steep a slope, slide down
            else if (isSlidingDownSlope)
            {
                velocity = Vector3.ProjectOnPlane(Vector3.down * slopeSlideSpeed, normalVector);
            }
            // Standard flat ground movement
            else
            {
                velocity = moveDirection * currentSpeed;
            }

            // Store last grounded move direction for air control
            if (moveDirection.magnitude > 0.1f)
            {
                lastMoveDirection = moveDirection;
            }
        }
        // Air movement - reduced control
        else
        {
            velocity = new Vector3(
                moveDirection.x * currentSpeed * airControlFactor,
                velocity.y,
                moveDirection.z * currentSpeed * airControlFactor
            );
        }
    }

    private void HandleGravity()
    {
        // Skip this method when in water
        if (isInWater) return;

        // Apply gravity when not grounded
        if (!isGrounded)
        {
            // Apply standard gravity
            velocity.y += gravity * Time.deltaTime;
        }
        // Apply a small downward force when grounded to stick to slopes
        else if (!isJumping)
        {
            velocity.y = -snapForce;
        }
    }

    private void HandleJump()
    {
        // Skip this method when in water
        if (isInWater) return;

        if (isJumping && isGrounded)
        {
            // Calculate jump velocity using physics formula: v = sqrt(2 * g * h)
            float jumpVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
            velocity.y = jumpVelocity;

            // Reset jumping flag
            isJumping = false;
        }
    }

    private void UpdateUnderwaterEffects()
    {
        // Apply underwater post-processing effects
        if (isUnderwater)
        {
            // Apply underwater fog
            RenderSettings.fog = true;
            RenderSettings.fogColor = underwaterFogColor;
            RenderSettings.fogDensity = underwaterFogDensity;
        }
        else
        {
            // Restore normal fog settings
            RenderSettings.fogColor = normalFogColor;
            RenderSettings.fogDensity = defaultFogDensity;
        }
    }

    private void ApplyFinalMovement()
    {
        // Apply external forces (like knockback)
        if (impact.magnitude > 0.2f)
        {
            velocity += impact;
            impact = Vector3.Lerp(impact, Vector3.zero, 5f * Time.deltaTime);
        }

        // Apply final movement
        controller.Move(velocity * Time.deltaTime);
    }

    // Public method to apply external forces (e.g. knockback)
    public void AddImpact(Vector3 force)
    {
        impact += force;
    }

    // This method can be used to get the player's current movement speed
    public float GetCurrentSpeed()
    {
        return currentSpeed;
    }

    // This method returns true if player is currently in the air
    public bool IsInAir()
    {
        return !isGrounded && !isInWater;
    }

    // This method returns true if player is currently in water
    public bool IsInWater()
    {
        return isInWater;
    }

    // This method returns true if player is currently underwater
    public bool IsUnderwater()
    {
        return isUnderwater;
    }
}