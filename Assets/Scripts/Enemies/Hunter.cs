using System.Collections.Generic;
using UnityEngine;

// The thing in the maze. It walks cell to cell along open corridors, so it
// can never clip a wall or leave the maze. It wanders until it catches the
// player's scent, then follows the trail through the corridors.
public class Hunter : MonoBehaviour
{
    public enum State { Wandering, Tracking, Chasing }

    [Header("Movement")]
    public float wanderSpeed = 1.2f;
    public float trackSpeed = 1.8f;
    [Tooltip("Speed once it can see him. Keep it below the player's 5.")]
    public float chaseSpeed = 1.6f;
    public float turnSpeed = 5f;

    [Header("Senses")]
    [Tooltip("How far it can smell the trail, in units.")]
    public float scentRange = 5f;
    [Tooltip("How far it can see him down a clear corridor.")]
    public float sightRange = 5f;
    [Tooltip("How close it has to be to catch him.")]
    public float catchRange = 1.3f;

    [Header("Height")]
    [Tooltip("How high above the maze floor it stands.")]
    public float standHeight = 0.1f;

    [Header("Safety")]
    [Tooltip("If it ever ends up further than this from where it should be, it's put back on its cell, so it can't drift off the map.")]
    public float maxStrayDistance = 6f;

    [Header("Tension")]
    [Range(0f, 1f)] public float wanderingTension = 0.15f;
    [Range(0f, 1f)] public float trackingTension = 0.4f;
    [Range(0f, 1f)] public float chasingTension = 0.85f;

    [Header("Memory")]
    [Tooltip("Seconds it keeps heading for where it last saw him.")]
    public float giveUpTime = 3f;

    [Header("Stamina")]
    [Tooltip("Seconds of chasing before it slows, giving the player a chance.")]
    public float chaseStamina = 8f;
    [Tooltip("How much it slows once winded.")]
    [Range(0.3f, 1f)] public float tiredSpeedScale = 0.6f;

    float chaseTimer;

    // The last few cells it walked through, so wandering doesn't circle the same spot
    readonly List<Vector2Int> recent = new List<Vector2Int>();
    const int RecentMemory = 5;

    Vector3 lastKnown;
    bool hasLastKnown;
    float lostTimer;

    public State CurrentState { get; private set; } = State.Wandering;

    [HideInInspector] public Transform face;
    [HideInInspector] public int faceIndex = 2;

    MazeGenerator maze;
    HunterAudio hunterAudio;

    int cellX, cellY;          // the cell it's standing in
    int nextX, nextY;          // the cell it's walking towards
    int homeX, homeY;          // the cell it started in, where it goes back to after catching him
    bool hasHome;
    bool moving;

    // The sounds the maze's theme gives it
    public LevelTheme Theme => maze != null ? maze.theme : null;

    void Awake()
    {
        StopRootMotion();

        // Added here so the prefab needs no setup
        hunterAudio = GetComponent<HunterAudio>();
        if (hunterAudio == null) hunterAudio = gameObject.AddComponent<HunterAudio>();
    }

    void Start()
    {
        TryFindMaze();
    }

    // The crawl animation carries its own movement, which pushes the hunter along on top of
    // the script. It kept doing that while the player was in the map or cube view, so the
    // hunter crawled off the edge of the maze. The script walks it cell to cell instead.
    void StopRootMotion()
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>())
            animator.applyRootMotion = false;
    }

    // Finds the maze, whenever the spawner gets round to assigning the face
    bool TryFindMaze()
    {
        if (maze != null) return true;

        if (face != null) maze = face.GetComponent<MazeGenerator>();
        if (maze == null) maze = GetComponentInParent<MazeGenerator>();

        if (maze == null) return false;

        // Found it, so work out which cell it's standing in
        if (face == null) face = maze.transform;
        SnapToNearestCell();

        return true;
    }

    void Update()
    {
        // Keep trying until the spawner has finished wiring it up
        if (maze == null && !TryFindMaze()) return;

        if (!IsActiveFace()) return;

        CubeWorld world = CubeWorld.Instance;
        Transform player = world.player.transform;

        // Caught him
        if (Vector3.Distance(transform.position, player.position) < catchRange)
        {
            Catch(world);
            return;
        }

        // Chasing is exhausting: it can't keep top speed forever
        if (CurrentState == State.Chasing) chaseTimer += Time.deltaTime;
        else chaseTimer = Mathf.Max(0f, chaseTimer - Time.deltaTime * 0.5f);

        if (!moving) ChooseNextCell(player);

        StepTowardsNextCell();
        KeepOnPath();
        FeedTension();
    }

    // --- Navigation -------------------------------------------------

    // Works out which cell it's standing in, and plants it there
    void SnapToNearestCell()
    {
        Vector3 local = face.InverseTransformPoint(transform.position);

        if (!maze.PositionToCell(local, out cellX, out cellY))
        {
            cellX = 0;
            cellY = 0;
        }

        transform.position = CellWorld(cellX, cellY);
        nextX = cellX;
        nextY = cellY;
        moving = false;

        // The first cell it lands in is its home
        if (!hasHome)
        {
            homeX = cellX;
            homeY = cellY;
            hasHome = true;
        }
    }

    // Picks which neighbouring cell to walk into next
    void ChooseNextCell(Transform player)
    {
        List<Vector2Int> options = OpenNeighbours();
        if (options.Count == 0) return;

        // Can it see him down a corridor? Then head straight for him
        if (CanSeePlayer(player))
        {
            CurrentState = State.Chasing;
            lastKnown = player.position;
            hasLastKnown = true;
            lostTimer = 0f;

            SetNext(BestTowards(options, player.position));
            return;
        }

        // Just lost sight of him, so head for where he was last seen
        if (hasLastKnown)
        {
            lostTimer += Time.deltaTime;

            if (lostTimer < giveUpTime)
            {
                CurrentState = State.Tracking;
                SetNext(BestTowards(options, lastKnown));

                // Arrived and he's not here, so stop chasing a memory
                if (Vector3.Distance(transform.position, lastKnown) < 2f)
                    hasLastKnown = false;

                return;
            }

            hasLastKnown = false;
        }

        // Follow the freshest scent it can smell
        ScentTrail trail = ScentTrail.Instance;
        if (trail != null)
        {
            ScentTrail.Mark mark = trail.FindFreshest(transform.position, scentRange);

            if (mark != null)
            {
                CurrentState = State.Tracking;
                SetNext(BestTowards(options, mark.position));
                return;
            }
        }

        // Nothing to follow, so amble. It prefers cells it hasn't just been through,
        // so it explores the maze rather than pacing one corridor
        CurrentState = State.Wandering;

        if (options.Count > 1)
        {
            List<Vector2Int> fresh = options.FindAll(o => !recent.Contains(o));
            if (fresh.Count > 0) options = fresh;
        }

        SetNext(options[Random.Range(0, options.Count)]);
    }

    // Every neighbouring cell with no wall in the way
    List<Vector2Int> OpenNeighbours()
    {
        List<Vector2Int> list = new List<Vector2Int>();

        if (maze.IsOpenBetween(cellX, cellY, cellX + 1, cellY))
            list.Add(new Vector2Int(cellX + 1, cellY));

        if (maze.IsOpenBetween(cellX, cellY, cellX - 1, cellY))
            list.Add(new Vector2Int(cellX - 1, cellY));

        if (maze.IsOpenBetween(cellX, cellY, cellX, cellY + 1))
            list.Add(new Vector2Int(cellX, cellY + 1));

        if (maze.IsOpenBetween(cellX, cellY, cellX, cellY - 1))
            list.Add(new Vector2Int(cellX, cellY - 1));

        return list;
    }

    // Whichever open neighbour gets it closest to a spot
    // Follows the real corridors (shortest route), so dead ends and walls don't trap it
    Vector2Int BestTowards(List<Vector2Int> options, Vector3 worldTarget)
    {
        if (TargetCell(worldTarget, out int goalX, out int goalY)
            && FirstStepTo(goalX, goalY, out Vector2Int step)
            && options.Contains(step))
            return step;

        return GreedyTowards(options, worldTarget);
    }

    // The cell a world position belongs to, or the nearest one if it's inside a wall
    bool TargetCell(Vector3 worldTarget, out int gx, out int gy)
    {
        Vector3 local = face.InverseTransformPoint(worldTarget);
        if (maze.PositionToCell(local, out gx, out gy)) return true;

        float best = float.MaxValue;
        gx = cellX;
        gy = cellY;

        for (int x = 0; x < maze.CellsWide; x++)
            for (int y = 0; y < maze.CellsDeep; y++)
            {
                float d = (CellWorld(x, y) - worldTarget).sqrMagnitude;
                if (d < best) { best = d; gx = x; gy = y; }
            }

        return best < float.MaxValue;
    }

    // Breadth-first search through the open corridors. Gives the first cell to step into
    bool FirstStepTo(int goalX, int goalY, out Vector2Int step)
    {
        step = new Vector2Int(cellX, cellY);
        if (goalX == cellX && goalY == cellY) return false;

        int w = maze.CellsWide, d = maze.CellsDeep;
        int[] from = new int[w * d];
        for (int i = 0; i < from.Length; i++) from[i] = -2;

        int start = cellY * w + cellX;
        int goal = goalY * w + goalX;
        from[start] = -1;

        Queue<int> queue = new Queue<int>();
        queue.Enqueue(start);

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0 && from[goal] == -2)
        {
            int current = queue.Dequeue();
            int cx = current % w, cy = current / w;

            for (int k = 0; k < 4; k++)
            {
                int nx = cx + dx[k], ny = cy + dy[k];
                if (nx < 0 || ny < 0 || nx >= w || ny >= d) continue;

                int n = ny * w + nx;
                if (from[n] != -2) continue;
                if (!maze.IsOpenBetween(cx, cy, nx, ny)) continue;

                from[n] = current;
                queue.Enqueue(n);
            }
        }

        if (from[goal] == -2) return false; // no route

        // Walk back from the goal to the cell right after the start
        int node = goal;
        while (from[node] != start) node = from[node];

        step = new Vector2Int(node % w, node / w);
        return true;
    }

    // Fallback: whichever open neighbour is nearest as the crow flies
    Vector2Int GreedyTowards(List<Vector2Int> options, Vector3 worldTarget)
    {
        Vector2Int best = options[0];
        float bestDistance = float.MaxValue;

        foreach (Vector2Int option in options)
        {
            float distance = Vector3.Distance(CellWorld(option.x, option.y), worldTarget);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = option;
            }
        }

        return best;
    }

    void SetNext(Vector2Int cell)
    {
        nextX = cell.x;
        nextY = cell.y;
        moving = true;
    }

    // Walks towards the next cell, arriving exactly on its centre
    void StepTowardsNextCell()
    {
        Vector3 destination = CellWorld(nextX, nextY);
        Vector3 toThere = destination - transform.position;

        if (toThere.sqrMagnitude < 0.0025f)
        {
            // Arrived, so this is now its cell
            transform.position = destination;

            Vector2Int left = new Vector2Int(cellX, cellY);
            if (recent.Count >= RecentMemory) recent.RemoveAt(0);
            recent.Add(left);

            cellX = nextX;
            cellY = nextY;
            moving = false;
            return;
        }

        float speed = CurrentState switch
        {
            State.Chasing => chaseSpeed,
            State.Tracking => trackSpeed,
            _ => wanderSpeed
        };

        // Winded after a long chase, so a determined runner can shake it off
        if (chaseTimer > chaseStamina) speed *= tiredSpeedScale;

        transform.position = Vector3.MoveTowards(transform.position, destination,
                                                 speed * Time.deltaTime);

        // Face the way it's walking, staying upright on the face
        Vector3 flat = Vector3.ProjectOnPlane(toThere, face.up);

        if (flat.sqrMagnitude > 0.001f)
        {
            Quaternion wanted = Quaternion.LookRotation(flat, face.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted,
                                                  turnSpeed * Time.deltaTime);
        }
    }

    // If anything ever pushes it off its path, put it back on its cell
    // rather than letting it drift out of the maze and off the map
    void KeepOnPath()
    {
        Vector3 cellCentre = CellWorld(cellX, cellY);
        Vector3 destination = CellWorld(nextX, nextY);

        float strayed = Mathf.Min(Vector3.Distance(transform.position, cellCentre),
                                  Vector3.Distance(transform.position, destination));

        if (strayed <= maxStrayDistance) return;

        Debug.LogWarning("Hunter strayed off its path, so it was put back on its cell.");

        transform.position = cellCentre;
        nextX = cellX;
        nextY = cellY;
        moving = false;
    }

    // Puts it back in the cell it started in, so it isn't waiting beside the player's spawn
    void ResetToHome()
    {
        cellX = homeX;
        cellY = homeY;
        nextX = homeX;
        nextY = homeY;
        moving = false;

        transform.position = CellWorld(homeX, homeY);
        CurrentState = State.Wandering;
        recent.Clear();
    }

    // The world position of a cell, standing on its floor
    Vector3 CellWorld(int cx, int cy)
    {
        return face.TransformPoint(maze.CellPosition(cx, cy, standHeight));
    }

    // --- Senses -----------------------------------------------------

    // True if it's in the same row or column with no walls between
    bool CanSeePlayer(Transform player)
    {
        if (Vector3.Distance(transform.position, player.position) > sightRange) return false;

        Vector3 local = face.InverseTransformPoint(player.position);
        if (!maze.PositionToCell(local, out int px, out int py)) return false;

        // Straight along a row
        if (py == cellY)
        {
            int step = px > cellX ? 1 : -1;
            for (int x = cellX; x != px; x += step)
                if (!maze.IsOpenBetween(x, cellY, x + step, cellY)) return false;

            return true;
        }

        // Straight along a column
        if (px == cellX)
        {
            int step = py > cellY ? 1 : -1;
            for (int y = cellY; y != py; y += step)
                if (!maze.IsOpenBetween(cellX, y, cellX, y + step)) return false;

            return true;
        }

        return false;
    }

    void FeedTension()
    {
        if (HeartbeatSystem.Instance == null) return;

        float tension = CurrentState switch
        {
            State.Chasing => chasingTension,
            State.Tracking => trackingTension,
            _ => wanderingTension
        };

        HeartbeatSystem.Instance.SetTension(tension);
    }

    // Sends the player back to the start of the maze
    void Catch(CubeWorld world)
    {
        Debug.Log("It found you.");

        // The lunge, then the player's own reaction
        hunterAudio.PlayAttack();
        world.player.Audio.PlayDeath();

        // It costs the player a heart
        world.player.Vitals.TakeDamage();

        CubeFace cubeFace = world.faces[world.CurrentFaceIndex];
        if (cubeFace != null && cubeFace.spawnPoint != null)
            world.player.Spawn(cubeFace.spawnPoint);

        // The trail goes cold, so it has to find him again
        ScentTrail.Instance?.Clear();

        // And it goes back to where it started, rather than sitting on top of him
        ResetToHome();
    }

    bool IsActiveFace()
    {
        CubeWorld world = CubeWorld.Instance;
        return world != null
            && world.CurrentMode == CubeWorld.Mode.Playing
            && world.CurrentFaceIndex == faceIndex
            && world.player != null
            && face != null
            && maze != null;
    }
}