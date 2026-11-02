using UnityEngine;
using UnityEngine.UI;

// Shows how aware the hunter is of the player, as a green, amber or red light.
//   Green = unaware, amber = suspicious (following the scent), red = detected (chasing).
// Creates its own on-screen indicator, so no UI setup is needed.
// It only appears while playing a level that has a hunter.
public class DetectionIndicator : MonoBehaviour
{
    [Header("Colours")]
    public Color unawareColor = new Color(0.25f, 0.85f, 0.4f);
    public Color suspiciousColor = new Color(1f, 0.7f, 0.1f);
    public Color detectedColor = new Color(1f, 0.15f, 0.1f);

    [Header("Labels")]
    [Tooltip("Words under the light, so it still reads without the colour.")]
    public string unawareLabel = "UNAWARE";
    public string suspiciousLabel = "SUSPICIOUS";
    public string detectedLabel = "DETECTED";

    [Header("Layout")]
    [Tooltip("Width of the light in pixels.")]
    public float size = 36f;
    [Tooltip("Gap between the top of the screen and the light, in pixels.")]
    public float topMargin = 40f;
    public int labelFontSize = 16;

    [Header("Feedback")]
    [Tooltip("How quickly the light changes colour.")]
    public float colorSpeed = 8f;
    [Tooltip("How quickly the whole indicator fades in and out.")]
    public float fadeSpeed = 4f;
    [Tooltip("How fast the light throbs while the hunter has spotted you.")]
    public float pulseSpeed = 9f;
    [Range(0f, 0.5f)]
    public float pulseAmount = 0.2f;

    [Header("Searching")]
    [Tooltip("How often to look for hunters, in seconds. A new one appears when a level is rebuilt.")]
    public float searchInterval = 0.5f;

    CanvasGroup group;
    Image dot;
    Image ring;
    Text label;

    Hunter[] hunters = new Hunter[0];
    float nextSearch;
    Color shownColor;

    void Awake()
    {
        BuildUI();
        shownColor = unawareColor;
        group.alpha = 0f;
    }

    void Update()
    {
        // Hunters come and go when a level is rebuilt, so look again every so often
        if (Time.unscaledTime >= nextSearch)
        {
            hunters = FindObjectsByType<Hunter>(FindObjectsSortMode.None);
            nextSearch = Time.unscaledTime + Mathf.Max(0.1f, searchInterval);
        }

        Hunter active = FindMostAwareHunter();

        // Fade in while there's a hunter to worry about, out when there isn't
        float targetAlpha = active != null ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);

        if (active == null) return;

        Color targetColor;
        string words;
        GetLook(active.CurrentState, out targetColor, out words);

        shownColor = Color.Lerp(shownColor, targetColor, colorSpeed * Time.unscaledDeltaTime);
        label.text = words;

        dot.color = shownColor;
        label.color = shownColor;

        Color ringColor = shownColor;
        ringColor.a = 0.55f;
        ring.color = ringColor;

        // The light throbs once it has been spotted, so it can't be missed
        float pulse = 1f;
        if (active.CurrentState == Hunter.State.Chasing)
            pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;

        dot.rectTransform.localScale = Vector3.one * pulse;
    }

    // Whichever hunter on the current level is the most alert, as red beats amber beats green
    Hunter FindMostAwareHunter()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.CurrentMode != CubeWorld.Mode.Playing) return null;

        Hunter best = null;

        foreach (Hunter hunter in hunters)
        {
            if (hunter == null) continue;
            if (hunter.faceIndex != world.CurrentFaceIndex) continue;

            if (best == null || hunter.CurrentState > best.CurrentState) best = hunter;
        }

        return best;
    }

    void GetLook(Hunter.State state, out Color color, out string words)
    {
        switch (state)
        {
            case Hunter.State.Chasing:
                color = detectedColor;
                words = detectedLabel;
                break;

            case Hunter.State.Tracking:
                color = suspiciousColor;
                words = suspiciousLabel;
                break;

            default:
                color = unawareColor;
                words = unawareLabel;
                break;
        }
    }

    // --- Building the UI ------------------------------------------------

    void BuildUI()
    {
        // Canvas drawn just under the screen fader, so fades still cover it
        GameObject canvasObj = new GameObject("DetectionIndicatorCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        // The whole indicator sits at the top centre of the screen
        GameObject rootObj = new GameObject("DetectionIndicator", typeof(RectTransform));
        rootObj.transform.SetParent(canvasObj.transform, false);
        group = rootObj.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; // never blocks mouse clicks
        group.interactable = false;

        float ringSize = size * 1.5f;

        RectTransform root = rootObj.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -topMargin);
        root.sizeDelta = new Vector2(240f, ringSize + labelFontSize + 12f);

        // A soft ring around the light, then the light itself
        ring = MakeImage("Ring", root, MakeCircle(128, 6f), ringSize, ringSize * 0.5f);
        dot = MakeImage("Dot", root, MakeCircle(128, 0f), size, ringSize * 0.5f);

        // The words under the light
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(root, false);
        label = labelObj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = labelFontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.UpperCenter;
        label.raycastTarget = false;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -(ringSize + 4f));
        labelRect.sizeDelta = new Vector2(240f, labelFontSize + 8f);
    }

    // A square image of a set size, centred this far down from the top of the indicator
    Image MakeImage(string name, RectTransform parent, Sprite sprite, float pixels, float centreDown)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -centreDown);
        rect.sizeDelta = new Vector2(pixels, pixels);

        return image;
    }

    // Draws a white circle in code, so there's no image to import.
    // A ring thickness of 0 gives a filled circle.
    Sprite MakeCircle(int resolution, float ringThickness)
    {
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = resolution * 0.5f;
        Vector2 centre = new Vector2(radius - 0.5f, radius - 0.5f);
        Color[] pixels = new Color[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), centre);

                // One pixel of softness on the outer edge
                float alpha = Mathf.Clamp01(radius - distance);

                // For a ring, also cut out the middle
                if (ringThickness > 0f)
                    alpha = Mathf.Min(alpha, Mathf.Clamp01(distance - (radius - ringThickness)));

                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f));
    }
}
