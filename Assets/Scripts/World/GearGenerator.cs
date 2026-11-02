using System.Collections.Generic;
using UnityEngine;

// Builds the Function face: a workshop floor with a gear machine at one end.
// A motor gear and an output gear sit either side of a row of empty pegs, and the
// right gears are lying about the room. Carry them to the pegs so the chain meshes,
// and the cage around the door of light sinks away.
// The sizes are picked so the puzzle can always be solved, with one spare gear as a decoy.
// Put this on the face instead of LevelGenerator.
[RequireComponent(typeof(CubeFace))]
public class GearGenerator : MonoBehaviour
{
    [Header("Theme")]
    [Tooltip("Used for this level's gravity and jump height.")]
    public LevelTheme theme;

    [Header("Seed")]
    public bool useFixedSeed = false;
    public int fixedSeed = 4242;

    [Header("Face Area")]
    public float faceSize = 30f;
    public float edgeMargin = 1f;
    [Tooltip("Height of the floor surface above the cube face.")]
    public float floorHeight = 0.5f;
    [Tooltip("Low walls round the edge, so the player can't walk off the face. 0 for none.")]
    public float borderHeight = 1.2f;

    [Header("The Machine")]
    [Tooltip("How many pegs sit between the motor and the output gear.")]
    [Range(2, 4)] public int pegCount = 3;
    [Tooltip("The gear sizes that can turn up. Each is the radius at the teeth.")]
    public float[] gearRadii = { 1f, 1.5f, 2f };
    [Tooltip("Radius of the fixed motor gear and the fixed output gear.")]
    public float motorRadius = 1.5f;
    public float outputRadius = 1.5f;
    [Tooltip("How far the machine sits from the far wall.")]
    public float machineDistanceFromWall = 10f;
    public float gearThickness = 0.3f;
    [Tooltip("How high a gear sits above the floor when it's on a peg.")]
    public float pegGearHeight = 0.5f;
    public float pegHeight = 0.7f;

    [Header("Gear Model (optional)")]
    [Tooltip("Your own gear model. Leave empty to use the built-in gear. It should lie flat, with its axle pointing up.")]
    public GameObject gearPrefab;
    [Tooltip("The model's radius to the tips of its teeth, at scale 1. Every gear is scaled from this, so measure it if the teeth don't line up.")]
    public float gearPrefabRadius = 1f;

    [Header("Loose Gears")]
    [Tooltip("Extra gears with no peg to call their own. They can be swapped in, but can't be relied on.")]
    public int decoyCount = 4;
    [Tooltip("How far a near-miss decoy is off from a real size. Too small and it would mesh anyway.")]
    public float decoyOffset = 0.25f;
    [Tooltip("Keeps loose gears at least this far apart, so they don't pile up.")]
    public float gearSpacing = 4f;
    [Tooltip("How high a loose gear floats above the floor.")]
    public float looseGearHeight = 0.7f;

    [Header("The Cage")]
    [Tooltip("How far back from the far wall the door of light stands.")]
    public float doorDistanceFromWall = 3.5f;
    public float cageSize = 5f;
    public float cageHeight = 4f;
    public int barsPerSide = 6;
    public float barThickness = 0.22f;

    [Header("Pillars")]
    public bool scatterPillars = true;
    public int pillarCount = 8;
    public Vector2 pillarSize = new Vector2(1.2f, 1.8f);
    public float pillarHeight = 3f;

    [Header("Atmosphere")]
    [Tooltip("Adds the steampunk lamps, steam vents, chimney smoke and pipes. Tune them on the GearAtmosphere component.")]
    public bool addAtmosphere = true;

    [Header("Sounds")]
    [Tooltip("Fitting a gear onto a peg. Also played, quietly, when one is put on the floor.")]
    public AudioClip placeClip;
    [Tooltip("A new gear joins the chain.")]
    public AudioClip meshClip;
    [Tooltip("The whole chain meshes and the machine starts.")]
    public AudioClip startClip;
    [Tooltip("The cage sinking into the floor.")]
    public AudioClip cageClip;
    [Tooltip("A puff of steam from a vent.")]
    public AudioClip steamClip;
    [Range(0f, 1f)] public float steamVolume = 0.6f;

    [Header("Colours (used when no material is assigned)")]
    public Color floorColor = new Color(0.28f, 0.27f, 0.3f);
    public Color wallColor = new Color(0.2f, 0.2f, 0.24f);
    public Color pillarColor = new Color(0.34f, 0.32f, 0.36f);
    public Color pegColor = new Color(0.45f, 0.42f, 0.4f);
    public Color motorColor = new Color(0.75f, 0.45f, 0.2f);
    public Color outputColor = new Color(0.3f, 0.65f, 0.75f);
    public Color looseGearColor = new Color(0.75f, 0.65f, 0.3f);
    public Color cageColor = new Color(0.15f, 0.15f, 0.17f);

    [Header("Materials (optional)")]
    public Material floorMaterial;
    public Material wallMaterial;
    public Material gearMaterial;
    public Material doorMaterial;

    const string GeneratedName = "Generated";

    // One material per colour, so every cube doesn't get its own copy
    Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();

    System.Random rng;
    Transform root;
    GearPuzzle puzzle;

    float usable, half, machineZ, doorZ;

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
            Debug.LogError("GearGenerator: no LevelContent found under " + name);
            return;
        }

        ClearGenerated();
        root = new GameObject(GeneratedName).transform;
        root.SetParent(content, false);

        pillarSpots.Clear();

        usable = faceSize - edgeMargin * 2f;
        half = usable * 0.5f;
        machineZ = half - machineDistanceFromWall;
        doorZ = half - doorDistanceFromWall;

        BuildFloor();
        if (borderHeight > 0f) BuildBorder();

        // The puzzle does the picking up and placing, the generator only builds the room
        puzzle = GetComponent<GearPuzzle>();
        if (puzzle == null) puzzle = gameObject.AddComponent<GearPuzzle>();

        puzzle.placeClip = placeClip;
        puzzle.meshClip = meshClip;
        puzzle.startClip = startClip;
        puzzle.cageClip = cageClip;

        // Pick the sizes first. The machine is built around them, so it always works.
        float[] needed = PickGearSizes();

        GearPiece motor;
        GearPiece output;
        List<GearPeg> pegs = BuildMachine(needed, out motor, out output);

        Transform cage;
        BuildDoorAndCage(out cage);

        if (scatterPillars) ScatterPillars();

        List<GearPiece> loose = ScatterGears(needed);
        PlaceSpawn();

        // Lamps, steam and smoke, so it feels like a working factory
        if (addAtmosphere)
        {
            GearAtmosphere atmosphere = GetComponent<GearAtmosphere>();
            if (atmosphere == null) atmosphere = gameObject.AddComponent<GearAtmosphere>();

            atmosphere.steamClip = steamClip;
            atmosphere.steamVolume = steamVolume;

            atmosphere.Build(root, floorHeight, half, machineZ,
                             motor.transform.localPosition, output.transform.localPosition,
                             pillarSpots, floorHeight + pillarHeight);
        }

        float cageDrop = cageHeight + 0.5f;
        puzzle.SetUp(motor, pegs, output, loose, cage, cageDrop,
                     root, pegGearHeight, looseGearHeight);
    }

    // --- Machine ----------------------------------------------------

    // The sizes the pegs need, one per peg
    float[] PickGearSizes()
    {
        float[] sizes = new float[pegCount];

        for (int i = 0; i < pegCount; i++)
            sizes[i] = gearRadii[rng.Next(0, gearRadii.Length)];

        return sizes;
    }

    // Lays the motor, pegs and output gear in a row, each exactly one gear-width apart
    List<GearPeg> BuildMachine(float[] needed, out GearPiece motor, out GearPiece output)
    {
        // Work out the whole length first, so the row can be centred
        float length = motorRadius + outputRadius;
        for (int i = 0; i < needed.Length; i++) length += needed[i] * 2f;

        float x = -length * 0.5f + motorRadius;
        float y = floorHeight;

        motor = BuildGear("MotorGear", motorRadius, motorColor, new Vector3(x, y + pegGearHeight, machineZ));
        BuildPegPost("MotorPost", new Vector3(x, y, machineZ), motorRadius);

        List<GearPeg> pegs = new List<GearPeg>();
        float previousRadius = motorRadius;

        for (int i = 0; i < needed.Length; i++)
        {
            // Touching the one before, so the gear that fits is the one that meshes
            x += previousRadius + needed[i];
            previousRadius = needed[i];

            Vector3 pegPos = new Vector3(x, y, machineZ);
            GearPeg peg = BuildPegPost("Peg_" + i, pegPos, needed[i]).GetComponent<GearPeg>();
            peg.requiredRadius = needed[i];
            pegs.Add(peg);
        }

        x += previousRadius + outputRadius;
        output = BuildGear("OutputGear", outputRadius, outputColor, new Vector3(x, y + pegGearHeight, machineZ));
        BuildPegPost("OutputPost", new Vector3(x, y, machineZ), outputRadius);

        return pegs;
    }

    // A short post for a gear to sit on, with a ring on the floor showing how wide the gear is
    GameObject BuildPegPost(string name, Vector3 floorPos, float gearRadius)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = name;
        post.transform.SetParent(root, false);
        post.transform.localPosition = floorPos + Vector3.up * (pegHeight * 0.5f);
        post.transform.localScale = new Vector3(0.35f, pegHeight * 0.5f, 0.35f);
        Tint(post, null, pegColor);
        SafeDestroy(post.GetComponent<Collider>());
        post.AddComponent<GearPeg>();

        // The ring is flat on the floor, so the player can see how big a gear it wants
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = name + "_Ring";
        ring.transform.SetParent(root, false);
        ring.transform.localPosition = floorPos + Vector3.up * 0.02f;
        ring.transform.localScale = new Vector3(gearRadius * 2f, 0.02f, gearRadius * 2f);
        Tint(ring, null, pegColor * 0.7f);
        SafeDestroy(ring.GetComponent<Collider>());

        return post;
    }

    // --- Gears ------------------------------------------------------

    // A toothed disc with a GearPiece on it, sized by its radius
    GearPiece BuildGear(string name, float radius, Color color, Vector3 localPos)
    {
        GameObject gear = new GameObject(name);
        gear.transform.SetParent(root, false);
        gear.transform.localPosition = localPos;

        // A model, if one has been given, instead of the built-in disc and teeth
        if (gearPrefab != null)
        {
            GameObject model = Instantiate(gearPrefab, gear.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Sized so the teeth reach exactly this radius, which is what makes them mesh
            model.transform.localScale = Vector3.one * (radius / Mathf.Max(0.01f, gearPrefabRadius));

            // Gears are never solid, so nothing catches the player or blocks the pegs
            foreach (Collider col in model.GetComponentsInChildren<Collider>())
                SafeDestroy(col);

            GearPiece modelPiece = gear.AddComponent<GearPiece>();
            modelPiece.radius = radius;
            return modelPiece;
        }

        float toothDepth = 0.3f;
        float bodyRadius = Mathf.Max(0.2f, radius - toothDepth);

        // The disc
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(gear.transform, false);
        body.transform.localScale = new Vector3(bodyRadius * 2f, gearThickness * 0.5f, bodyRadius * 2f);
        Tint(body, gearMaterial, color);
        SafeDestroy(body.GetComponent<Collider>());

        // The teeth, evenly round the edge
        int teeth = Mathf.Max(6, Mathf.RoundToInt(radius * 8f));

        for (int i = 0; i < teeth; i++)
        {
            float angle = 360f / teeth * i;

            GameObject tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tooth.name = "Tooth_" + i;
            tooth.transform.SetParent(gear.transform, false);
            tooth.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            tooth.transform.localPosition = tooth.transform.localRotation * Vector3.forward * (radius - toothDepth * 0.5f);
            tooth.transform.localScale = new Vector3(0.3f, gearThickness, toothDepth);
            Tint(tooth, gearMaterial, color);
            SafeDestroy(tooth.GetComponent<Collider>());
        }

        GearPiece piece = gear.AddComponent<GearPiece>();
        piece.radius = radius;
        return piece;
    }

    // Scatters the gears the machine needs, plus the decoys, around the room
    List<GearPiece> ScatterGears(float[] needed)
    {
        List<GearPiece> loose = new List<GearPiece>();
        List<Vector3> taken = new List<Vector3>();

        // Everything the machine needs, then the spares
        List<float> radii = new List<float>(needed);

        for (int i = 0; i < decoyCount; i++)
            radii.Add(PickDecoyRadius(needed, i));

        for (int i = 0; i < radii.Count; i++)
        {
            Vector3 spot = FindGearSpot(taken);
            taken.Add(spot);

            GearPiece gear = BuildGear("Gear_" + i, radii[i], looseGearColor, spot);
            gear.SetLoose(true);
            loose.Add(gear);
        }

        return loose;
    }

    // A decoy that looks right at a glance. Every other one is a spare of a real size,
    // and the rest are a touch too big or too small, so they don't quite mesh.
    float PickDecoyRadius(float[] needed, int index)
    {
        float basis = needed[rng.Next(0, needed.Length)];

        if (index % 2 == 0) return basis;

        float sign = rng.Next(0, 2) == 0 ? -1f : 1f;
        float radius = Mathf.Max(0.6f, basis + sign * decoyOffset);

        // Never so close to a real size that it would mesh anyway
        foreach (float real in gearRadii)
            if (Mathf.Abs(radius - real) < 0.15f) return basis + decoyOffset * 2f;

        return radius;
    }

    // A spot in the room clear of the machine, the cage, the player's start and other gears
    Vector3 FindGearSpot(List<Vector3> taken)
    {
        Vector3 best = new Vector3(0f, floorHeight + looseGearHeight, -half + 6f);

        for (int attempt = 0; attempt < 80; attempt++)
        {
            float px = RandomRange(-half + 3f, half - 3f);
            float pz = RandomRange(-half + 5f, machineZ - 3.5f);
            Vector3 spot = new Vector3(px, floorHeight + looseGearHeight, pz);

            bool clear = true;

            foreach (Vector3 other in taken)
            {
                if (Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(other.x, other.z)) < gearSpacing)
                {
                    clear = false;
                    break;
                }
            }

            if (clear && !NearPillar(spot, 3f)) return spot;
            best = spot;
        }

        // Crowded, so settle for the last spot tried
        return best;
    }

    // --- Door -------------------------------------------------------

    // The door of light, with a cage round it until the machine runs
    void BuildDoorAndCage(out Transform cage)
    {
        Vector3 doorPos = new Vector3(0f, floorHeight + 1.6f, doorZ);

        GameObject door = MakeCube("DreamDoor", doorPos, new Vector3(3f, 3f, 0.5f), doorMaterial, Color.white);
        door.GetComponent<Collider>().isTrigger = true;
        door.AddComponent<DreamDoor>();

        // The cage is four sides of bars, with a solid collider across each
        GameObject cageObj = new GameObject("Cage");
        cageObj.transform.SetParent(root, false);
        cageObj.transform.localPosition = new Vector3(0f, floorHeight, doorZ);
        cage = cageObj.transform;

        float reach = cageSize * 0.5f;

        BuildCageSide(cage, "Front", new Vector3(0f, 0f, -reach), false);
        BuildCageSide(cage, "Back", new Vector3(0f, 0f, reach), false);
        BuildCageSide(cage, "Left", new Vector3(-reach, 0f, 0f), true);
        BuildCageSide(cage, "Right", new Vector3(reach, 0f, 0f), true);
    }

    void BuildCageSide(Transform parent, string name, Vector3 offset, bool alongZ)
    {
        GameObject side = new GameObject("Cage_" + name);
        side.transform.SetParent(parent, false);
        side.transform.localPosition = offset;

        // One solid collider across the whole side, so nothing slips between the bars
        BoxCollider box = side.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, cageHeight * 0.5f, 0f);
        box.size = alongZ
            ? new Vector3(0.4f, cageHeight, cageSize)
            : new Vector3(cageSize, cageHeight, 0.4f);

        int bars = Mathf.Max(2, barsPerSide);

        for (int i = 0; i < bars; i++)
        {
            float t = bars > 1 ? (float)i / (bars - 1) : 0.5f;
            float along = Mathf.Lerp(-cageSize * 0.5f, cageSize * 0.5f, t);

            Vector3 pos = alongZ ? new Vector3(0f, cageHeight * 0.5f, along)
                                 : new Vector3(along, cageHeight * 0.5f, 0f);

            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar_" + i;
            bar.transform.SetParent(side.transform, false);
            bar.transform.localPosition = pos;
            bar.transform.localScale = new Vector3(barThickness, cageHeight, barThickness);
            Tint(bar, null, cageColor);
            SafeDestroy(bar.GetComponent<Collider>());
        }
    }

    // --- Scenery ----------------------------------------------------

    void BuildFloor()
    {
        MakeCube("Floor", new Vector3(0f, floorHeight - 0.5f, 0f),
                 new Vector3(usable, 1f, usable), floorMaterial, floorColor);
    }

    // A low wall round the edge
    void BuildBorder()
    {
        float y = floorHeight + borderHeight * 0.5f;
        float wall = 0.5f;

        MakeCube("Border_N", new Vector3(0f, y, half + wall * 0.5f), new Vector3(usable + wall * 2f, borderHeight, wall), wallMaterial, wallColor);
        MakeCube("Border_S", new Vector3(0f, y, -half - wall * 0.5f), new Vector3(usable + wall * 2f, borderHeight, wall), wallMaterial, wallColor);
        MakeCube("Border_E", new Vector3(half + wall * 0.5f, y, 0f), new Vector3(wall, borderHeight, usable), wallMaterial, wallColor);
        MakeCube("Border_W", new Vector3(-half - wall * 0.5f, y, 0f), new Vector3(wall, borderHeight, usable), wallMaterial, wallColor);
    }

    List<Vector3> pillarSpots = new List<Vector3>();

    // Pillars to walk round. They keep clear of the machine, the cage and the start.
    void ScatterPillars()
    {
        pillarSpots.Clear();

        for (int i = 0; i < pillarCount; i++)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float px = RandomRange(-half + 2f, half - 2f);
                float pz = RandomRange(-half + 5f, half - 2f);

                // Clear of the machine row, and the door and its cage
                if (Mathf.Abs(pz - machineZ) < 4f) continue;
                if (pz > machineZ && Mathf.Abs(px) < cageSize) continue;
                if (NearPillar(new Vector3(px, 0f, pz), 4f)) continue;

                float size = RandomRange(pillarSize.x, pillarSize.y);

                MakeCube("Pillar_" + i,
                    new Vector3(px, floorHeight + pillarHeight * 0.5f, pz),
                    new Vector3(size, pillarHeight, size), wallMaterial, pillarColor);

                pillarSpots.Add(new Vector3(px, 0f, pz));
                break;
            }
        }
    }

    bool NearPillar(Vector3 spot, float distance)
    {
        foreach (Vector3 pillar in pillarSpots)
            if (Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(pillar.x, pillar.z)) < distance)
                return true;

        return false;
    }

    // At the near end, so the machine is across the room from him
    void PlaceSpawn()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face == null || face.spawnPoint == null) return;

        face.spawnPoint.localPosition = new Vector3(0f, floorHeight + 0.3f, -half + 2.5f);
        face.spawnPoint.localRotation = Quaternion.identity;
    }

    // --- Helpers ----------------------------------------------------

    GameObject MakeCube(string name, Vector3 localPos, Vector3 size, Material mat, Color fallback)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
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

        rend.sharedMaterial = mat != null ? mat : GetTinted(fallback);
    }

    // Builds one material per colour and reuses it, rather than copying
    // a material onto every single cube
    Material GetTinted(Color color)
    {
        if (materialCache.TryGetValue(color, out Material cached) && cached != null)
            return cached;

        // Shader.Find fails in builds unless the shader is referenced by an asset,
        // so fall back to the one URP always includes
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.name = "Gear_" + ColorUtility.ToHtmlStringRGBA(color);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);

        materialCache[color] = mat;
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
