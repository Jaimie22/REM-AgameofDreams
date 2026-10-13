using UnityEngine;

// The heartbeat heard on tense levels. It speeds up and gets louder as
// tension rises. Generates its own sound, so no audio file is needed.
// Put this on DreamCube.
public class HeartbeatSystem : MonoBehaviour
{
    public static HeartbeatSystem Instance { get; private set; }

    [Header("Rate")]
    [Tooltip("Beats per minute when calm.")]
    public float restingBPM = 55f;
    [Tooltip("Beats per minute at full tension.")]
    public float maxBPM = 150f;

    [Header("Volume")]
    [Tooltip("How loud the heartbeat is when calm. 0 means silent until tension rises.")]
    [Range(0f, 1f)] public float restingVolume = 0.15f;
    [Range(0f, 1f)] public float maxVolume = 0.7f;

    [Header("Sound")]
    [Tooltip("Optional. Leave empty and a heartbeat is generated in code.")]
    public AudioClip heartbeatClip;
    [Tooltip("Pitch of the generated thump. Lower is heavier.")]
    public float thumpFrequency = 48f;

    [Header("Response")]
    [Tooltip("How quickly the heartbeat reacts to a change in tension.")]
    public float blendSpeed = 1.5f;

    public float Tension { get; private set; }   // smoothed, 0 to 1

    AudioSource source;
    float targetTension;
    float beatTimer;

    void Awake()
    {
        Instance = this;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // heard everywhere, it's inside his head

        if (heartbeatClip == null) heartbeatClip = BuildHeartbeatClip();
    }

    // Builds a lub-dub from two decaying low thumps
    AudioClip BuildHeartbeatClip()
    {
        const int rate = 44100;
        const float length = 0.55f;
        int samples = Mathf.RoundToInt(rate * length);

        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;

            // Two thumps: the "lub" at the start, the quieter "dub" just after
            data[i] = Thump(t, 0f, 1f) + Thump(t, 0.19f, 0.65f);
        }

        AudioClip clip = AudioClip.Create("Heartbeat", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // One low sine pulse that fades away quickly
    float Thump(float t, float startTime, float loudness)
    {
        float local = t - startTime;
        if (local < 0f) return 0f;

        float envelope = Mathf.Exp(-local * 22f);           // sharp decay
        float wave = Mathf.Sin(local * thumpFrequency * Mathf.PI * 2f);

        // A touch of the octave above gives it body rather than a pure tone
        wave += 0.3f * Mathf.Sin(local * thumpFrequency * 2f * Mathf.PI * 2f);

        return wave * envelope * loudness * 0.5f;
    }

    // Called by the demon director, or anything else that raises tension
    public void SetTension(float value)
    {
        targetTension = Mathf.Clamp01(value);
    }

    // Stops the heartbeat, for levels that shouldn't have one
    public void Silence()
    {
        targetTension = 0f;
        Tension = 0f;
    }

    void Update()
    {
        Tension = Mathf.Lerp(Tension, targetTension, blendSpeed * Time.deltaTime);
        if (Tension < 0.01f) return;

        float bpm = Mathf.Lerp(restingBPM, maxBPM, Tension);
        float interval = 60f / Mathf.Max(1f, bpm);

        beatTimer -= Time.deltaTime;
        if (beatTimer > 0f) return;

        beatTimer = interval;

        // Faster beats are also sharper and louder
        source.pitch = Mathf.Lerp(0.9f, 1.15f, Tension);
        source.PlayOneShot(heartbeatClip, Mathf.Lerp(restingVolume, maxVolume, Tension));
    }
}
