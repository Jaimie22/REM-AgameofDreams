using UnityEngine;

// Unsettles the camera as tension rises, and thumps gently with the heartbeat.
// Put this on the Main Camera, alongside DreamCamera.
// The execution order makes sure it runs after DreamCamera has positioned itself.
[DefaultExecutionOrder(100)]
public class CameraShake : MonoBehaviour
{
    [Header("Tension Shake")]
    [Tooltip("How far the camera drifts at full tension, in units.")]
    public float maxShake = 0.12f;
    [Tooltip("How fast the drift moves. Slow feels queasy, fast feels violent.")]
    public float shakeSpeed = 2f;
    [Tooltip("Tension below this causes no shake at all.")]
    [Range(0f, 1f)] public float threshold = 0.4f;

    [Header("Roll")]
    [Tooltip("Degrees the view tilts at full tension. Keep this small, it's strong stuff.")]
    public float maxRoll = 0.5f;

    [Header("Heartbeat Pulse")]
    [Tooltip("A small kick on each beat, as if the camera has a pulse.")]
    public float beatKick = 0.04f;
    [Tooltip("How quickly each kick settles.")]
    public float beatRecovery = 6f;

    [Header("Settling")]
    [Tooltip("How quickly the shake eases in and out. Lower = smoother, never abrupt.")]
    public float settleSpeed = 2f;
    [Tooltip("Below this the camera is forced perfectly still, so it always fully stops.")]
    public float stillThreshold = 0.01f;

    float seedX, seedY;
    float beatPulse;
    float lastPhase;
    float currentAmount;   // smoothed, so the shake never snaps on or off

    private void Start()
    {
        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
    }

    private void LateUpdate()
    {
        // No heartbeat system, or no tension, means no shake at all
        float tension = HeartbeatSystem.Instance != null
            ? HeartbeatSystem.Instance.Tension
            : 0f;

        float wanted = Mathf.InverseLerp(threshold, 1f, tension);

        // Ease towards the wanted amount, so it never jolts on or off
        currentAmount = Mathf.Lerp(currentAmount, wanted, settleSpeed * Time.deltaTime);
        beatPulse = Mathf.Lerp(beatPulse, 0f, beatRecovery * Time.deltaTime);

        // Force a complete stop rather than drifting forever at a tiny value
        if (currentAmount < stillThreshold && beatPulse < stillThreshold)
        {
            currentAmount = 0f;
            beatPulse = 0f;
            return;
        }

        // Smooth wandering drift, rather than a jittery rattle
        float t = Time.time * shakeSpeed;
        float x = (Mathf.PerlinNoise(seedX, t) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(seedY, t) - 0.5f) * 2f;

        Vector3 offset = transform.right * (x * maxShake * currentAmount)
                       + transform.up * (y * maxShake * currentAmount + beatPulse * beatKick);

        transform.position += offset;
        transform.Rotate(Vector3.forward, x * maxRoll * currentAmount, Space.Self);

        trackBeats(tension);
    }

    // Kicks the camera roughly in time with the heartbeat
    private void trackBeats(float tension)
    {
        if (HeartbeatSystem.Instance == null || tension < 0.05f) return;

        float bpm = Mathf.Lerp(HeartbeatSystem.Instance.restingBPM,
                               HeartbeatSystem.Instance.maxBPM, tension);
        float phase = (Time.time * bpm / 60f) % 1f;

        if (phase < lastPhase) beatPulse = 1f; // the phase wrapped, so that's a beat
        lastPhase = phase;
    }
}
