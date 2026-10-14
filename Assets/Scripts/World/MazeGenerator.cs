using UnityEngine;
using System.Collections.Generic;

// Builds a proper maze across a face: corridors carved through a grid,
// with solid walls between them. Put this on a face instead of LevelGenerator.
[RequireComponent(typeof(CubeFace))]
public class MazeGenerator : MonoBehaviour
{
    [Header("Theme")]
    [Tooltip("The LevelTheme asset holding this level's materials and settings.")]
    public LevelTheme theme;

    [Header("Seed")]
    public bool useFixedSeed = false;
    public int fixedSeed = 12345;

    [Header("Face Area")]
    public float faceSize = 30f;
    [Tooltip("Keeps the maze away from the very edge of the cube.")]
    public float edgeMargin = 1f;
    [Tooltip("How high the maze floor sits above the cube surface.")]
    public float baseHeight = 1f;

    [Header("Maze Shape")]
    [Tooltip("Width of the corridors. Smaller = more cells = a harder maze.")]
    public float corridorWidth = 3f;
    [Tooltip("Thickness of the walls between corridors.")]
    public float wallThickness = 0.6f;
    [Tooltip("Height of the walls. Tall enough to block the view is the point.")]
    public float wallHeight = 3.5f;

    [Header("Difficulty")]
    [Range(0f, 1f)]
    [Tooltip("How often the carve keeps going straight. Higher = longer corridors, fewer turns.")]
    public float straightBias = 0.6f;
    [Range(0f, 0.3f)]
    [Tooltip("Knocks out some walls to make loops, so it's not a single solution.")]
    public float loopChance = 0.06f;

    [Header("Floor")]
    [Tooltip("Thickness of the floor slab under the maze.")]
    public float floorThickness = 1f;

    [Header("Fragments")]
    [Tooltip("How high above the floor fragments hover.")]
    public float fragmentHeight = 1f;
    [Tooltip("Optional. A model to use instead of a plain crystal.")]
    public GameObject fragmentPrefab;
    public float fragmentSize = 0.6f;

    [Header("Altar")]
    public float altarSize = 1.4f;
    public float altarTriggerRadius = 2f;
    [Tooltip("Optional. A model to use instead of a plain block.")]
    public GameObject altarPrefab;

    const string GeneratedName = "Generated";

    System.Random rng;
    Transform root;

    // Maze state
    int cellsWide, cellsDeep;
    float corridor;          // stretched to fill the face exactly
    float totalWidth, totalDepth;

    // The wall grid: odd indices are corridors, even indices are wall slots.
    // true means solid.
    bool[,] solid;
    int gridW, gridH;

    int[] cellParent;
    int[] cellDepth;
    int startCellX, startCellY;
    int exitCellX, exitCellY;

    List<int> deadEnds = new List<int>();
    List<Renderer> floorRenderers = new List<Renderer>();

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

        old.name = "GeneratedOld";
        safeDestroy(old.gameObject);
    }

    // Builds the maze. The same seed always gives the same layout.
    public void generate(int seed)
    {
        if (theme == null)
        {
            Debug.LogError("MazeGenerator on " + name + " has no LevelTheme assigned.");
            return;
        }

        rng = new System.Random(useFixedSeed ? fixedSeed : seed);

        Transform content = getContent();
        if (content == null)
        {
            Debug.LogError("MazeGenerator: no LevelContent found under " + name);
            return;
        }

        clearGenerated();
        root = new GameObject(GeneratedName).transform;
        root.SetParent(content, false);

        workOutGridSize();
        carveMaze();
        openEntranceAndExit();
        buildFloor();
        buildWalls();
        placeContents();
    }

    // --- Sizing -----------------------------------------------------

    // Works out how many cells fit, then stretches them to fill the face exactly
    private void workOutGridSize()
    {
        float usable = faceSize - edgeMargin * 2f;

        // Each cell needs one corridor plus one wall, and there's one extra wall at the end
        cellsWide = Mathf.Max(3, Mathf.FloorToInt((usable - wallThickness) /
                                                  (corridorWidth + wallThickness)));
        cellsDeep = cellsWide;

        // Stretch the corridors so the maze fills the whole face
        corridor = (usable - (cellsWide + 1) * wallThickness) / cellsWide;

        totalWidth = (cellsWide + 1) * wallThickness + cellsWide * corridor;
        totalDepth = totalWidth;

        gridW = cellsWide * 2 + 1;
        gridH = cellsDeep * 2 + 1;
    }

    // Even slots are walls, odd slots are corridors
    private float slotSize(int i)
    {
        return (i % 2 == 0) ? wallThickness : corridor;
    }

    // The centre of one slot, measured from the middle of the face
    private float slotCenter(int i)
    {
        float pos = 0f;
        for (int k = 0; k < i; k++) pos += slotSize(k);
        pos += slotSize(i) * 0.5f;
        return pos - totalWidth * 0.5f;
    }

    // The world position of a cell's centre
    private Vector3 cellCenter(int cx, int cy, float height)
    {
        return new Vector3(slotCenter(cx * 2 + 1), height, slotCenter(cy * 2 + 1));
    }

    // --- Maze carving -----------------------------------------------

    private int cellIndex(int cx, int cy) { return cx + cy * cellsWide; }
    private bool inCells(int cx, int cy) { return cx >= 0 && cx < cellsWide && cy >= 0 && cy < cellsDeep; }

    // Depth-first carve: start solid, then tunnel passages through
    private void carveMaze()
    {
        solid = new bool[gridW, gridH];
        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
                solid[x, y] = true;

        int total = cellsWide * cellsDeep;
        cellParent = new int[total];
        cellDepth = new int[total];
        bool[] visited = new bool[total];

        for (int i = 0; i < total; i++) cellParent[i] = -1;

        // Start at the middle of the near edge
        startCellX = cellsWide / 2;
        startCellY = 0;

        int start = cellIndex(startCellX, startCellY);
        visited[start] = true;
        solid[startCellX * 2 + 1, startCellY * 2 + 1] = false;

        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(startCellX, startCellY));

        Vector2Int lastDirection = Vector2Int.up;

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> options = unvisitedNeighbours(current, visited);

            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }

            // Carrying straight on more often gives long corridors instead of a scribble
            Vector2Int direction = PickDirection(options, current, lastDirection);
            Vector2Int next = current + direction;

            // Open the cell and the wall slot between the two cells
            solid[next.x * 2 + 1, next.y * 2 + 1] = false;
            solid[current.x * 2 + 1 + direction.x, current.y * 2 + 1 + direction.y] = false;

            int nextIndex = cellIndex(next.x, next.y);
            int currentIndex = cellIndex(current.x, current.y);

            visited[nextIndex] = true;
            cellParent[nextIndex] = currentIndex;
            cellDepth[nextIndex] = cellDepth[currentIndex] + 1;

            lastDirection = direction;
            stack.Push(next);
        }

        addLoops();
        findDeadEnds();
        findExit();
    }

    List<Vector2Int> unvisitedNeighbours(Vector2Int cell, bool[] visited)
    {
        List<Vector2Int> list = new List<Vector2Int>();

        Vector2Int[] directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        foreach (Vector2Int d in directions)
        {
            int nx = cell.x + d.x;
            int ny = cell.y + d.y;

            if (!inCells(nx, ny)) continue;
            if (visited[cellIndex(nx, ny)]) continue;

            list.Add(d);
        }

        return list;
    }

    // Prefers to keep going the same way, which makes proper corridors
    Vector2Int PickDirection(List<Vector2Int> options, Vector2Int cell, Vector2Int last)
    {
        if (options.Contains(last) && rng.NextDouble() < straightBias) return last;
        return options[rng.Next(options.Count)];
    }

    // Knocks a few extra walls out, so there's more than one way through
    private void addLoops()
    {
        if (loopChance <= 0f) return;

        for (int x = 1; x < gridW - 1; x++)
        {
            for (int y = 1; y < gridH - 1; y++)
            {
                // Only wall slots between two corridors can be opened
                bool isWallSlot = (x % 2 == 0) != (y % 2 == 0);
                if (!isWallSlot || !solid[x, y]) continue;

                if (rng.NextDouble() < loopChance) solid[x, y] = false;
            }
        }
    }

    // Cells with only one way in and out, which are the best places to hide things
    private void findDeadEnds()
    {
        deadEnds.Clear();

        for (int cx = 0; cx < cellsWide; cx++)
        {
            for (int cy = 0; cy < cellsDeep; cy++)
            {
                if (cx == startCellX && cy == startCellY) continue;

                int gx = cx * 2 + 1;
                int gy = cy * 2 + 1;
                int openSides = 0;

                if (!solid[gx + 1, gy]) openSides++;
                if (!solid[gx - 1, gy]) openSides++;
                if (!solid[gx, gy + 1]) openSides++;
                if (!solid[gx, gy - 1]) openSides++;

                if (openSides == 1) deadEnds.Add(cellIndex(cx, cy));
            }
        }
    }

    // The exit is the deepest cell along the far edge
    private void findExit()
    {
        exitCellX = 0;
        exitCellY = cellsDeep - 1;
        int best = -1;

        for (int cx = 0; cx < cellsWide; cx++)
        {
            int depth = cellDepth[cellIndex(cx, cellsDeep - 1)];
            if (depth > best)
            {
                best = depth;
                exitCellX = cx;
            }
        }
    }

    // Cuts a gap in the outer wall at the start and at the exit
    private void openEntranceAndExit()
    {
        solid[startCellX * 2 + 1, 0] = false;              // near edge
        solid[exitCellX * 2 + 1, gridH - 1] = false;       // far edge
    }

    // --- Geometry ---------------------------------------------------

    private void buildFloor()
    {
        floorRenderers.Clear();

        GameObject floor = makeCube("MazeFloor",
            new Vector3(0f, baseHeight - floorThickness * 0.5f, 0f),
            new Vector3(totalWidth, floorThickness, totalDepth),
            theme.floorMaterial);

        floorRenderers.Add(floor.GetComponent<Renderer>());
    }

    // Merges neighbouring wall slots into long boxes, so the maze
    // is built from a handful of walls rather than hundreds of blocks
    private void buildWalls()
    {
        bool[,] used = new bool[gridW, gridH];
        int built = 0;

        // Pass 1: long horizontal runs
        for (int y = 0; y < gridH; y++)
        {
            int x = 0;
            while (x < gridW)
            {
                if (!solid[x, y] || used[x, y]) { x++; continue; }

                int runStart = x;
                while (x < gridW && solid[x, y] && !used[x, y]) x++;
                int runEnd = x - 1;

                // A single slot might belong to a vertical run instead, so leave it
                if (runEnd == runStart) continue;

                for (int k = runStart; k <= runEnd; k++) used[k, y] = true;
                makeWallBox(runStart, runEnd, y, y, built++);
            }
        }

        // Pass 2: whatever is left, merged vertically
        for (int x = 0; x < gridW; x++)
        {
            int y = 0;
            while (y < gridH)
            {
                if (!solid[x, y] || used[x, y]) { y++; continue; }

                int runStart = y;
                while (y < gridH && solid[x, y] && !used[x, y]) y++;
                int runEnd = y - 1;

                for (int k = runStart; k <= runEnd; k++) used[x, k] = true;
                makeWallBox(x, x, runStart, runEnd, built++);
            }
        }
    }

    // One wall, spanning a range of slots
    private void makeWallBox(int x0, int x1, int y0, int y1, int number)
    {
        float left = slotCenter(x0) - slotSize(x0) * 0.5f;
        float right = slotCenter(x1) + slotSize(x1) * 0.5f;
        float back = slotCenter(y0) - slotSize(y0) * 0.5f;
        float front = slotCenter(y1) + slotSize(y1) * 0.5f;

        Vector3 center = new Vector3((left + right) * 0.5f,
                                     baseHeight + wallHeight * 0.5f,
                                     (back + front) * 0.5f);

        Vector3 size = new Vector3(right - left, wallHeight, front - back);

        makeCube("Wall_" + number, center, size, theme.wallMaterial);
    }

    // --- Contents ---------------------------------------------------

    private void placeContents()
    {
        // Spawn at the entrance
        CubeFace face = GetComponent<CubeFace>();
        if (face != null && face.spawnPoint != null)
        {
            face.spawnPoint.localPosition =
                cellCenter(startCellX, startCellY, baseHeight + 0.2f);
            face.spawnPoint.localRotation = Quaternion.identity;
        }

        // The altar goes at the deepest dead end, so it's a real journey
        int altarCell = deepestDeadEnd();
        int ax = altarCell % cellsWide;
        int ay = altarCell / cellsWide;

        Vector3 altarPos = cellCenter(ax, ay, baseHeight);
        DreamAltar altar = BuildAltar(altarPos);

        // The door of light sits in the gap in the far wall
        Vector3 doorPos = new Vector3(
            slotCenter(exitCellX * 2 + 1),
            baseHeight + wallHeight * 0.45f,
            slotCenter(gridH - 1));

        GameObject door = makeCube("DreamDoor", doorPos,
            new Vector3(corridor * 0.9f, wallHeight * 0.9f, wallThickness * 1.5f),
            theme.doorMaterial);

        door.GetComponent<Collider>().isTrigger = true;
        door.AddComponent<DreamDoor>();

        int placed = placeFragments(altarCell);
        LevelObjectives.Instance?.setUpLevel(placed, altar, door);
    }

    // The dead end furthest from the start
    private int deepestDeadEnd()
    {
        if (deadEnds.Count == 0) return cellIndex(exitCellX, exitCellY);

        int best = deadEnds[0];
        foreach (int c in deadEnds)
            if (cellDepth[c] > cellDepth[best]) best = c;

        return best;
    }

    DreamAltar BuildAltar(Vector3 position)
    {
        GameObject obj;

        if (altarPrefab != null)
        {
            obj = Instantiate(altarPrefab, root);
            obj.transform.localPosition = position;
        }
        else
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.transform.SetParent(root, false);
            obj.transform.localPosition = position + Vector3.up * altarSize * 0.5f;
            obj.transform.localScale = Vector3.one * altarSize;
        }

        obj.name = "DreamAltar";

        DreamAltar altar = obj.GetComponent<DreamAltar>();
        if (altar == null) altar = obj.AddComponent<DreamAltar>();

        // A trigger around it, so walking up delivers what he's carrying
        GameObject triggerObj = new GameObject("AltarTrigger");
        triggerObj.transform.SetParent(root, false);
        triggerObj.transform.localPosition = position + Vector3.up;

        SphereCollider trigger = triggerObj.AddComponent<SphereCollider>();
        trigger.radius = altarTriggerRadius;
        trigger.isTrigger = true;
        triggerObj.AddComponent<AltarTrigger>();

        return altar;
    }

    // Fragments go down the dead ends, so searching the maze pays off
    private int placeFragments(int altarCell)
    {
        int wanted = theme.collectibles;
        if (wanted <= 0) return 0;

        List<int> spots = new List<int>();
        foreach (int c in deadEnds)
            if (c != altarCell) spots.Add(c);

        shuffle(spots);

        // If there aren't enough dead ends, use the deepest ordinary cells
        if (spots.Count < wanted)
        {
            List<int> extras = new List<int>();
            for (int i = 0; i < cellsWide * cellsDeep; i++)
                if (i != altarCell && !spots.Contains(i) && cellDepth[i] > 2) extras.Add(i);

            extras.Sort((a, b) => cellDepth[b].CompareTo(cellDepth[a]));
            spots.AddRange(extras);
        }

        int count = Mathf.Min(wanted, spots.Count);

        for (int i = 0; i < count; i++)
        {
            int cx = spots[i] % cellsWide;
            int cy = spots[i] / cellsWide;

            Vector3 pos = cellCenter(cx, cy, baseHeight + fragmentHeight);
            spawnFragment(i, pos);
        }

        if (count < wanted)
            Debug.LogWarning("MazeGenerator: placed " + count + " of " + wanted +
                             " fragments. Try a smaller Corridor Width for more cells.");

        return count;
    }

    private void spawnFragment(int number, Vector3 localPos)
    {
        GameObject go;

        if (fragmentPrefab != null)
        {
            go = Instantiate(fragmentPrefab, root);
            go.transform.localPosition = localPos;

            Collider col = go.GetComponentInChildren<Collider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * fragmentSize;
            go.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);

            if (theme.collectibleMaterial != null)
                go.GetComponent<Renderer>().sharedMaterial = theme.collectibleMaterial;

            go.GetComponent<Collider>().isTrigger = true;
        }

        go.name = "DreamFragment_" + number;
        if (go.GetComponent<DreamFragment>() == null) go.AddComponent<DreamFragment>();
    }

    // this is what helps to create the building blocks for generating the maze

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

    private void shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void safeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    // this then attaches it to the desired face on the cube
    private Transform getContent()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face != null && face.levelContent != null) return face.levelContent.transform;
        return transform.Find("LevelContent");
    }
}
