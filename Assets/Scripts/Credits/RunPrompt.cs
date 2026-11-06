using UnityEngine;
using UnityEngine.UI;

// The final run. While the player is on this face it switches sprinting off,
// hides the energy bar, and puts a huge "RUN!" in the middle of the screen,
// which turns into "JUMP!" once he reaches the end of the board.
// Added to the face automatically by AwakeningGenerator.
[RequireComponent(typeof(CubeFace))]
public class RunPrompt : MonoBehaviour
{
    [Header("Text")]
    public string runText = "RUN!";
    public string jumpText = "JUMP!";
    public int fontSize = 260;
    public Color runColor = new Color(1f, 0.25f, 0.2f);
    public Color jumpColor = new Color(1f, 0.92f, 0.5f);

    [Header("Placement")]
    [Tooltip("0 is the bottom of the screen, 1 the top. 0.5 is dead centre, which covers him, so it sits near the top.")]
    [Range(0f, 1f)] public float verticalPosition = 0.86f;

    [Header("Jump")]
    [Tooltip("The text changes to JUMP! once he's this close to the tip of the board.")]
    public float jumpZoneLength = 3f;

    int faceIndex = -1;
    float plankEndZ = 20.5f;

    Canvas canvas;
    CanvasGroup group;
    Text label;
    RectTransform labelRect;
    bool showingJump;

    void Start()
    {
        AwakeningGenerator generator = GetComponent<AwakeningGenerator>();
        if (generator != null) plankEndZ = generator.PlankEndZ;

        BuildUI();
    }

    void Update()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null || label == null) return;

        if (faceIndex < 0)
            faceIndex = System.Array.IndexOf(world.faces, GetComponent<CubeFace>());

        bool here = world.CurrentMode == CubeWorld.Mode.Playing && world.CurrentFaceIndex == faceIndex;

        // No running on this face
        world.player.Vitals.SprintLocked = here;

        Vector3 local = transform.InverseTransformPoint(world.player.transform.position);
        bool fallen = local.y < -1f;
        bool visible = here && !fallen;

        bool jump = local.z > plankEndZ - jumpZoneLength;
        if (jump != showingJump)
        {
            showingJump = jump;
            label.text = jump ? jumpText : runText;
            label.color = jump ? jumpColor : runColor;
        }

        group.alpha = visible ? 1f : 0f;
    }

    void OnDisable()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world != null && world.player != null) world.player.Vitals.SprintLocked = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (canvas != null) canvas.gameObject.SetActive(true);
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void BuildUI()
    {
        GameObject canvasObj = new GameObject("RunPromptCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        group = canvasObj.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject textObj = new GameObject("Prompt");
        textObj.transform.SetParent(canvasObj.transform, false);

        label = textObj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = runText;
        label.color = runColor;

        labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, verticalPosition);
        labelRect.anchorMax = new Vector2(0.5f, verticalPosition);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(1400f, 360f);
        labelRect.anchoredPosition = Vector2.zero;
    }
}
