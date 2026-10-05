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

    CharacterController controller;
    float verticalVelocity;
    float lastGroundedTime;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
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
        if (move.sqrMagnitude > 1f) move.Normalize();

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
            if (verticalVelocity < 0f) verticalVelocity = -2f; // keeps him stuck to the ground
        }

        // Jump, with a little forgiveness just after walking off an edge
        bool canJump = grounded || Time.time - lastGroundedTime <= coyoteTime;
        if (jumpHeight > 0f && canJump && JumpPressed())
        {
            // The maths that turns a height in units into an upward speed
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastGroundedTime = -999f; // stops a second jump in mid-air
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * moveSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // Feed the Animator
        if (animator != null)
        {
            animator.SetFloat(speedParameter, move.magnitude, animationDamping, Time.deltaTime);
            if (!string.IsNullOrEmpty(airborneParameter))
                animator.SetBool(airborneParameter, !grounded);
        }
    }

    Vector2 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        Vector2 v = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
        return v;
    }

    bool JumpPressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.spaceKey.wasPressedThisFrame;
    }

    // Places the player at a spawn point and shows them
    public void Spawn(Transform point)
    {
        gameObject.SetActive(true);

        // A CharacterController must be disabled to teleport it
        controller.enabled = false;
        transform.SetPositionAndRotation(point.position, point.rotation);
        controller.enabled = true;

        verticalVelocity = 0f;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}