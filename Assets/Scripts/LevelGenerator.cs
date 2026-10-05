using UnityEngine;
using System.Collections.Generic;

// Builds a random level that spans the whole face, using a grid maze.
// It carves a long main route from one edge to the opposite edge, then keeps
// adding branches and rooms until the face is filled to the coverage setting.
// Settings come from the LevelTheme asset, so each level generates differently.
[RequireComponent(typeof(CubeFace))]
public class LevelGenerator : MonoBehaviour
{
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

    [Header("Fragment Placement")]
    [Tooltip("How high above the floor fragments hover.")]
    public float fragmentHeight = 1f;
    [Tooltip("Clear space a fragment needs around it. Raise if they still feel cramped.")]
    public float fragmentClearance = 0.8f;

    const string GeneratedName = "Generated";

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

    // Cells with clear, flat, unobstructed floor — the only places fragments may go
    HashSet<int> clearCells = new HashSet<int>();

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

        // Rename first, because Destroy only takes effect at the end of the frame
        old.name = "GeneratedOld";
        if (Application.isPlaying) Destroy(old.gameObject);
        else DestroyImmediate(old.gameObject);
    }

    // Builds the level. The same seed always gives the same layout.
    public void Generate(int seed)
    {
        if (theme == null)
        {
            Debug.LogError("LevelGenerator on " + name + " has no LevelTheme assigned.");
            return;
        }

        rng = new System.Random(useFixedSeed ? fixedSeed : seed);

        Transform content = GetContent();
        if (content == null)
        {
            Debug.LogError("LevelGenerator: no LevelContent found under " + name);
            return;
        }

        ClearGenerated();
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
            CarveMaze();
            if (DepthOf(exitCell) >= theme.minRouteCells) break;
        }

        BuildCells();
        PlaceDoorAndSpawn();
        PlaceFragments();
        ApplyFaceColor();
    }

    // --- Grid maths -------------------------------------------------

    int Index(int x, int y) { return x + y * gw; }
    int CellX(int c) { return c % gw; }
    int CellY(int c) { return c / gw; }
    bool InGrid(int x, int y) { return x >= 0 && x < gw && y >= 0 && y < gh; }

    // The world position of a cell's floor surface
    Vector3 CellCenter(int c, float height)
    {
        float x = (CellX(c) - (gw - 1) * 0.5f) * cell;
        float z = (CellY(c) - (gh - 1) * 0.5f) * cell;
        return new Vector3(x, height, z);
    }

    // --- Maze carving -----------------------------------------------

    // Depth-first maze over the whole grid, which gives long winding routes
    void CarveMaze()
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

        startCell = Index(gw / 2, 0); // near edge, middle
        visited[startCell] = true;
        depthCache[startCell] = 0;
        heightCache[startCell] = 0f;

        Stack<int> stack = new Stack<int>();
        stack.Push(startCell);

        while (stack.Count > 0)
        {
            int current = stack.Peek();
            List<int> options = UnvisitedNeighbours(current, visited);

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
        exitCell = Index(0, gh - 1);
        for (int x = 0; x < gw; x++)
        {
            int c = Index(x, gh - 1);
            if (DepthOf(c) > DepthOf(exitCell)) exitCell = c;
        }

        // Carve the main route, then fill the rest of the face
        carved.Clear();
        deadEnds.Clear();

        CarveChain(exitCell);
        CarveBranches();
        CarveRooms();
    }

    List<int> UnvisitedNeighbours(int c, bool[] visited)
    {
        List<int> list = new List<int>();
        int x = CellX(c), y = CellY(c);

        if (InGrid(x + 1, y) && !visited[Index(x + 1, y)]) list.Add(Index(x + 1, y));
        if (InGrid(x - 1, y) && !visited[Index(x - 1, y)]) list.Add(Index(x - 1, y));
        if (InGrid(x, y + 1) && !visited[Index(x, y + 1)]) list.Add(Index(x, y + 1));
        if (InGrid(x, y - 1) && !visited[Index(x, y - 1)]) list.Add(Index(x, y - 1));

        return list;
    }

    // How many cells from the start, following the carve tree back
    int DepthOf(int c)
    {
        if (c == startCell) return 0;
        if (depthCache[c] >= 0) return depthCache[c];
        if (parent[c] < 0) return 0;

        depthCache[c] = DepthOf(parent[c]) + 1;
        return depthCache[c];
    }

    // Each cell sits slightly above or below the one it was carved from,
    // so neighbours are always within one step of each other
    float HeightOf(int c)
    {
        if (c == startCell) return 0f;
        if (!float.IsNaN(heightCache[c])) return heightCache[c];
        if (parent[c] < 0) return 0f;

        float step = (float)(rng.NextDouble() * 2.0 - 1.0) * theme.stepHeight;
        float h = Mathf.Clamp(HeightOf(parent[c]) + step, -theme.maxHeightDrift, theme.maxHeightDrift);

        heightCache[c] = h;
        return h;
    }

    // Carves a cell and everything between it and the already-carved area
    void CarveChain(int tip)
    {
        int c = tip;
        while (c >= 0 && !carved.ContainsKey(c))
        {
            carved[c] = HeightOf(c);
            c = parent[c];
        }
    }

    // How far a cell is from carved ground, following the tree back
    int DistanceToCarved(int c)
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
    void CarveBranches()
    {
        int total = gw * gh;
        int target = Mathf.RoundToInt(total * Mathf.Clamp01(theme.coverage));

        List<int> candidates = new List<int>();
        for (int i = 0; i < total; i++)
            if (!carved.ContainsKey(i)) candidates.Add(i);

        Shuffle(candidates);

        // Pass 1: short spurs off the existing route, which read as proper dead ends
        foreach (int c in candidates)
        {
            if (carved.Count >= target) return;
            if (carved.ContainsKey(c)) continue;

            int distance = DistanceToCarved(c);
            if (distance < 1 || distance > theme.maxBranchLength) continue;

            CarveChain(c);
            deadEnds.Add(c);
        }

        // Pass 2: fill whatever's left, however long the chain needs to be
        foreach (int c in candidates)
        {
            if (carved.Count >= target) return;
            if (carved.ContainsKey(c)) continue;

            CarveChain(c);
            deadEnds.Add(c);
        }
    }

    // Opens some cells out into wider rooms by flattening their neighbours
    void CarveRooms()
    {
        List<int> existing = new List<int>(carved.Keys);

        foreach (int c in existing)
        {
            if (c == startCell || c == exitCell) continue;
            if (rng.NextDouble() > theme.roomChance) continue;

            float h = carved[c];
            int x = CellX(c), y = CellY(c);

            // Room cells share the centre cell's height, so the floor stays flat
            TryCarveFlat(x + 1, y, h);
            TryCarveFlat(x - 1, y, h);
            TryCarveFlat(x, y + 1, h);
            TryCarveFlat(x, y - 1, h);
        }
    }

    void TryCarveFlat(int x, int y, float height)
    {
        if (!InGrid(x, y)) return;

        int c = Index(x, y);
        if (carved.ContainsKey(c)) return;

        carved[c] = height;
    }

    // --- Geometry ---------------------------------------------------

    void BuildCells()
    {
        floorRenderers.Clear();
        clearCells.Clear();

        foreach (KeyValuePair<int, float> pair in carved)
        {
            int c = pair.Key;
            float h = baseHeight + pair.Value;
            Vector3 center = CellCenter(c, h);

            bool special = c == startCell || c == exitCell;
            Vector3 dir = DirectionFromParent(c);

            // Occasionally split the floor, leaving a gap to jump
            bool gap = !special && dir != Vector3.zero && rng.NextDouble() < theme.gapChance;

            if (gap) MakeSplitFloor(c, center, dir);
            else MakeFloor("Floor_" + c, center, new Vector3(cell, 1f, cell));

            // A split or special cell is never a fragment spot
            if (special || gap) continue;

            bool blocked = false;

            // A wall across the cell with a gap to walk through
            if (dir != Vector3.zero && rng.NextDouble() < theme.wallChance)
            {
                MakeWall(c, center, dir);
                blocked = true;
            }

            // Loose pillars to break up the space
            int pillars = rng.Next(theme.pillarsPerCell.x, theme.pillarsPerCell.y + 1);
            for (int p = 0; p < pillars; p++)
            {
                float ox = (float)(rng.NextDouble() - 0.5) * cell * 0.6f;
                float oz = (float)(rng.NextDouble() - 0.5) * cell * 0.6f;

                Vector3 pos = center + new Vector3(ox, theme.pillarSize, oz);
                MakeCube("Pillar_" + c + "_" + p, pos,
                         new Vector3(theme.pillarSize, theme.pillarSize * 2f, theme.pillarSize),
                         theme.wallMaterial);
                blocked = true;
            }

            // Only solid, flat, empty cells can hold a fragment
            if (!blocked) clearCells.Add(c);
        }
    }

    // Which way the player travels through this cell
    Vector3 DirectionFromParent(int c)
    {
        if (c == startCell || parent[c] < 0) return Vector3.zero;

        int p = parent[c];
        if (!carved.ContainsKey(p)) return Vector3.zero;

        int dx = CellX(c) - CellX(p);
        int dy = CellY(c) - CellY(p);
        return new Vector3(dx, 0f, dy).normalized;
    }

    // Two slabs with a gap between them, running across the travel direction
    void MakeSplitFloor(int c, Vector3 center, Vector3 dir)
    {
        float gap = Mathf.Clamp(RandomRange(theme.gapSize), 0.5f, cell - 1f);
        float piece = (cell - gap) * 0.5f;
        float offset = gap * 0.5f + piece * 0.5f;

        Vector3 size = AxisSize(dir, piece, cell);

        MakeFloor("Floor_" + c + "_A", center - dir * offset, size);
        MakeFloor("Floor_" + c + "_B", center + dir * offset, size);
    }

    // A wall across the cell, with an opening somewhere along it
    void MakeWall(int c, Vector3 center, Vector3 dir)
    {
        Vector3 cross = Vector3.Cross(Vector3.up, dir);

        float width = cell;
        float gapCenter = (float)(rng.NextDouble() - 0.5) * Mathf.Max(0f, width - theme.wallGap);

        float leftWidth = (gapCenter - theme.wallGap * 0.5f) + width * 0.5f;
        float rightWidth = width * 0.5f - (gapCenter + theme.wallGap * 0.5f);

        if (leftWidth > 0.3f)
        {
            Vector3 size = AxisSize(dir, 0.8f, leftWidth);
            size.y = theme.wallHeight;
            Vector3 pos = center + cross * (-width * 0.5f + leftWidth * 0.5f)
                                 + Vector3.up * theme.wallHeight * 0.5f;
            MakeCube("Wall_" + c + "_L", pos, size, theme.wallMaterial);
        }

        if (rightWidth > 0.3f)
        {
            Vector3 size = AxisSize(dir, 0.8f, rightWidth);
            size.y = theme.wallHeight;
            Vector3 pos = center + cross * (width * 0.5f - rightWidth * 0.5f)
                                 + Vector3.up * theme.wallHeight * 0.5f;
            MakeCube("Wall_" + c + "_R", pos, size, theme.wallMaterial);
        }
    }

    void PlaceDoorAndSpawn()
    {
        // Door of light at the far edge
        float exitHeight = baseHeight + carved[exitCell];
        Vector3 exitPos = CellCenter(exitCell, exitHeight);
        Vector3 dir = DirectionFromParent(exitCell);
        if (dir == Vector3.zero) dir = Vector3.forward;

        GameObject door = MakeCube("DreamDoor", exitPos + Vector3.up * 1.5f,
                                   new Vector3(3f, 3f, 0.5f), theme.doorMaterial);
        door.transform.localRotation = Quaternion.LookRotation(dir);
        door.GetComponent<Collider>().isTrigger = true;
        door.AddComponent<DreamDoor>();

        // Spawn point on the starting cell
        CubeFace face = GetComponent<CubeFace>();
        if (face != null && face.spawnPoint != null)
        {
            float startHeight = baseHeight + carved[startCell];
            face.spawnPoint.localPosition = CellCenter(startCell, startHeight) + Vector3.up * 0.2f;
            face.spawnPoint.localRotation = Quaternion.identity;
        }
    }

    // Fragments go in clear cells only, dead ends first so exploring pays off
    void PlaceFragments()
    {
        int wanted = theme.collectibles;
        if (LevelObjectives.Instance != null) LevelObjectives.Instance.SetRequired(0);
        if (wanted <= 0) return;

        // Dead ends that are actually clear make the best hiding spots
        List<int> spots = new List<int>();
        foreach (int c in deadEnds)
            if (clearCells.Contains(c)) spots.Add(c);

        Shuffle(spots);

        // Top up with any other clear cell if there aren't enough dead ends
        List<int> extras = new List<int>();
        foreach (int c in clearCells)
            if (!spots.Contains(c)) extras.Add(c);

        Shuffle(extras);
        spots.AddRange(extras);

        int placed = 0;
        foreach (int c in spots)
        {
            if (placed >= wanted) break;

            Vector3 pos = CellCenter(c, baseHeight + carved[c]) + Vector3.up * fragmentHeight;

            // Final safety check: skip the spot if anything solid is already there
            if (IsBlocked(pos)) continue;

            GameObject go = MakeCube("DreamFragment_" + placed, pos, Vector3.one * 0.6f,
                                     theme.collectibleMaterial);
            go.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            go.GetComponent<Collider>().isTrigger = true;
            go.AddComponent<DreamFragment>();
            placed++;
        }

        if (placed < wanted)
            Debug.LogWarning("Only placed " + placed + " of " + wanted +
                             " fragments — not enough clear cells. Lower Wall Chance or Pillars.");

        // Ask for what was actually placed, so the door can still open
        if (LevelObjectives.Instance != null) LevelObjectives.Instance.SetRequired(placed);
    }

    // True if anything solid overlaps this spot
    bool IsBlocked(Vector3 localPos)
    {
        Vector3 worldPos = root.TransformPoint(localPos);
        Collider[] hits = Physics.OverlapSphere(worldPos, fragmentClearance);

        foreach (Collider col in hits)
        {
            if (col.isTrigger) continue; // doors and other fragments don't count
            if (col.GetComponent<PlayerController>() != null) continue;

            // Floors sit below the fragment, so ignore anything clearly underneath
            if (col.bounds.max.y < worldPos.y - 0.4f) continue;

            return true;
        }
        return false;
    }

    // --- Building blocks --------------------------------------------

    // Floor slabs hang below the walking surface, so their tops are the ground
    void MakeFloor(string name, Vector3 center, Vector3 size)
    {
        Vector3 pos = center - new Vector3(0f, size.y * 0.5f, 0f);
        GameObject go = MakeCube(name, pos, size, theme.floorMaterial);
        floorRenderers.Add(go.GetComponent<Renderer>());
    }

    GameObject MakeCube(string name, Vector3 localPos, Vector3 size, Material mat)
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
    Vector3 AxisSize(Vector3 dir, float along, float across)
    {
        bool alongX = Mathf.Abs(dir.x) > 0.5f;
        return alongX ? new Vector3(along, 1f, across) : new Vector3(across, 1f, along);
    }

    float RandomRange(Vector2 range)
    {
        return Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());
    }

    void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Hands the new floors to CubeFace so the face colour still applies
    void ApplyFaceColor()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face == null) return;

        face.colorRenderers = floorRenderers.ToArray();
        face.ApplyColor();
    }

    Transform GetContent()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face != null && face.levelContent != null) return face.levelContent.transform;
        return transform.Find("LevelContent");
    }
}