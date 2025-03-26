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
        jumpPressed = context.performed;
    }

    private void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.performed;
    }

    private void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.performed)
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
        ProcessInputs();
        CheckGroundStatus();
        HandleCrouching();
        HandleMovement();
        HandleGravity();
        HandleJump();
        ApplyFinalMovement();
    }

    private void ProcessInputs()
    {
        // Calculate move direction relative to camera orientation
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        // Project vectors onto the horizontal plane
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // Create the movement vector based on input system values
        moveDirection = (forward * currentMovementInput.y + right * currentMovementInput.x).normalized;

        // Handle sprint state
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

        // Handle jump
        if (jumpPressed && isGrounded && !crouchToggled)
        {
            isJumping = true;
            jumpPressed = false; // Reset jump flag to prevent multiple jumps
        }
    }

    private void CheckGroundStatus()
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

    private void HandleCrouching()
    {
        // Check for obstacles above the player
        if (!crouchToggled && Physics.Raycast(transform.position, Vector3.up, standingHeight - crouchingHeight + 0.1f, groundMask))
        {
            crouchToggled = true; // Force crouch if there's an obstacle above
            targetHeight = crouchingHeight;
        }
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
        if (isJumping && isGrounded)
        {
            // Calculate jump velocity using physics formula: v = sqrt(2 * g * h)
            float jumpVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
            velocity.y = jumpVelocity;

            // Reset jumping flag
            isJumping = false;
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
        return !isGrounded;
    }
}
