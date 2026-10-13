using UnityEngine;

// One appearance of the sleep-paralysis demon. It fades in at a distance,
// watches, and vanishes before the player can be sure of what they saw.
public class DemonSighting : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds it takes to fade into view.")]
    public float fadeInTime = 0.8f;
    [Tooltip("Seconds it stands there before leaving.")]
    public float lingerTime = 2.5f;
    [Tooltip("Seconds it takes to fade away.")]
    public float fadeOutTime = 0.5f;

    [Header("Behaviour")]
    [Tooltip("It vanishes if the player gets this close.")]
    public float fleeDistance = 8f;
    [Tooltip("It turns to face the player while it's visible.")]
    public bool watchPlayer = true;
    [Tooltip("Vanish instantly if it leaves the screen, so it's never seen going.")]
    public bool vanishWhenUnseen = true;

    [Header("Look")]
    [Tooltip("Highest opacity it reaches. Below 1 keeps it ghostly.")]
    [Range(0f, 1f)] public float maxOpacity = 0.85f;
    [Tooltip("Small sway, so it never looks like a statue.")]
    public float swayAmount = 0.04f;
    public float swaySpeed = 1.3f;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");

    Transform player;
    Camera cam;
    Renderer[] renderers;
    float age;
    float opacity;
    bool leaving;
    Vector3 basePosition;

    // Called by the director right after it spawns one
    public void Begin(Transform playerTransform, Camera viewCamera)
    {
        player = playerTransform;
        cam = viewCamera;
        basePosition = transform.position;

        renderers = GetComponentsInChildren<Renderer>();
        SetOpacity(0f);
    }

    void Update()
    {
        age += Time.deltaTime;

        if (player != null)
        {
            // Turn to watch, but stay upright
            if (watchPlayer)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer = Vector3.ProjectOnPlane(toPlayer, transform.up);

                if (toPlayer.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(toPlayer, transform.up);
            }

            // Gone if the player closes in
            if (Vector3.Distance(player.position, transform.position) < fleeDistance)
                leaving = true;
        }

        // Gone the moment it's off screen, so it's never seen leaving
        if (vanishWhenUnseen && opacity > 0.1f && !IsOnScreen())
        {
            Destroy(gameObject);
            return;
        }

        if (age > fadeInTime + lingerTime) leaving = true;

        // Fade in, then out
        float target = leaving ? 0f : maxOpacity;
        float speed = leaving ? 1f / Mathf.Max(0.01f, fadeOutTime)
                              : 1f / Mathf.Max(0.01f, fadeInTime);

        opacity = Mathf.MoveTowards(opacity, target, speed * maxOpacity * Time.deltaTime);
        SetOpacity(opacity);

        // A slow sway on the spot
        float sway = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
        transform.position = basePosition + transform.up * sway;

        if (leaving && opacity <= 0.01f) Destroy(gameObject);
    }

    bool IsOnScreen()
    {
        if (cam == null) return true;

        Vector3 viewPoint = cam.WorldToViewportPoint(transform.position);
        return viewPoint.z > 0f &&
               viewPoint.x > -0.1f && viewPoint.x < 1.1f &&
               viewPoint.y > -0.1f && viewPoint.y < 1.1f;
    }

    // Fades every part of it together
    void SetOpacity(float value)
    {
        if (renderers == null) return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(block);

            Color c = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorID)
                ? r.sharedMaterial.GetColor(BaseColorID)
                : Color.black;

            c.a = value;
            block.SetColor(BaseColorID, c);
            block.SetColor(ColorID, c);
            r.SetPropertyBlock(block);
        }
    }
}
