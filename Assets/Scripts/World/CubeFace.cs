using System.Collections.Generic;
using UnityEngine;

// One face of the dream cube.
// Build the level as a child of this object as if it were lying flat on top.
// This object's green Y arrow points "outward" from the cube = "up" for the level.
public class CubeFace : MonoBehaviour
{
    [Header("Level")]
    [Tooltip("Where the player appears when entering this face.")]
    public Transform spawnPoint;

    [Tooltip("Parent object holding all of this face's level geometry.")]
    public GameObject levelContent;

    [Tooltip("Grey 'fog' object shown while this face is locked.")]
    public GameObject lockedCover;

    [Header("Locked Fog")]
    [Tooltip("A thick dark fog that completely hides this face until the level before it is finished. Only shows while the face is locked, so it never appears when CubeWorld's Unlock All Faces is on.")]
    public bool darkFog = true;
    [Tooltip("Width of the fog across the face.")]
    public float fogSize = 30f;
    [Tooltip("How high the fog banks rise above the face.")]
    public float fogHeight = 5f;
    [Range(10, 120)] public int fogPuffs = 45;
    public Color fogColor = new Color(0.05f, 0.04f, 0.09f);
    [Tooltip("Seconds the fog takes to clear when the face unlocks.")]
    public float fogClearTime = 1.8f;

    [Header("Camera")]
    [Tooltip("Which camera the player gets on this face. Set Face_3 (the maze) to ThirdPerson.")]
    public DreamCamera.PlayerCameraStyle cameraStyle = DreamCamera.PlayerCameraStyle.Isometric;

    [Header("Face Colour")]
    [Tooltip("Untick to leave materials exactly as they are — needed for glass or any textured material.")]
    public bool tintFloors = false;

    [Tooltip("Identifying colour for this face. Only used when Tint Floors is ticked.")]
    public Color faceColor = Color.white;

    [Tooltip("Objects tinted with the face colour. Filled in automatically by the generator.")]
    public Renderer[] colorRenderers;

    [Header("Scene View")]
    [Tooltip("Colour of the gizmo line, so faces are still tellable apart in the editor.")]
    public Color gizmoColor = Color.cyan;

    // Colour property names: URP shaders use _BaseColor, older shaders use _Color
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");

    public bool IsUnlocked { get; private set; }

    // The fog is built the first time it's needed
    GameObject fogRoot;
    Renderer fogSlab;
    readonly List<Transform> puffs = new List<Transform>();
    readonly List<SpriteRenderer> puffRenderers = new List<SpriteRenderer>();
    readonly List<Vector3> puffHome = new List<Vector3>();
    readonly List<float> puffSeed = new List<float>();
    readonly List<float> puffSize = new List<float>();
    float fogAmount = 1f;
    static Sprite puffSprite;

    void Awake()
    {
        ApplyColor();
    }

    // Runs in the editor whenever you change a value, so colours update instantly
    void OnValidate()
    {
        ApplyColor();
    }

    // Tints each renderer without touching the shared material.
    // With Tint Floors off, it clears any tint so the real material shows through.
    public void ApplyColor()
    {
        if (colorRenderers == null) return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (Renderer r in colorRenderers)
        {
            if (r == null) continue;

            if (!tintFloors)
            {
                // An empty block removes the override entirely
                r.SetPropertyBlock(null);
                continue;
            }

            r.GetPropertyBlock(block);
            block.SetColor(BaseColorID, faceColor);
            block.SetColor(ColorID, faceColor);
            r.SetPropertyBlock(block);
        }
    }

    // Shows the level or the fog, depending on lock state
    public void SetUnlocked(bool unlocked)
    {
        IsUnlocked = unlocked;
        if (levelContent != null) levelContent.SetActive(unlocked);

        // The dark fog replaces the plain grey cover
        if (lockedCover != null) lockedCover.SetActive(!unlocked && !darkFog);

        if (!darkFog) return;

        if (!unlocked)
        {
            BuildFog();
            fogAmount = 1f;
            fogRoot.SetActive(true);
            ApplyFog(0f);
        }
        // Unlocking: it's left on and Update thins it away
    }

    void Update()
    {
        if (fogRoot == null || !fogRoot.activeSelf) return;

        if (IsUnlocked)
        {
            fogAmount -= Time.unscaledDeltaTime / Mathf.Max(0.1f, fogClearTime);

            if (fogAmount <= 0f)
            {
                fogRoot.SetActive(false);
                return;
            }
        }

        ApplyFog(Time.time);
    }

    // Slowly drifts the banks of fog, turns each one to the camera, and fades them as a whole
    void ApplyFog(float time)
    {
        Camera cam = Camera.main;
        float amount = Mathf.SmoothStep(0f, 1f, fogAmount);

        for (int i = 0; i < puffs.Count; i++)
        {
            float seed = puffSeed[i];
            Vector3 drift = new Vector3(Mathf.Sin(time * 0.3f + seed) * 0.9f,
                                        Mathf.Sin(time * 0.5f + seed * 2f) * 0.35f,
                                        Mathf.Cos(time * 0.27f + seed) * 0.9f);

            puffs[i].localPosition = puffHome[i] + drift;

            // It billows, and shrinks away as it clears
            float breathe = 1f + 0.06f * Mathf.Sin(time * 0.8f + seed);
            puffs[i].localScale = Vector3.one * puffSize[i] * breathe * Mathf.Lerp(0.7f, 1f, amount);

            if (cam != null) puffs[i].rotation = cam.transform.rotation;

            Color c = puffRenderers[i].color;
            c.a = 0.6f * amount;
            puffRenderers[i].color = c;
        }

        if (fogSlab != null)
        {
            Color c = fogColor;
            c.a = amount;
            fogSlab.sharedMaterial.color = c;
        }
    }

    void BuildFog()
    {
        if (fogRoot != null) return;

        fogRoot = new GameObject("DarkFog");
        fogRoot.transform.SetParent(transform, false);

        // A dark slab that covers the whole face, so nothing shows through
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "FogSlab";
        slab.transform.SetParent(fogRoot.transform, false);
        slab.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        slab.transform.localScale = new Vector3(fogSize + 0.6f, 2.4f, fogSize + 0.6f);
        Destroy(slab.GetComponent<Collider>());

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        fogSlab = slab.GetComponent<Renderer>();
        fogSlab.sharedMaterial = new Material(shader) { color = fogColor };

        if (puffSprite == null) puffSprite = MakePuffSprite();

        // Banks of fog piled on top, thickest low down
        System.Random random = new System.Random(GetInstanceID());
        float half = fogSize * 0.5f;

        for (int i = 0; i < fogPuffs; i++)
        {
            float x = (float)(random.NextDouble() * 2 - 1) * (half - 1f);
            float z = (float)(random.NextDouble() * 2 - 1) * (half - 1f);
            float low = (float)random.NextDouble();
            float y = 1.5f + low * low * fogHeight;
            float size = 9f + (float)random.NextDouble() * 7f;

            GameObject puff = new GameObject("Puff_" + i);
            puff.transform.SetParent(fogRoot.transform, false);

            SpriteRenderer sr = puff.AddComponent<SpriteRenderer>();
            sr.sprite = puffSprite;
            sr.color = new Color(fogColor.r, fogColor.g, fogColor.b, 0.6f);
            sr.sortingOrder = i % 8;

            puffs.Add(puff.transform);
            puffRenderers.Add(sr);
            puffHome.Add(new Vector3(x, y, z));
            puffSeed.Add((float)random.NextDouble() * 20f);
            puffSize.Add(size);
        }
    }

    // A soft, cloudy blob drawn in code, so no art is needed
    static Sprite MakePuffSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);

                float noise = Mathf.PerlinNoise(x * 0.06f + 3f, y * 0.06f + 7f) * 0.5f
                            + Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.25f + 0.25f;

                float edge = Mathf.Clamp01(1f - r);
                float a = Mathf.Pow(edge, 1.6f) * noise * 1.4f;

                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // Scene view helpers: line = face "up", green sphere = spawn point
    void OnDrawGizmos()
    {
        Gizmos.color = tintFloors ? faceColor : gizmoColor;
        Gizmos.DrawLine(transform.position, transform.position + transform.up * 3f);

        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
        }
    }
}