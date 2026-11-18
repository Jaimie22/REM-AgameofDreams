using UnityEngine;

// Drives the demon's animation and its glow from whichever brain is on the same object:
// Hunter in the maze, OpenHunter on open ground. Neither brain needs to change.
//
//   Wandering  -> Idle when standing, Walk when moving     (glow: calm)
//   Tracking   -> Walk, a little faster                    (glow: suspicious)
//   Chasing    -> Run                                      (glow: angry)
// A short Alert pose plays the moment it notices something.
[DisallowMultipleComponent]
public class DemonAnimator : MonoBehaviour
{
    [Tooltip("Seconds to blend between animations.")]
    [SerializeField] float fade = 0.2f;

    [Tooltip("Below this speed (units per second) it counts as standing still.")]
    [SerializeField] float moveThreshold = 0.15f;

    [Header("Detection fed to the glow and face")]
    [SerializeField, Range(0f, 1f)] float wanderingDetection = 0.1f;
    [SerializeField, Range(0f, 1f)] float trackingDetection = 0.5f;
    [SerializeField, Range(0f, 1f)] float chasingDetection = 1f;

    [Header("Alert beat when it notices something")]
    [SerializeField] float alertSeconds = 0.6f;

    Animator animator;
    DemonDetectionColor glow;
    Hunter mazeBrain;
    OpenHunter openBrain;

    Vector3 lastPosition;
    float smoothedSpeed;
    int lastLevel;
    float alertUntil;
    string playing;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        glow = GetComponentInChildren<DemonDetectionColor>();
        lastPosition = transform.position;
    }

    void Update()
    {
        // The spawner adds the brain after the prefab is created, so keep looking
        if (openBrain == null) openBrain = GetComponent<OpenHunter>();
        if (mazeBrain == null) mazeBrain = GetComponent<Hunter>();

        // 0 wandering, 1 tracking, 2 chasing
        int level = 0;
        if (openBrain != null)
            level = openBrain.CurrentState == OpenHunter.State.Chasing ? 2 : 0;
        else if (mazeBrain != null)
            level = mazeBrain.CurrentState == Hunter.State.Chasing ? 2
                  : mazeBrain.CurrentState == Hunter.State.Tracking ? 1 : 0;

        // How fast it is really moving, smoothed so it doesn't flicker between Idle and Walk
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = (transform.position - lastPosition).magnitude / dt;
        lastPosition = transform.position;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-8f * dt));
        bool moving = smoothedSpeed > moveThreshold;

        // Just noticed something: hold the Alert pose briefly
        if (level > lastLevel) alertUntil = Time.time + alertSeconds;
        lastLevel = level;

        if (glow != null)
            glow.SetDetection(level == 2 ? chasingDetection
                            : level == 1 ? trackingDetection
                            : wanderingDetection);

        if (animator == null || animator.runtimeAnimatorController == null) return;

        string wanted;
        float animSpeed = 1f;

        if (Time.time < alertUntil) wanted = "Alert";
        else if (level == 2) wanted = "Run";
        else if (level == 1) { wanted = "Walk"; animSpeed = 1.3f; }
        else wanted = moving ? "Walk" : "Idle";

        animator.speed = animSpeed;

        if (wanted != playing)
        {
            animator.CrossFadeInFixedTime(wanted, fade);
            playing = wanted;
        }
    }
}
