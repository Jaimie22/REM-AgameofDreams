using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Shows the credits over the menu, using the same text as the ending.
public class CreditsPanel : MonoBehaviour
{
    [Header("Where the words come from")]
    [Tooltip("The ending sequence, so both use the same credits. Leave empty to type your own below.")]
    public AwakeningSequence endingSequence;

    [TextArea(8, 20)]
    [Tooltip("Used only when no ending sequence is assigned.")]
    public string ownText = "";

    [Header("Look")]
    public Font font;
    public int fontSize = 26;
    public Color textColor = new Color(0.95f, 0.93f, 0.88f);
    [Tooltip("How dark the panel behind the words is.")]
    [Range(0f, 1f)] public float backgroundAlpha = 0.85f;
    public float fadeTime = 0.6f;

    [Header("Scrolling")]
    [Tooltip("Let the credits climb the screen by themselves.")]
    public bool scrollable = true;
    [Tooltip("Seconds for the credits to climb from bottom to top.")]
    public float scrollDuration = 20f;
    [Tooltip("How far below the screen they start, in pixels.")]
    public float startBelow = 1300f;
    [Tooltip("How far they climb in total. Longer credits need more.")]
    public float scrollDistance = 2000f;

    [Header("Back")]
    [Tooltip("The menu to return to. Leave empty to find it.")]
    public MainMenu mainMenu;

    CanvasGroup group;
    Text credits;
    RectTransform textRect;
    bool built;

    public void Show()
    {
        if (!built) Build();

        gameObject.SetActive(true);
        StopAllCoroutines();

        // Back to the starting position, so a second viewing isn't half scrolled
        if (scrollable)
            textRect.anchoredPosition = new Vector2(0f, -startBelow);
        else
            textRect.anchoredPosition = Vector2.zero;

        StartCoroutine(Fade(0f, 1f));

        // This was the missing piece: the scroll was written but never started
        if (scrollable) StartCoroutine(Scroll());
    }

    // Hide the credits and return to the menu.
    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(HideThen());
    }

    // Fade out, then return to the menu.
    IEnumerator HideThen()
    {
        yield return Fade(1f, 0f);

        group.interactable = false;
        group.blocksRaycasts = false;

        if (mainMenu == null) mainMenu = FindFirstObjectByType<MainMenu>();
        if (mainMenu != null) mainMenu.CloseCredits();
    }

    // Climbs the credits up the screen, from below to above
    IEnumerator Scroll()
    {
        if (scrollDuration <= 0f) yield break;

        float from = -startBelow;
        float to = from + scrollDistance;

        for (float t = 0f; t < 1f; t += Time.deltaTime / scrollDuration)
        {
            textRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, t));
            yield return null;
        }

        textRect.anchoredPosition = new Vector2(0f, to);
    }

    // Fade the credits in or out, over the given time.
    IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, fadeTime))
        {
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        group.alpha = to;
        group.interactable = to > 0.5f;
        group.blocksRaycasts = to > 0.5f;
    }

    // Build the credits panel, if it hasn't been built yet.
    void Build()
    {
        built = true;

        GameObject canvasObj = new GameObject("CreditsCanvas");
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;   // over the menu

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.AddComponent<GraphicRaycaster>();

        group = canvasObj.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // A dark panel, so the room doesn't fight the words
        GameObject backObj = new GameObject("Panel");
        backObj.transform.SetParent(canvasObj.transform, false);

        Image panel = backObj.AddComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, backgroundAlpha);
        Stretch(panel.rectTransform);

        // The credits themselves, in a tall box that slides upward
        GameObject textObj = new GameObject("Credits");
        textObj.transform.SetParent(canvasObj.transform, false);

        credits = textObj.AddComponent<Text>();
        credits.text = endingSequence != null ? endingSequence.creditsText : ownText;

        credits.font = font != null
            ? font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        credits.fontSize = fontSize;
        credits.alignment = TextAnchor.UpperCenter;
        credits.color = textColor;
        credits.lineSpacing = 1.3f;
        credits.raycastTarget = false;
        credits.horizontalOverflow = HorizontalWrapMode.Wrap;
        credits.verticalOverflow = VerticalWrapMode.Overflow;

        // Anchored to the bottom, so it can be slid up past the top of the screen
        textRect = credits.rectTransform;
        textRect.anchorMin = new Vector2(0.15f, 0f);
        textRect.anchorMax = new Vector2(0.85f, 0f);
        textRect.pivot = new Vector2(0.5f, 0f);
        textRect.sizeDelta = new Vector2(0f, 2400f);
        textRect.anchoredPosition = new Vector2(0f, -startBelow);

        BuildBackButton(canvasObj.transform);
    }

    // Build a button that returns to the menu.
    void BuildBackButton(Transform parent)
    {
        GameObject obj = new GameObject("Back");
        obj.transform.SetParent(parent, false);

        Text text = obj.AddComponent<Text>();
        text.text = "Back";
        text.font = credits.font;
        text.fontSize = Mathf.RoundToInt(fontSize * 0.9f);
        text.alignment = TextAnchor.MiddleRight;
        text.color = textColor;

        Button button = obj.AddComponent<Button>();
        button.targetGraphic = text;
        button.onClick.AddListener(Hide);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.85f, 0.55f);
        button.colors = colors;

        // Bottom right, clear of the credits as they climb past
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(220f, 70f);
        rect.anchoredPosition = new Vector2(-60f, 50f);
    }

    // Stretch the given RectTransform to fill its parent.
    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}