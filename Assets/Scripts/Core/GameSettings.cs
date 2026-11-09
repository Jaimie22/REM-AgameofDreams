using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// The settings shown on the television. Saves what the player chooses,
// and applies it again next time the game starts.
public class GameSettings : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Optional. An Audio Mixer with exposed Master, Music and SFX volume parameters.")]
    public AudioMixer mixer;
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Display")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Toggle vsyncToggle;

    [Header("Back")]
    public Button backButton;
    [Tooltip("The menu to return to. Leave empty to find it.")]
    public MainMenu mainMenu;

    const string MasterKey = "Volume_Master";
    const string MusicKey = "Volume_Music";
    const string SfxKey = "Volume_SFX";
    const string FullscreenKey = "Display_Fullscreen";
    const string VsyncKey = "Display_VSync";
    const string ResolutionKey = "Display_Resolution";

    Resolution[] resolutions;

    void Start()
    {
        if (mainMenu == null) mainMenu = FindFirstObjectByType<MainMenu>();

        SetUpAudio();
        SetUpResolutions();
        SetUpToggles();

        if (backButton != null) backButton.onClick.AddListener(Back);
    }

    // --- Audio ------------------------------------------------------

    void SetUpAudio()
    {
        HookSlider(masterSlider, MasterKey, 0.8f, SetMasterVolume);
        HookSlider(musicSlider, MusicKey, 0.7f, SetMusicVolume);
        HookSlider(sfxSlider, SfxKey, 0.8f, SetSfxVolume);
    }

    // Loads the saved value, applies it, and listens for changes
    void HookSlider(Slider slider, string key, float fallback,
                    UnityEngine.Events.UnityAction<float> apply)
    {
        float saved = PlayerPrefs.GetFloat(key, fallback);
        apply(saved);

        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(saved);

        slider.onValueChanged.AddListener(value =>
        {
            apply(value);
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        });
    }

    public void SetMasterVolume(float value)
    {
        // Without a mixer, fall back to Unity's global volume
        if (mixer == null)
        {
            AudioListener.volume = value;
            return;
        }

        mixer.SetFloat("MasterVolume", ToDecibels(value));
    }

    public void SetMusicVolume(float value)
    {
        if (mixer != null) mixer.SetFloat("MusicVolume", ToDecibels(value));
    }

    public void SetSfxVolume(float value)
    {
        if (mixer != null) mixer.SetFloat("SFXVolume", ToDecibels(value));
    }

    // Sliders run 0 to 1, but mixers work in decibels, which are logarithmic.
    // This makes the slider feel even across its whole range.
    float ToDecibels(float value)
    {
        if (value <= 0.0001f) return -80f;      // silent
        return Mathf.Log10(value) * 20f;
    }

    // --- Resolution -------------------------------------------------

    void SetUpResolutions()
    {
        if (resolutionDropdown == null) return;

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        System.Collections.Generic.List<string> names =
            new System.Collections.Generic.List<string>();

        int current = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            names.Add(resolutions[i].width + " x " + resolutions[i].height);

            // Remember which one matches the window right now
            if (resolutions[i].width == Screen.width &&
                resolutions[i].height == Screen.height)
                current = i;
        }

        resolutionDropdown.AddOptions(names);

        int saved = PlayerPrefs.GetInt(ResolutionKey, current);
        saved = Mathf.Clamp(saved, 0, resolutions.Length - 1);

        resolutionDropdown.SetValueWithoutNotify(saved);
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    public void SetResolution(int index)
    {
        if (resolutions == null || index < 0 || index >= resolutions.Length) return;

        Resolution chosen = resolutions[index];
        Screen.SetResolution(chosen.width, chosen.height, Screen.fullScreen);

        PlayerPrefs.SetInt(ResolutionKey, index);
        PlayerPrefs.Save();
    }

    // --- Toggles ----------------------------------------------------

    void SetUpToggles()
    {
        if (fullscreenToggle != null)
        {
            bool saved = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
            Screen.fullScreen = saved;

            fullscreenToggle.SetIsOnWithoutNotify(saved);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        if (vsyncToggle != null)
        {
            bool saved = PlayerPrefs.GetInt(VsyncKey, 1) == 1;
            QualitySettings.vSyncCount = saved ? 1 : 0;

            vsyncToggle.SetIsOnWithoutNotify(saved);
            vsyncToggle.onValueChanged.AddListener(SetVSync);
        }
    }

    public void SetFullscreen(bool on)
    {
        Screen.fullScreen = on;
        PlayerPrefs.SetInt(FullscreenKey, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetVSync(bool on)
    {
        QualitySettings.vSyncCount = on ? 1 : 0;
        PlayerPrefs.SetInt(VsyncKey, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    // --- Back -------------------------------------------------------

    public void Back()
    {
        if (mainMenu != null) mainMenu.CloseSettings();
    }
}