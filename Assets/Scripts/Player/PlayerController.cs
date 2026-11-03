using UnityEngine;
using UnityEngine.InputSystem;

// Isometric movement with jumping. "Up" on the keyboard = "up" on screen.
// Gravity and jump height can be changed per level by the LevelTheme.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 12f;

    [Header("Sprinting")]
    [Tooltip("Hold Left Shift to run. Costs energy, which comes back when you ease off.")]
    public float sprintMultiplier = 1.6f;
    [Tooltip("How much faster the walk animation plays while sprinting.")]
    public float sprintAnimationSpeed = 1.3f;

    [Header("Gravity & Jumping")]
    [Tooltip("Default gravity. Levels can override this.")]
    public float gravity = -20f;
    [Tooltip("Jump height in units. 0 disables jumping.")]
    public float jumpHeight = 2f;
    [Tooltip("Grace period after leaving a ledge where a jump still works.")]
    public float coyoteTime = 0.15f;

    [Tooltip("Drag the Main Camera here.")]
    public Transform cameraTransform;

    [Header("Animation")]
    [Tooltip("Leave empty to find the Animator automatically.")]
    public Animator animator;
    public string speedParameter = "Speed";
    [Tooltip("Optional Bool parameter, ticked while airborne.")]
    public string airborneParameter = "";
    public float animationDamping = 0.1f;

    // The player's footsteps, jumps and pickup sounds. Added the first time it's asked for,
    // so the player needs no setup, even if it starts the game hidden.
    public PlayerAudio Audio
    {
        get
        {
            if (playerAudio == null) playerAudio = GetComponent<PlayerAudio>();
            if (playerAudio == null) playerAudio = gameObject.AddComponent<PlayerAudio>();
            return playerAudio;
        }
    }

    PlayerAudio playerAudio;

    // The player's health and energy. Added the first time it's asked for, like the audio.
    public PlayerVitals Vitals
    {
        get
        {
            if (playerVitals == null) playerVitals = GetComponent<PlayerVitals>();
            if (playerVitals == null) playerVitals = gameObject.AddComponent<PlayerVitals>();
            return playerVitals;
        }
    }

    PlayerVitals playerVitals;

    CharacterController controller;
    float verticalVelocity;
    float lastGroundedTime;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    // Called when entering a level, so each face can feel different
    public void ApplyMovementSettings(float newGravity, float newJumpHeight)
    {
        gravity = newGravity;
        jumpHeight = newJumpHeight;
    }

    void Update()
    {
        Vector2 input = ReadMoveInput();

        // Camera-relative directions, flattened onto the ground
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = cameraTransform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 move = forward * input.y + right * input.x;
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        // Face the direction we're moving
        if (move.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        bool grounded = controller.isGrounded;
        if (grounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f; // Keeps the character grounded.
            }
        }

        // Jump, with a little forgiveness just after walking off an edge
        bool canJump = grounded || Time.time - lastGroundedTime <= coyoteTime;
        if (jumpHeight > 0f && canJump && JumpPressed())
        {
            // The maths that turns a height in units into an upward speed
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastGroundedTime = -999f; // stops a second jump in mid-air

            Audio.PlayJump();
        }

        verticalVelocity += gravity * Time.deltaTime;

        // Sprinting uses up energy, and only counts while actually moving
        bool wantsSprint = SprintHeld() && move.sqrMagnitude > 0.01f;
        bool sprinting = Vitals.UpdateSprint(wantsSprint, Time.deltaTime);
        float speed = sprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        // A moving platform carries whoever is standing on it
        Vector3 carry = PlatformCarry();

        Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime + carry);

        // Feed the Animator
        if (animator != null)
        {
            animator.SetFloat(speedParameter, move.magnitude, animationDamping, Time.deltaTime);
            animator.speed = sprinting ? sprintAnimationSpeed : 1f;
            if (!string.IsNullOrEmpty(airborneParameter))
            {
                animator.SetBool(airborneParameter, !grounded);
            }
        }
    }

    Vector2 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
        {
            return Vector2.zero;
        }

        Vector2 v = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
        {
            v.y += 1f;
        }
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
        {
            v.y -= 1f;
        }
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
        {
            v.x += 1f;
        }
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
        {
            v.x -= 1f;
        }
        return v;
    }

    // How far the platform underfoot moved this frame, if it's a moving one
    Vector3 PlatformCarry()
    {
        // Look straight down from the middle of the body, a little past the feet
        Vector3 centre = controller.bounds.center;
        float reach = controller.bounds.extents.y + 0.4f;

        if (!Physics.Raycast(centre, Vector3.down, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            return Vector3.zero;

        MovingPlatform platform = hit.collider.GetComponentInParent<MovingPlatform>();
        if (platform == null) return Vector3.zero;

        float yaw;
        Vector3 carry = platform.CarryDelta(transform.position, out yaw);

        // A turning platform turns the player with it
        transform.Rotate(0f, yaw, 0f, Space.World);

        return carry;
    }

    bool SprintHeld()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.leftShiftKey.isPressed;
    }

    bool JumpPressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.spaceKey.wasPressedThisFrame;
    }

    // Places the player at a spawn point and shows them
    public void Spawn(Transform point)
    {
        Spawn(point.position, point.rotation);
    }

    // Places the player at an exact spot, used when returning from the map
    public void Spawn(Vector3 position, Quaternion rotation)
    {
        gameObject.SetActive(true);

        // A CharacterController must be disabled to teleport it
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;

        verticalVelocity = 0f;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}