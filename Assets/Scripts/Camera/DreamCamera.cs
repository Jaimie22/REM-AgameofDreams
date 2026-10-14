using UnityEngine;

// Smoothly blends between three views:
// Playing  = close in, following the player at a fixed isometric angle
// Cube     = pulled back, looking at the whole cube so it can be spun
// Map      = straight down onto one face, so the player can read the level
[RequireComponent(typeof(Camera))]
public class DreamCamera : MonoBehaviour
{
    [Header("Cube View (zoomed out)")]
    [Tooltip("Drag the cube's root object here.")]
    public Transform cubeCenter;
    public float overviewDistance = 45f;
    [Tooltip("X = pitch (look down angle), Y = yaw.")]
    public Vector2 overviewAngles = new Vector2(20f, 0f);
    public float overviewFOV = 50f;

    [Header("Map View (M key)")]
    [Tooltip("How far above the face the map camera sits. Lower = the face fills more of the screen.")]
    public float mapDistance = 38f;
    [Tooltip("Degrees off dead-on. 0 is straight down, which reads best as a map.")]
    public float mapTilt = 0f;
    public float mapFOV = 50f;

    [Header("Isometric (zoomed in)")]
    public float isoDistance = 30f;
    [Tooltip("35 / 45 gives a classic isometric look.")]
    public Vector2 isoAngles = new Vector2(35f, 45f);
    [Tooltip("A low FOV from far away flattens perspective to feel isometric.")]
    public float isoFOV = 20f;

    [Header("Smoothing")]
    public float moveSmoothTime = 0.35f;
    public float rotateSpeed = 6f;

    public Camera Cam { get; private set; }

    Transform followTarget;
    Transform mapFace;        // the face being shown as a map, if any
    bool playing;
    Vector3 moveVelocity;

    private void Awake()
    {
        Cam = GetComponent<Camera>();
    }

    // Pull back to the whole cube, free to be spun
    public void setOverview()
    {
        playing = false;
        followTarget = null;
        mapFace = null;
    }

    // Look straight down onto one face, as a map
    public void setMapView(Transform face)
    {
        playing = false;
        followTarget = null;
        mapFace = face;
    }

    public void setPlaying(Transform target)
    {
        playing = true;
        followTarget = target;
        mapFace = null;
    }

    // Jumps straight to the current view with no smoothing
    public void snapToTarget()
    {
        getTargetPose(out Vector3 pos, out Quaternion rot, out float fov);
        transform.SetPositionAndRotation(pos, rot);
        Cam.fieldOfView = fov;
        moveVelocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        getTargetPose(out Vector3 pos, out Quaternion rot, out float fov);

        transform.position = Vector3.SmoothDamp(transform.position, pos, ref moveVelocity, moveSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotateSpeed * Time.deltaTime);
        Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, fov, rotateSpeed * Time.deltaTime);
    }

    // Works out where the camera *should* be for the current view
    private void getTargetPose(out Vector3 pos, out Quaternion rot, out float fov)
    {
        // Following the player
        if (playing && followTarget != null)
        {
            rot = Quaternion.Euler(isoAngles.x, isoAngles.y, 0f);
            pos = followTarget.position - rot * Vector3.forward * isoDistance;
            fov = isoFOV;
            return;
        }

        // Straight down onto one face
        if (mapFace != null)
        {
            // The face's green Y arrow points outward, so that's where the camera goes
            Vector3 outward = mapFace.up;

            if (Mathf.Abs(mapTilt) > 0.01f)
                outward = Quaternion.AngleAxis(mapTilt, mapFace.right) * outward;

            // The face's own forward keeps the map the right way up, however the cube sits
            pos = mapFace.position + outward * mapDistance;
            rot = Quaternion.LookRotation(-outward, mapFace.forward);
            fov = mapFOV;
            return;
        }

        // The whole cube, from the fixed angle
        Vector3 center = cubeCenter != null ? cubeCenter.position : Vector3.zero;
        rot = Quaternion.Euler(overviewAngles.x, overviewAngles.y, 0f);
        pos = center - rot * Vector3.forward * overviewDistance;
        fov = overviewFOV;
    }
}
