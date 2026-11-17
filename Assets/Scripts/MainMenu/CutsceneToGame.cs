using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fades the cutscene to black near its end, then opens the game.
// Put this on the Cutscene object, next to the Playable Director.
public class CutsceneToGame : MonoBehaviour
{
    [Header("The Cutscene")]
    [Tooltip("The director to watch. Leave empty to find one here.")]
    public PlayableDirector director;

    [Header("Timing")]
    [Tooltip("The frame the fade begins on.")]
    public int fadeStartFrame = 1680;
    [Tooltip("The timeline's frame rate. Unity's default is 60.")]
    public float frameRate = 60f;
    [Tooltip("Seconds the fade to black takes.")]
    public float fadeDuration = 2f;
    [Tooltip("Seconds to hold on black before the game loads.")]
    public float holdOnBlack = 1f;

    [Header("Afterwards")]
    public string nextScene = "GameWindow";

    Image blackout;
    bool started;

    void Start()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
        BuildBlackout();
    }

    void Update()
    {
        if (started || director == null) return;

        // The timeline reports its position in seconds, so convert the frame
        float fadeStartTime = fadeStartFrame / Mathf.Max(1f, frameRate);

        if (director.time < fadeStartTime) return;

        started = true;
        StartCoroutine(FadeAndLoad());
    }

    // Fade to black, hold, then load the next scene.
    IEnumerator FadeAndLoad()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, fadeDuration))
        {
            blackout.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        blackout.color = Color.black;

        yield return new WaitForSeconds(holdOnBlack);

        if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene);
    }

    // Build a fullscreen black image to fade in and out.
    void BuildBlackout()
    {
        GameObject canvasObj = new GameObject("CutsceneFadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // over everything, subtitles included

        GameObject obj = new GameObject("Blackout");
        obj.transform.SetParent(canvasObj.transform, false);

        blackout = obj.AddComponent<Image>();
        blackout.color = new Color(0f, 0f, 0f, 0f);
        blackout.raycastTarget = false;

        RectTransform rect = blackout.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
