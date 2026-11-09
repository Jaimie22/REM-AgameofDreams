using System.Collections;
using UnityEngine;

// Glides the camera between fixed viewpoints in the room, so the menu
// moves through the space rather than cutting.
// Put this on the Main Camera.
public class MenuCamera : MonoBehaviour
{
    [System.Serializable]
    public class Viewpoint
    {
        public string name = "Menu";
        [Tooltip("An empty GameObject placed where the camera should sit.")]
        public Transform marker;
        [Tooltip("Field of view at this viewpoint, for a tighter or wider shot.")]
        public float fieldOfView = 40f;
    }

    [Header("Viewpoints")]
    [Tooltip("The resting shot of the room, where the menu sits.")]
    public Viewpoint menuView;
    [Tooltip("Close on the television, where the settings live.")]
    public Viewpoint settingsView;

    [Header("Movement")]
    [Tooltip("Seconds the glide takes.")]
    public float glideTime = 2f;
    [Tooltip("Shapes the movement, so it eases out of one shot and into the next.")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    Camera cam;
    Coroutine gliding;

    public bool IsMoving => gliding != null;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        // Begin on the menu shot, with no glide
        SnapTo(menuView);
    }

    // Jumps straight to a viewpoint
    public void SnapTo(Viewpoint view)
    {
        if (view == null || view.marker == null) return;

        transform.SetPositionAndRotation(view.marker.position, view.marker.rotation);
        if (cam != null) cam.fieldOfView = view.fieldOfView;
    }

    public void GoToMenu()
    {
        Glide(menuView);
    }

    public void GoToSettings()
    {
        Glide(settingsView);
    }

    // Eases across to a viewpoint over a few seconds
    public void Glide(Viewpoint view)
    {
        if (view == null || view.marker == null) return;

        if (gliding != null) StopCoroutine(gliding);
        gliding = StartCoroutine(GlideRoutine(view));
    }

    IEnumerator GlideRoutine(Viewpoint view)
    {
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        float fromFov = cam != null ? cam.fieldOfView : 60f;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, glideTime))
        {
            float shaped = ease.Evaluate(Mathf.Clamp01(t));

            transform.position = Vector3.Lerp(fromPos, view.marker.position, shaped);
            transform.rotation = Quaternion.Slerp(fromRot, view.marker.rotation, shaped);

            if (cam != null)
                cam.fieldOfView = Mathf.Lerp(fromFov, view.fieldOfView, shaped);

            yield return null;
        }

        SnapTo(view);
        gliding = null;
    }
}
