using UnityEngine;

// The hiss that goes with a steam vent's puff. It keeps time with the particles,
// and gets quieter the further the player is from the vent.
public class GearVent : MonoBehaviour
{
    [Tooltip("How far away the puff can be heard, in units.")]
    public float hearingRange = 22f;
    [Range(0f, 0.3f)] public float pitchVariation = 0.08f;

    AudioClip clip;
    float volume;
    float interval;
    float timer;
    AudioSource source;

    // Called by GearAtmosphere with the same timing as the particle system
    public void SetUp(AudioClip puffClip, float puffVolume, float puffInterval, float startDelay)
    {
        clip = puffClip;
        volume = puffVolume;
        interval = Mathf.Max(0.5f, puffInterval);
        timer = startDelay;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    void Update()
    {
        if (clip == null) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer += interval;

        if (!SoundSpace.IsOnActiveFace(transform)) return;

        float loudness = SoundSpace.Loudness(transform.position, hearingRange);
        if (loudness <= 0.01f) return;

        source.panStereo = SoundSpace.Pan(transform.position);
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, Mathf.Clamp01(loudness * volume));
    }
}
