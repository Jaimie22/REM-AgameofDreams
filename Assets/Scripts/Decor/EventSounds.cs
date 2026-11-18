using System.Collections.Generic;
using UnityEngine;

// Plays the generated one-shot sounds at the right moments without touching any of the team's scripts.
// It only watches the game: it never changes it.
//   Heart_Lose    when the player loses a heart
//   Demon_Alert   when a demon starts tracking/chasing, Demon_Growl while it keeps chasing
//   Spawn_Chime   when a level's entry animation finishes and play starts
//   Crystal_Shatter  when a level is finished and its glow outline appears
//   Beacon_Hum    quiet loop in the cube/map view, louder the more levels are finished
public class EventSounds : MonoBehaviour
{
    public AudioClip heartLose;
    public AudioClip demonAlert;
    public AudioClip demonGrowl;
    public AudioClip spawnChime;
    public AudioClip crystalShatter;
    public AudioClip beaconHum;
    [Tooltip("A light chitter for the Level 5 spiders, instead of the demon's growl.")]
    public AudioClip spiderAlert;

    [Range(0f, 1f)] public float volume = 0.8f;
    public float demonHearingRange = 14f;

    AudioSource oneShot, hum;
    PlayerVitals vitals;
    int lastHearts = -1;
    CubeWorld.Mode lastMode;
    bool[] wasCompleted;
    float nextScan, nextGrowl;

    readonly List<Hunter> hunters = new List<Hunter>();
    readonly List<OpenHunter> openHunters = new List<OpenHunter>();
    readonly Dictionary<Object, int> lastLevel = new Dictionary<Object, int>();

    void Awake()
    {
        oneShot = gameObject.AddComponent<AudioSource>();
        oneShot.spatialBlend = 0f;
        oneShot.playOnAwake = false;

        hum = gameObject.AddComponent<AudioSource>();
        hum.spatialBlend = 0f;
        hum.loop = true;
        hum.playOnAwake = false;
        hum.volume = 0f;
    }

    void Start()
    {
        if (beaconHum != null) { hum.clip = beaconHum; hum.Play(); }
    }

    void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 2f;
            if (vitals == null) vitals = FindFirstObjectByType<PlayerVitals>();
            hunters.Clear(); hunters.AddRange(FindObjectsByType<Hunter>(FindObjectsSortMode.None));
            openHunters.Clear(); openHunters.AddRange(FindObjectsByType<OpenHunter>(FindObjectsSortMode.None));
        }

        WatchHearts();
        WatchDemons();
        WatchWorld();
    }

    void Play(AudioClip clip, float scale = 1f)
    {
        if (clip != null) oneShot.PlayOneShot(clip, volume * scale);
    }

    void WatchHearts()
    {
        if (vitals == null) return;
        int h = vitals.Hearts;
        if (lastHearts >= 0 && h < lastHearts) Play(heartLose);
        lastHearts = h;
    }

    // 0 = wandering, 1 = tracking, 2 = chasing
    void WatchDemons()
    {
        foreach (Hunter h in hunters)
            if (h != null) Watch(h, CreatureMotion.AlertLevel(h, null), h.transform);
        foreach (OpenHunter h in openHunters)
            if (h != null) Watch(h, CreatureMotion.AlertLevel(null, h), h.transform);
    }

    void Watch(Object key, int level, Transform at)
    {
        if (key == null || at == null) return;

        lastLevel.TryGetValue(key, out int before);
        lastLevel[key] = level;

        float loud = SoundSpace.Loudness(at.position, demonHearingRange);
        if (loud <= 0.02f) return;

        // Spiders chitter softly; the demon growl is for the heavier enemies
        CreatureMotion motion = at.GetComponent<CreatureMotion>();
        bool spider = motion != null && motion.kind == CreatureMotion.Kind.Crawler;

        if (level > before && level >= 1) Play(spider ? spiderAlert : demonAlert, loud * (spider ? 0.7f : 1f));

        if (level == 2 && !spider && Time.time >= nextGrowl)
        {
            nextGrowl = Time.time + Random.Range(5f, 8f);
            Play(demonGrowl, loud * 0.8f);
        }
    }

    void WatchWorld()
    {
        CubeWorld w = CubeWorld.Instance;
        if (w == null || w.faces == null) return;

        if (wasCompleted == null || wasCompleted.Length != w.faces.Length)
        {
            wasCompleted = new bool[w.faces.Length];
            for (int i = 0; i < w.faces.Length; i++) wasCompleted[i] = w.faces[i] != null && w.faces[i].IsCompleted;
        }

        for (int i = 0; i < w.faces.Length; i++)
        {
            bool now = w.faces[i] != null && w.faces[i].IsCompleted;
            if (now && !wasCompleted[i]) Play(crystalShatter);
            wasCompleted[i] = now;
        }

        if (w.CurrentMode == CubeWorld.Mode.Playing && lastMode == CubeWorld.Mode.Transitioning) Play(spawnChime, 0.7f);
        lastMode = w.CurrentMode;

        // The beacon hum: only in the cube/map view, and only once something is finished
        int finished = 0;
        foreach (CubeFace f in w.faces) if (f != null && f.IsCompleted) finished++;
        bool showing = w.CurrentMode == CubeWorld.Mode.Overview || w.CurrentMode == CubeWorld.Mode.Map;
        float target = showing ? Mathf.Clamp01(finished / 3f) * 0.35f * volume : 0f;
        hum.volume = Mathf.MoveTowards(hum.volume, target, Time.unscaledDeltaTime * 0.5f);
    }
}
