using UnityEngine;

// A platform that moves on its own: sliding, rising and falling, or turning.
// Anything standing on it gets carried along. The player asks for that each frame (see PlayerController).
// Its position works out from the clock, so it carries on smoothly even after a trip out to the map and back.
// It runs before the player, so the player always sees where the platform is this frame.
[DefaultExecutionOrder(-50)]
public class MovingPlatform : MonoBehaviour
{
    public enum Motion { Slide, Lift, Spin }

    [Header("Motion")]
    public Motion motion = Motion.Slide;
    [Tooltip("Direction of a slide or a lift, in the face's own space.")]
    public Vector3 direction = Vector3.right;
    [Tooltip("How far it travels each way from where it starts.")]
    public float travel = 2f;
    [Tooltip("Seconds for one trip there and back.")]
    public float period = 6f;
    [Tooltip("Where it is in its trip when the level begins, 0 to 1. Keeps platforms out of step with each other.")]
    [Range(0f, 1f)] public float startOffset = 0f;
    [Tooltip("0 is a gentle swing that slows at each end. 1 is an even speed all the way, with only a short ease as it turns round.")]
    [Range(0f, 1f)] public float evenness = 0.8f;
    [Tooltip("Degrees a second for a spinning platform. Negative turns the other way.")]
    public float spinSpeed = 25f;

    Vector3 origin;
    Quaternion originRotation;

    // Where it was at the start of this frame, so the player can work out how far it moved
    Matrix4x4 before;
    Matrix4x4 beforeInverse;
    Matrix4x4 after;
    float yawThisFrame;
    int movedOnFrame = -1;

    // Called by the generator once the platform is in its starting place
    public void Setup(Motion kind, Vector3 localDirection, float distance, float seconds,
                      float offset, float evennessAmount, float degreesPerSecond)
    {
        motion = kind;
        direction = localDirection.normalized;
        travel = distance;
        period = Mathf.Max(0.5f, seconds);
        startOffset = offset;
        evenness = evennessAmount;
        spinSpeed = degreesPerSecond;

        origin = transform.localPosition;
        originRotation = transform.localRotation;

        // A kinematic body is the proper way to have a collider that moves
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    void Update()
    {
        before = transform.localToWorldMatrix;
        beforeInverse = before.inverse;

        if (motion == Motion.Spin)
        {
            float angle = spinSpeed * Time.time + startOffset * 360f;
            transform.localRotation = originRotation * Quaternion.Euler(0f, angle, 0f);
            yawThisFrame = spinSpeed * Time.deltaTime;
        }
        else
        {
            transform.localPosition = origin + direction * (Wave() * travel);
            yawThisFrame = 0f;
        }

        after = transform.localToWorldMatrix;
        movedOnFrame = Time.frameCount;
    }

    // A trip from -1 to 1 and back with no sudden changes of speed.
    // A plain sine slows right down at the ends. Feeding it through asin evens the speed out,
    // so it moves at a steady pace and only eases for a moment as it turns round.
    float Wave()
    {
        float phase = (Time.time / period + startOffset) * Mathf.PI * 2f;

        float flatten = Mathf.Lerp(0.001f, 0.92f, evenness);
        return Mathf.Asin(flatten * Mathf.Sin(phase)) / Mathf.Asin(flatten);
    }

    // How far a point standing on the platform was carried this frame, and how much the platform turned
    public Vector3 CarryDelta(Vector3 worldPoint, out float yaw)
    {
        yaw = 0f;

        // If it hasn't moved yet this frame there's nothing to carry
        if (movedOnFrame != Time.frameCount) return Vector3.zero;

        yaw = yawThisFrame;

        // Where the point sat on the platform, then where that same spot is now
        Vector3 onPlatform = beforeInverse.MultiplyPoint3x4(worldPoint);
        return after.MultiplyPoint3x4(onPlatform) - worldPoint;
    }
}
