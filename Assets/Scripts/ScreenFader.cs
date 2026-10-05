using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Fades the screen to and from a colour (black by default).
// Creates its own full-screen overlay, so no UI setup is needed.
public class ScreenFader : MonoBehaviour
{
    [Tooltip("The colour the screen fades from/to.")]
    public Color fadeColor = Color.black;

    Image image;

    void Awake()
    {
        // Full-screen canvas drawn on top of everything
        GameObject canvasObj = new GameObject("ScreenFaderCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        // A plain image stretched to fill the screen
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        image = imageObj.AddComponent<Image>();
        image.raycastTarget = false; // never blocks mouse clicks

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        SetAlpha(0f);
    }

    // 0 = fully clear, 1 = fully covered
    public void SetAlpha(float alpha)
    {
        Color c = fadeColor;
        c.a = alpha;
        image.color = c;
    }

    // Smoothly fades between two alpha values
    public IEnumerator Fade(float from, float to, float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / d)
        {
            SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        SetAlpha(to);
    }
}
