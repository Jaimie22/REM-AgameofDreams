using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// The sound of the bedroom: music over the top, rain and television
// in the room itself, and the little noises the menu makes.
// Put this on the same object as MainMenu.
public class MenuAudio : MonoBehaviour
{
    public static MenuAudio Instance { get; private set; }

    [Header("Mixer")]
    [Tooltip("Optional. Routes these through the mixer so the settings sliders reach them.")]
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup sfxGroup;

    [Header("Music")]
    public AudioClip menuMusic;
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    [Tooltip("Seconds the music takes to fade up when the menu opens.")]
    public float musicFadeIn = 3f;

    [Header("Interface")]
    [Tooltip("A soft click when an option is chosen.")]
    public AudioClip buttonPress;
    [Tooltip("A quieter sound when the mouse passes over an option.")]
    public AudioClip buttonHover;
    [Range(0f, 1f)] public float interfaceVolume = 0.5f;

    AudioSource music;
    AudioSource interfaceSounds;

    void Awake()
    {
        Instance = this;

        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        music.volume = 0f;
        music.outputAudioMixerGroup = musicGroup;

        interfaceSounds = gameObject.AddComponent<AudioSource>();
        interfaceSounds.playOnAwake = false;
        interfaceSounds.spatialBlend = 0f;
        interfaceSounds.volume = interfaceVolume;
        interfaceSounds.outputAudioMixerGroup = sfxGroup;
    }

    void Start()
    {
        if (menuMusic == null) return;

        music.clip = menuMusic;
        music.Play();
        StartCoroutine(FadeMusicIn());
    }

    IEnumerator FadeMusicIn()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, musicFadeIn))
        {
            music.volume = Mathf.Lerp(0f, musicVolume, t);
            yield return null;
        }

        music.volume = musicVolume;
    }

    // Called when a menu option is clicked
    public void PlayPress()
    {
        Debug.Log("Button press sound");
        if (buttonPress != null) interfaceSounds.PlayOneShot(buttonPress, interfaceVolume);
    }

    // Called when the mouse moves over an option
    public void PlayHover()
    {
        if (buttonHover != null)
            interfaceSounds.PlayOneShot(buttonHover, interfaceVolume * 0.5f);
    }

    // Fades the music away, for leaving the menu
    public void FadeOut(float seconds)
    {
        StartCoroutine(FadeMusicOut(seconds));
    }

    IEnumerator FadeMusicOut(float seconds)
    {
        float from = music.volume;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, seconds))
        {
            music.volume = Mathf.Lerp(from, 0f, t);
            yield return null;
        }

        music.volume = 0f;
        music.Stop();
    }
}
