using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

// The settings shown on the television. Saves what the player chooses,
// and applies it again next time the game starts.
public class GameSettings : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("The Audio Mixer holding the Music and SFX groups.")]
    public AudioMixer mixer;
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
        HookSlider(musicSlider, MusicKey, 0.7f, "MusicVolume");
        HookSlider(sfxSlider, SfxKey, 0.8f, "SFXVolume");
    }

    // Loads the saved value, applies it, and listens for changes
    void HookSlider(Slider slider, string saveKey, float fallback, string mixerParameter)
    {
        float saved = PlayerPrefs.GetFloat(saveKey, fallback);
        Apply(mixerParameter, saved);

        if (slider == null)
        {
            Debug.LogWarning("GameSettings: no slider assigned for " + mixerParameter);
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(saved);

        slider.onValueChanged.AddListener(value =>
        {
            Apply(mixerParameter, value);
            PlayerPrefs.SetFloat(saveKey, value);
            PlayerPrefs.Save();
        });
    }

    // Sliders run 0 to 1, but mixers work in decibels, which are logarithmic.
    // This makes the slider feel even across its whole range.
    void Apply(string parameter, float value)
    {
        if (mixer == null) return;

        float decibels = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
        mixer.SetFloat(parameter, decibels);
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