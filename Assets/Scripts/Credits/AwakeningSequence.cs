using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The ending. The player falls into a blue gradient while the credits
// roll beside him, then everything fades and he wakes.
// Put this anywhere in the scene, for example on DreamCube.
public class AwakeningSequence : MonoBehaviour
{
    [Header("The Fall")]
    [Tooltip("Seconds of falling before the credits begin.")]
    public float fallBeforeCredits = 3f;
    [Tooltip("How fast he falls, in units per second.")]
    public float fallSpeed = 9f;
    [Tooltip("How fast he tumbles as he falls.")]
    public Vector3 tumble = new Vector3(25f, 10f, 0f);

    [Header("Framing")]
    [Tooltip("Where the camera sits relative to him. Z is how far in front, X slides him across the frame.")]
    public Vector3 cameraOffset = new Vector3(-2.5f, 0.5f, 6f);
    [Tooltip("How high up his body the camera aims.")]
    public float aimHeight = 0.6f;
    [Tooltip("How quickly the camera catches up as he falls. Lower = more drift.")]
    public float cameraLag = 1.5f;

    [Header("Background")]
    public Color skyTop = new Color(0.03f, 0.06f, 0.18f);
    public Color skyBottom = new Color(0.45f, 0.7f, 0.9f);
    [Tooltip("Seconds the background takes to fade in as he falls.")]
    public float skyFadeTime = 2f;

    [Header("Credits")]
    [TextArea(8, 20)]
    [Tooltip("One line each. Blank lines make gaps.")]
    public string creditsText =
        "R.E.M\nA Game of Dreams\n\n\nDesign\nJaimie Neilson\n\n\nProgramming\nJaimie Neilson\n\n\nThank you for playing";

    [Tooltip("The credits font. Leave empty for Unity's built-in one.")]
    public Font creditsFont;
    [Tooltip("Seconds the credits take to scroll from bottom to top.")]
    public float creditsDuration = 24f;
    public int creditsFontSize = 28;
    public Color creditsColor = new Color(0.9f, 0.95f, 1f);

    [Header("Music")]
    [Tooltip("Plays as he falls and the credits roll. Leave empty for silence.")]
    public AudioClip creditsMusic;
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Tooltip("Seconds the music takes to fade in.")]
    public float musicFadeIn = 2f;
    [Tooltip("Seconds it takes to fade out at the very end.")]
    public float musicFadeOut = 3f;

    [Header("Ending")]
    [Tooltip("Seconds of black after the credits before anything else.")]
    public float holdOnBlack = 3f;

    Transform falling;
    Canvas canvas;
    RawImage sky;
    Text credits;
    Image blackout;
    AudioSource music;

    bool running;

    // Called by FallWatcher when he goes over the edge
    public void Begin(Transform player)
    {
        if (running) return;
        running = true;

        falling = player;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        BuildCanvas();

        // Take the camera off the cube system, so it can follow him down
        DreamCamera dreamCamera = CubeWorld.Instance != null
            ? CubeWorld.Instance.dreamCamera
            : null;

        if (dreamCamera != null) dreamCamera.enabled = false;

        // Stop the player script fighting us for control
        PlayerController controller = falling.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        CharacterController charController = falling.GetComponent<CharacterController>();
        if (charController != null) charController.enabled = false;

        // Tell the Animator to switch to the falling state
        Animator animator = falling.GetComponentInChildren<Animator>();

        if (animator != null)
        {
            // Stop any movement animation, so he doesn't run while falling
            animator.SetFloat("Speed", 0f);
            animator.SetTrigger("Fall");
        }

        Camera cam = Camera.main;
        float elapsed = 0f;

        StartCoroutine(FadeSky());
        StartMusic();

        // Turn him to face the camera, so we see him rather than his back
        falling.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        // Fall, with the credits starting partway down
        bool creditsStarted = false;

        while (elapsed < fallBeforeCredits + creditsDuration)
        {
            elapsed += Time.deltaTime;

            // Down he goes, drifting slowly
            falling.position += Vector3.down * fallSpeed * Time.deltaTime;
            falling.Rotate(tumble * Time.deltaTime, Space.Self);

            // Keep him in frame
            if (cam != null) FrameFall(cam);

            if (!creditsStarted && elapsed >= fallBeforeCredits)
            {
                creditsStarted = true;
                StartCoroutine(RollCredits());
            }

            yield return null;
        }

        // Fade the music out alongside the picture, so they land together
        StartCoroutine(FadeMusicOut());

        yield return FadeToBlack();
        yield return new WaitForSeconds(holdOnBlack);

        Debug.Log("He wakes.");
    }

    // Sits the camera at a fixed offset from him, lagging a little as he
    // falls so he drifts within the frame rather than hanging motionless
    void FrameFall(Camera cam)
    {
        Vector3 wanted = falling.position + cameraOffset;

        // Only lag vertically: sideways tracking stays exact so he
        // doesn't wander out of shot
        Vector3 here = cam.transform.position;
        float laggedY = Mathf.Lerp(here.y, wanted.y, cameraLag * Time.deltaTime);

        cam.transform.position = new Vector3(wanted.x, laggedY, wanted.z);
        cam.transform.LookAt(falling.position + Vector3.up * aimHeight);
    }

    // --- Music ------------------------------------------------------

    void StartMusic()
    {
        if (creditsMusic == null) return;

        music = gameObject.AddComponent<AudioSource>();
        music.clip = creditsMusic;
        music.loop = true;
        music.spatialBlend = 0f;    // heard everywhere, not from a point in space
        music.volume = 0f;
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

    IEnumerator FadeMusicOut()
    {
        if (music == null) yield break;

        float from = music.volume;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, musicFadeOut))
        {
            music.volume = Mathf.Lerp(from, 0f, t);
            yield return null;
        }

        music.volume = 0f;
        music.Stop();
    }

    // --- Canvas and UI ----------------------------------------------

    void BuildCanvas()
    {
        GameObject canvasObj = new GameObject("AwakeningCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;   // under the screen fader, over everything else

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        BuildCredits(canvasObj.transform);
        BuildBlackout(canvasObj.transform);
    }

    // The credits, scrolling up the left-hand side
    void BuildCredits(Transform parent)
    {
        GameObject obj = new GameObject("Credits");
        obj.transform.SetParent(parent, false);

        credits = obj.AddComponent<Text>();
        credits.text = creditsText;

        // Use the chosen font, or Unity's built-in one if none is set
        credits.font = creditsFont != null
            ? creditsFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        credits.fontSize = creditsFontSize;
        credits.color = new Color(creditsColor.r, creditsColor.g, creditsColor.b, 0f);
        credits.alignment = TextAnchor.UpperLeft;
        credits.lineSpacing = 1.4f;
        credits.raycastTarget = false;

        // Let long credits run past the box rather than being cut off
        credits.horizontalOverflow = HorizontalWrapMode.Wrap;
        credits.verticalOverflow = VerticalWrapMode.Overflow;

        // Down the left third of the screen
        RectTransform rect = credits.rectTransform;
        rect.anchorMin = new Vector2(0.08f, 0f);
        rect.anchorMax = new Vector2(0.42f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, 2000f);
        rect.anchoredPosition = new Vector2(0f, -2000f);
    }

    void BuildBlackout(Transform parent)
    {
        GameObject obj = new GameObject("Blackout");
        obj.transform.SetParent(parent, false);

        blackout = obj.AddComponent<Image>();
        blackout.color = new Color(0f, 0f, 0f, 0f);
        blackout.raycastTarget = false;

        Stretch(blackout.rectTransform);
        obj.transform.SetAsLastSibling();
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // --- Timed pieces -----------------------------------------------

    // Fades the camera's own background to the dream colour, so he
    // falls in front of it rather than behind it
    IEnumerator FadeSky()
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        cam.clearFlags = CameraClearFlags.SolidColor;
        Color from = cam.backgroundColor;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, skyFadeTime))
        {
            cam.backgroundColor = Color.Lerp(from, skyBottom, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        cam.backgroundColor = skyBottom;
    }

    // Scrolls the credits up the left-hand side, fading them in as they rise
    IEnumerator RollCredits()
    {
        RectTransform rect = credits.rectTransform;

        float from = -1200f;
        float to = 3600f;
        float fadeIn = 2.5f;

        // Fade and climb together, so the words drift into view
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(1f, creditsDuration))
        {
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, t));

            float elapsed = t * creditsDuration;
            Color c = creditsColor;
            c.a = Mathf.Clamp01(elapsed / fadeIn);
            credits.color = c;

            yield return null;
        }
    }

    // Fades the screen to black, so he wakes in darkness
    IEnumerator FadeToBlack()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 2f)
        {
            blackout.color = new Color(0f, 0f, 0f, t);
            yield return null;
        }

        blackout.color = Color.black;
    }
}