using UnityEngine;

// Builds the final face: a shallow pool stretching to the edge of the dream,
// with a diving board reaching out over nothing.
// Put this on the face instead of the other generators.
[RequireComponent(typeof(CubeFace))]
public class AwakeningGenerator : MonoBehaviour
{
    [Header("Theme")]
    [Tooltip("Used for this level's gravity and jump height.")]
    public LevelTheme theme;

    [Header("Face Area")]
    public float faceSize = 30f;
    public float edgeMargin = 0.5f;
    [Tooltip("Height of the pool floor above the cube face.")]
    public float baseHeight = 1f;

    [Header("The Pool")]
    [Tooltip("How deep the water sits above the floor. Ankle deep reads best.")]
    public float waterDepth = 0.12f;
    [Tooltip("How fast the surface ripples.")]
    public float rippleSpeed = 0.6f;
    [Tooltip("How far the surface rises and falls.")]
    public float rippleHeight = 0.02f;

    [Header("The Board")]
    [Tooltip("How far the board reaches out past the edge of the face.")]
    public float boardOverhang = 6f;
    public float boardWidth = 2f;
    public float boardThickness = 0.25f;
    [Tooltip("How far the board sits above the water.")]
    public float boardHeight = 0.6f;
    [Tooltip("Optional. A model to use instead of a plain plank.")]
    public GameObject boardPrefab;

    [Header("Start")]
    [Tooltip("How far back from the centre he starts, as a fraction of the pool. 0.4 is the far end, 0 is the middle.")]
    [Range(0f, 0.45f)] public float startDepth = 0.15f;

    [Header("Materials")]
    public Material floorMaterial;
    public Material waterMaterial;
    public Material boardMaterial;

    [Header("Colours (used when no material is assigned)")]
    public Color floorColor = new Color(0.22f, 0.24f, 0.3f);
    public Color waterColor = new Color(0.4f, 0.6f, 0.75f);
    public Color boardColor = new Color(0.5f, 0.42f, 0.33f);

    // How far out the tip of the board is, along the face
    public float PlankEndZ => (faceSize - edgeMargin * 2f) * 0.5f + boardOverhang;

    const string GeneratedName = "Generated";

    Transform root;
    Transform waterSurface;
    Vector3 waterRest;

    [ContextMenu("Generate Preview")]
    void GeneratePreview()
    {
        Generate(0);
    }

    [ContextMenu("Clear Generated")]
    public void ClearGenerated()
    {
        Transform content = GetContent();
        if (content == null) return;

        Transform old = content.Find(GeneratedName);
        if (old == null) return;

        old.name = "GeneratedOld";
        SafeDestroy(old.gameObject);
    }

    // Builds the face. The seed is ignored: this one is always the same.
    public void Generate(int seed)
    {
        Transform content = GetContent();
        if (content == null)
        {
            Debug.LogError("AwakeningGenerator: no LevelContent found under " + name);
            return;
        }

        ClearGenerated();
        root = new GameObject(GeneratedName).transform;
        root.SetParent(content, false);

        float usable = faceSize - edgeMargin * 2f;

        BuildPool(usable);
        BuildBoard(usable);
        PlaceSpawn(usable);

        // The "RUN!" and "JUMP!" prompt, and no sprinting on this level
        if (Application.isPlaying && GetComponent<RunPrompt>() == null)
            gameObject.AddComponent<RunPrompt>();
    }

    // A shallow sheet of water over a flat floor
    void BuildPool(float usable)
    {
        // The floor he actually walks on
        MakeCube("PoolFloor", new Vector3(0f, baseHeight - 0.5f, 0f),
                 new Vector3(usable, 1f, usable), floorMaterial, floorColor);

        // The water on top, see-through and non-solid so he wades through it
        GameObject water = MakeCube("Water",
                 new Vector3(0f, baseHeight + waterDepth * 0.5f, 0f),
                 new Vector3(usable, waterDepth, usable), waterMaterial, waterColor);

        SafeDestroy(water.GetComponent<Collider>());
        if (waterMaterial == null) MakeTransparent(water, waterColor, 0.55f);

        waterSurface = water.transform;
        waterRest = water.transform.localPosition;
    }

    // The board, reaching out past the edge into nothing
    void BuildBoard(float usable)
    {
        float half = usable * 0.5f;

        // Half on the face, half hanging over the drop
        float length = boardOverhang * 2f;
        float centreZ = half;

        if (boardPrefab != null)
        {
            GameObject fromPrefab = Instantiate(boardPrefab, root);
            fromPrefab.name = "DivingBoard";
            fromPrefab.transform.localPosition =
                new Vector3(0f, baseHeight + boardHeight, centreZ);
            return;
        }

        GameObject board = MakeCube("DivingBoard",
                 new Vector3(0f, baseHeight + boardHeight, centreZ),
                 new Vector3(boardWidth, boardThickness, length),
                 boardMaterial, boardColor);

        // A low rail each side, so it reads as a board rather than a plank
        MakeCube("BoardRailL",
                 new Vector3(-boardWidth * 0.5f, baseHeight + boardHeight + 0.3f, centreZ - length * 0.25f),
                 new Vector3(0.12f, 0.6f, length * 0.4f), boardMaterial, boardColor);

        MakeCube("BoardRailR",
                 new Vector3(boardWidth * 0.5f, baseHeight + boardHeight + 0.3f, centreZ - length * 0.25f),
                 new Vector3(0.12f, 0.6f, length * 0.4f), boardMaterial, boardColor);
    }

    void PlaceSpawn(float usable)
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face == null || face.spawnPoint == null) return;

        // At the far end, so the board is a walk away across the water
        face.spawnPoint.localPosition =
            new Vector3(0f, baseHeight + 0.3f, -usable * startDepth);
        face.spawnPoint.localRotation = Quaternion.identity;
    }

    void Update()
    {
        if (waterSurface == null) return;

        // A slow swell, so the pool is never quite still
        float ripple = Mathf.Sin(Time.time * rippleSpeed) * rippleHeight;
        waterSurface.localPosition = waterRest + Vector3.up * ripple;
    }


    // Makes a cube with the given size and material, parented to the root.
    GameObject MakeCube(string name, Vector3 localPos, Vector3 size,
                        Material mat, Color fallback)
    {
        // The cube is created with a default material, which we can then replace
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;

        Renderer rend = go.GetComponent<Renderer>();

        if (mat != null)
        {
            rend.sharedMaterial = mat;
        }
        else
        {
            Material copy = rend.material;
            copy.SetColor("_BaseColor", fallback);
            copy.SetColor("_Color", fallback);
        }

        return go;
    }

    // Makes a material transparent, so the water can be seen through.
    void MakeTransparent(GameObject obj, Color color, float alpha)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend == null) return;

        Material mat = rend.material;

        // Unity's standard shader has two color properties, and we need to set both.
        Color c = color;
        c.a = alpha;
        mat.SetColor("_BaseColor", c);
        mat.SetColor("_Color", c);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
    }

    // Destroys an object safely, whether in play mode or edit mode.
    void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    /// Finds the LevelContent under this face, or creates one if it doesn't exist.
    Transform GetContent()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face != null && face.levelContent != null) return face.levelContent.transform;
        return transform.Find("LevelContent");
    }
}