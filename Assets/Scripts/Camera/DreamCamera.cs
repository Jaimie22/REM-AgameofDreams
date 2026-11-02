using System.Collections.Generic;
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
        ThirdPerson,
        TopDown
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

    [Header("Top Down Camera")]
    [Tooltip("Distance above the player. Zooming with the scroll wheel never goes further than this")]
    public float topDownDistance = 7f;

    [Tooltip("Downward angle. 90 is straight down, lower tilts it to see ahead")]
    [Range(40f, 89f)]
    public float topDownPitch = 70f;

    [Tooltip("Compass direction the camera faces. It never turns, so up on the keyboard is always up on screen")]
    public float topDownYaw = 0f;

    [Range(20f, 100f)]
    public float topDownFOV = 60f;

    [Tooltip("Height above the player's feet that the camera looks at")]
    public float topDownFocusHeight = 1.1f;

    [Header("Third Person Camera")]
    [Tooltip("Distance behind the player")]
    public float thirdPersonDistance = 5f;

    [Tooltip("Height above the player's feet that the camera looks at. About chest height keeps the whole player in the middle of the screen")]
    public float thirdPersonFocusHeight = 1.1f;

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

    [Header("Third Person Camera Height")]
    [Tooltip("Lowest the camera can go above the player's feet. Stops it dipping under the floor, where the player is seen through the ground")]
    public float minimumCameraHeight = 0.8f;

    [Header("Third Person Camera Collision")]
    [Tooltip("Walls between the camera and the player become invisible while they're in the way, so the player can always be seen. The camera then keeps its full distance")]
    public bool hideBlockingWalls = false;

    [Tooltip("Width of the line of sight checked for walls")]
    public float blockCheckRadius = 0.4f;

    [Tooltip("Move the camera closer when a wall blocks its view. Only used when Hide Blocking Walls is off")]
    public bool enableCameraCollision = true;

    [Tooltip("Layers that can block the camera")]
    public LayerMask collisionLayers = ~0;

    [Tooltip("Radius used to check for obstacles")]
    public float collisionRadius = 0.2f;

    [Tooltip("Extra space kept between the camera and obstacles")]
    public float collisionPadding = 0.1f;

    [Tooltip("The camera never comes closer to the player than this, so a wall can't push it into the player's head")]
    public float minimumCameraDistance = 2.5f;

    [Header("Cursor")]
    [Tooltip("Hides and locks the mouse cursor while playing, with any camera style. It's freed in the map and cube views. Escape opens the cube view")]
    public bool lockCursorWhilePlaying = true;

    [Header("Zoom")]
    [Tooltip("Scroll wheel zooms the player camera in and out. The distances set above are the furthest it goes.")]
    public bool enableZoom = true;

    [Tooltip("Closest the camera gets, as a share of the full distance. 0.3 = 30% of the way in")]
    [Range(0.1f, 1f)]
    public float minimumZoom = 0.35f;

    [Tooltip("How much each scroll step zooms. Bigger = faster")]
    [Range(0.02f, 0.5f)]
    public float zoomStep = 0.1f;

    [Tooltip("How quickly the zoom catches up with the scroll wheel")]
    public float zoomSmoothing = 10f;

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

    // Walls currently hidden because they were in the way, and how they were drawn before
    readonly Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> hiddenWalls =
        new Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>();
    readonly HashSet<Renderer> blockingNow = new HashSet<Renderer>();
    readonly List<Renderer> wallsToRestore = new List<Renderer>();

    // 1 is fully zoomed out, which is the distance set in the Inspector
    float zoomTarget = 1f;
    float zoom = 1f;

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

        // Zoom with the scroll wheel while playing
        UpdateZoom();

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

        // Walls in the way fade out of sight, so the player is never hidden
        if (playing &&
            followTarget != null &&
            playerCameraStyle == PlayerCameraStyle.ThirdPerson &&
            hideBlockingWalls)
        {
            HideBlockingWalls();
        }
        else
        {
            RestoreWalls();
        }
    }

    // Makes anything between the camera and the player invisible, apart from its shadow
    void HideBlockingWalls()
    {
        Vector3 focus = followTarget.position + Vector3.up * thirdPersonFocusHeight;
        Vector3 from = transform.position;
        Vector3 toFocus = focus - from;
        float distance = toFocus.magnitude;

        blockingNow.Clear();

        if (distance > 0.01f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                from,
                blockCheckRadius,
                toFocus / distance,
                distance,
                collisionLayers,
                QueryTriggerInteraction.Ignore
            );

            foreach (RaycastHit hit in hits)
            {
                AddBlocker(hit.collider);
            }
        }

        // A wall the camera is inside of isn't found by the line above
        foreach (Collider inside in Physics.OverlapSphere(
            from,
            blockCheckRadius,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            AddBlocker(inside);
        }

        // Anything that is no longer in the way comes back
        wallsToRestore.Clear();

        foreach (KeyValuePair<Renderer, UnityEngine.Rendering.ShadowCastingMode> entry in hiddenWalls)
        {
            if (!blockingNow.Contains(entry.Key)) wallsToRestore.Add(entry.Key);
        }

        foreach (Renderer wall in wallsToRestore)
        {
            if (wall != null) wall.shadowCastingMode = hiddenWalls[wall];
            hiddenWalls.Remove(wall);
        }
    }

    void AddBlocker(Collider col)
    {
        // The player is never hidden
        if (col.transform.IsChildOf(followTarget)) return;

        // Floors and low steps stay, only things standing above the player's feet are walls
        if (col.bounds.max.y < followTarget.position.y + 0.5f) return;

        Renderer rend = col.GetComponent<Renderer>();
        if (rend == null) rend = col.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        blockingNow.Add(rend);

        if (!hiddenWalls.ContainsKey(rend))
        {
            hiddenWalls[rend] = rend.shadowCastingMode;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
    }

    // Puts every hidden wall back
    void RestoreWalls()
    {
        if (hiddenWalls.Count == 0) return;

        foreach (KeyValuePair<Renderer, UnityEngine.Rendering.ShadowCastingMode> entry in hiddenWalls)
        {
            if (entry.Key != null) entry.Key.shadowCastingMode = entry.Value;
        }

        hiddenWalls.Clear();
    }

    // Scroll up zooms in, scroll down zooms out, never further than the Inspector distance
    void UpdateZoom()
    {
        Mouse mouse = Mouse.current;

        if (enableZoom && playing && followTarget != null && mouse != null)
        {
            // Only the direction matters, as the size of a scroll step varies between computers
            float scroll = mouse.scroll.ReadValue().y;

            if (scroll > 0f) zoomTarget -= zoomStep;
            else if (scroll < 0f) zoomTarget += zoomStep;

            zoomTarget = Mathf.Clamp(zoomTarget, minimumZoom, 1f);
        }

        // Ease towards the target, so each scroll glides rather than jumps
        zoom = Mathf.Lerp(zoom, zoomTarget, Mathf.Clamp01(zoomSmoothing * Time.deltaTime));
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
            else if (playerCameraStyle == PlayerCameraStyle.TopDown)
            {
                GetTopDownPose(
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
                   rotation * Vector3.forward * (isoDistance * zoom);

        fov = isoFOV;
    }

    // A fixed angle looking down on the player, which follows them but never turns
    void GetTopDownPose(
        out Vector3 position,
        out Quaternion rotation,
        out float fov)
    {
        rotation = Quaternion.Euler(
            topDownPitch,
            topDownYaw,
            0f
        );

        Vector3 focusPoint = followTarget.position +
                             Vector3.up * topDownFocusHeight;

        // Scroll zoom pulls it in, never out past the Inspector distance
        position = focusPoint -
                   rotation * Vector3.forward * (topDownDistance * zoom);

        fov = topDownFOV;
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
                             Vector3.up * thirdPersonFocusHeight;

        // Place the camera behind its focus point
        Vector3 cameraDirection = -(rotation * Vector3.forward);

        float distance = Mathf.Max(0.1f, thirdPersonDistance * zoom);

        // Check for walls between the player and camera
        if (enableCameraCollision && !hideBlockingWalls)
        {
            distance = GetCollisionDistance(
                focusPoint,
                cameraDirection,
                distance
            );
        }

        position = focusPoint + cameraDirection * distance;
        fov = thirdPersonFOV;

        // Never below the floor. When the mouse drags the camera too low, it slides along at
        // this height and tilts up to keep looking at the player, so they stay in the centre.
        float lowestY = followTarget.position.y + minimumCameraHeight;

        if (position.y < lowestY)
        {
            position.y = lowestY;
            rotation = Quaternion.LookRotation(focusPoint - position, Vector3.up);
        }
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

            // Ignore floors and the tops of walls. The camera rises over them rather than
            // being dragged in, which is what used to hide the player when looking down
            if (hit.normal.y > 0.7f)
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

        // Close to a wall, the camera keeps its distance from the player, and may poke into the wall
        // instead. The player stays in the centre of the screen, as the camera always looks at them.
        float closestAllowed = Mathf.Min(desiredDistance, minimumCameraDistance);
        return Mathf.Max(closestDistance, closestAllowed);
    }

    // Manage the mouse cursor based on the active camera style
    void UpdateCursorState()
    {
        // Locked whenever the player is being followed, whichever camera style is used
        bool thirdPersonActive =
            playing &&
            followTarget != null &&
            lockCursorWhilePlaying;

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

        Mouse mouse = Mouse.current;

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
        RestoreWalls();
        UnlockCursor();
        wasThirdPersonActive = false;
    }
}