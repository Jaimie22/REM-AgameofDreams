using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

// The main menu, fading in over the bedroom after a beat.
// Put this on an empty GameObject in the menu scene.
public class MainMenu : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds before the menu appears, so the room is seen first.")]
    public float delayBeforeShowing = 2.5f;
    [Tooltip("Seconds the menu takes to fade in.")]
    public float fadeInTime = 1.5f;

    [Header("Layout")]
    [Tooltip("How far in from the left edge, as a fraction of the screen.")]
    [Range(0f, 0.4f)] public float leftMargin = 0.08f;
    [Tooltip("Where the block of buttons sits vertically. 0.5 is centred.")]
    [Range(0f, 1f)] public float verticalPosition = 0.45f;
    public float buttonSpacing = 56f;
    public int fontSize = 30;

    [Header("Logo")]
    [Tooltip("The game's title, shown above the options. Leave empty for none.")]
    public Sprite logo;
    [Tooltip("How wide it is, in reference pixels.")]
    public float logoWidth = 700f;
    [Tooltip("Where it sits. 0.5 is centre, higher is nearer the top.")]
    [Range(0f, 1f)] public float logoHeight = 0.78f;
    [Tooltip("How far in from the left. Match the options to line them up.")]
    [Range(0f, 0.5f)] public float logoLeft = 0.08f;

    [Header("Look")]
    public Font menuFont;
    public Color textColor = new Color(0.92f, 0.9f, 0.85f);
    [Tooltip("Colour when the mouse is over an option.")]
    public Color hoverColor = new Color(1f, 0.85f, 0.55f);

    [Header("Camera")]
    [Tooltip("The camera that glides between viewpoints. Leave empty to find it.")]
    public MenuCamera menuCamera;

    [Header("Scenes")]
    [Tooltip("The scene that starts a new game.")]
    public string newGameScene = "GameWindow";

    [Header("Opening")]
    [Tooltip("The cutscene that plays when a new game starts.")]
    public UnityEngine.Playables.PlayableDirector OpeningCutscene;

    [Header("Opening Fade")]
    [Tooltip("Seconds to fade to black before the cutscene.")]
    public float fadeToBlackTime = 1f;
    [Tooltip("Seconds to fade back in once the cutscene is running.")]
    public float fadeFromBlackTime = 1.5f;

    Image openingFade;

    [Header("Panels")]
    [Tooltip("Optional. Shown when Settings is chosen.")]
    public GameObject settingsPanel;

    [Tooltip("Optional. Shown when Credits is chosen.")]
    public GameObject creditsPanel;

    [Tooltip("The credits shown over the menu.")]
    public CreditsPanel creditsPanelScript;

    CanvasGroup group;
    Text[] options;

    void Start()
    {
        BuildMenu();
        StartCoroutine(FadeIn());
    }

    void BuildMenu()
    {
        GameObject canvasObj = new GameObject("MenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.AddComponent<GraphicRaycaster>();

        group = canvasObj.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // An EventSystem is needed for the buttons to respond at all
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject events = new GameObject("EventSystem");
            events.AddComponent<UnityEngine.EventSystems.EventSystem>();
            events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // The title sits above the options, and fades in with them
        BuildLogo(canvasObj.transform);

        // Does a save exist? If not, Load is shown greyed out
        bool hasSave = PlayerPrefs.HasKey("SaveExists");

        options = new Text[5];
        options[0] = MakeOption(canvasObj.transform, "New Game", 0, NewGame, true);
        options[1] = MakeOption(canvasObj.transform, "Load Game", 1, LoadGame, hasSave);
        options[2] = MakeOption(canvasObj.transform, "Settings", 2, OpenSettings, true);
        options[3] = MakeOption(canvasObj.transform, "Credits", 3, OpenCredits, true);
        options[4] = MakeOption(canvasObj.transform, "Exit", 4, Quit, true);
    }

    // The game's title above the options
    void BuildLogo(Transform parent)
    {
        if (logo == null) return;

        GameObject obj = new GameObject("Logo");
        obj.transform.SetParent(parent, false);

        Image image = obj.AddComponent<Image>();
        image.sprite = logo;
        image.preserveAspect = true;
        image.raycastTarget = false;

        // Sized by width, with the height worked out from the artwork itself
        float ratio = logo.rect.height / logo.rect.width;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(logoLeft, logoHeight);
        rect.anchorMax = new Vector2(logoLeft, logoHeight);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(logoWidth, logoWidth * ratio);
        rect.anchoredPosition = Vector2.zero;
    }

    // One menu line: plain text that lights up when hovered
    Text MakeOption(Transform parent, string label, int index,
                    UnityEngine.Events.UnityAction action, bool enabled)
    {
        GameObject obj = new GameObject(label);
        obj.transform.SetParent(parent, false);

        Text text = obj.AddComponent<Text>();
        text.text = label;
        text.font = menuFont != null
            ? menuFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = enabled ? textColor : textColor * 0.4f;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // Down the left, stacked from the vertical position
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(leftMargin, verticalPosition);
        rect.anchorMax = new Vector2(leftMargin, verticalPosition);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(400f, buttonSpacing);
        rect.anchoredPosition = new Vector2(0f, -index * buttonSpacing);

        if (!enabled) return text;

        // The clickable part, with no graphic of its own
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = text;
        button.onClick.AddListener(action);

        // Light up on hover rather than the default tint
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = hoverColor;
        colors.pressedColor = hoverColor * 0.8f;
        colors.fadeDuration = 0.15f;
        button.colors = colors;

        // The little noises the menu makes
        UnityEngine.EventSystems.EventTrigger trigger =
            obj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        UnityEngine.EventSystems.EventTrigger.Entry enter =
            new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };

        enter.callback.AddListener(_ => MenuAudio.Instance?.PlayHover());
        trigger.triggers.Add(enter);

        button.onClick.AddListener(() => MenuAudio.Instance?.PlayPress());

        return text;
    }

    IEnumerator FadeIn()
    {
        yield return new WaitForSeconds(delayBeforeShowing);

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, fadeInTime))
        {
            group.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    void NewGame()
    {
        Debug.Log("Starting a new dream.");
        StartCoroutine(PlayOpening());
    }

    // Fades the menu away, then runs the cutscene
    IEnumerator PlayOpening()
    {
        group.interactable = false;
        group.blocksRaycasts = false;

        // The menu goes first
        for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.6f)
        {
            group.alpha = t;
            yield return null;
        }

        group.alpha = 0f;

        // The menu music fades away as the room goes dark
        MenuAudio.Instance?.FadeOut(fadeToBlackTime);

        // Then the room fades to black, so the cut to the cutscene isn't seen
        BuildOpeningFade();

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, fadeToBlackTime))
        {
            openingFade.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        openingFade.color = Color.black;

        // Hand the camera over to Cinemachine while the screen is dark
        if (menuCamera == null) menuCamera = FindFirstObjectByType<MenuCamera>();

        if (menuCamera != null)
        {
            menuCamera.enabled = false;
            if (menuCamera.brain != null) menuCamera.brain.enabled = true;
        }

        if (OpeningCutscene == null)
        {
            SceneManager.LoadScene(newGameScene);
            yield break;
        }

        OpeningCutscene.Play();

        // Give it a frame to take hold of the camera before revealing it
        yield return null;
        yield return null;

        // And fade back in on the first shot
        for (float t = 1f; t > 0f; t -= Time.deltaTime / Mathf.Max(0.1f, fadeFromBlackTime))
        {
            openingFade.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        openingFade.color = new Color(0f, 0f, 0f, 0f);
    }

    // Called by the credits panel's Back button
    public void CloseCredits()
    {
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    // A full-screen black overlay, built when it's needed
    void BuildOpeningFade()
    {
        if (openingFade != null) return;

        GameObject canvasObj = new GameObject("OpeningFadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // over everything, including the menu

        GameObject obj = new GameObject("Fade");
        obj.transform.SetParent(canvasObj.transform, false);

        openingFade = obj.AddComponent<Image>();
        openingFade.color = new Color(0f, 0f, 0f, 0f);
        openingFade.raycastTarget = false;

        RectTransform rect = openingFade.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void LoadGame()
    {
        Debug.Log("Loading the saved dream.");
        SceneManager.LoadScene(newGameScene);
    }

    void OpenSettings()
    {
        if (menuCamera == null) menuCamera = FindFirstObjectByType<MenuCamera>();

        if (menuCamera != null)
        {
            StartCoroutine(ToSettings());
            return;
        }

        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    // Glides to the television, then brings the settings up on its screen
    IEnumerator ToSettings()
    {
        // Hide the menu while the camera moves
        group.interactable = false;
        group.blocksRaycasts = false;

        float fade = 0.5f;
        for (float t = 1f; t > 0f; t -= Time.deltaTime / fade)
        {
            group.alpha = t;
            yield return null;
        }

        group.alpha = 0f;
        menuCamera.GoToSettings();

        while (menuCamera.IsMoving) yield return null;

        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    // Called by the Back button on the settings screen
    public void CloseSettings()
    {
        StartCoroutine(FromSettings());
    }

    IEnumerator FromSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (menuCamera != null)
        {
            menuCamera.GoToMenu();
            while (menuCamera.IsMoving) yield return null;
        }

        // Bring the menu back
        float fade = 0.5f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / fade)
        {
            group.alpha = t;
            yield return null;
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    // Called by the Credits button on the menu
    void OpenCredits()
    {
        if (creditsPanelScript == null)
            creditsPanelScript = FindFirstObjectByType<CreditsPanel>();

        if (creditsPanelScript != null)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
            creditsPanelScript.Show();
            return;
        }

        if (creditsPanel != null) creditsPanel.SetActive(true);
        else Debug.Log("No credits panel assigned yet.");
    }

    void Quit()
    {
        Debug.Log("Waking up.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}