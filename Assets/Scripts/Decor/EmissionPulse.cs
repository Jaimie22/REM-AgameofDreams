using UnityEngine;

// Pulses a material's glow on a sine wave.
public class EmissionPulse : MonoBehaviour
{
    [Tooltip("Material to pulse.")]
    public Material target;
    [Tooltip("Pulse size as a fraction of normal strength (0.5 = +/-50%).")]
    [Range(0f, 1f)] public float amount = 0.5f;
    [Tooltip("Seconds per pulse.")]
    public float period = 5f;

    Material runtimeCopy;
    Color baseEmission;
    float nextSwap;

    void Awake()
    {
        if (target == null) { enabled = false; return; }

        runtimeCopy = new Material(target) { name = target.name + " (pulse)" };
        baseEmission = target.HasProperty("_EmissionColor") ? target.GetColor("_EmissionColor") : Color.white;
    }

    void Update()
    {
        if (runtimeCopy == null) return;

        // Rescan every couple of seconds for walls built after the scene starts.
        if (Time.time >= nextSwap)
        {
            nextSwap = Time.time + 2f;
            Swap();
        }

        float breath = 1f + amount * Mathf.Sin(Time.time * 6.2832f / Mathf.Max(0.1f, period));
        runtimeCopy.SetColor("_EmissionColor", baseEmission * breath);
    }

    void Swap()
    {
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != target) continue;
                mats[i] = runtimeCopy;
                changed = true;
            }

            if (changed) r.sharedMaterials = mats;
        }
    }

    void OnDestroy()
    {
        if (runtimeCopy != null) Destroy(runtimeCopy);
    }
}
