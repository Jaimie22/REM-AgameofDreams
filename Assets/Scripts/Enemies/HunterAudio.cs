using UnityEngine;

// The hunter's sounds: the scrabble of it crawling, and the attack when it catches the player.
// Hunter adds this itself, so nothing needs setting up on the prefab.
// The clips come from the maze's LevelTheme.
[RequireComponent(typeof(Hunter))]
public class HunterAudio : MonoBehaviour
{
    [Header("Crawling")]
    [Tooltip("Distance crawled between sounds, in units.")]
    public float strideLength = 0.9f;
    [Tooltip("While chasing, the stride is shortened by this much, so it sounds faster.")]
    [Range(0.3f, 1f)] public float chaseStrideScale = 0.7f;
    [Tooltip("How far away it can be heard, in units.")]
    public float hearingRange = 24f;
    [Range(0f, 0.3f)] public float pitchVariation = 0.1f;

    [Header("Attack")]
    [Range(0f, 1.5f)] public float attackVolume = 1f;

    Hunter hunter;
    AudioSource source;

    Vector3 lastPosition;
    float distanceCrawled;
    int lastClip = -1;

    void Awake()
    {
        hunter = GetComponent<Hunter>();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // placed by hand, in SoundSpace

        lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;

        // Being put back at its start isn't crawling
        if (moved.magnitude > 3f) return;

        LevelTheme theme = hunter.Theme;
        if (theme == null || theme.crawlClips == null || theme.crawlClips.Length == 0) return;
        if (!SoundSpace.IsOnActiveFace(transform)) return;

        moved.y = 0f;
        distanceCrawled += moved.magnitude;

        float stride = strideLength;
        if (hunter.CurrentState == Hunter.State.Chasing) stride *= chaseStrideScale;

        if (distanceCrawled < stride) return;
        distanceCrawled = 0f;

        float loudness = SoundSpace.Loudness(transform.position, hearingRange);
        if (loudness <= 0.01f) return;

        AudioClip[] clips = theme.crawlClips;
        int pick = Random.Range(0, clips.Length);

        // Never the same clip twice in a row
        if (clips.Length > 1 && pick == lastClip) pick = (pick + 1) % clips.Length;
        lastClip = pick;

        source.panStereo = SoundSpace.Pan(transform.position);
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clips[pick], Mathf.Clamp01(loudness * theme.crawlVolume));
    }

    // The lunge when it catches the player
    public void PlayAttack()
    {
        LevelTheme theme = hunter.Theme;
        if (theme == null || theme.attackClip == null) return;

        source.panStereo = 0f;
        source.pitch = 1f;
        source.PlayOneShot(theme.attackClip, Mathf.Clamp01(attackVolume));
    }
}
