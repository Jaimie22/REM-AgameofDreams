using UnityEngine;
using UnityEngine.UI;

// Plain on-screen text for hearts, energy and the current objective.
// Builds its own canvas in code. Only shows while playing a level.
public class VitalsUI : MonoBehaviour
{
    public float margin = 24f;
    public int fontSize = 26;

    Canvas canvas;
    Text vitalsText;
    Text objectiveText;
    PlayerVitals vitals;

    // Cached so the same puzzle isn't searched for every frame
    int cachedFace = -1;
    DigPuzzle digPuzzle;
    GearPuzzle gearPuzzle;

    void Awake()
    {
        GameObject canvasObject = new GameObject("VitalsCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        vitalsText = MakeText(canvasObject.transform, font, new Vector2(0f, 1f), TextAnchor.UpperLeft, new Vector2(margin, -margin));
        objectiveText = MakeText(canvasObject.transform, font, new Vector2(1f, 1f), TextAnchor.UpperRight, new Vector2(-margin, -margin));
        canvas.enabled = false;
    }

    Text MakeText(Transform parent, Font font, Vector2 anchor, TextAnchor alignment, Vector2 position)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(600f, 120f);
        return text;
    }

    void Update()
    {
        CubeWorld world = CubeWorld.Instance;
        bool playing = world != null && world.CurrentMode == CubeWorld.Mode.Playing && world.player != null;
        canvas.enabled = playing;
        if (!playing) return;

        vitals = world.player.Vitals;

        string text = "Hearts: " + vitals.Hearts + "/" + vitals.maxHearts;
        if (!vitals.SprintLocked)
            text += "\nEnergy: " + Mathf.RoundToInt(vitals.EnergyFraction * 100f) + "%";
        vitalsText.text = text;

        objectiveText.text = ObjectiveLine(world);
    }

    string ObjectiveLine(CubeWorld world)
    {
        int index = world.CurrentFaceIndex;
        if (index < 0 || index >= world.faces.Length) return "";

        // A different face has different puzzles, so look again
        if (index != cachedFace)
        {
            cachedFace = index;
            digPuzzle = world.faces[index].GetComponent<DigPuzzle>();
            gearPuzzle = world.faces[index].GetComponent<GearPuzzle>();
        }

        // Level 2, dig a channel to the vat
        if (digPuzzle != null)
        {
            if (digPuzzle.Finished) return "Objective: Enter the door of light";
            if (digPuzzle.requiresSpade && !digPuzzle.HasSpade) return "Objective: Find the spade";
            if (digPuzzle.Flowing) return "Objective: Wait for the vat to fill";
            return "Objective: Dig a channel to the vat";
        }

        // Level 4, join the gears up from the motor
        if (gearPuzzle != null)
        {
            if (gearPuzzle.Solved) return "Objective: Enter the door of light";
            int need = gearPuzzle.PegCount + 1;
            int have = Mathf.Min(gearPuzzle.LinkedCount, need);
            return "Objective: Connect the gears " + have + "/" + need;
        }

        // Levels with dream fragments and an altar
        LevelObjectives objectives = LevelObjectives.Instance;
        if (objectives != null && objectives.Required > 0)
        {
            int need = objectives.Required;
            if (objectives.DoorOpen) return "Objective: Enter the door of light";
            if (objectives.AllCollected) return "Objective: Take them to the altar " + objectives.Delivered + "/" + need;
            return "Objective: Collect dream fragments " + (objectives.Carried + objectives.Delivered) + "/" + need;
        }

        return "";
    }
}
