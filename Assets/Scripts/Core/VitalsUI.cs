using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The on-screen health, energy and objective.
//   Top left:  a row of hearts, with the energy bar under it.
//   Top right: the objective panel, showing the current task and how far along it is.
// Creates its own canvas and draws every picture in code, so there's nothing to import or set up.
// It only shows while playing a level.
public class VitalsUI : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Gap between the edge of the screen and the UI, in pixels.")]
    public float margin = 24f;
    public float heartSize = 44f;
    public float heartGap = 6f;
    public Vector2 energyBarSize = new Vector2(260f, 30f);
    public float energyIconSize = 40f;
    public Vector2 objectivePanelSize = new Vector2(380f, 116f);

    [Header("Colours")]
    public Color heartColor = new Color(0.92f, 0.16f, 0.24f);
    public Color emptyHeartColor = new Color(0.42f, 0.42f, 0.5f, 0.75f);
    public Color energyStartColor = new Color(0.25f, 0.55f, 1f);
    public Color energyEndColor = new Color(0.68f, 0.3f, 0.95f);
    public Color energyEmptyColor = new Color(0.84f, 0.84f, 0.9f, 0.85f);
    public Color exhaustedColor = new Color(1f, 0.45f, 0.45f);
    public Color panelColor = new Color(0.36f, 0.3f, 0.48f, 0.62f);
    public Color goldColor = new Color(1f, 0.78f, 0.25f);

    [Header("Feedback")]
    [Tooltip("How quickly the whole UI fades in and out.")]
    public float fadeSpeed = 4f;
    [Tooltip("How quickly the energy bar slides to its new level.")]
    public float barSpeed = 6f;
    [Tooltip("How long a lost heart pops for, in seconds.")]
    public float heartPopTime = 0.45f;

    CanvasGroup group;
    RectTransform root;

    // Health
    readonly List<Image> hearts = new List<Image>();
    int shownHearts = -1;
    float heartPopStart = -999f;
    int poppedHeart = -1;

    // Energy
    Image energyFill;
    GameObject energyIcon, energyBack;
    float shownEnergy = 1f;

    // Objective
    CanvasGroup objectiveGroup;
    Image checkBox;
    Text countText;
    Text taskText;
    Sprite boxEmpty;
    Sprite boxTicked;

    [Header("Art")]
    [Tooltip("Found automatically in Assets/Art/UI. Any left empty is drawn in code instead, so nothing breaks.")]
    public Sprite heartArt;
    public Sprite emptyHeartArt;
    public Sprite boltArt;
    public Sprite barBackArt;
    public Sprite barFillArt;
    public Sprite panelArt;
    public Sprite diamondArt;

    Font sansFont;
    Font serifFont;
    PlayerVitals vitals;

    // Cached so the same puzzle isn't searched for every frame
    int cachedFace = -1;
    DigPuzzle digPuzzle;
    GearPuzzle gearPuzzle;

    void Awake()
    {
        BuildUI();
        group.alpha = 0f;
    }

    void Update()
    {
        CubeWorld world = CubeWorld.Instance;
        bool playing = world != null && world.CurrentMode == CubeWorld.Mode.Playing && world.player != null;

        float targetAlpha = playing ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);

        if (!playing) return;

        vitals = world.player.Vitals;

        UpdateHearts();
        UpdateEnergy();
        UpdateObjective(world);
    }

    // --- Health ---------------------------------------------------------

    void UpdateHearts()
    {
        // The player might be given more or fewer hearts than the UI was built with
        if (hearts.Count != vitals.maxHearts) BuildHearts(vitals.maxHearts);

        // A heart was just lost, so that one pops
        if (shownHearts >= 0 && vitals.Hearts < shownHearts)
        {
            poppedHeart = vitals.Hearts;
            heartPopStart = Time.unscaledTime;
        }
        shownHearts = vitals.Hearts;

        float pop = Mathf.Clamp01((Time.unscaledTime - heartPopStart) / Mathf.Max(0.01f, heartPopTime));

        for (int i = 0; i < hearts.Count; i++)
        {
            bool full = i < vitals.Hearts;

            // The artwork has its own colours, so it's swapped rather than tinted
            if (heartArt != null && emptyHeartArt != null)
            {
                hearts[i].sprite = full ? heartArt : emptyHeartArt;
                hearts[i].color = Color.white;
            }
            else
            {
                hearts[i].color = full ? heartColor : emptyHeartColor;
            }

            // The lost heart swells and settles back
            float scale = 1f;
            if (i == poppedHeart && pop < 1f) scale = 1f + Mathf.Sin(pop * Mathf.PI) * 0.45f;
            hearts[i].rectTransform.localScale = Vector3.one * scale;
        }
    }

    // --- Energy ---------------------------------------------------------

    void UpdateEnergy()
    {
        // No running on this level, so no energy bar either
        bool showEnergy = !vitals.SprintLocked;
        if (energyIcon != null && energyIcon.activeSelf != showEnergy) energyIcon.SetActive(showEnergy);
        if (energyBack != null && energyBack.activeSelf != showEnergy) energyBack.SetActive(showEnergy);
        if (!showEnergy) return;

        shownEnergy = Mathf.Lerp(shownEnergy, vitals.EnergyFraction, barSpeed * Time.unscaledDeltaTime);
        energyFill.fillAmount = shownEnergy;

        // Reddens and throbs while sprinting is locked, so it's clear why shift isn't working
        if (vitals.Exhausted)
        {
            float throb = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            energyFill.color = Color.Lerp(Color.white, exhaustedColor, throb);
        }
        else
        {
            energyFill.color = Color.white;
        }
    }

    // --- Objective ------------------------------------------------------

    void UpdateObjective(CubeWorld world)
    {
        int index = world.CurrentFaceIndex;
        if (index < 0 || index >= world.faces.Length) return;

        // A different face has different puzzles, so look again
        if (index != cachedFace)
        {
            cachedFace = index;
            digPuzzle = world.faces[index].GetComponent<DigPuzzle>();
            gearPuzzle = world.faces[index].GetComponent<GearPuzzle>();
        }

        string task;
        int have;
        int need;
        bool done;
        bool known = ReadObjective(out task, out have, out need, out done);

        // A level with nothing to track just hides the panel
        objectiveGroup.alpha = Mathf.MoveTowards(objectiveGroup.alpha, known ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        if (!known) return;

        taskText.text = task;
        countText.text = need > 0 ? have + "/" + need : "";
        checkBox.sprite = done ? boxTicked : boxEmpty;
    }

    // Works out what the player should be doing right now on this level
    bool ReadObjective(out string task, out int have, out int need, out bool done)
    {
        task = "";
        have = 0;
        need = 0;
        done = false;

        // Level 2, dig a channel to the vat
        if (digPuzzle != null)
        {
            if (digPuzzle.Finished)
            {
                task = "Enter the door of light";
                done = true;
            }
            else if (digPuzzle.requiresSpade && !digPuzzle.HasSpade)
            {
                task = "Find the spade";
                need = 1;
            }
            else if (digPuzzle.Flowing)
            {
                task = "Wait for the vat to fill";
            }
            else
            {
                task = "Dig a channel to the vat";
            }
            return true;
        }

        // Level 4, join the gears up from the motor
        if (gearPuzzle != null)
        {
            if (gearPuzzle.Solved)
            {
                task = "Enter the door of light";
                done = true;
            }
            else
            {
                task = "Connect the gears";
                need = gearPuzzle.PegCount + 1;
                have = Mathf.Min(gearPuzzle.LinkedCount, need);
            }
            return true;
        }

        // Levels with dream fragments and an altar
        LevelObjectives objectives = LevelObjectives.Instance;
        if (objectives != null && objectives.Required > 0)
        {
            need = objectives.Required;

            if (objectives.DoorOpen)
            {
                task = "Enter the door of light";
                have = need;
                done = true;
            }
            else if (objectives.AllCollected)
            {
                task = "Take them to the altar";
                have = objectives.Delivered;
            }
            else
            {
                task = "Collect dream fragments";
                have = objectives.Carried + objectives.Delivered;
            }
            return true;
        }

        return false;
    }

    // --- Building the UI ------------------------------------------------

#if UNITY_EDITOR
    // Fills in the pictures from Assets/Art/UI when the component is added, so there's nothing to drag in.
    // It's saved with the scene, which is what lets a build use them too.
    void Reset()
    {
        FindArt();
    }

    [ContextMenu("Find UI Art")]
    void FindArt()
    {
        if (heartArt == null) heartArt = LoadArt("Heart_Full");
        if (emptyHeartArt == null) emptyHeartArt = LoadArt("Heart_Empty");
        if (boltArt == null) boltArt = LoadArt("Energy_Bolt");
        if (barBackArt == null) barBackArt = LoadArt("Energy_Bar_Back");
        if (barFillArt == null) barFillArt = LoadArt("Energy_Bar_Fill");
        if (panelArt == null) panelArt = LoadArt("Objective_Panel");
        if (diamondArt == null) diamondArt = LoadArt("Objective_Diamond");
    }

    Sprite LoadArt(string name)
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/" + name + ".png");
    }
#endif

    // Width for a picture drawn at a set height, keeping its shape
    float WidthFor(Sprite sprite, float height)
    {
        return height * sprite.rect.width / sprite.rect.height;
    }

    void BuildUI()
    {
#if UNITY_EDITOR
        // Playing in the editor works even if the component was created by CubeWorld
        FindArt();
#endif

        sansFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // A serif for the title if the computer has one, else the plain font
        serifFont = Font.CreateDynamicFontFromOSFont(new string[] { "Georgia", "Palatino Linotype", "Times New Roman", "Serif" }, 30);
        if (serifFont == null) serifFont = sansFont;

        // Drawn under the detection light and the screen fader
        GameObject canvasObj = new GameObject("VitalsCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 890;

        // Keeps the UI the same size on any screen
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObj = new GameObject("Vitals", typeof(RectTransform));
        rootObj.transform.SetParent(canvasObj.transform, false);
        group = rootObj.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; // never blocks mouse clicks
        group.interactable = false;

        root = rootObj.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        BuildHearts(3);
        BuildEnergy();
        BuildObjective();
    }

    // The row of hearts, top left
    void BuildHearts(int count)
    {
        foreach (Image old in hearts)
            if (old != null) Destroy(old.gameObject);
        hearts.Clear();

        Sprite heartSprite = heartArt != null ? heartArt : MakeHeart();
        float heartWidth = heartArt != null ? WidthFor(heartArt, heartSize) : heartSize;

        for (int i = 0; i < count; i++)
        {
            Image heart = MakeImage("Heart" + (i + 1), root, heartSprite);
            RectTransform rect = heart.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(heartWidth, heartSize);
            rect.anchoredPosition = new Vector2(margin + heartWidth * 0.5f + i * (heartWidth + heartGap),
                                                -(margin + heartSize * 0.5f));

            AddShadow(heart.gameObject, 0.35f);
            heart.color = heartColor;
            hearts.Add(heart);
        }

        shownHearts = -1;
    }

    // The lightning bolt and the arrow shaped bar, under the hearts
    void BuildEnergy()
    {
        float rowCentre = margin + heartSize + 14f + energyIconSize * 0.5f;

        // The bolt's artwork is tall and thin, so it gets a little more height
        float boltHeight = boltArt != null ? energyIconSize * 1.3f : energyIconSize;
        float boltWidth = boltArt != null ? WidthFor(boltArt, boltHeight) : energyIconSize;

        Image bolt = MakeImage("EnergyIcon", root, boltArt != null ? boltArt : MakeBolt());
        RectTransform boltRect = bolt.rectTransform;
        boltRect.anchorMin = new Vector2(0f, 1f);
        boltRect.anchorMax = new Vector2(0f, 1f);
        boltRect.pivot = new Vector2(0.5f, 0.5f);
        boltRect.sizeDelta = new Vector2(boltWidth, boltHeight);
        boltRect.anchoredPosition = new Vector2(margin + boltWidth * 0.5f, -rowCentre);
        bolt.color = boltArt != null ? Color.white : new Color(0.78f, 0.55f, 1f);
        AddShadow(bolt.gameObject, 0.35f);
        energyIcon = bolt.gameObject;

        float barLeft = margin + boltWidth + 8f;

        // The artwork keeps its own shape, so only the length is set
        Vector2 barSize = energyBarSize;
        if (barBackArt != null) barSize.y = energyBarSize.x * barBackArt.rect.height / barBackArt.rect.width;

        // The grey of the bar that's been used up
        Image back = MakeImage("EnergyBack", root, barBackArt != null ? barBackArt : MakeBar(false));
        RectTransform backRect = back.rectTransform;
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.sizeDelta = barSize;
        backRect.anchoredPosition = new Vector2(barLeft, -rowCentre);
        back.color = barBackArt != null ? Color.white : energyEmptyColor;
        AddShadow(back.gameObject, 0.3f);
        energyBack = back.gameObject;

        // The blue to purple energy, which is cropped from the right as it drains
        energyFill = MakeImage("EnergyFill", backRect, barFillArt != null ? barFillArt : MakeBar(true));
        RectTransform fillRect = energyFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        energyFill.type = Image.Type.Filled;
        energyFill.fillMethod = Image.FillMethod.Horizontal;
        energyFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        energyFill.fillAmount = 1f;
    }

    // The panel with the gold diamond, "Objective", and the current task, top right
    void BuildObjective()
    {
        GameObject panelObj = new GameObject("ObjectivePanel", typeof(RectTransform));
        panelObj.transform.SetParent(root, false);
        objectiveGroup = panelObj.AddComponent<CanvasGroup>();
        objectiveGroup.blocksRaycasts = false;
        objectiveGroup.interactable = false;
        objectiveGroup.alpha = 0f;

        Image panel = panelObj.AddComponent<Image>();
        panel.sprite = panelArt != null ? panelArt : MakePanel();
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = false;

        // The artwork already has its shadow and colour, so only a touch of see-through is added
        if (panelArt != null)
        {
            panel.color = new Color(1f, 1f, 1f, 0.88f);
        }
        else
        {
            panel.color = panelColor;
            AddShadow(panelObj, 0.45f);
        }

        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = objectivePanelSize;
        panelRect.anchoredPosition = new Vector2(-margin, -margin);

        // The gold diamond beside the title
        Image diamond = MakeImage("Diamond", panelRect, diamondArt != null ? diamondArt : MakeDiamond());
        RectTransform diamondRect = diamond.rectTransform;
        float diamondWidth = diamondArt != null ? WidthFor(diamondArt, 34f) : 30f;
        float diamondHeight = diamondArt != null ? 34f : 30f;
        PlaceTopLeft(diamondRect, 24f, 18f, diamondWidth, diamondHeight);
        diamond.color = diamondArt != null ? Color.white : goldColor;

        // The title
        Text title = MakeText("Title", panelRect, serifFont, 30, FontStyle.Normal, TextAnchor.MiddleLeft);
        title.text = "Objective";
        title.color = new Color(1f, 1f, 1f, 0.95f);
        PlaceTopLeft(title.rectTransform, 64f, 14f, 280f, 42f);

        // The tick box, the count, and what to do
        boxEmpty = MakeBox(false);
        boxTicked = MakeBox(true);
        checkBox = MakeImage("CheckBox", panelRect, boxEmpty);
        PlaceTopLeft(checkBox.rectTransform, 26f, 68f, 26f, 26f);
        checkBox.color = new Color(1f, 0.82f, 0.55f, 0.9f);

        // The count is in the same serif as the title
        countText = MakeText("Count", panelRect, serifFont, 28, FontStyle.Normal, TextAnchor.MiddleLeft);
        countText.color = Color.white;
        PlaceTopLeft(countText.rectTransform, 64f, 62f, 84f, 38f);

        taskText = MakeText("Task", panelRect, sansFont, 19, FontStyle.Normal, TextAnchor.MiddleLeft);
        taskText.color = new Color(1f, 1f, 1f, 0.85f);
        taskText.horizontalOverflow = HorizontalWrapMode.Wrap;
        PlaceTopLeft(taskText.rectTransform, 150f, 58f, objectivePanelSize.x - 168f, 46f);
    }

    // --- Little helpers -------------------------------------------------

    void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    Image MakeImage(string name, Transform parent, Sprite sprite)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    Text MakeText(string name, Transform parent, Font font, int size, FontStyle style, TextAnchor anchor)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.raycastTarget = false;
        return text;
    }

    // A soft drop shadow, so the UI lifts off the scene
    void AddShadow(GameObject obj, float strength)
    {
        Shadow shadow = obj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, strength);
        shadow.effectDistance = new Vector2(3f, -3f);
    }

    // --- Pictures drawn in code -----------------------------------------

    // Paints a texture by asking a function for the colour at each point (0 to 1 across and up).
    // Each pixel is sampled several times, which smooths the edges.
    Sprite Paint(int width, int height, System.Func<float, float, Color> colourAt, Vector4 border)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        const int samples = 3;
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float alphaSum = 0f;
                Vector3 colourSum = Vector3.zero;

                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        float u = (x + (sx + 0.5f) / samples) / width;
                        float v = (y + (sy + 0.5f) / samples) / height;
                        Color c = colourAt(u, v);

                        alphaSum += c.a;
                        colourSum += new Vector3(c.r, c.g, c.b) * c.a;
                    }
                }

                float total = samples * samples;
                Vector3 rgb = alphaSum > 0f ? colourSum / alphaSum : Vector3.one;
                pixels[y * width + x] = new Color(rgb.x, rgb.y, rgb.z, alphaSum / total);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect, border);
    }

    Color Inside(bool inside)
    {
        return inside ? Color.white : new Color(1f, 1f, 1f, 0f);
    }

    // The classic heart curve
    Sprite MakeHeart()
    {
        return Paint(96, 96, (u, v) =>
        {
            float x = (u * 2f - 1f) * 1.3f;
            float y = (v * 2f - 1f) * 1.3f + 0.1f;

            float a = x * x + y * y - 1f;
            return Inside(a * a * a - x * x * y * y * y <= 0f);
        }, Vector4.zero);
    }

    // A lightning bolt, as a polygon
    Sprite MakeBolt()
    {
        Vector2[] points =
        {
            new Vector2(0.55f, 1.00f), new Vector2(0.82f, 1.00f), new Vector2(0.62f, 0.62f),
            new Vector2(0.88f, 0.62f), new Vector2(0.30f, 0.00f), new Vector2(0.42f, 0.40f),
            new Vector2(0.14f, 0.40f)
        };

        return Paint(96, 96, (u, v) => Inside(InPolygon(points, u, v)), Vector4.zero);
    }

    // The energy bar: a long strip with a pointed right end.
    // The filled one has the blue to purple colour baked in.
    Sprite MakeBar(bool gradient)
    {
        int width = Mathf.RoundToInt(energyBarSize.x * 2f);
        int height = Mathf.RoundToInt(energyBarSize.y * 2f);

        return Paint(width, height, (u, v) =>
        {
            float x = u * width;
            float y = v * height;

            // The point at the right end is as long as half the bar is tall
            bool inside = x <= width - Mathf.Abs(y - height * 0.5f);
            if (!inside) return new Color(1f, 1f, 1f, 0f);

            return gradient ? Color.Lerp(energyStartColor, energyEndColor, u) : Color.white;
        }, Vector4.zero);
    }

    Sprite MakeDiamond()
    {
        return Paint(64, 64, (u, v) => Inside(Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f) <= 0.5f), Vector4.zero);
    }

    // The rounded panel, stretchable because the corners are sliced off
    Sprite MakePanel()
    {
        const int size = 64;
        const float radius = 20f;

        return Paint(size, size, (u, v) => Inside(RoundedBox(u, v, size, radius) <= 0f),
                     new Vector4(radius, radius, radius, radius));
    }

    // The tick box, with a tick in it when it's done
    Sprite MakeBox(bool ticked)
    {
        const int size = 64;

        return Paint(size, size, (u, v) =>
        {
            float d = RoundedBox(u, v, size, 12f);
            bool inside = d <= 0f && d >= -6f;

            if (ticked)
            {
                // Two short strokes make the tick
                float tick = Mathf.Min(DistanceToSegment(u, v, 0.24f, 0.52f, 0.42f, 0.30f),
                                       DistanceToSegment(u, v, 0.42f, 0.30f, 0.78f, 0.74f));
                inside = inside || tick < 0.07f;
            }

            return Inside(inside);
        }, Vector4.zero);
    }

    // How far a point is outside a rounded square. Below zero means inside.
    float RoundedBox(float u, float v, float size, float radius)
    {
        float px = (u - 0.5f) * size;
        float py = (v - 0.5f) * size;
        float half = size * 0.5f - radius;

        float dx = Mathf.Max(Mathf.Abs(px) - half, 0f);
        float dy = Mathf.Max(Mathf.Abs(py) - half, 0f);

        return Mathf.Sqrt(dx * dx + dy * dy) - radius;
    }

    float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        float abx = bx - ax;
        float aby = by - ay;
        float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));

        float cx = ax + abx * t - px;
        float cy = ay + aby * t - py;
        return Mathf.Sqrt(cx * cx + cy * cy);
    }

    // Standard even-odd test for a point inside a polygon
    bool InPolygon(Vector2[] polygon, float x, float y)
    {
        bool inside = false;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            bool crosses = (polygon[i].y > y) != (polygon[j].y > y);
            if (!crosses) continue;

            float edgeX = (polygon[j].x - polygon[i].x) * (y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x;
            if (x < edgeX) inside = !inside;
        }

        return inside;
    }
}
