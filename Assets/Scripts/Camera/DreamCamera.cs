using UnityEngine;
using UnityEngine.InputSystem;

// Handles the player camera, cube overview and map view
// The player camera can use isometric or third person
[RequireComponent(typeof(Camera))]
public class DreamCamera : MonoBehaviour
{
    // Available camera styles when playing a level
    public enum PlayerCameraStyle
    {
        Isometric,
        ThirdPerson
    }

    [Header("Player Camera Mode")]
    [Tooltip("Choose the camera style used while playing a level")]
    public PlayerCameraStyle playerCameraStyle = PlayerCameraStyle.Isometric;

    [Header("Isometric Camera")]
    [Tooltip("Distance between the camera and the player")]
    public float isoDistance = 30f;

    [Tooltip("X is the downward angle and Y is the horizontal angle")]
    public Vector2 isoAngles = new Vector2(35f, 45f);

    [Tooltip("Lower values give a flatter isometric appearance")]
    [Range(10f, 90f)]
    public float isoFOV = 20f;

    [Tooltip("How high above the player the camera focuses")]
    public float isoFocusHeight = 0.8f;

    [Tooltip("How far ahead of the player the camera looks")]
    public float isoLookAheadDistance = 0.75f;

    [Header("Third Person Camera")]
    [Tooltip("Distance behind the player")]
    public float thirdPersonDistance = 5f;

    [Tooltip("Height of the camera's focus point above the player")]
    public float thirdPersonHeight = 2.5f;

    [Tooltip("Starting downward camera angle")]
    public float thirdPersonPitch = 15f;

    [Range(30f, 100f)]
    public float thirdPersonFOV = 60f;

    [Header("Third Person Mouse Controls")]
    [Tooltip("Hold right mouse button and move the mouse to look around")]
    public bool enableMouseRotation = true;

    [Tooltip("How quickly the camera rotates with the mouse")]
    public float mouseSensitivity = 0.15f;

    [Tooltip("Reverse vertical mouse movement")]
    public bool invertMouseY = false;

    [Tooltip("Lowest vertical viewing angle")]
    public float minimumPitch = -20f;

    [Tooltip("Highest vertical viewing angle")]
    public float maximumPitch = 70f;

    [Header("Third Person Camera Collision")]
    [Tooltip("Move the camera closer when a wall blocks its view")]
    public bool enableCameraCollision = true;

    [Tooltip("Layers that can block the camera")]
    public LayerMask collisionLayers = ~0;

    [Tooltip("Radius used to check for obstacles")]
    public float collisionRadius = 0.2f;

    [Tooltip("Extra space kept between the camera and obstacles")]
    public float collisionPadding = 0.1f;

    [Header("Cube Overview")]
    [Tooltip("Drag the cube root object here")]
    public Transform cubeCenter;

    public float overviewDistance = 45f;

    [Tooltip("X is pitch and Y is yaw")]
    public Vector2 overviewAngles = new Vector2(20f, 0f);

    public float overviewFOV = 50f;

    [Header("Map View")]
    [Tooltip("Distance above the selected cube face")]
    public float mapDistance = 38f;

    [Tooltip("Map viewing angle away from straight down")]
    public float mapTilt = 0f;

    public float mapFOV = 50f;

    [Header("Camera Smoothing")]
    [Tooltip("Time taken to smoothly follow the target")]
    public float moveSmoothTime = 0.35f;

    [Tooltip("Speed of camera rotation and FOV changes")]
    public float rotateSpeed = 6f;

    // Keep this property for CubeWorld and other scripts
    public Camera Cam { get; private set; }

    // Current camera targets
    Transform followTarget;
    Transform mapFace;

    // Current camera state
    bool playing;
    Vector3 moveVelocity;

    // Third person rotation values
    float orbitYaw;
    float orbitPitch;

    // Tracks when the third person camera becomes active
    bool wasThirdPersonActive;

    void Awake()
    {
        Cam = GetComponent<Camera>();
    }

    // Switch to the cube overview
    public void SetOverview()
    {
        playing = false;
        followTarget = null;
        mapFace = null;
        moveVelocity = Vector3.zero;
    }

    // Switch to the overhead map
    public void SetMapView(Transform face)
    {
        playing = false;
        followTarget = null;
        mapFace = face;
        moveVelocity = Vector3.zero;
    }

    // Switch to the player camera
    public void SetPlaying(Transform target)
    {
        playing = true;
        followTarget = target;
        mapFace = null;
        moveVelocity = Vector3.zero;

        // Start the third person camera behind the player
        if (target != null)
        {
            orbitYaw = target.eulerAngles.y;
            orbitPitch = Mathf.Clamp(
                thirdPersonPitch,
                minimumPitch,
                maximumPitch
            );
        }
    }

    // Immediately move to the correct camera position
    public void SnapToTarget()
    {
        GetTargetPose(
            out Vector3 position,
            out Quaternion rotation,
            out float fov
        );

        transform.SetPositionAndRotation(position, rotation);
        Cam.fieldOfView = fov;
        moveVelocity = Vector3.zero;
    }

    void LateUpdate()
    {
        // Handle locking and unlocking the cursor
        UpdateCursorState();

        // Rotate the third person camera with the mouse
        if (playing &&
            followTarget != null &&
            playerCameraStyle == PlayerCameraStyle.ThirdPerson)
        {
            UpdateThirdPersonInput();
        }

        GetTargetPose(
            out Vector3 position,
            out Quaternion rotation,
            out float fov
        );

        // Smoothly move towards the target camera position
        transform.position = Vector3.SmoothDamp(
            transform.position,
            position,
            ref moveVelocity,
            Mathf.Max(0.01f, moveSmoothTime)
        );

        // Smoothly rotate towards the target camera angle
        float rotationAmount = Mathf.Clamp01(
            rotateSpeed * Time.deltaTime
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            rotation,
            rotationAmount
        );

        Cam.fieldOfView = Mathf.Lerp(
            Cam.fieldOfView,
            fov,
            rotationAmount
        );
    }

    // Rotate the third person camera whenever the mouse moves
    void UpdateThirdPersonInput()
    {
        if (!enableMouseRotation)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        // Only rotate while the cursor is locked
        if (mouse == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 mouseDelta = mouse.delta.ReadValue();

        // Turn the camera left and right
        orbitYaw += mouseDelta.x * mouseSensitivity;

        // Turn the camera up and down
        float verticalDirection = invertMouseY ? 1f : -1f;

        orbitPitch += mouseDelta.y *
                    mouseSensitivity *
                    verticalDirection;

        // Stop the camera rotating too far vertically
        orbitPitch = Mathf.Clamp(
            orbitPitch,
            minimumPitch,
            maximumPitch
        );
    }

    // Calculate the camera position for the active view
    void GetTargetPose(
        out Vector3 position,
        out Quaternion rotation,
        out float fov)
    {
        // Follow the player using the selected camera style
        if (playing && followTarget != null)
        {
            if (playerCameraStyle == PlayerCameraStyle.ThirdPerson)
            {
                GetThirdPersonPose(
                    out position,
                    out rotation,
                    out fov
                );
            }
            else
            {
                GetIsometricPose(
                    out position,
                    out rotation,
                    out fov
                );
            }

            return;
        }

        // Keep the existing overhead map behaviour
        if (mapFace != null)
        {
            Vector3 outward = mapFace.up;

            if (Mathf.Abs(mapTilt) > 0.01f)
            {
                outward = Quaternion.AngleAxis(
                    mapTilt,
                    mapFace.right
                ) * outward;
            }

            position = mapFace.position + outward * mapDistance;

            rotation = Quaternion.LookRotation(
                -outward,
                mapFace.forward
            );

            fov = mapFOV;
            return;
        }

        // Keep the existing cube overview behaviour
        Vector3 center = cubeCenter != null
            ? cubeCenter.position
            : Vector3.zero;

        rotation = Quaternion.Euler(
            overviewAngles.x,
            overviewAngles.y,
            0f
        );

        position = center -
                   rotation * Vector3.forward * overviewDistance;

        fov = overviewFOV;
    }

    // Calculate the improved isometric camera position
    void GetIsometricPose(
        out Vector3 position,
        out Quaternion rotation,
        out float fov)
    {
        rotation = Quaternion.Euler(
            isoAngles.x,
            isoAngles.y,
            0f
        );

        // Look slightly above and ahead of the player
        Vector3 focusPoint = followTarget.position +
                             Vector3.up * isoFocusHeight +
                             followTarget.forward * isoLookAheadDistance;

        position = focusPoint -
                   rotation * Vector3.forward * isoDistance;

        fov = isoFOV;
    }

    // Calculate the third person camera position
    void GetThirdPersonPose(
        out Vector3 position,
        out Quaternion rotation,
        out float fov)
    {
        // Build the camera rotation from mouse input
        rotation = Quaternion.Euler(
            orbitPitch,
            orbitYaw,
            0f
        );

        // Focus above the player's feet
        Vector3 focusPoint = followTarget.position +
                             Vector3.up * thirdPersonHeight;

        // Place the camera behind its focus point
        Vector3 cameraDirection = -(rotation * Vector3.forward);

        float distance = Mathf.Max(0.1f, thirdPersonDistance);

        // Check for walls between the player and camera
        if (enableCameraCollision)
        {
            distance = GetCollisionDistance(
                focusPoint,
                cameraDirection,
                distance
            );
        }

        position = focusPoint + cameraDirection * distance;
        fov = thirdPersonFOV;
    }

    // Move the camera closer when an obstacle blocks the view
    float GetCollisionDistance(
        Vector3 origin,
        Vector3 direction,
        float desiredDistance)
    {
        float closestDistance = desiredDistance;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, collisionRadius),
            direction,
            desiredDistance,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            // Ignore the player and any child colliders
            if (hit.collider.transform.IsChildOf(followTarget))
            {
                continue;
            }

            // Keep the camera just in front of the obstacle
            float safeDistance = Mathf.Max(
                0.05f,
                hit.distance - collisionPadding
            );

            if (safeDistance < closestDistance)
            {
                closestDistance = safeDistance;
            }
        }

        return closestDistance;
    }

    // Manage the mouse cursor based on the active camera style
    void UpdateCursorState()
    {
        bool thirdPersonActive =
            playing &&
            followTarget != null &&
            playerCameraStyle == PlayerCameraStyle.ThirdPerson &&
            enableMouseRotation;

        // Lock or unlock when changing camera styles
        if (thirdPersonActive != wasThirdPersonActive)
        {
            if (thirdPersonActive)
            {
                LockCursor();
            }
            else
            {
                UnlockCursor();
            }

            wasThirdPersonActive = thirdPersonActive;
        }

        if (!thirdPersonActive)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        // Escape releases the cursor
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
            return;
        }

        // Left-click locks the cursor again
        if (mouse != null &&
            mouse.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    // Lock and hide the mouse cursor
    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Unlock and show the mouse cursor
    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Restore the cursor when the camera is disabled
    void OnDisable()
    {
        UnlockCursor();
        wasThirdPersonActive = false;
    }
}