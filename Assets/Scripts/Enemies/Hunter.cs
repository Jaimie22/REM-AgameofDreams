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
    public float chaseSpeed = 2.5f;
    public float turnSpeed = 5f;

    [Header("Senses")]
    [Tooltip("How far it can smell the trail, in units.")]
    public float scentRange = 14f;
    [Tooltip("How far it can see him down a clear corridor.")]
    public float sightRange = 7f;
    [Tooltip("How close it has to be to catch him.")]
    public float catchRange = 1.3f;

    [Header("Height")]
    [Tooltip("How high above the maze floor it stands.")]
    public float standHeight = 0.1f;

    [Header("Tension")]
    [Range(0f, 1f)] public float wanderingTension = 0.15f;
    [Range(0f, 1f)] public float trackingTension = 0.4f;
    [Range(0f, 1f)] public float chasingTension = 0.85f;

    public State CurrentState { get; private set; } = State.Wandering;

    [HideInInspector] public Transform face;
    [HideInInspector] public int faceIndex = 2;

    MazeGenerator maze;

    int cellX, cellY;          // the cell it's standing in
    int nextX, nextY;          // the cell it's walking towards
    bool moving;

    void Start()
    {
        TryFindMaze();
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

        if (!moving) ChooseNextCell(player);

        StepTowardsNextCell();
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
            Vector2Int towards = BestTowards(options, player.position);
            SetNext(towards);
            return;
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

        // Nothing to follow, so amble, preferring not to double straight back
        CurrentState = State.Wandering;

        if (options.Count > 1)
        {
            Vector2Int back = new Vector2Int(cellX - (nextX - cellX), cellY - (nextY - cellY));
            options.RemoveAll(o => o.x == back.x && o.y == back.y);
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
    Vector2Int BestTowards(List<Vector2Int> options, Vector3 worldTarget)
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

        CubeFace cubeFace = world.faces[world.CurrentFaceIndex];
        if (cubeFace != null && cubeFace.spawnPoint != null)
            world.player.Spawn(cubeFace.spawnPoint);

        // The trail goes cold, so it has to find him again
        ScentTrail.Instance?.Clear();

        CurrentState = State.Wandering;
        moving = false;
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