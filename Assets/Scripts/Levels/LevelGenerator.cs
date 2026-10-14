using UnityEngine;
using System.Collections.Generic;

// Builds a random level that spans the whole face, using a grid maze.
// It carves a long main route from one edge to the opposite edge, then keeps
// adding branches and rooms until the face is filled to the coverage setting.
// Settings come from the LevelTheme asset, so each level generates differently.
[RequireComponent(typeof(CubeFace))]
public class LevelGenerator : MonoBehaviour
{
    // Shapes a dream fragment can take
    public enum FragmentShape { Crystal, Cube, Sphere, Capsule, Cylinder, Prefab }

    [Header("Theme")]
    [Tooltip("The LevelTheme asset holding this level's settings.")]
    public LevelTheme theme;

    [Header("Seed")]
    [Tooltip("Tick to always build the same layout (handy while testing one level).")]
    public bool useFixedSeed = false;
    public int fixedSeed = 12345;

    [Header("Face Area")]
    public float faceSize = 30f;
    [Tooltip("Keeps the layout away from the very edge of the cube.")]
    public float edgeMargin = 1f;
    [Tooltip("How high the walkways float above the cube surface.")]
    public float baseHeight = 1.5f;

    [Header("Fragment Shape")]
    public FragmentShape fragmentShape = FragmentShape.Crystal;
    [Tooltip("Overall size of the fragment.")]
    public float fragmentSize = 0.6f;
    [Tooltip("Stretches the shape vertically. 1 = even, 2 = tall and thin.")]
    public float fragmentStretch = 1.4f;
    [Tooltip("Used when Shape is set to Prefab. Drop any model in here.")]
    public GameObject fragmentPrefab;

    [Header("Fragment Placement")]
    [Tooltip("How high above the floor fragments hover. Raise if they sit too low.")]
    public float fragmentHeight = 1.2f;
    [Tooltip("Clear space a fragment needs around it. Raise if they feel cramped.")]
    public float fragmentClearance = 1f;
    [Tooltip("If the level is too crowded, remove pillars to fit every fragment in.")]
    public bool guaranteeFragmentCount = true;

    const string GeneratedName = "Generated";

    [Header("Altar")]
    [Tooltip("Size of the altar the player delivers fragments to.")]
    public float altarSize = 1.6f;

    [Tooltip("How far around the altar counts as delivering.")]
    public float altarTriggerRadius = 2.5f;

    [Tooltip("Optional. A model to use instead of a plain block.")]
    public GameObject altarPrefab;

    System.Random rng;
    Transform root;

    // Grid state
    int gw, gh;              // grid width and height in cells
    float cell;              // actual cell size, stretched to fill the face
    int startCell, exitCell;
    int[] parent;            // which cell each one was carved from
    int[] depthCache;
    float[] heightCache;

    Dictionary<int, float> carved = new Dictionary<int, float>(); // cell -> floor height
    List<int> deadEnds = new List<int>();
    List<Renderer> floorRenderers = new List<Renderer>();

    // What's in the way, recorded as the level is built
    HashSet<int> wallCells = new HashSet<int>();
    HashSet<int> gapCells = new HashSet<int>();
    Dictionary<int, List<GameObject>> pillarsByCell = new Dictionary<int, List<GameObject>>();
    HashSet<int> usedCells = new HashSet<int>();

    [ContextMenu("Generate Preview")]
    private void generatePreview()
    {
        generate(Random.Range(int.MinValue, int.MaxValue));
    }

    [ContextMenu("Clear Generated")]
    public void clearGenerated()
    {
        Transform content = getContent();
        if (content == null) return;

        Transform old = content.Find(GeneratedName);
        if (old == null) return;

        // Rename first, because Destroy only takes effect at the end of the frame
        old.name = "GeneratedOld";
        safeDestroy(old.gameObject);
    }

    // Builds the level. The same seed always gives the same layout.
    public void generate(int seed)
    {
        if (theme == null)
        {
            Debug.LogError("LevelGenerator on " + name + " has no LevelTheme assigned.");
            return;
        }

        rng = new System.Random(useFixedSeed ? fixedSeed : seed);

        Transform content = getContent();
        if (content == null)
        {
            Debug.LogError("LevelGenerator: no LevelContent found under " + name);
            return;
        }

        clearGenerated();
        root = new GameObject(GeneratedName).transform;
        root.SetParent(content, false);

        // Pick a grid size, then stretch the cells so they fill the face exactly
        float usable = faceSize - edgeMargin * 2f;
        gw = Mathf.Max(3, Mathf.RoundToInt(usable / theme.cellSize));
        gh = gw;
        cell = usable / gw;

        // A maze can wander into a short route, so try a few times for a long one
        for (int attempt = 0; attempt < 8; attempt++)
        {
            carveMaze();
            if (depthOf(exitCell) >= theme.minRouteCells) break;
        }

        buildCells();
        placeDoorAndSpawn();

        // The new floors need to be registered with physics before we can measure them
        Physics.SyncTransforms();

        placeFragments();
        applyFaceColor();
    }

    // --- Grid maths -------------------------------------------------

    private int index(int x, int y) { return x + y * gw; }
    private int cellX(int c) { return c % gw; }
    private int cellY(int c) { return c / gw; }
    private bool inGrid(int x, int y) { return x >= 0 && x < gw && y >= 0 && y < gh; }

    // The world position of a cell's floor surface
    private Vector3 cellCenter(int c, float height)
    {
        float x = (cellX(c) - (gw - 1) * 0.5f) * cell;
        float z = (cellY(c) - (gh - 1) * 0.5f) * cell;
        return new Vector3(x, height, z);
    }

    // --- Maze carving -----------------------------------------------

    // Depth-first maze over the whole grid, which gives long winding routes
    private void carveMaze()
    {
        int total = gw * gh;
        parent = new int[total];
        depthCache = new int[total];
        heightCache = new float[total];
        bool[] visited = new bool[total];

        for (int i = 0; i < total; i++)
        {
            parent[i] = -1;
            depthCache[i] = -1;
            heightCache[i] = float.NaN;
        }

        startCell = index(gw / 2, 0); // near edge, middle
        visited[startCell] = true;
        depthCache[startCell] = 0;
        heightCache[startCell] = 0f;

        Stack<int> stack = new Stack<int>();
        stack.Push(startCell);

        while (stack.Count > 0)
        {
            int current = stack.Peek();
            List<int> options = unvisitedNeighbours(current, visited);

            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }

            int next = options[rng.Next(options.Count)];
            visited[next] = true;
            parent[next] = current;
            stack.Push(next);
        }

        // The exit goes on the far edge, so the route crosses the whole face
        exitCell = index(0, gh - 1);
        for (int x = 0; x < gw; x++)
        {
            int c = index(x, gh - 1);
            if (depthOf(c) > depthOf(exitCell)) exitCell = c;
        }

        // Carve the main route, then fill the rest of the face
        carved.Clear();
        deadEnds.Clear();

        carveChain(exitCell);
        carveBranches();
        carveRooms();
    }

    private List<int> unvisitedNeighbours(int c, bool[] visited)
    {
        List<int> list = new List<int>();
        int x = cellX(c), y = cellY(c);

        if (inGrid(x + 1, y) && !visited[index(x + 1, y)]) list.Add(index(x + 1, y));
        if (inGrid(x - 1, y) && !visited[index(x - 1, y)]) list.Add(index(x - 1, y));
        if (inGrid(x, y + 1) && !visited[index(x, y + 1)]) list.Add(index(x, y + 1));
        if (inGrid(x, y - 1) && !visited[index(x, y - 1)]) list.Add(index(x, y - 1));

        return list;
    }

    // How many cells from the start, following the carve tree back
    private int depthOf(int c)
    {
        if (c == startCell) return 0;
        if (depthCache[c] >= 0) return depthCache[c];
        if (parent[c] < 0) return 0;

        depthCache[c] = depthOf(parent[c]) + 1;
        return depthCache[c];
    }

    // Each cell sits slightly above or below the one it was carved from,
    // so neighbours are always within one step of each other
    private float heightOf(int c)
    {
        if (c == startCell) return 0f;
        if (!float.IsNaN(heightCache[c])) return heightCache[c];
        if (parent[c] < 0) return 0f;

        float step = (float)(rng.NextDouble() * 2.0 - 1.0) * theme.stepHeight;
        float h = Mathf.Clamp(heightOf(parent[c]) + step, -theme.maxHeightDrift, theme.maxHeightDrift);

        heightCache[c] = h;
        return h;
    }

    // Carves a cell and everything between it and the already-carved area
    private void carveChain(int tip)
    {
        int c = tip;
        while (c >= 0 && !carved.ContainsKey(c))
        {
            carved[c] = heightOf(c);
            c = parent[c];
        }
    }

    // How far a cell is from carved ground, following the tree back
    private int distanceToCarved(int c)
    {
        int steps = 0;
        while (c >= 0 && !carved.ContainsKey(c))
        {
            steps++;
            c = parent[c];
        }
        return c < 0 ? int.MaxValue : steps;
    }

    // Keeps carving until the face is filled to the coverage setting
    private void carveBranches()
    {
        int total = gw * gh;
        int target = Mathf.RoundToInt(total * Mathf.Clamp01(theme.coverage));

        List<int> candidates = new List<int>();
        for (int i = 0; i < total; i++)
            if (!carved.ContainsKey(i)) candidates.Add(i);

        shuffle(candidates);

        // Pass 1: short spurs off the existing route, which read as proper dead ends
        foreach (int c in candidates)
        {
            if (carved.Count >= target) return;
            if (carved.ContainsKey(c)) continue;

            int distance = distanceToCarved(c);
            if (distance < 1 || distance > theme.maxBranchLength) continue;

            carveChain(c);
            deadEnds.Add(c);
        }

        // Pass 2: fill whatever's left, however long the chain needs to be
        foreach (int c in candidates)
        {
            if (carved.Count >= target) return;
            if (carved.ContainsKey(c)) continue;

            carveChain(c);
            deadEnds.Add(c);
        }
    }

    // Opens some cells out into wider rooms by flattening their neighbours
    private void carveRooms()
    {
        List<int> existing = new List<int>(carved.Keys);

        foreach (int c in existing)
        {
            if (c == startCell || c == exitCell) continue;
            if (rng.NextDouble() > theme.roomChance) continue;

            float h = carved[c];
            int x = cellX(c), y = cellY(c);

            // Room cells share the centre cell's height, so the floor stays flat
            tryCarveFlat(x + 1, y, h);
            tryCarveFlat(x - 1, y, h);
            tryCarveFlat(x, y + 1, h);
            tryCarveFlat(x, y - 1, h);
        }
    }

    private void tryCarveFlat(int x, int y, float height)
    {
        if (!inGrid(x, y)) return;

        int c = index(x, y);
        if (carved.ContainsKey(c)) return;

        carved[c] = height;
    }

    // --- Geometry ---------------------------------------------------

    private void buildCells()
    {
        floorRenderers.Clear();
        wallCells.Clear();
        gapCells.Clear();
        pillarsByCell.Clear();
        usedCells.Clear();

        foreach (KeyValuePair<int, float> pair in carved)
        {
            int c = pair.Key;
            float h = baseHeight + pair.Value;
            Vector3 center = cellCenter(c, h);

            bool special = c == startCell || c == exitCell;
            Vector3 dir = directionFromParent(c);

            // Occasionally split the floor, leaving a gap to jump
            bool gap = !special && dir != Vector3.zero && rng.NextDouble() < theme.gapChance;

            if (gap)
            {
                makeSplitFloor(c, center, dir);
                gapCells.Add(c);
            }
            else
            {
                makeFloor("Floor_" + c, center, new Vector3(cell, 1f, cell));
            }

            if (special || gap) continue;

            // A wall across the cell with a gap to walk through
            if (dir != Vector3.zero && rng.NextDouble() < theme.wallChance)
            {
                makeWall(c, center, dir);
                wallCells.Add(c);
            }

            // Loose pillars to break up the space, remembered so fragments can dodge them
            int pillars = rng.Next(theme.pillarsPerCell.x, theme.pillarsPerCell.y + 1);
            for (int p = 0; p < pillars; p++)
            {
                float ox = (float)(rng.NextDouble() - 0.5) * cell * 0.6f;
                float oz = (float)(rng.NextDouble() - 0.5) * cell * 0.6f;

                Vector3 pos = center + new Vector3(ox, theme.pillarSize, oz);
                GameObject pillar = makeCube("Pillar_" + c + "_" + p, pos,
                    new Vector3(theme.pillarSize, theme.pillarSize * 2f, theme.pillarSize),
                    theme.wallMaterial);

                if (!pillarsByCell.ContainsKey(c)) pillarsByCell[c] = new List<GameObject>();
                pillarsByCell[c].Add(pillar);
            }
        }
    }

    // Which way the player travels through this cell
    private Vector3 directionFromParent(int c)
    {
        if (c == startCell || parent[c] < 0) return Vector3.zero;

        int p = parent[c];
        if (!carved.ContainsKey(p)) return Vector3.zero;

        int dx = cellX(c) - cellX(p);
        int dy = cellY(c) - cellY(p);
        return new Vector3(dx, 0f, dy).normalized;
    }

    // Two slabs with a gap between them, running across the travel direction
    private void makeSplitFloor(int c, Vector3 center, Vector3 dir)
    {
        float gap = Mathf.Clamp(randomRange(theme.gapSize), 0.5f, cell - 1f);
        float piece = (cell - gap) * 0.5f;
        float offset = gap * 0.5f + piece * 0.5f;

        Vector3 size = axisSize(dir, piece, cell);

        makeFloor("Floor_" + c + "_A", center - dir * offset, size);
        makeFloor("Floor_" + c + "_B", center + dir * offset, size);
    }

    // A wall across the cell, with an opening somewhere along it
    private void makeWall(int c, Vector3 center, Vector3 dir)
    {
        Vector3 cross = Vector3.Cross(Vector3.up, dir);

        float width = cell;
        float gapCenter = (float)(rng.NextDouble() - 0.5) * Mathf.Max(0f, width - theme.wallGap);

        float leftWidth = (gapCenter - theme.wallGap * 0.5f) + width * 0.5f;
        float rightWidth = width * 0.5f - (gapCenter + theme.wallGap * 0.5f);

        if (leftWidth > 0.3f)
        {
            Vector3 size = axisSize(dir, 0.8f, leftWidth);
            size.y = theme.wallHeight;
            Vector3 pos = center + cross * (-width * 0.5f + leftWidth * 0.5f)
                                 + Vector3.up * theme.wallHeight * 0.5f;
            makeCube("Wall_" + c + "_L", pos, size, theme.wallMaterial);
        }

        if (rightWidth > 0.3f)
        {
            Vector3 size = axisSize(dir, 0.8f, rightWidth);
            size.y = theme.wallHeight;
            Vector3 pos = center + cross * (width * 0.5f - rightWidth * 0.5f)
                                 + Vector3.up * theme.wallHeight * 0.5f;
            makeCube("Wall_" + c + "_R", pos, size, theme.wallMaterial);
        }
    }

    private void placeDoorAndSpawn()
    {
        // Spawn point on the starting cell
        CubeFace face = GetComponent<CubeFace>();
        if (face != null && face.spawnPoint != null)
        {
            float startHeight = baseHeight + carved[startCell];
            face.spawnPoint.localPosition = cellCenter(startCell, startHeight) + Vector3.up * 0.2f;
            face.spawnPoint.localRotation = Quaternion.identity;
        }
    }

    // Builds the altar, the fragments and the (hidden) door
    private void placeFragments()
    {
        int wanted = theme.collectibles;

        // --- The altar, on the exit cell so it's a clear destination ---
        float exitHeight = baseHeight + carved[exitCell];
        Vector3 altarPos = cellCenter(exitCell, exitHeight);

        GameObject altarObj;
        if (altarPrefab != null)
        {
            altarObj = Instantiate(altarPrefab, root);
            altarObj.transform.localPosition = altarPos;
        }
        else
        {
            altarObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            altarObj.transform.SetParent(root, false);
            altarObj.transform.localPosition = altarPos + Vector3.up * altarSize * 0.5f;
            altarObj.transform.localScale = new Vector3(altarSize, altarSize, altarSize);
        }

        altarObj.name = "DreamAltar";
        DreamAltar altar = altarObj.GetComponent<DreamAltar>();
        if (altar == null) altar = altarObj.AddComponent<DreamAltar>();

        // A wide trigger around it, so the player just has to walk up
        GameObject triggerObj = new GameObject("AltarTrigger");
        triggerObj.transform.SetParent(root, false);
        triggerObj.transform.localPosition = altarPos + Vector3.up;

        SphereCollider trigger = triggerObj.AddComponent<SphereCollider>();
        trigger.radius = altarTriggerRadius;
        trigger.isTrigger = true;
        triggerObj.AddComponent<AltarTrigger>();

        // --- The door, placed beside the altar and hidden for now ---
        Vector3 doorDir = directionFromParent(exitCell);
        if (doorDir == Vector3.zero) doorDir = Vector3.forward;

        GameObject door = makeCube("DreamDoor", altarPos + doorDir * cell * 0.4f + Vector3.up * 1.5f,
                                   new Vector3(3f, 3f, 0.5f), theme.doorMaterial);
        door.transform.localRotation = Quaternion.LookRotation(doorDir);
        door.GetComponent<Collider>().isTrigger = true;
        door.AddComponent<DreamDoor>();

        // --- The fragments themselves ---
        if (wanted <= 0)
        {
            Debug.LogWarning("LevelGenerator: '" + theme.name +
                             "' has Collectibles set to 0, so no dream fragments were placed.");
            LevelObjectives.Instance?.setUpLevel(0, altar, door);
            return;
        }

        // Dead ends are the best hiding spots, then anywhere else that's suitable
        List<int> order = new List<int>();
        foreach (int c in deadEnds)
            if (isCandidateCell(c) && !order.Contains(c)) order.Add(c);
        shuffle(order);

        List<int> extras = new List<int>();
        foreach (int c in carved.Keys)
            if (isCandidateCell(c) && !order.Contains(c)) extras.Add(c);
        shuffle(extras);

        order.AddRange(extras);

        int placed = 0;

        // Pass 1: find a clear spot inside each candidate cell
        foreach (int c in order)
        {
            if (placed >= wanted) break;
            if (!tryFindSpot(c, out Vector3 spot)) continue;

            spawnFragment(placed, spot);
            usedCells.Add(c);
            placed++;
        }

        // Pass 2: if the level is too crowded, clear a cell's pillars to make room
        if (placed < wanted && guaranteeFragmentCount)
        {
            foreach (int c in order)
            {
                if (placed >= wanted) break;
                if (usedCells.Contains(c)) continue;

                clearPillars(c);
                Physics.SyncTransforms();

                if (!tryFindSpot(c, out Vector3 spot)) continue;

                spawnFragment(placed, spot);
                usedCells.Add(c);
                placed++;
            }
        }

        if (placed < wanted)
            Debug.LogWarning("LevelGenerator: placed " + placed + " of " + wanted +
                             " fragments. Try raising Coverage or lowering Wall Chance.");

        // The altar asks for what actually got placed, so the level can always be finished
        LevelObjectives.Instance?.setUpLevel(placed, altar, door);
    }

    // Cells that could hold a fragment: solid floor, not the start or the exit
    private bool isCandidateCell(int c)
    {
        if (!carved.ContainsKey(c)) return false;
        if (c == startCell || c == exitCell) return false;
        if (gapCells.Contains(c)) return false;   // split floor, nothing solid in the middle
        if (wallCells.Contains(c)) return false;  // a wall crosses it
        if (usedCells.Contains(c)) return false;
        return true;
    }

    // Looks for a spot in the cell that isn't inside a pillar,
    // sitting on the real floor surface rather than a calculated height
    private bool tryFindSpot(int c, out Vector3 spot)
    {
        float guessHeight = baseHeight + carved[c];
        Vector3 center = cellCenter(c, guessHeight);

        // Try the middle first, then a ring of offsets around it
        List<Vector3> tries = new List<Vector3> { center };
        float reach = cell * 0.3f;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            tries.Add(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach);
        }

        float needed = fragmentClearance + theme.pillarSize * 0.5f;

        foreach (Vector3 candidate in tries)
        {
            if (!isClearOfPillars(c, candidate, needed)) continue;

            // Find the actual surface under this spot, so nothing ends up buried
            float surface = findSurfaceHeight(candidate, guessHeight);

            spot = new Vector3(candidate.x, surface + fragmentHeight, candidate.z);
            return true;
        }

        spot = Vector3.zero;
        return false;
    }

    // Casts a ray down onto the generated floor and returns its top surface,
    // falling back to the calculated height if nothing is hit
    private float findSurfaceHeight(Vector3 localPos, float fallback)
    {
        // Start well above the spot, in world space, pointing at the face
        Vector3 worldStart = root.TransformPoint(localPos + Vector3.up * 10f);
        Vector3 worldDown = root.TransformDirection(Vector3.down);

        if (Physics.Raycast(worldStart, worldDown, out RaycastHit hit, 30f,
                            ~0, QueryTriggerInteraction.Ignore))
        {
            // Only trust a hit on our own generated geometry
            if (hit.transform.IsChildOf(root))
                return root.InverseTransformPoint(hit.point).y;
        }

        return fallback;
    }

    // True if the spot is far enough from every pillar in this cell
    private bool isClearOfPillars(int c, Vector3 pos, float needed)
    {
        if (!pillarsByCell.ContainsKey(c)) return true;

        foreach (GameObject pillar in pillarsByCell[c])
        {
            if (pillar == null) continue;

            Vector3 p = pillar.transform.localPosition;
            float dx = p.x - pos.x;
            float dz = p.z - pos.z;

            if (dx * dx + dz * dz < needed * needed) return false;
        }
        return true;
    }

    // Removes a cell's pillars so a fragment can fit
    private void clearPillars(int c)
    {
        if (!pillarsByCell.ContainsKey(c)) return;

        foreach (GameObject pillar in pillarsByCell[c])
            safeDestroy(pillar);

        pillarsByCell[c].Clear();
    }

    // Builds one fragment in the chosen shape
    private void spawnFragment(int number, Vector3 localPos)
    {
        GameObject go;

        if (fragmentShape == FragmentShape.Prefab && fragmentPrefab != null)
        {
            go = Instantiate(fragmentPrefab, root);
            go.transform.localPosition = localPos;

            // Make sure it has a trigger collider to pick up
            Collider col = go.GetComponentInChildren<Collider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
        }
        else
        {
            go = GameObject.CreatePrimitive(ShapeToPrimitive(fragmentShape));
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(fragmentSize,
                                                  fragmentSize * fragmentStretch,
                                                  fragmentSize);

            // Tilting a cube on two axes gives it a cut-crystal look
            go.transform.localRotation = fragmentShape == FragmentShape.Crystal
                ? Quaternion.Euler(45f, 45f, 0f)
                : Quaternion.identity;

            if (theme.collectibleMaterial != null)
                go.GetComponent<Renderer>().sharedMaterial = theme.collectibleMaterial;

            go.GetComponent<Collider>().isTrigger = true;
        }

        go.name = "DreamFragment_" + number;
        if (go.GetComponent<DreamFragment>() == null) go.AddComponent<DreamFragment>();
    }

    PrimitiveType ShapeToPrimitive(FragmentShape shape)
    {
        switch (shape)
        {
            case FragmentShape.Sphere: return PrimitiveType.Sphere;
            case FragmentShape.Capsule: return PrimitiveType.Capsule;
            case FragmentShape.Cylinder: return PrimitiveType.Cylinder;
            default: return PrimitiveType.Cube; // Crystal is a tilted cube
        }
    }

    // --- Building blocks --------------------------------------------

    // Floor slabs hang below the walking surface, so their tops are the ground
    private void makeFloor(string name, Vector3 center, Vector3 size)
    {
        Vector3 pos = center - new Vector3(0f, size.y * 0.5f, 0f);
        GameObject go = makeCube(name, pos, size, theme.floorMaterial);
        floorRenderers.Add(go.GetComponent<Renderer>());
    }

    private GameObject makeCube(string name, Vector3 localPos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Works out which axis the size goes on, depending on which way the cell points
    private Vector3 axisSize(Vector3 dir, float along, float across)
    {
        bool alongX = Mathf.Abs(dir.x) > 0.5f;
        return alongX ? new Vector3(along, 1f, across) : new Vector3(across, 1f, along);
    }

    private float randomRange(Vector2 range)
    {
        return Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());
    }

    private void shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Works both in Play mode and in the editor
    private void safeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    // Hands the new floors to CubeFace so the face colour still applies
    private void applyFaceColor()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face == null) return;

        face.colorRenderers = floorRenderers.ToArray();
        face.applyColor();
    }

    private Transform getContent()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face != null && face.levelContent != null) return face.levelContent.transform;
        return transform.Find("LevelContent");
    }
}
