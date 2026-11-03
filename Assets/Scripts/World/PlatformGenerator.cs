using System.Collections.Generic;
using UnityEngine;

// Builds the Light Sleep level: islands hanging high above a dark floor, joined by platforms that
// all slide, rise and fall, or turn. The player has to time each hop from the start island to the door.
// Demons roam the floor below. Only the start island is low enough to climb back onto, so a fall means
// getting across the floor to it without being caught.
// Dream fragments ride on small moving side islands, off the main route.
// Put this on a face instead of the other generators.
[RequireComponent(typeof(CubeFace))]
public class PlatformGenerator : MonoBehaviour
{
    [Header("Theme")]
    [Tooltip("Optional. Gives the level its gravity, jump height, fragment count and sounds.")]
    public LevelTheme theme;
    [Tooltip("Fragments to place if there's no theme, or the theme's count is 0.")]
    public int fallbackFragments = 3;

    [Header("Seed")]
    public bool useFixedSeed = false;
    public int fixedSeed = 12345;

    [Header("Face Area")]
    public float faceSize = 30f;
    [Tooltip("Keeps the islands away from the very edge of the cube.")]
    public float edgeMargin = 1f;

    [Header("Heights")]
    [Tooltip("The top of the dark floor the demons roam.")]
    public float groundHeight = 0.5f;
    [Tooltip("How high the start island is above the floor. Kept under the jump height, because it's the one platform you can climb back onto.")]
    public float startIslandHeight = 1.5f;
    [Tooltip("How far above the top of a jump from the floor the first platform hangs. Small, so it's an easy step up from the start island but still out of reach from the floor.")]
    public float firstStepAboveReach = 0.3f;
    [Tooltip("Each platform along the route is this much higher than the one before, so the route climbs.")]
    public float climbPerPlatform = 0.35f;

    [Header("Layout")]
    [Tooltip("The face is split into this many squares each way. The route hops from square to square.")]
    [Range(3, 5)] public int gridSize = 4;
    [Tooltip("The gap between neighbouring platforms when they're at rest. They move, so it opens and closes a little.")]
    public float platformGap = 2f;
    [Tooltip("The route is retried until it has at least this many squares, from the start island to the door.")]
    public int minRouteLength = 9;
    public float platformThickness = 0.6f;

    [Header("Moving Platforms")]
    [Range(0f, 1f)]
    [Tooltip("How many of the platforms are lifts, going up and down. The rest slide or spin.")]
    public float liftShare = 0.45f;
    [Tooltip("How far a ferry slides each way, towards and away from its neighbours. Keep it under half the gap.")]
    public float ferryTravel = 0.9f;
    [Tooltip("How far a sweeping platform slides each way, side to side.")]
    public float sweepTravel = 2f;
    [Tooltip("The most a lift rises. Each lift is given a height between 60% of this and all of it.")]
    public float liftHeight = 2.5f;
    public float spinSpeed = 25f;
    [Tooltip("Seconds for a platform's trip there and back, picked at random in this range.")]
    public Vector2 periodRange = new Vector2(5f, 8f);
    [Range(0f, 1f)]
    [Tooltip("1 moves at an even speed all the way. 0 is a gentle swing that slows at each end.")]
    public float evenness = 0.8f;

    [Header("The Floor")]
    [Tooltip("Optional. Your own material for the floor.")]
    public Material groundMaterial;
    public Color groundColor = new Color(0.1f, 0.11f, 0.16f);
    [Tooltip("Height of the walls round the edge, so nothing wanders off the face.")]
    public float edgeWallHeight = 4f;

    [Header("Demons")]
    [Tooltip("The same hunter prefab used on the maze level. Leave empty for no demons.")]
    public GameObject demonPrefab;
    [Range(0, 8)] public int demonCount = 4;
    [Tooltip("Keep this below the player's move speed so they can be outrun.")]
    public float demonChaseSpeed = 3f;
    public float demonWanderSpeed = 1.5f;
    [Tooltip("How far a demon notices the player. Height counts too, so one right under a platform will see you.")]
    public float demonSightRange = 12f;
    [Tooltip("The demons only notice you while you're no higher than this above them, so they roam below until you fall. 0 means they always notice you.")]
    public float demonChaseBelow = 1f;
    public float demonCatchRange = 1.5f;
    [Tooltip("Demons never start closer than this to the start island.")]
    public float demonsClearOfStart = 7f;
    [Tooltip("Each demon is a little faster or slower than the others, so they don't move as a block.")]
    [Range(0f, 0.5f)] public float demonSpeedVariation = 0.2f;

    [Header("Fragments")]
    [Tooltip("How high above the island a fragment hovers.")]
    public float fragmentHeight = 1.2f;
    [Tooltip("Optional. A model to use instead of a plain crystal.")]
    public GameObject fragmentPrefab;
    public float fragmentSize = 0.6f;

    [Header("Altar")]
    public float altarSize = 1.4f;
    public float altarTriggerRadius = 2.5f;
    [Tooltip("Optional. A model to use instead of a plain block.")]
    public GameObject altarPrefab;

    [Header("Colours (used when no material is assigned)")]
    public Color islandColor = new Color(0.38f, 0.4f, 0.52f);
    public Color ferryColor = new Color(0.35f, 0.6f, 0.78f);
    public Color sweepColor = new Color(0.3f, 0.7f, 0.65f);
    public Color liftColor = new Color(0.6f, 0.45f, 0.82f);
    public Color spinColor = new Color(0.85f, 0.62f, 0.35f);
    public Color sideColor = new Color(0.32f, 0.62f, 0.55f);

    [Header("Materials (optional)")]
    public Material islandMaterial;
    public Material movingMaterial;
    public Material doorMaterial;

    const string GeneratedName = "Generated";

    enum Kind { Island, Ferry, Sweep, Lift, Spin }

    // One square of the route
    class Stop
    {
        public Vector2Int cell;
        public Kind kind = Kind.Ferry;
        public Vector3 centre;           // local, at the top surface
        public Transform platform;
    }

    // A small island off the route that carries a fragment
    class SideIsland
    {
        public Vector2Int cell;
        public Vector2Int fromHost;      // which way it lies from the platform it's beside
        public int host;                 // which stop on the route it's beside
    }

    Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();

    System.Random rng;
    Transform root;

    float usable, half, cell, platformSize, islandSize;
    List<Stop> route = new List<Stop>();

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
            Debug.LogError("PlatformGenerator: no LevelContent found under " + name);
            return;
        }

        ClearGenerated();
        root = new GameObject(GeneratedName).transform;
        root.SetParent(content, false);

        usable = faceSize - edgeMargin * 2f;
        half = usable * 0.5f;
        cell = usable / gridSize;

        // Platforms fill a square except for the gap, so the jump between them is always the same
        platformSize = Mathf.Max(2.5f, cell - platformGap);
        islandSize = platformSize + 1f;

        List<Vector2Int> path = FindRoute();
        BuildStops(path);

        List<SideIsland> sides = ChooseSideIslands(path);
        ChooseKinds();

        BuildRoute();
        BuildSideIslands(sides);

        PlaceSpawn();
        BuildEnd();
        BuildGround();
        BuildDemons();
    }

    // --- The route ------------------------------------------------------

    // A winding path through the grid, from one corner to the opposite one
    List<Vector2Int> FindRoute()
    {
        Vector2Int start = new Vector2Int(0, 0);
        Vector2Int end = new Vector2Int(gridSize - 1, gridSize - 1);

        List<Vector2Int> best = null;

        for (int attempt = 0; attempt < 80; attempt++)
        {
            List<Vector2Int> path = new List<Vector2Int> { start };
            bool[,] used = new bool[gridSize, gridSize];
            used[start.x, start.y] = true;

            if (!Walk(path, used, end)) continue;

            if (best == null || path.Count > best.Count) best = new List<Vector2Int>(path);
            if (best.Count >= minRouteLength) break;
        }

        // If nothing worked, a plain L shape always does
        if (best == null)
        {
            best = new List<Vector2Int>();
            for (int x = 0; x < gridSize; x++) best.Add(new Vector2Int(x, 0));
            for (int y = 1; y < gridSize; y++) best.Add(new Vector2Int(gridSize - 1, y));
        }

        return best;
    }

    // Wanders at random until it reaches the end, backing up when it gets stuck
    bool Walk(List<Vector2Int> path, bool[,] used, Vector2Int end)
    {
        Vector2Int here = path[path.Count - 1];
        if (here == end) return true;

        List<Vector2Int> options = new List<Vector2Int>
        {
            here + Vector2Int.right, here + Vector2Int.left,
            here + Vector2Int.up, here + Vector2Int.down
        };
        Shuffle(options);

        foreach (Vector2Int next in options)
        {
            if (next.x < 0 || next.y < 0 || next.x >= gridSize || next.y >= gridSize) continue;
            if (used[next.x, next.y]) continue;

            used[next.x, next.y] = true;
            path.Add(next);

            if (Walk(path, used, end)) return true;

            path.RemoveAt(path.Count - 1);
            used[next.x, next.y] = false;
        }

        return false;
    }

    void BuildStops(List<Vector2Int> path)
    {
        route.Clear();

        float jump = JumpHeight();

        // The start island is low enough to jump onto from the floor
        float startTop = groundHeight + Mathf.Min(startIslandHeight, jump * 0.8f);

        // Everything else hangs out of reach of a jump from the floor, and climbs from there
        float firstHang = groundHeight + jump + firstStepAboveReach;

        for (int i = 0; i < path.Count; i++)
        {
            Stop stop = new Stop();
            stop.cell = path[i];
            stop.kind = (i == 0 || i == path.Count - 1) ? Kind.Island : Kind.Ferry;

            float height = i == 0 ? startTop : firstHang + (i - 1) * climbPerPlatform;
            stop.centre = new Vector3(CellX(path[i].x), height, CellX(path[i].y));
            route.Add(stop);
        }
    }

    // How high the player can jump on this level
    float JumpHeight()
    {
        if (theme != null && theme.jumpHeight > 0f) return theme.jumpHeight;
        return 2f;
    }

    // The middle of a square along one axis
    float CellX(int index)
    {
        return -half + cell * (index + 0.5f);
    }

    // --- Side islands ----------------------------------------------------

    // Finds a free square beside the route for each fragment
    List<SideIsland> ChooseSideIslands(List<Vector2Int> path)
    {
        List<SideIsland> found = new List<SideIsland>();

        int wanted = FragmentCount();
        if (wanted <= 0) return found;

        HashSet<Vector2Int> taken = new HashSet<Vector2Int>(path);

        // Any stop between the two islands can host one
        List<int> candidates = new List<int>();
        for (int i = 1; i < path.Count - 1; i++) candidates.Add(i);
        Shuffle(candidates);

        foreach (int index in candidates)
        {
            if (found.Count >= wanted) break;

            Vector2Int[] around =
            {
                Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
            };

            foreach (Vector2Int step in around)
            {
                Vector2Int spot = path[index] + step;

                if (spot.x < 0 || spot.y < 0 || spot.x >= gridSize || spot.y >= gridSize) continue;
                if (taken.Contains(spot)) continue;

                taken.Add(spot);
                found.Add(new SideIsland { cell = spot, fromHost = step, host = index });
                break;
            }
        }

        return found;
    }

    int FragmentCount()
    {
        if (theme != null && theme.collectibles > 0) return theme.collectibles;
        return Mathf.Max(0, fallbackFragments);
    }

    // --- What each platform does -----------------------------------------

    // Every platform on the route moves. Lifts get the biggest share, the rest are mixed.
    void ChooseKinds()
    {
        for (int i = 1; i < route.Count - 1; i++)
            route[i].kind = PickMovingKind(i);
    }

    // A ferry only works where the route goes straight on, so it can carry you across
    Kind PickMovingKind(int i)
    {
        if (rng.NextDouble() < liftShare) return Kind.Lift;

        Vector2Int before = route[i].cell - route[i - 1].cell;
        Vector2Int after = route[i + 1].cell - route[i].cell;
        bool straight = before == after;

        List<Kind> options = new List<Kind> { Kind.Sweep, Kind.Spin };
        if (straight) options.Add(Kind.Ferry);

        return options[rng.Next(options.Count)];
    }

    // --- Building --------------------------------------------------------

    void BuildRoute()
    {
        for (int i = 0; i < route.Count; i++)
        {
            Stop stop = route[i];
            bool island = stop.kind == Kind.Island;

            // A spinner is made a little smaller, so its corners don't swing into its neighbours
            float size = island ? islandSize : platformSize;
            if (stop.kind == Kind.Spin) size = platformSize * 0.8f;

            // The start island is a solid block down to the floor, so it can be climbed from there
            float thickness = i == 0 ? stop.centre.y - groundHeight : platformThickness;

            stop.platform = MakePlatform("Platform_" + i, stop.centre, size, ColourFor(stop.kind), island, thickness);

            if (island) continue;

            AddMotion(stop, i);
        }
    }

    void AddMotion(Stop stop, int i)
    {
        float seconds = RandomRange(periodRange.x, periodRange.y);
        float offset = (float)rng.NextDouble();

        // The way the route runs through this square
        Vector2Int step = stop.cell - route[i - 1].cell;
        Vector3 along = new Vector3(step.x, 0f, step.y);
        Vector3 across = new Vector3(step.y, 0f, step.x);

        switch (stop.kind)
        {
            case Kind.Ferry:
                Move(stop.platform, MovingPlatform.Motion.Slide, along, ferryTravel, seconds, offset, 0f);
                break;

            case Kind.Sweep:
                Move(stop.platform, MovingPlatform.Motion.Slide, across, sweepTravel, seconds, offset, 0f);
                break;

            case Kind.Lift:
                MakeLift(stop.platform, RandomRange(liftHeight * 0.6f, liftHeight), seconds, offset);
                break;

            case Kind.Spin:
                float turn = rng.NextDouble() < 0.5 ? spinSpeed : -spinSpeed;
                Move(stop.platform, MovingPlatform.Motion.Spin, Vector3.up, 0f, seconds, offset, turn);
                break;
        }
    }

    void Move(Transform platform, MovingPlatform.Motion kind, Vector3 direction, float distance,
              float seconds, float offset, float degrees)
    {
        MovingPlatform mover = platform.gameObject.AddComponent<MovingPlatform>();
        mover.Setup(kind, direction, distance, seconds, offset, evenness, degrees);
    }

    // A lift starts halfway up its trip, so its lowest point is the height of the islands
    void MakeLift(Transform platform, float height, float seconds, float offset)
    {
        platform.localPosition += Vector3.up * height * 0.5f;
        Move(platform, MovingPlatform.Motion.Lift, Vector3.up, height * 0.5f, seconds, offset, 0f);
    }

    // The small islands that carry the fragments. They move too, and the fragment goes with them.
    void BuildSideIslands(List<SideIsland> sides)
    {
        float size = platformSize * 0.75f;

        for (int i = 0; i < sides.Count; i++)
        {
            Vector3 centre = new Vector3(CellX(sides[i].cell.x), route[sides[i].host].centre.y, CellX(sides[i].cell.y));
            Transform island = MakePlatform("SideIsland_" + i, centre, size, sideColor, false);

            float seconds = RandomRange(periodRange.x, periodRange.y);
            float offset = (float)rng.NextDouble();

            // Either bobbing up and down, or sliding along the edge of the route (never towards it)
            if (rng.NextDouble() < 0.6)
            {
                MakeLift(island, liftHeight * 0.7f, seconds, offset);
            }
            else
            {
                Vector2Int away = sides[i].fromHost;
                Vector3 along = new Vector3(away.y, 0f, away.x);
                Move(island, MovingPlatform.Motion.Slide, along, sweepTravel * 0.75f, seconds, offset, 0f);
            }

            // Parented to the island, so it rides along
            SpawnFragment(i, Vector3.up * fragmentHeight, island);
        }

        // Any fragments without an island of their own ride on the route platforms
        int placed = sides.Count;
        int wanted = FragmentCount();

        for (int i = 1; i < route.Count - 1 && placed < wanted; i++)
        {
            SpawnFragment(placed, Vector3.up * fragmentHeight, route[i].platform);
            placed++;
        }

        if (placed < wanted)
            Debug.LogWarning("PlatformGenerator: placed " + placed + " of " + wanted +
                             " fragments. Try a bigger grid.");
    }

    // The altar and the door of light, on the last island
    void BuildEnd()
    {
        Stop end = route[route.Count - 1];

        // The route reaches this island from the near side, so the door goes at the far edge
        Vector3 altarPos = end.centre + new Vector3(0f, 0f, -islandSize * 0.1f);
        DreamAltar altar = BuildAltar(altarPos);

        Vector3 doorPos = end.centre + new Vector3(0f, 1.6f, islandSize * 0.5f - 0.5f);
        GameObject door = MakeCube("DreamDoor", doorPos, new Vector3(3f, 3f, 0.5f), doorMaterial, Color.white, root);
        door.GetComponent<Collider>().isTrigger = true;
        door.AddComponent<DreamDoor>();

        int placed = CountFragments();
        LevelObjectives.Instance?.SetUpLevel(placed, altar, door);
    }

    // The dark floor the demons roam, with a wall round the edge
    void BuildGround()
    {
        MakeCube("Ground", new Vector3(0f, groundHeight - 0.5f, 0f),
                 new Vector3(faceSize, 1f, faceSize), groundMaterial, groundColor, root);

        float edge = faceSize * 0.5f;
        float wall = 0.5f;
        float centreY = groundHeight + edgeWallHeight * 0.5f;

        MakeCube("WallNorth", new Vector3(0f, centreY, edge - wall * 0.5f),
                 new Vector3(faceSize, edgeWallHeight, wall), groundMaterial, groundColor, root);
        MakeCube("WallSouth", new Vector3(0f, centreY, -edge + wall * 0.5f),
                 new Vector3(faceSize, edgeWallHeight, wall), groundMaterial, groundColor, root);
        MakeCube("WallEast", new Vector3(edge - wall * 0.5f, centreY, 0f),
                 new Vector3(wall, edgeWallHeight, faceSize), groundMaterial, groundColor, root);
        MakeCube("WallWest", new Vector3(-edge + wall * 0.5f, centreY, 0f),
                 new Vector3(wall, edgeWallHeight, faceSize), groundMaterial, groundColor, root);
    }

    // Demons wandering the floor. They hunt by sight, and a catch costs a heart.
    void BuildDemons()
    {
        if (demonPrefab == null || demonCount <= 0) return;

        CubeWorld world = CubeWorld.Instance;
        int faceIndex = world != null ? System.Array.IndexOf(world.faces, GetComponent<CubeFace>()) : -1;

        Vector3 start = route[0].centre;
        int placed = 0;
        int attempts = 0;

        while (placed < demonCount && attempts < demonCount * 30)
        {
            attempts++;

            Vector3 spot = new Vector3(RandomRange(-half + 1f, half - 1f), groundHeight + 0.05f,
                                       RandomRange(-half + 1f, half - 1f));

            // Never right beside the start island
            Vector2 apart = new Vector2(spot.x - start.x, spot.z - start.z);
            if (apart.magnitude < demonsClearOfStart) continue;

            BuildDemon(placed, spot, faceIndex);
            placed++;
        }
    }

    void BuildDemon(int number, Vector3 spot, int faceIndex)
    {
        GameObject demon = Instantiate(demonPrefab, root);
        demon.name = "Demon_" + number;
        demon.transform.localPosition = spot;
        demon.transform.localRotation = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f);

        // The maze hunter's brain can't find its way round open ground, so it's swapped for the open one
        Hunter mazeBrain = demon.GetComponent<Hunter>();
        if (mazeBrain != null) SafeDestroy(mazeBrain);

        OpenHunter brain = demon.GetComponent<OpenHunter>();
        if (brain == null) brain = demon.AddComponent<OpenHunter>();

        brain.face = transform;
        brain.faceIndex = faceIndex;
        brain.standHeight = groundHeight + 0.05f;
        brain.faceLimit = half - 0.5f;
        brain.sightRange = demonSightRange;
        brain.catchRange = demonCatchRange;
        brain.chaseMaxHeight = demonChaseBelow;

        float variation = 1f + RandomRange(-demonSpeedVariation, demonSpeedVariation);
        brain.chaseSpeed = demonChaseSpeed * variation;
        brain.wanderSpeed = demonWanderSpeed * variation;
    }

    int CountFragments()
    {
        int count = 0;
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
            if (child.name.StartsWith("DreamFragment_")) count++;
        return count;
    }

    void PlaceSpawn()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face == null || face.spawnPoint == null) return;

        face.spawnPoint.localPosition = route[0].centre + Vector3.up * 0.3f;
        face.spawnPoint.localRotation = Quaternion.identity;
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

    void SpawnFragment(int number, Vector3 localPos, Transform parent)
    {
        GameObject go;

        if (fragmentPrefab != null)
        {
            go = Instantiate(fragmentPrefab, parent);
            go.transform.localPosition = localPos;

            Collider col = go.GetComponentInChildren<Collider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * fragmentSize;
            go.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);

            if (theme != null && theme.collectibleMaterial != null)
                go.GetComponent<Renderer>().sharedMaterial = theme.collectibleMaterial;

            go.GetComponent<Collider>().isTrigger = true;
        }

        go.name = "DreamFragment_" + number;
        if (go.GetComponent<DreamFragment>() == null) go.AddComponent<DreamFragment>();
    }

    // --- Building blocks ---------------------------------------------------

    // An empty object at the top surface, with the slab hanging below it.
    // The object itself isn't scaled, so whatever is parented to it keeps its shape.
    Transform MakePlatform(string name, Vector3 surface, float size, Color colour, bool isIsland, float thickness = -1f)
    {
        if (thickness < 0f) thickness = platformThickness;

        GameObject holder = new GameObject(name);
        holder.transform.SetParent(root, false);
        holder.transform.localPosition = surface;

        Material material = isIsland ? islandMaterial : movingMaterial;

        MakeCube("Slab", new Vector3(0f, -thickness * 0.5f, 0f),
                 new Vector3(size, thickness, size), material, colour, holder.transform);

        return holder.transform;
    }

    GameObject MakeCube(string name, Vector3 localPos, Vector3 size, Material mat, Color fallback, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = mat != null ? mat : GetTinted(fallback);

        return go;
    }

    Color ColourFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Island: return islandColor;
            case Kind.Ferry: return ferryColor;
            case Kind.Sweep: return sweepColor;
            case Kind.Lift: return liftColor;
            default: return spinColor;
        }
    }

    // One material per colour, reused, rather than a copy on every cube
    Material GetTinted(Color color)
    {
        if (materialCache.TryGetValue(color, out Material cached) && cached != null)
            return cached;

        // Shader.Find fails in builds unless the shader is referenced by an asset,
        // so fall back to the one URP always includes
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.name = "Platform_" + ColorUtility.ToHtmlStringRGBA(color);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);

        materialCache[color] = mat;
        return mat;
    }

    float RandomRange(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)rng.NextDouble());
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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
