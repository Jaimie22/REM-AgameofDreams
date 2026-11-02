using UnityEngine;

// The player's sounds: footsteps, jumping and picking things up.
// What it plays comes from the current level's LevelTheme, so glass sounds like glass
// on level 1 and sand sounds like sand on level 2.
// PlayerController adds this itself, so no setup is needed on the player.
[RequireComponent(typeof(CharacterController))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Footsteps")]
    [Tooltip("Distance walked between footsteps, in units. Smaller = quicker steps.")]
    public float strideLength = 0.6f;
    [Tooltip("Slowest speed that still makes footsteps, so nudging against a wall stays quiet.")]
    public float minimumSpeed = 0.6f;
    [Tooltip("Random change in pitch, so each step doesn't sound identical.")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.08f;
    [Range(0f, 0.5f)] public float volumeVariation = 0.15f;

    [Header("Landing")]
    [Tooltip("Seconds in the air before landing makes a sound. Stops small bumps being heard.")]
    public float landingAirTime = 0.15f;
    [Range(0f, 2f)] public float landingVolume = 1.3f;

    [Header("Ambient")]
    [Tooltip("Seconds the background loop takes to fade in when a level starts.")]
    public float ambientFadeTime = 1.5f;

    [Header("Overall")]
    [Range(0f, 1f)] public float masterVolume = 0.8f;

    CharacterController controller;
    AudioSource source;
    AudioSource ambient;
    float ambientTarget;
    AudioSource flow;
    float flowTarget;
    int lastDig = -1;
    LevelTheme theme;

    Vector3 lastPosition;
    float distanceWalked;
    float airTime;
    int lastStep = -1;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        MakeSources();

        lastPosition = transform.position;
    }

    // Safe to call more than once, as a level can set the surface before the player is first shown
    void MakeSources()
    {
        if (source != null) return;

        // A flat sound, as the camera is far from the player and it should be heard evenly
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        // The background loop has its own source, so it carries on under the other sounds
        ambient = gameObject.AddComponent<AudioSource>();
        ambient.playOnAwake = false;
        ambient.spatialBlend = 0f;
        ambient.loop = true;
        ambient.volume = 0f;

        // The sound of running water, started and stopped by the dig puzzle
        flow = gameObject.AddComponent<AudioSource>();
        flow.playOnAwake = false;
        flow.spatialBlend = 0f;
        flow.loop = true;
        flow.volume = 0f;
    }

    // The loop restarts whenever the player is shown again, such as coming back from the map
    void OnEnable()
    {
        if (ambient != null && ambient.clip != null && !ambient.isPlaying) ambient.Play();

        // Water that was running when the player was hidden isn't still running
        flowTarget = 0f;
    }

    // Called on entering a level, so the sounds match the surface
    public void SetSurface(LevelTheme newTheme)
    {
        MakeSources();

        theme = newTheme;
        distanceWalked = 0f;
        lastPosition = transform.position;

        // Swap the background loop if this level has a different one
        AudioClip wanted = theme != null ? theme.ambientClip : null;

        if (ambient.clip != wanted)
        {
            ambient.clip = wanted;
            ambient.volume = 0f;

            if (wanted != null && isActiveAndEnabled) ambient.Play();
            else ambient.Stop();
        }

        ambientTarget = wanted != null ? theme.ambientVolume * masterVolume : 0f;
    }

    void Update()
    {
        FadeLoops();

        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;

        // A jump to another spot, such as a respawn, isn't walking
        if (moved.magnitude > 2f) return;

        bool grounded = controller.isGrounded;

        if (!grounded)
        {
            airTime += Time.deltaTime;
            return;
        }

        // Back on the ground after a proper jump or fall
        if (airTime >= landingAirTime) PlayFootstep(landingVolume);
        airTime = 0f;

        // Count only the distance along the ground
        moved.y = 0f;
        float speed = moved.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

        if (speed < minimumSpeed)
        {
            // Standing still, so the next step starts a fresh stride
            distanceWalked = strideLength * 0.5f;
            return;
        }

        distanceWalked += moved.magnitude;

        if (distanceWalked >= strideLength)
        {
            distanceWalked = 0f;
            PlayFootstep(1f);
        }
    }

    // Fades the background loop and the water towards their levels, so nothing starts or stops with a jolt
    void FadeLoops()
    {
        float step = Time.unscaledDeltaTime / Mathf.Max(0.1f, ambientFadeTime);
        ambient.volume = Mathf.MoveTowards(ambient.volume, ambientTarget, step * Mathf.Max(ambientTarget, 0.1f));

        if (ambientTarget <= 0f && ambient.volume <= 0f && ambient.isPlaying) ambient.Stop();

        // The water fades a little quicker, as it follows what's happening on screen
        float flowStep = Time.unscaledDeltaTime / 0.8f;
        flow.volume = Mathf.MoveTowards(flow.volume, flowTarget, flowStep * Mathf.Max(flowTarget, 0.1f));

        if (flowTarget <= 0f && flow.volume <= 0f && flow.isPlaying) flow.Stop();
    }

    // --- Sounds -----------------------------------------------------

    void PlayFootstep(float volume)
    {
        if (theme == null || theme.footstepClips == null || theme.footstepClips.Length == 0) return;

        AudioClip[] clips = theme.footstepClips;
        int pick = Random.Range(0, clips.Length);

        // Never the same clip twice in a row
        if (clips.Length > 1 && pick == lastStep) pick = (pick + 1) % clips.Length;
        lastStep = pick;

        Play(clips[pick], volume * theme.footstepVolume, true);
    }

    public void PlayJump()
    {
        if (theme == null) return;
        Play(theme.jumpClip, 1f, true);
    }

    public void PlayPickup()
    {
        if (theme == null) return;
        Play(theme.pickupClip, 1f, false);
    }

    public void PlayDoor()
    {
        if (theme == null) return;
        Play(theme.doorClip, 1f, false);
    }

    // When the hunter catches the player
    public void PlayDeath()
    {
        if (theme == null) return;
        Play(theme.deathClip, 1f, false);
    }

    // The spade going into the ground
    public void PlayDig()
    {
        if (theme == null || theme.digClips == null || theme.digClips.Length == 0) return;

        int pick = Random.Range(0, theme.digClips.Length);

        // Never the same clip twice in a row
        if (theme.digClips.Length > 1 && pick == lastDig) pick = (pick + 1) % theme.digClips.Length;
        lastDig = pick;

        Play(theme.digClips[pick], 1f, true);
    }

    // Starts the sound of water running, which carries on until it's stopped
    public void StartWaterFlow()
    {
        if (theme == null || theme.waterFlowClip == null) return;

        flow.clip = theme.waterFlowClip;
        flowTarget = theme.waterFlowVolume * masterVolume;

        if (!flow.isPlaying && isActiveAndEnabled) flow.Play();
    }

    public void StopWaterFlow()
    {
        flowTarget = 0f;
    }

    // For puzzles to call when the spade goes in
    public static void PlayDigSound()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null) return;

        world.player.Audio.PlayDig();
    }

    public static void StartWaterFlowSound()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null) return;

        world.player.Audio.StartWaterFlow();
    }

    public static void StopWaterFlowSound()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null) return;

        world.player.Audio.StopWaterFlow();
    }

    // For puzzles to call when their door of light appears
    public static void PlayDoorSound()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null) return;

        world.player.Audio.PlayDoor();
    }

    void Play(AudioClip clip, float volume, bool varyPitch)
    {
        if (clip == null) return;

        source.pitch = varyPitch ? 1f + Random.Range(-pitchVariation, pitchVariation) : 1f;

        float variation = 1f + Random.Range(-volumeVariation, volumeVariation);
        source.PlayOneShot(clip, Mathf.Clamp01(volume * variation * masterVolume));
    }
}
