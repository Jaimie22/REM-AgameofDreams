using UnityEngine;

// Smoothly blends between two views:
// Overview  = pulled back, looking at the whole cube
// Isometric = close in, following the player at a fixed angle
[RequireComponent(typeof(Camera))]
public class DreamCamera : MonoBehaviour
{
    [Header("Overview (zoomed out)")]
    [Tooltip("Drag the cube's root object here.")]
    public Transform cubeCenter;
    public float overviewDistance = 45f;
    [Tooltip("X = pitch (look down angle), Y = yaw.")]
    public Vector2 overviewAngles = new Vector2(20f, 0f);
    public float overviewFOV = 50f;

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
    bool playing;
    Vector3 moveVelocity;

    void Awake()
    {
        Cam = GetComponent<Camera>();
    }

    public void SetOverview()
    {
        playing = false;
        followTarget = null;
    }

    public void SetPlaying(Transform target)
    {
        playing = true;
        followTarget = target;
    }

    // Jumps straight to the current view with no smoothing
    public void SnapToTarget()
    {
        GetTargetPose(out Vector3 pos, out Quaternion rot, out float fov);
        transform.SetPositionAndRotation(pos, rot);
        Cam.fieldOfView = fov;
        moveVelocity = Vector3.zero;
    }

    void LateUpdate()
    {
        GetTargetPose(out Vector3 pos, out Quaternion rot, out float fov);

        transform.position = Vector3.SmoothDamp(transform.position, pos, ref moveVelocity, moveSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotateSpeed * Time.deltaTime);
        Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, fov, rotateSpeed * Time.deltaTime);
    }

    // Works out where the camera *should* be for the current mode
    void GetTargetPose(out Vector3 pos, out Quaternion rot, out float fov)
    {
        if (playing && followTarget != null)
        {
            rot = Quaternion.Euler(isoAngles.x, isoAngles.y, 0f);
            pos = followTarget.position - rot * Vector3.forward * isoDistance;
            fov = isoFOV;
        }
        else
        {
            Vector3 center = cubeCenter != null ? cubeCenter.position : Vector3.zero;
            rot = Quaternion.Euler(overviewAngles.x, overviewAngles.y, 0f);
            pos = center - rot * Vector3.forward * overviewDistance;
            fov = overviewFOV;
        }
    }
}