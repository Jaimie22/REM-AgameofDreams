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

    [Header("Panels")]
    [Tooltip("Optional. Shown when Settings is chosen.")]
    public GameObject settingsPanel;
    [Tooltip("Optional. Shown when Credits is chosen.")]
    public GameObject creditsPanel;

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

        // Does a save exist? If not, Load is shown greyed out
        bool hasSave = PlayerPrefs.HasKey("SaveExists");

        options = new Text[5];
        options[0] = MakeOption(canvasObj.transform, "New Game", 0, NewGame, true);
        options[1] = MakeOption(canvasObj.transform, "Load Game", 1, LoadGame, hasSave);
        options[2] = MakeOption(canvasObj.transform, "Settings", 2, OpenSettings, true);
        options[3] = MakeOption(canvasObj.transform, "Credits", 3, OpenCredits, true);
        options[4] = MakeOption(canvasObj.transform, "Exit", 4, Quit, true);
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
        SceneManager.LoadScene(newGameScene);
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

    void OpenCredits()
    {
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
