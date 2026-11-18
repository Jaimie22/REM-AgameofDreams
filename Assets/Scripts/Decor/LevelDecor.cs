using System.Collections.Generic;
using UnityEngine;

// Dresses each level with props, particle effects, enemy swaps and the finished-level glow.
public class LevelDecor : MonoBehaviour
{
    [Tooltip("Prop prefabs, looked up by name.")]
    public GameObject[] prefabs;
    [Tooltip("Particle materials, e.g. P_Mist.")]
    public Material[] materials;

    [Header("Enemies")]
    [Tooltip("Stalker prefab for the Level 6 chase.")]
    public GameObject stalkerPrefab;
    [Tooltip("Spider prefab for the floor under the Level 5 islands.")]
    public GameObject crawlerPrefab;

    [Header("Options")]
    public bool spawnProps = true;
    public bool spawnEffects = true;
    [Tooltip("Give dead trees and big rocks a collider.")]
    public bool solidBigProps = false;
    [Tooltip("Half-width of the area used on each face.")]
    public float halfSize = 13.5f;
    public int seed = 7;
    [Tooltip("Purple glow outline round finished levels in the cube view.")]
    public bool showFinishedGlow = true;

    // The Generated object each face was last dressed for.
    readonly Dictionary<CubeFace, Transform> handled = new Dictionary<CubeFace, Transform>();
    float nextScan;

    // Level pieces, measured from their meshes.
    struct Box { public Transform t; public Bounds b; public Transform piece; public bool ground; }
    readonly List<Box> boxes = new List<Box>();
    readonly List<Vector4> placed = new List<Vector4>();     // xyz = position, w = radius of props already placed
    Transform lastPiece;
    readonly List<GameObject> pendingFx = new List<GameObject>();
    Mesh slabMesh, cloudMesh;

    readonly Dictionary<CubeFace, Transform> outlines = new Dictionary<CubeFace, Transform>();

    void Awake()
    {
        // Crystal pillars use the crystal mesh instead of cubes.
        GameObject pillar = Prefab("Crystal_Pillar");
        MeshFilter mf = pillar != null ? pillar.GetComponentInChildren<MeshFilter>(true) : null;
        if (mf != null && mf.sharedMesh != null) CrystalKit.PillarMesh = UnitMesh(mf.sharedMesh);

        GameObject slab = Prefab("Slab_Medium");
        mf = slab != null ? slab.GetComponentInChildren<MeshFilter>(true) : null;
        if (mf != null && mf.sharedMesh != null) slabMesh = UnitMesh(mf.sharedMesh);

        GameObject cloud = Prefab("Cloud_Puff");
        mf = cloud != null ? cloud.GetComponentInChildren<MeshFilter>(true) : null;
        if (mf != null && mf.sharedMesh != null) cloudMesh = UnitMesh(mf.sharedMesh);

        SwapEnemies();
    }

    // Swap in spiders (Level 5) and stalkers (Level 6).
    void SwapEnemies()
    {
        if (crawlerPrefab != null)
            foreach (PlatformGenerator g in FindObjectsByType<PlatformGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                g.demonPrefab = crawlerPrefab;

        if (stalkerPrefab != null)
            foreach (OpenHunterSpawner sp in FindObjectsByType<OpenHunterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sp.hunterPrefab = stalkerPrefab;
                sp.standHeight = 1.03f;      // pivot is at the feet, floor is 1 up
            }
    }

    void OnDestroy()
    {
        CrystalKit.PillarMesh = null;
        if (outlineMat != null) Destroy(outlineMat);
        if (haloMat != null) Destroy(haloMat);
    }

    // Copy of a model mesh scaled to fit a unit cube.
    static Mesh UnitMesh(Mesh src)
    {
        Bounds b = src.bounds;
        Vector3 size = new Vector3(Mathf.Max(b.size.x, 0.001f), Mathf.Max(b.size.y, 0.001f), Mathf.Max(b.size.z, 0.001f));

        Vector3[] v = src.vertices;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = v[i] - b.center;
            v[i] = new Vector3(p.x / size.x, p.y / size.y, p.z / size.z);
        }

        Mesh m = new Mesh { name = src.name + "_unit" };
        m.vertices = v;
        m.normals = src.normals;
        m.uv = src.uv;
        m.subMeshCount = src.subMeshCount;
        for (int i = 0; i < src.subMeshCount; i++) m.SetTriangles(src.GetTriangles(i), i);
        m.RecalculateBounds();
        return m;
    }

    void Update()
    {
        if (showFinishedGlow) UpdateFinishedGlow();

        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.faces == null) return;

        if (Time.time < nextScan) return;
        nextScan = Time.time + 0.1f;

        // Gives a newly built level its shapes, props and effects.
        for (int i = 0; i < world.faces.Length; i++)
        {
            CubeFace f = world.faces[i];
            if (f == null || f.levelContent == null) continue;

            Transform generated = f.levelContent.transform.Find("Generated");
            if (generated == null || generated.childCount == 0) continue;

            handled.TryGetValue(f, out Transform before);
            if (before == generated) continue;
            handled[f] = generated;

            Skin(f, generated);
            Dress(f, i, generated);
        }
    }

    // Swaps cube slabs and clouds for their meshes, then separates overlapping platforms.
    void Skin(CubeFace face, Transform generated)
    {
        Mesh mesh = null;
        string prefix = null;
        if (face.GetComponent<PlatformGenerator>() != null) { mesh = cloudMesh; prefix = "CloudPlatform"; }
        else if (face.GetComponent<LevelGenerator>() != null) { mesh = slabMesh; prefix = "Slab"; }
        if (prefix == null) return;

        var pieces = new List<Transform>();
        foreach (MeshFilter mf in generated.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.sharedMesh.name != "Cube") continue;
            Renderer r = mf.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) continue;
            if (!r.sharedMaterial.name.StartsWith(prefix)) continue;
            if (mesh != null) mf.sharedMesh = mesh;
            if (!r.sharedMaterial.name.Contains("Moving")) pieces.Add(mf.transform);     // moving pieces are skipped
        }

        SeparatePlatforms(pieces);
    }

    // Pulls overlapping platforms apart by shrinking the lower one along the shortest overlap.
    static Bounds BoundsOf(Transform t)
    {
        Vector3 s = t.lossyScale;
        return new Bounds(t.position, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
    }

    static void SeparatePlatforms(List<Transform> pieces)
    {
        const float gap = 0.08f;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                for (int j = i + 1; j < pieces.Count; j++)
                {
                    if (pieces[j] == null) continue;

                    Bounds ba = BoundsOf(pieces[i]), bb = BoundsOf(pieces[j]);
                    Vector3 ov = new Vector3(
                        Mathf.Min(ba.max.x, bb.max.x) - Mathf.Max(ba.min.x, bb.min.x),
                        Mathf.Min(ba.max.y, bb.max.y) - Mathf.Max(ba.min.y, bb.min.y),
                        Mathf.Min(ba.max.z, bb.max.z) - Mathf.Max(ba.min.z, bb.min.z));
                    if (ov.x <= 0.01f || ov.y <= 0.01f || ov.z <= 0.01f) continue;

                    // Shrink the lower piece along the axis with least overlap.
                    bool aLower = ba.max.y <= bb.max.y;
                    Transform lower = aLower ? pieces[i] : pieces[j];
                    Bounds bl = aLower ? ba : bb;
                    Bounds bo = aLower ? bb : ba;
                    bool useX = ov.x <= ov.z;
                    float cut = (useX ? ov.x : ov.z) + gap;

                    Vector3 sc = lower.localScale;
                    float world = useX ? bl.size.x : bl.size.z;
                    float factor = Mathf.Clamp((world - cut) / Mathf.Max(world, 0.01f), 0.3f, 1f);
                    if (useX) sc.x *= factor; else sc.z *= factor;

                    // Keep the far edge fixed.
                    float dir = useX ? Mathf.Sign(bl.center.x - bo.center.x) : Mathf.Sign(bl.center.z - bo.center.z);
                    if (dir == 0f) dir = 1f;
                    Vector3 pos = lower.position;
                    if (useX) pos.x += dir * (world - world * factor) * 0.5f;
                    else pos.z += dir * (world - world * factor) * 0.5f;

                    lower.localScale = sc;
                    lower.position = pos;
                }
            }
        }
    }

    // Purple outline round a finished level, replacing the floating crystal.
    Material outlineMat, haloMat;
    float faceSize = -1f;

    static Material GlowMaterial(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh) { name = "FinishedGlow" };
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);                                   // additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
        }
        m.renderQueue = 3100;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        return m;
    }

    static void SetAlpha(Material m, Color c, float a)
    {
        c.a = a;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
    }

    // Four thin bars along the face edges.
    Transform BuildOutline(CubeFace face, float size)
    {
        if (outlineMat == null) outlineMat = GlowMaterial(new Color(0.68f, 0.38f, 1f, 1f));
        if (haloMat == null) haloMat = GlowMaterial(new Color(0.55f, 0.25f, 1f, 1f));

        GameObject root = new GameObject("FinishedOutline");
        root.transform.SetParent(face.transform, false);

        float half = size * 0.5f;
        AddFrame(root.transform, half, 0.35f, 0.07f, outlineMat, "Line");
        AddFrame(root.transform, half, 1.6f, 0.05f, haloMat, "Halo");
        return root.transform;
    }

    static void AddFrame(Transform parent, float half, float width, float y, Material mat, string name)
    {
        float inset = width * 0.5f;
        float len = half * 2f;
        Vector3[] pos = { new Vector3(0, y, half - inset), new Vector3(0, y, -half + inset),
                          new Vector3(half - inset, y, 0), new Vector3(-half + inset, y, 0) };
        Vector3[] size = { new Vector3(len, 0.02f, width), new Vector3(len, 0.02f, width),
                           new Vector3(width, 0.02f, len - width * 2f), new Vector3(width, 0.02f, len - width * 2f) };

        for (int i = 0; i < 4; i++)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name + i;
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = pos[i];
            bar.transform.localScale = size[i];
            Renderer r = bar.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    void UpdateFinishedGlow()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.faces == null) return;

        if (faceSize < 0f)
        {
            DreamCubeBuilder builder = FindFirstObjectByType<DreamCubeBuilder>();
            faceSize = builder != null ? builder.cubeSize : 20f;
        }

        float t = Time.unscaledTime;
        foreach (CubeFace f in world.faces)
        {
            if (f == null) continue;
            Transform crystal = f.transform.Find("CompletedCrystal");
            if (crystal == null) continue;

            MeshRenderer own = crystal.GetComponent<MeshRenderer>();
            if (own != null) own.enabled = false;                     // no more floating crystal

            outlines.TryGetValue(f, out Transform outline);
            if (outline == null)
            {
                outline = BuildOutline(f, faceSize);
                outlines[f] = outline;
            }

            // Grow the crystal as it fades in, switch it off when hidden.
            float amount = crystal.gameObject.activeSelf ? Mathf.Clamp01(crystal.localScale.x / 2f) : 0f;
            outline.gameObject.SetActive(amount > 0.001f);
            if (amount <= 0.001f) continue;

            float pulse = 0.78f + 0.22f * Mathf.Sin(t * 2.2f);
            SetAlpha(outlineMat, new Color(0.68f, 0.38f, 1f), amount * pulse);
            SetAlpha(haloMat, new Color(0.55f, 0.25f, 1f), amount * pulse * 0.28f);
        }
    }

    // Places props and effects for a newly built level.
    void Dress(CubeFace face, int index, Transform generated)
    {
        Transform content = face.levelContent.transform;
        Transform old = content.Find("Decor");
        if (old != null) { old.name = "DecorOld"; old.gameObject.SetActive(false); Destroy(old.gameObject); }

        GameObject root = new GameObject("Decor");
        root.transform.SetParent(content, false);

        CollectBoxes(face, generated);
        placed.Clear();
        pendingFx.Clear();

        var rng = new System.Random(seed * 1000 + index * 17 + 3);

        if (face.GetComponent<LevelGenerator>() != null) GateFragments(face, generated);

        if (face.GetComponent<BiomeGenerator>() != null) DressBeach(face, root.transform, rng);
        else if (face.GetComponent<MazeGenerator>() != null) DressMaze(face, root.transform, rng);
        else if (face.GetComponent<GearGenerator>() != null) DressGears(face, root.transform, rng);
        else if (face.GetComponent<PlatformGenerator>() != null) DressClouds(face, root.transform, rng);
        else if (face.GetComponent<AwakeningGenerator>() != null) DressBedroom(face, root.transform, rng);
        else DressUnconscious(face, root.transform, rng);

        // Effects start when the first piece appears.
        if (pendingFx.Count > 0)
        {
            var reveal = root.AddComponent<RevealEffects>();
            reveal.generated = generated;
            reveal.targets = pendingFx.ToArray();
            foreach (GameObject g in reveal.targets) g.SetActive(false);
        }
    }

    // Level 1: keeps fragments on platforms, moving any over the void to the nearest one.
    void GateFragments(CubeFace face, Transform generated)
    {
        Vector3 up = face.transform.up;
        var frags = new List<Transform>();
        foreach (Transform c in generated)
            if (c.name.StartsWith("DreamFragment")) frags.Add(c);
        if (frags.Count == 0) return;

        var grounds = new List<int>();
        for (int i = 0; i < boxes.Count; i++)
            if (boxes[i].ground && !boxes[i].piece.name.StartsWith("DreamFragment")) grounds.Add(i);
        if (grounds.Count == 0) return;

        var used = new HashSet<Transform>();
        foreach (Transform f in frags)
        {
            Ray ray = new Ray(f.position + up * 1.5f, -up);
            float best = float.MaxValue;
            int hit = -1;
            foreach (int i in grounds)
                if (RayTop(boxes[i], ray, out float d) && d < best) { best = d; hit = i; }

            // Already on a platform?
            bool ok = hit >= 0 && best < 3.5f;
            if (!ok)
            {
                // Over the void: move to the nearest free platform.
                float bestDist = float.MaxValue;
                hit = -1;
                foreach (int i in grounds)
                {
                    if (used.Contains(boxes[i].piece)) continue;
                    Vector3 top = boxes[i].t.TransformPoint(boxes[i].b.center + new Vector3(0f, boxes[i].b.extents.y, 0f));
                    float d = (top - f.position).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; hit = i; }
                }
                if (hit < 0) continue;
                Vector3 t = boxes[hit].t.TransformPoint(boxes[hit].b.center + new Vector3(0f, boxes[hit].b.extents.y, 0f));
                f.position = t + up * 0.9f;
            }

            used.Add(boxes[hit].piece);
            var gate = f.gameObject.AddComponent<FragmentGate>();
            gate.piece = boxes[hit].piece;
        }
    }

    // Measures every level piece from its mesh.
    void CollectBoxes(CubeFace face, Transform generated)
    {
        boxes.Clear();
        Vector3 up = face.transform.up;

        foreach (MeshFilter mf in generated.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;

            Transform piece = mf.transform;
            while (piece.parent != null && piece.parent != generated) piece = piece.parent;

            Bounds mb = mf.sharedMesh.bounds;
            Vector3 ls = mf.transform.lossyScale;
            float sx = mb.size.x * Mathf.Abs(ls.x), sy = mb.size.y * Mathf.Abs(ls.y), sz = mb.size.z * Mathf.Abs(ls.z);
            bool flat = Vector3.Dot(mf.transform.up, up) > 0.98f;
            string n = piece.name;
            string meshName = mf.sharedMesh.name;
            bool notFloor = meshName == "Cylinder" || meshName == "Sphere" || meshName == "Capsule" ||
                            n.Contains("Gear") || n.Contains("Door") || n.Contains("Altar") || n.Contains("Cage") ||
                            n.Contains("Fragment") || n.Contains("Moving") || n.Contains("Demon") || n.Contains("Hunter");

            boxes.Add(new Box
            {
                t = mf.transform, b = mb, piece = piece,
                // Wide, thin pieces count as floors.
                ground = flat && !notFloor && Mathf.Min(sx, sz) >= 1.2f && sy <= 3f
            });
        }

        // Levels with one big floor only use that floor.
        bool oneFloor = face.GetComponent<GearGenerator>() != null || face.GetComponent<BiomeGenerator>() != null ||
                        face.GetComponent<AwakeningGenerator>() != null;
        if (!oneFloor) return;

        float bestArea = 0f;
        for (int i = 0; i < boxes.Count; i++)
        {
            if (!boxes[i].ground) continue;
            Vector3 s = Vector3.Scale(boxes[i].b.size, boxes[i].t.lossyScale);
            bestArea = Mathf.Max(bestArea, Mathf.Abs(s.x * s.z));
        }

        float floorTop = float.MaxValue;
        for (int i = 0; i < boxes.Count; i++)
        {
            if (!boxes[i].ground) continue;
            Vector3 s = Vector3.Scale(boxes[i].b.size, boxes[i].t.lossyScale);
            if (Mathf.Abs(s.x * s.z) < bestArea * 0.8f) continue;
            floorTop = Mathf.Min(floorTop, TopHeight(boxes[i], face.transform));
        }
        if (floorTop == float.MaxValue) return;

        for (int i = 0; i < boxes.Count; i++)
        {
            Box bx = boxes[i];
            if (!bx.ground) continue;
            float top = TopHeight(bx, face.transform);
            bx.ground = top >= floorTop - 0.15f && top <= floorTop + 0.6f;
            boxes[i] = bx;
        }
    }

    // Height of a piece's top surface above the face.
    static float TopHeight(Box bx, Transform face)
    {
        Vector3 topPoint = bx.t.TransformPoint(bx.b.center + new Vector3(0f, bx.b.extents.y, 0f));
        return Vector3.Dot(topPoint - face.position, face.up);
    }

    // ------------------------------------------------------------------ recipes

    void DressUnconscious(CubeFace face, Transform root, System.Random rng)
    {
        if (spawnProps) Scatter(face, root, rng, "Crystal_Cluster", 7, 0.7f, 1.2f, 1.0f, false);
        if (spawnEffects)
        {
            Fx(face, root, "P_StarDust", 55, 7f, 0.12f, 0.35f, 4f, 0.4f, 0.4f, 1f);
            Fx(face, root, "P_Mist", 14, 8f, 2.5f, 4.5f, 9f, 0.15f, 0.3f, 0.2f);
        }
    }

    void DressBeach(CubeFace face, Transform root, System.Random rng)
    {
        if (spawnProps)
        {
            Scatter(face, root, rng, "Rock_Small", 16, 0.8f, 1.4f, 0.5f, false);
            Scatter(face, root, rng, "Rock_Medium", 6, 0.8f, 1.2f, 1.0f, solidBigProps);
            Scatter(face, root, rng, "Rock_Large", 2, 0.9f, 1.2f, 2.0f, solidBigProps);
            Scatter(face, root, rng, "Shell_Large", 11, 0.8f, 1.3f, 0.5f, false);
            Scatter(face, root, rng, "Driftwood", 4, 0.8f, 1.2f, 1.2f, false);
            Scatter(face, root, rng, "Dead_Tree", 3, 0.9f, 1.3f, 1.2f, solidBigProps);
        }
        if (spawnEffects)
        {
            Fx(face, root, "P_SandPuff", 18, 4f, 0.4f, 1.0f, 5f, 0.5f, 0.3f, 0.1f);   // dust blowing along the sand
            Fx(face, root, "P_Mist", 10, 6f, 3f, 5f, 10f, 0.3f, 0.22f, 0.2f);
        }
    }

    void DressMaze(CubeFace face, Transform root, System.Random rng)
    {
        // Atmosphere only, no physical props.
        if (spawnEffects)
        {
            Fx(face, root, "P_Mist", 22, 4f, 1.6f, 3.2f, 8f, 0.2f, 0.22f, 0.1f);
            Fx(face, root, "P_StarDust", 40, 6f, 0.08f, 0.2f, 5f, 0.5f, 0.3f, 1.0f);
        }
    }

    void DressGears(CubeFace face, Transform root, System.Random rng)
    {
        if (spawnProps)
        {
            Scatter(face, root, rng, "Pipe_Straight", 7, 0.9f, 1.2f, 1.0f, false);
            Scatter(face, root, rng, "Valve_Wheel", 4, 0.9f, 1.2f, 0.8f, false);
        }
        if (spawnEffects)
        {
            // Steam vents low to the floor.
            for (int i = 0; i < 5; i++)
            {
                if (FindSpot(face, rng, 0.6f, out Vector3 p)) SteamVent(face, root, p);
            }
        }
    }

    void DressClouds(CubeFace face, Transform root, System.Random rng)
    {
        if (spawnEffects)
        {
            Fx(face, root, "P_StarDust", 60, 9f, 0.1f, 0.3f, 4f, 0.8f, 0.5f, 1.0f);
            Fx(face, root, "P_Cloud", 12, -8f, 3.5f, 6f, 10f, 0.3f, 0.3f, -0.3f);   // below the islands
            Fx(face, root, "P_Sparkle", 22, 9f, 0.12f, 0.3f, 3f, 0.4f, 0.6f, 0.6f);
        }
    }

    void DressBedroom(CubeFace face, Transform root, System.Random rng)
    {
        if (spawnProps)
        {
            Scatter(face, root, rng, "Bed_Distorted", 1, 1f, 1f, 2.6f, false);
            Scatter(face, root, rng, "Wardrobe_Tall", 1, 1f, 1f, 1.6f, false);
            Scatter(face, root, rng, "Bedside_Table", 2, 1f, 1f, 0.8f, false);
            Scatter(face, root, rng, "Lamp_Table", 1, 1f, 1f, 0.8f, false);
        }
        if (spawnEffects)
        {
            Fx(face, root, "P_LightRays", 4, 6f, 4f, 7f, 12f, 0.05f, 0.18f, 0.05f);
            Fx(face, root, "P_StarDust", 30, 8f, 0.05f, 0.15f, 5f, 0.4f, 0.4f, 0.6f);
        }
    }

    // ------------------------------------------------------------------ placing props

    GameObject Prefab(string name)
    {
        if (prefabs == null) return null;
        foreach (GameObject p in prefabs) if (p != null && p.name == name) return p;
        return null;
    }

    Material Mat(string name)
    {
        if (materials == null) return null;
        foreach (Material m in materials) if (m != null && m.name == name) return m;
        return null;
    }

    // Distance along a ray to the top of a piece's mesh bounds.
    static bool RayTop(Box bx, Ray ray, out float dist)
    {
        dist = 0f;
        Matrix4x4 w2l = bx.t.worldToLocalMatrix;
        Vector3 o = w2l.MultiplyPoint3x4(ray.origin);
        Vector3 d = w2l.MultiplyVector(ray.direction);
        Vector3 mn = bx.b.min, mx = bx.b.max;

        float t0 = 0f, t1 = float.MaxValue;
        int axisIn = -1;
        for (int a = 0; a < 3; a++)
        {
            if (Mathf.Abs(d[a]) < 1e-6f)
            {
                if (o[a] < mn[a] || o[a] > mx[a]) return false;
                continue;
            }
            float ta = (mn[a] - o[a]) / d[a], tb = (mx[a] - o[a]) / d[a];
            if (ta > tb) { float tmp = ta; ta = tb; tb = tmp; }
            if (ta > t0) { t0 = ta; axisIn = a; }
            if (tb < t1) t1 = tb;
            if (t0 > t1) return false;
        }

        // Only the upper (+Y) face counts as a surface.
        if (axisIn != 1 || d.y >= 0f) return false;
        dist = t0;
        return true;
    }

    // True if a sphere at this point touches any piece other than the given one.
    bool Touches(Vector3 centre, float radius, Transform except)
    {
        foreach (Box bx in boxes)
        {
            if (bx.t == except) continue;
            Matrix4x4 w2l = bx.t.worldToLocalMatrix;
            Vector3 lc = w2l.MultiplyPoint3x4(centre);
            Vector3 cp = new Vector3(Mathf.Clamp(lc.x, bx.b.min.x, bx.b.max.x),
                                     Mathf.Clamp(lc.y, bx.b.min.y, bx.b.max.y),
                                     Mathf.Clamp(lc.z, bx.b.min.z, bx.b.max.z));
            Vector3 wp = bx.t.localToWorldMatrix.MultiplyPoint3x4(cp);
            if ((wp - centre).sqrMagnitude < radius * radius) return true;
        }
        foreach (Vector4 p in placed)
        {
            Vector3 q = new Vector3(p.x, p.y, p.z);
            float r = radius + p.w;
            if ((q - centre).sqrMagnitude < r * r) return true;
        }
        return false;
    }

    // Random clear spot on the face's ground; returns the surface point.
    bool FindSpot(CubeFace face, System.Random rng, float clearRadius, out Vector3 point)
    {
        point = Vector3.zero;
        Transform ft = face.transform;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            float x = ((float)rng.NextDouble() * 2f - 1f) * halfSize;
            float z = ((float)rng.NextDouble() * 2f - 1f) * halfSize;
            Ray ray = new Ray(ft.TransformPoint(new Vector3(x, 12f, z)), -ft.up);

            // The first hit must be a floor.
            float best = float.MaxValue;
            int bestIndex = -1;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (!RayTop(boxes[i], ray, out float dist) || dist >= best) continue;
                best = dist; bestIndex = i;
            }
            if (bestIndex < 0 || !boxes[bestIndex].ground) continue;

            Vector3 hit = ray.GetPoint(best);
            if (Touches(hit + ft.up * (clearRadius + 0.25f), clearRadius, boxes[bestIndex].t)) continue;

            point = hit;
            lastPiece = boxes[bestIndex].piece;
            placed.Add(new Vector4(hit.x, hit.y, hit.z, clearRadius));
            return true;
        }
        return false;
    }

    readonly Dictionary<GameObject, float> lowest = new Dictionary<GameObject, float>();

    // Offset of the prefab's lowest point from its pivot (negative = below).
    float LowestPoint(GameObject prefab)
    {
        if (lowest.TryGetValue(prefab, out float v)) return v;

        float min = 0f;
        bool any = false;
        Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float y = m.MultiplyPoint3x4(c).y;
                if (!any || y < min) { min = y; any = true; }
            }
        }
        lowest[prefab] = min;
        return min;
    }

    void Scatter(CubeFace face, Transform root, System.Random rng, string prefabName, int count,
                 float minScale, float maxScale, float clearRadius, bool solid)
    {
        GameObject prefab = Prefab(prefabName);
        if (prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            if (!FindSpot(face, rng, clearRadius, out Vector3 p)) continue;

            GameObject go = Instantiate(prefab, root);
            go.name = prefabName;
            float s = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
            // Align upright to the face.
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, face.transform.up) *
                                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            Vector3 finalScale = prefab.transform.localScale * s;
            go.transform.position = p + face.transform.up * (-LowestPoint(prefab) * s);   // rest on the ground, never sink in
            go.transform.localScale = Vector3.zero;
            var pop = go.AddComponent<PropPop>();
            pop.target = finalScale;
            pop.delay = 0.04f * i + (float)rng.NextDouble() * 0.25f;
            pop.waitFor = lastPiece;           // grows when its piece appears

            if (!solid)
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }
    }

    // ------------------------------------------------------------------ particles

    // Soft particle cloud over the face (height is above the ground, negative = below).
    void Fx(CubeFace face, Transform root, string materialName, int count, float height,
            float sizeMin, float sizeMax, float life, float drift, float alpha, float rise)
    {
        Material m = Mat(materialName);
        if (m == null) return;

        GameObject go = new GameObject("FX_" + materialName);
        go.transform.SetParent(root, false);
        go.transform.position = face.transform.position + face.transform.up * (height * 0.5f);
        go.transform.rotation = Quaternion.LookRotation(face.transform.forward, face.transform.up);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(count * 2, 20);
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life * 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startSpeed = 0f;
        main.startColor = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

        var emission = ps.emission;
        emission.rateOverTime = Mathf.Max(1f, count / Mathf.Max(0.5f, life));

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(halfSize * 2f, Mathf.Max(0.5f, Mathf.Abs(height)), halfSize * 2f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        Vector3 up = face.transform.up * rise * 0.25f;
        Vector3 side = face.transform.right * drift;
        vel.x = new ParticleSystem.MinMaxCurve(-Mathf.Abs(side.x) - 0.05f + up.x, Mathf.Abs(side.x) + 0.05f + up.x);
        vel.y = new ParticleSystem.MinMaxCurve(-Mathf.Abs(side.y) - 0.05f + up.y, Mathf.Abs(side.y) + 0.05f + up.y);
        vel.z = new ParticleSystem.MinMaxCurve(-Mathf.Abs(side.z) - 0.05f + up.z, Mathf.Abs(side.z) + 0.05f + up.z);

        // Fade in and out.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                          new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        pendingFx.Add(go);
    }

    void SteamVent(CubeFace face, Transform root, Vector3 position)
    {
        Material m = Mat("P_Steam");
        if (m == null) return;

        GameObject go = new GameObject("FX_SteamVent");
        go.transform.SetParent(root, false);
        go.transform.position = position + face.transform.up * 0.15f;
        go.transform.rotation = Quaternion.LookRotation(face.transform.forward, face.transform.up) * Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startColor = new Color(1f, 1f, 1f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.08f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(1f, 1.6f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
        pendingFx.Add(go);
    }
}

// Grows a prop to full size with a small overshoot.
public class PropPop : MonoBehaviour
{
    public Vector3 target = Vector3.one;
    public float delay;
    public float duration = 0.6f;
    [Tooltip("Hidden until this piece is visible.")]
    public Transform waitFor;
    float t;
    bool finished;

    void Update()
    {
        if (waitFor != null && !waitFor.gameObject.activeInHierarchy)
        {
            t = 0f; finished = false;                      // piece hidden again: hide too
            transform.localScale = Vector3.zero;
            return;
        }
        if (finished) { if (waitFor == null) Destroy(this); return; }

        t += Time.deltaTime;
        float u = Mathf.Clamp01((t - delay) / Mathf.Max(0.05f, duration));
        float k = u - 1f;
        float eased = 1f + 2.70158f * k * k * k + 1.70158f * k * k;     // ease out back
        transform.localScale = target * (u <= 0f ? 0f : Mathf.Max(0f, eased));
        if (u >= 1f) { transform.localScale = target; finished = true; }
    }
}

// Keeps a level's particle effects off until its first piece appears.
public class RevealEffects : MonoBehaviour
{
    public Transform generated;
    public GameObject[] targets;

    void Update()
    {
        if (generated != null)
        {
            bool any = false;
            foreach (Transform c in generated)
                if (c.gameObject.activeInHierarchy) { any = true; break; }
            if (!any) return;
        }

        foreach (GameObject g in targets) if (g != null) g.SetActive(true);
        Destroy(this);
    }
}

// Hides a fragment until the platform it sits on appears.
public class FragmentGate : MonoBehaviour
{
    public Transform piece;
    Renderer[] renderers;
    Collider[] colliders;
    bool shown = true;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    void LateUpdate()
    {
        bool visible = piece == null || piece.gameObject.activeInHierarchy;
        if (visible == shown) return;
        shown = visible;
        foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
        foreach (Collider c in colliders) if (c != null) c.enabled = visible;
    }

    void OnEnable()
    {
        shown = true;
        LateUpdate();
    }
}
