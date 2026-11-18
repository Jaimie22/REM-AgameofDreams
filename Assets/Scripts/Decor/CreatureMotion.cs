using UnityEngine;

// Simple procedural body motion for the Stalker and Crawler, applied to the child "visual" only.
public class CreatureMotion : MonoBehaviour
{
    public enum Kind { Stalker, Crawler }

    public Kind kind = Kind.Stalker;
    [Tooltip("The model. Defaults to the first child.")]
    public Transform visual;

    Hunter hunter;
    OpenHunter open;
    Vector3 basePos, baseScale, lastPos;
    float phase, speed, alert;
    int previousLevel;

    void Start()
    {
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual == null) { enabled = false; return; }

        basePos = visual.localPosition;
        baseScale = visual.localScale;
        lastPos = transform.position;
        phase = Random.value * 20f;
    }

    // 0 = wandering, 1 = tracking, 2 = chasing, for either kind of demon brain (also used by EventSounds)
    public static int AlertLevel(Hunter hunter, OpenHunter open)
    {
        if (hunter != null) return (int)hunter.CurrentState;
        if (open != null) return open.CurrentState == OpenHunter.State.Chasing ? 2 : 0;
        return 0;
    }

    int Level()
    {
        if (hunter == null) hunter = GetComponent<Hunter>();
        if (open == null) open = GetComponent<OpenHunter>();
        return AlertLevel(hunter, open);
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        int level = Level();
        if (level > previousLevel && level >= 1) alert = 1f;       // startle when it notices the player
        previousLevel = level;
        alert = Mathf.MoveTowards(alert, 0f, dt * 2.2f);

        float v = (transform.position - lastPos).magnitude / dt;
        lastPos = transform.position;
        speed = Mathf.Lerp(speed, v, dt * 8f);
        float move = Mathf.Clamp01(speed / 1.4f);

        float t = Time.time + phase;
        float pitch, yaw = 0f, roll, lift;
        Vector3 scale = Vector3.one;

        if (kind == Kind.Stalker)
        {
            float rate = level == 2 ? 7f : (level == 1 ? 4.5f : 2.8f);
            roll = Mathf.Sin(t * rate) * (1.5f + 4f * move);                       // swaying walk
            pitch = (level == 2 ? 13f : (level == 1 ? 5f : 0f)) * move - alert * 9f;   // leans in to chase, rears back on alert
            lift = Mathf.Abs(Mathf.Sin(t * rate)) * 0.04f * move;
            scale.y = 1f + Mathf.Sin(t * 1.1f) * 0.015f + alert * 0.07f;           // slow breathing, stretches tall on alert
        }
        else
        {
            float rate = level == 2 ? 20f : 12f;
            yaw = Mathf.Sin(t * rate) * (2f + 5f * move);                           // skittering
            roll = Mathf.Sin(t * rate * 0.5f) * 2f * move;
            pitch = 0f;
            lift = Mathf.Abs(Mathf.Sin(t * rate)) * 0.05f * move;
            float crouch = level == 2 ? 0.88f : 1f;
            scale = new Vector3(1f + alert * 0.12f, crouch + alert * 0.12f, 1f + alert * 0.12f);
        }

        visual.localPosition = basePos + Vector3.up * lift;
        visual.localRotation = Quaternion.Euler(pitch, yaw, roll);
        visual.localScale = Vector3.Scale(baseScale, scale);
    }
}
