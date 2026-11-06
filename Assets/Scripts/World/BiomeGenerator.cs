using System.Collections.Generic;
using UnityEngine;

// Builds the Slow Wave face: a sunken sea on one side, and layered land on the
// other — sand underneath, grass on top. The player digs through the grass to
// cut a channel from the shore to the vat.
// Put this on the face instead of LevelGenerator.
[RequireComponent(typeof(CubeFace))]
public class BiomeGenerator : MonoBehaviour
{
    [Header("Theme")]
    [Tooltip("Used for this level's gravity and jump height.")]
    public LevelTheme theme;

    [Header("Seed")]
    public bool useFixedSeed = false;
    public int fixedSeed = 2024;

    [Header("Face Area")]
    public float faceSize = 30f;
    public float edgeMargin = 1f;
    [Tooltip("Height of the grass surface above the cube face.")]
    public float baseHeight = 1f;

    [Header("Terrain Layers")]
    [Tooltip("How deep the grass layer is. This is how far the player digs down.")]
    public float grassDepth = 0.8f;
    [Tooltip("How thick the sand beneath the grass is.")]
    public float sandDepth = 1.5f;

    [Header("Biome Split")]
    [Tooltip("How much of the face is land. The rest is the sea.")]
    [Range(0.4f, 0.85f)] public float landFraction = 0.62f;
    [Tooltip("How far the sea bed sits below the grass.")]
    public float seaDepth = 3f;
    [Tooltip("How far the sea surface sits below the grass. Must be deeper than Grass Depth.")]
    public float seaLevel = 0.3f;
    [Tooltip("How far the bare sand reaches inland from the shore, in squares. 0 = grass all the way.")]
    public int bareSandRows = 3;
    [Tooltip("Randomness in the grass line, so it isn't a straight edge.")]
    public int sandRowVariation = 2;

    [Header("The Spade")]
    [Tooltip("How high above the sand it sits.")]
    public float spadeHeight = 0.4f;
    [Tooltip("Keeps it this far from the vat, so it isn't found instantly.")]
    public float spadeClearance = 6f;

    [Header("Dig Grid")]
    [Tooltip("Size of one diggable square. Smaller = finer digging, more squares.")]
    public float cellSize = 2f;

    [Header("The Vat")]
    [Tooltip("How far back from the shore the vat sits, in squares.")]
    public int vatDistanceFromShore = 6;
    public float vatSize = 3f;

    [Header("Palm Trees")]
    public bool scatterPalms = true;
    public int palmCount = 14;
    public Vector2 palmHeight = new Vector2(3.5f, 5.5f);
    [Tooltip("Keeps palms this far from the vat, so they never block it.")]
    public float palmClearance = 4f;

    [Header("Rocks")]
    public bool scatterRocks = true;
    public int rockCount = 30;
    [Tooltip("Size range of the boulders. Bigger ones block more of the route.")]
    public Vector2 rockSize = new Vector2(1.2f, 2.4f);
    [Tooltip("Optional. A rock model to use instead of a block.")]
    public Material rockMaterial;

    [Header("Colours (used when no material is assigned)")]
    public Color sandColor = new Color(0.82f, 0.72f, 0.5f);
    public Color grassColor = new Color(0.38f, 0.58f, 0.3f);
    public Color waterColor = new Color(0.2f, 0.5f, 0.75f);
    public Color trunkColor = new Color(0.45f, 0.33f, 0.22f);
    public Color frondColor = new Color(0.3f, 0.52f, 0.26f);
    public Color vatColor = new Color(0.5f, 0.45f, 0.4f);

    [Header("Materials (optional)")]
    public Material sandMaterial;
    public Material grassMaterial;
    public Material waterMaterial;
    public Material trunkMaterial;
    public Material frondMaterial;
    public Material vatMaterial;
    public Material doorMaterial;

    const string GeneratedName = "Generated";

    // One material per colour, so every cube doesn't get its own copy
    Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();

    System.Random rng;
    Transform root;
    DigPuzzle puzzle;

    float usable, half, landDepth, seaWidth, shoreZ;
    int gridW, gridH;
    DigPuzzle.DigCell[,] cells;
    DigPuzzle.DigCell vatCell;

    [ContextMenu("Generate Preview")]
    void GeneratePreview()
    {
        Generate(Random.Range(int.MinValue, int.MaxValue));
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

    // Builds the level. The same seed always gives the same layout.
    public void Generate(int seed)
    {
        materialCache.Clear();
        rng = new System.Random(useFixedSeed ? fixedSeed : seed);

        Transform content = GetContent();
        if (content == null)
        {
            Debug.LogError("BiomeGenerator: no LevelContent found under " + name);
            return;
        }

        ClearGenerated();
        root = new GameObject(GeneratedName).transform;
        root.SetParent(content, false);

        // Work out where the land ends and the sea begins
        usable = faceSize - edgeMargin * 2f;
        half = usable * 0.5f;
        landDepth = usable * landFraction;
        seaWidth = usable - landDepth;
        shoreZ = -half + landDepth;

        puzzle = GetComponent<DigPuzzle>();
        if (puzzle == null) puzzle = gameObject.AddComponent<DigPuzzle>();

        // The puzzle needs to know the theme so it can set gravity and jump height
        BuildSea();
        BuildDigGrid();
        
        WaterVat vat = BuildVat();
        GameObject door = BuildDoor();

        // The vat and door are always present, so the player can always complete the puzzle
        if (scatterPalms) ScatterPalms();
        if (scatterRocks) ScatterRocks();

        // The spade is optional, but if it exists the player must find it before digging
        PlaceSpade();
        PlaceSpawn();

        puzzle.SetUp(cells, gridW, gridH, gridH - 1, vatCell, vat, door, root);
    }

    // --- Terrain ----------------------------------------------------

    void BuildSea()
    {
        float centerZ = shoreZ + seaWidth * 0.5f;
        float bedTop = baseHeight - seaDepth;

        // The sea bed, which the player can wade down onto
        MakeCube("SeaBed", new Vector3(0f, bedTop - 0.5f, centerZ),
                 new Vector3(usable, 1f, seaWidth), sandMaterial, sandColor * 0.8f);

        // The sea surface, see-through and non-solid
        GameObject water = MakeCube("Sea",
                 new Vector3(0f, baseHeight - seaLevel, centerZ),
                 new Vector3(usable, 0.12f, seaWidth), waterMaterial, waterColor);

        SafeDestroy(water.GetComponent<Collider>());
        if (waterMaterial == null) MakeTransparent(water, waterColor, 0.6f);
    }

    // The land, as a grid of squares with sand underneath and grass on top
    void BuildDigGrid()
    {
        gridW = Mathf.Max(4, Mathf.FloorToInt(usable / cellSize));
        gridH = Mathf.Max(4, Mathf.FloorToInt(landDepth / cellSize));

        float cellW = usable / gridW;
        float cellD = landDepth / gridH;

        cells = new DigPuzzle.DigCell[gridW, gridH];

        // Where the trench floor sits once the grass is dug out
        float channelFloor = baseHeight - grassDepth;

        for (int x = 0; x < gridW; x++)
        {
            for (int y = 0; y < gridH; y++)
            {
                float px = -half + cellW * (x + 0.5f);
                float pz = -half + cellD * (y + 0.5f);

                DigPuzzle.DigCell cell = new DigPuzzle.DigCell
                {
                    x = x,
                    y = y,
                    center = new Vector3(px, baseHeight, pz)
                };

                // The sand layer, which never changes
                MakeCube("Sand_" + x + "_" + y,
                    new Vector3(px, channelFloor - sandDepth * 0.5f, pz),
                    new Vector3(cellW, sandDepth, cellD),
                    sandMaterial, sandColor);

                // The grass line recedes near the shore, with a ragged edge
                int grassStartsAt = bareSandRows
                                  + rng.Next(-sandRowVariation, sandRowVariation + 1);

                bool hasGrass = y < gridH - grassStartsAt;

                if (hasGrass)
                {
                    // The grass on top, removed when the square is dug
                    cell.grass = MakeCube("Grass_" + x + "_" + y,
                        new Vector3(px, baseHeight - grassDepth * 0.5f, pz),
                        new Vector3(cellW, grassDepth, cellD),
                        grassMaterial, grassColor);
                }
                else
                {
                    // Bare sand on top, dug out just like the grass is
                    cell.grass = MakeCube("Beach_" + x + "_" + y,
                        new Vector3(px, baseHeight - grassDepth * 0.5f, pz),
                        new Vector3(cellW, grassDepth, cellD),
                        sandMaterial, sandColor);
                }

                // The water that appears in the trench once it floods
                GameObject trench = MakeCube("Water_" + x + "_" + y,
                    new Vector3(px, channelFloor + 0.2f, pz),
                    new Vector3(cellW * 0.95f, 0.4f, cellD * 0.95f),
                    waterMaterial, waterColor);

                SafeDestroy(trench.GetComponent<Collider>());
                if (waterMaterial == null) MakeTransparent(trench, waterColor, 0.7f);

                trench.SetActive(false);
                cell.water = trench.transform;

                cells[x, y] = cell;
            }
        }
    }

    // --- The vat and the door ---------------------------------------

    WaterVat BuildVat()
    {
        // Somewhere along the back of the land, a set distance from the shore
        int vx = rng.Next(1, gridW - 1);
        int vy = Mathf.Clamp(gridH - 1 - vatDistanceFromShore, 1, gridH - 2);

        vatCell = cells[vx, vy];
        vatCell.isVatCell = true;

        // The grass stays put under the vat, since it can't be dug
        Vector3 spot = vatCell.center;

        GameObject vatObj = new GameObject("WaterVat");
        vatObj.transform.SetParent(root, false);
        vatObj.transform.localPosition = spot;

        float wall = 0.3f;
        float height = 2.2f;

        // Four walls and a base, so it reads as an open container
        MakeChild(vatObj.transform, "VatBase", Vector3.up * wall * 0.5f,
                  new Vector3(vatSize, wall, vatSize), vatMaterial, vatColor);

        MakeChild(vatObj.transform, "VatWallN", new Vector3(0f, height * 0.5f, vatSize * 0.5f),
                  new Vector3(vatSize, height, wall), vatMaterial, vatColor);
        MakeChild(vatObj.transform, "VatWallS", new Vector3(0f, height * 0.5f, -vatSize * 0.5f),
                  new Vector3(vatSize, height, wall), vatMaterial, vatColor);
        MakeChild(vatObj.transform, "VatWallE", new Vector3(vatSize * 0.5f, height * 0.5f, 0f),
                  new Vector3(wall, height, vatSize), vatMaterial, vatColor);
        MakeChild(vatObj.transform, "VatWallW", new Vector3(-vatSize * 0.5f, height * 0.5f, 0f),
                  new Vector3(wall, height, vatSize), vatMaterial, vatColor);

        // The water inside, which grows upward as the vat fills
        GameObject water = MakeChild(vatObj.transform, "VatWater",
                  new Vector3(0f, height * 0.5f + wall * 0.5f, 0f),
                  new Vector3(vatSize - wall * 2f, height - wall, vatSize - wall * 2f),
                  waterMaterial, waterColor);

        SafeDestroy(water.GetComponent<Collider>());
        if (waterMaterial == null) MakeTransparent(water, waterColor, 0.75f);

        WaterVat vat = vatObj.AddComponent<WaterVat>();
        vat.SetUp(water.transform);

        return vat;
    }

    GameObject BuildDoor()
    {
        // Beside the vat, facing back down the land
        Vector3 doorPos = vatCell.center + new Vector3(0f, 1.6f, -vatSize);

        GameObject door = MakeCube("DreamDoor", doorPos,
                 new Vector3(3f, 3f, 0.5f), doorMaterial, Color.white);

        door.GetComponent<Collider>().isTrigger = true;
        door.AddComponent<DreamDoor>();

        return door;
    }

    // --- Scenery ----------------------------------------------------

    // Drops the spade somewhere on the land, away from the vat
    void PlaceSpade()
    {
        Vector3 spot = Vector3.zero;
        bool found = false;

        for (int i = 0; i < 60; i++)
        {
            Vector3 candidate = RandomLandSpot();

            // Not right next to the vat, and not on the bare beach
            if (Vector3.Distance(candidate, vatCell.center) < spadeClearance) continue;

            spot = candidate;
            found = true;
            break;
        }

        // Fall back to the middle of the land if nowhere suitable turned up
        if (!found) spot = new Vector3(0f, baseHeight, -half + landDepth * 0.5f);

        // A plain spade: a thin handle with a flat blade at the bottom
        GameObject spade = new GameObject("Spade");
        spade.transform.SetParent(root, false);
        spade.transform.localPosition = spot + Vector3.up * spadeHeight;

        MakeSpadePart(spade.transform, "Handle", PrimitiveType.Cylinder,
                      new Vector3(0f, 0.8f, 0f), new Vector3(0.08f, 0.5f, 0.08f),
                      new Color(0.45f, 0.3f, 0.15f));
        MakeSpadePart(spade.transform, "Blade", PrimitiveType.Cube,
                      new Vector3(0f, 0.2f, 0f), new Vector3(0.35f, 0.4f, 0.05f),
                      new Color(0.6f, 0.62f, 0.66f));
        spade.transform.localRotation = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f);

        // Make sure it can actually be picked up
        if (spade.GetComponent<SpadePickup>() == null)
            spade.AddComponent<SpadePickup>();

        BoxCollider col = spade.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.7f, 0f);
        col.size = new Vector3(0.6f, 1.4f, 0.6f);
        col.isTrigger = true;
    }

    void MakeSpadePart(Transform parent, string name, PrimitiveType shape,
                       Vector3 localPos, Vector3 scale, Color colour)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = scale;

        Collider c = part.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = part.GetComponent<Renderer>();
        rend.material.SetColor("_BaseColor", colour);
        rend.material.SetColor("_Color", colour);
    }
    void ScatterPalms()
    {
        int placed = 0;
        int attempts = 0;

        while (placed < palmCount && attempts < 200)
        {
            attempts++;

            Vector3 spot = RandomLandSpot();

            // Keep them clear of the vat, so it's always findable
            if (Vector3.Distance(spot, vatCell.center) < palmClearance) continue;

            BuildPalm(spot, placed);
            placed++;
        }
    }

    // A palm tree built from primitives: a leaning trunk and a crown of fronds
    void BuildPalm(Vector3 spot, int number)
    {
        GameObject palm = new GameObject("Palm_" + number);
        palm.transform.SetParent(root, false);
        palm.transform.localPosition = spot;
        palm.transform.localRotation = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f);

        float height = RandomRange(palmHeight.x, palmHeight.y);
        float lean = RandomRange(-8f, 8f);

        // The trunk, built from a few segments so it can curve as it rises
        const int segments = 4;
        float segmentHeight = height / segments;
        Vector3 position = Vector3.zero;
        float tilt = 0f;

        for (int i = 0; i < segments; i++)
        {
            tilt += lean / segments;

            float thickness = Mathf.Lerp(0.45f, 0.28f, (float)i / segments);
            position += Quaternion.Euler(tilt, 0f, 0f) * Vector3.up * segmentHeight;

            GameObject part = MakeChild(palm.transform, "Trunk_" + i,
                position - Quaternion.Euler(tilt, 0f, 0f) * Vector3.up * segmentHeight * 0.5f,
                new Vector3(thickness, segmentHeight, thickness),
                trunkMaterial, trunkColor);

            part.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            SafeDestroy(part.GetComponent<Collider>());
        }

        // The crown: fronds splayed out and drooping
        int fronds = rng.Next(6, 9);
        for (int i = 0; i < fronds; i++)
        {
            float angle = (360f / fronds) * i + RandomRange(-10f, 10f);
            float droop = RandomRange(18f, 42f);
            float length = RandomRange(1.6f, 2.4f);

            GameObject frond = MakeChild(palm.transform, "Frond_" + i,
                position, new Vector3(0.55f, 0.1f, length),
                frondMaterial, frondColor);

            // Pushed outward from the top of the trunk, angled down
            frond.transform.localRotation = Quaternion.Euler(droop, angle, 0f);
            frond.transform.localPosition = position
                + frond.transform.localRotation * Vector3.forward * length * 0.5f;

            SafeDestroy(frond.GetComponent<Collider>());
        }

        // A couple of coconuts under the crown
        int coconuts = rng.Next(0, 3);
        for (int i = 0; i < coconuts; i++)
        {
            float angle = RandomRange(0f, 360f) * Mathf.Deg2Rad;

            GameObject nut = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nut.name = "Coconut_" + i;
            nut.transform.SetParent(palm.transform, false);
            nut.transform.localPosition = position
                + new Vector3(Mathf.Cos(angle) * 0.3f, -0.3f, Mathf.Sin(angle) * 0.3f);
            nut.transform.localScale = Vector3.one * 0.28f;

            SafeDestroy(nut.GetComponent<Collider>());
            Tint(nut, trunkMaterial, trunkColor * 0.7f);
        }
    }

    void ScatterRocks()
    {
        for (int i = 0; i < rockCount; i++)
        {
            Vector3 spot = RandomLandSpot();
            if (Vector3.Distance(spot, vatCell.center) < 3f) continue;

            // Boulders block the way, so the channel has to go around them
            float size = RandomRange(rockSize.x, rockSize.y);

            GameObject rock = MakeCube("Rock_" + i,
                spot + Vector3.up * size * 0.5f,
                new Vector3(size, size * RandomRange(0.7f, 1.2f), size * RandomRange(0.8f, 1.3f)),
                rockMaterial != null ? rockMaterial : sandMaterial, sandColor * 0.6f);

            rock.transform.localRotation = Quaternion.Euler(
                RandomRange(-12f, 12f), RandomRange(0f, 360f), RandomRange(-12f, 12f));

            // Keep the collider: these are obstacles, not scenery
            BlockCellUnder(spot);
        }
    }

    // Marks the square under a rock as undiggable, so channels must route around
    void BlockCellUnder(Vector3 localSpot)
    {
        float cellW = usable / gridW;
        float cellD = landDepth / gridH;

        int x = Mathf.FloorToInt((localSpot.x + half) / cellW);
        int y = Mathf.FloorToInt((localSpot.z + half) / cellD);

        if (x < 0 || x >= gridW || y < 0 || y >= gridH) return;

        DigPuzzle.DigCell cell = cells[x, y];
        if (cell == null || cell.isVatCell) return;

        cell.blocked = true;
    }

    void PlaceSpawn()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face == null || face.spawnPoint == null) return;

        // At the back of the land, so the sea is ahead of him
        face.spawnPoint.localPosition = new Vector3(0f, baseHeight + 0.3f, -half + 2.5f);
        face.spawnPoint.localRotation = Quaternion.identity;
    }

    // --- Helpers ----------------------------------------------------

    Vector3 RandomLandSpot()
    {
        float x = RandomRange(-half + 1.5f, half - 1.5f);
        float z = RandomRange(-half + 1.5f, shoreZ - 1.5f);
        return new Vector3(x, baseHeight, z);
    }

    GameObject MakeCube(string name, Vector3 localPos, Vector3 size,
                        Material mat, Color fallback)
    {
        return MakeChild(root, name, localPos, size, mat, fallback);
    }

    GameObject MakeChild(Transform parent, string name, Vector3 localPos,
                         Vector3 size, Material mat, Color fallback)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;

        Tint(go, mat, fallback);
        return go;
    }

    // Uses the assigned material, or a shared tinted one when none is set
    void Tint(GameObject go, Material mat, Color fallback)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend == null) return;

        rend.sharedMaterial = mat != null ? mat : GetTinted(fallback, 1f);
    }

    // Switches an object to a shared see-through material
    void MakeTransparent(GameObject obj, Color color, float alpha)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend == null) return;

        rend.sharedMaterial = GetTinted(color, alpha);
    }

    // Builds one material per colour and reuses it, rather than copying
    // a material onto every single cube
    Material GetTinted(Color color, float alpha)
    {
        Color key = new Color(color.r, color.g, color.b, alpha);
        if (materialCache.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        // Shader.Find fails in builds unless the shader is referenced by an asset,
        // so fall back to the one URP always includes
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.name = "Biome_" + ColorUtility.ToHtmlStringRGBA(key);

        // DontSave alone keeps it out of the saved scene but lets it survive
        // leaving Play mode, so the preview doesn't turn magenta
        // Nothing special: an ordinary material that works in editor and build alike
        mat.hideFlags = HideFlags.None;

        Color c = key;
        mat.SetColor("_BaseColor", c);
        mat.SetColor("_Color", c);

        // Only switch to transparent mode when it actually needs to be
        if (alpha < 0.999f)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
        }

        materialCache[key] = mat;
        return mat;
    }

    float RandomRange(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)rng.NextDouble());
    }

    void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    Transform GetContent()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face != null && face.levelContent != null) return face.levelContent.transform;
        return transform.Find("LevelContent");
    }
}