using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The dig puzzle on the Slow Wave face. The player digs through the grass
// to cut a channel from the shore to the vat. When the channel connects,
// water flows along it and fills the vat, which opens the door.
public class DigPuzzle : MonoBehaviour
{
    // One square of diggable ground
    public class DigCell
    {
        public int x, y;
        public Vector3 center;       // local position of the grass surface
        public GameObject grass;     // removed when dug
        public Transform water;      // shown when the channel floods
        public bool dug;
        public bool flooded;
        public bool isVatCell;       // the vat stands here, so it can't be dug
        public bool blocked;         // a rock sits here, so it can't be dug
    }

    // Called by the spade pickup when the player collects it
    public void GiveSpade()
    {
        HasSpade = true;
    }

    [Header("Digging")]

    [Header("The Spade")]
    [Tooltip("He must find the spade before he can dig.")]
    public bool requiresSpade = true; 
    public bool HasSpade { get; private set; }
    [Tooltip("How close the player must be to dig a square.")]
    public float digRange = 2.5f;
    [Tooltip("Seconds between digs, so holding the key doesn't tear up the map.")]
    public float digCooldown = 0.25f;

    [Header("Flow")]
    [Tooltip("Seconds the water takes to spread from one square to the next.")]
    public float flowStepTime = 0.12f;
    [Tooltip("Seconds the vat takes to fill once the water reaches it.")]
    public float vatFillTime = 2.5f;
    [Tooltip("Pause before the door appears, so the fill has a moment to land.")]
    public float doorDelay = 0.6f;

    DigCell[,] cells;
    int gridW, gridH;
    int shoreRow;     
    DigCell vatCell;

    WaterVat vat;
    GameObject door;
    Transform root;

    float lastDigTime;
    bool flowing;
    bool finished;

    public int DugCount { get; private set; }

    // For the objective UI
    public bool Flowing => flowing;
    public bool Finished => finished;

    // Called by the generator once the terrain is built
    public void SetUp(DigCell[,] grid, int width, int height, int shoreRowIndex,
                      DigCell vatSquare, WaterVat levelVat, GameObject levelDoor,
                      Transform levelRoot)
    {
        cells = grid;
        gridW = width;
        gridH = height;
        shoreRow = shoreRowIndex;
        vatCell = vatSquare;
        vat = levelVat;
        door = levelDoor;
        root = levelRoot;

        DugCount = 0;
        flowing = false;
        finished = false;

        // The door doesn't exist until the vat is full
        if (door != null) door.SetActive(false);
    }

    void Update()
    {
        if (requiresSpade && !HasSpade)
        {
            // Only say so when he actually tries to dig
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.eKey.wasPressedThisFrame)
                Debug.Log("You need something to dig with.");

            return;
        }
        if (finished || flowing || cells == null) return;
        if (!IsActiveFace()) return;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        if (Time.time - lastDigTime < digCooldown) return;
        lastDigTime = Time.time;

        TryDig();
    }

    // True when the player is actually on this face
    bool IsActiveFace()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.CurrentMode != CubeWorld.Mode.Playing) return false;
        if (world.CurrentFaceIndex < 0) return false;

        return world.faces[world.CurrentFaceIndex] == GetComponent<CubeFace>();
    }

    // Digs the nearest square the player is standing on or beside
    void TryDig()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null) return;

        // The player is in world space, the grid is local to the face
        Vector3 playerLocal = root.InverseTransformPoint(world.player.transform.position);

        DigCell best = null;
        float bestDistance = digRange;

        foreach (DigCell cell in cells)
        {
            // Only consider squares that can be dug
            if (cell == null || cell.dug || cell.isVatCell || cell.blocked) continue;

            // Distance is measured in the XZ plane, ignoring height
            float d = Vector2.Distance(new Vector2(playerLocal.x, playerLocal.z),
                                       new Vector2(cell.center.x, cell.center.z));

            // The closest square within range is the one that gets dug
            if (d < bestDistance)
            {
                bestDistance = d;
                best = cell;
            }
        }

        if (best == null)
        {
            Debug.Log("Nothing to dig here.");
            return;
        }

        Dig(best);
    }

    // Breaks the grass on one square, leaving a trench
    void Dig(DigCell cell)
    {
        cell.dug = true;
        DugCount++;


        if (cell.grass != null) cell.grass.SetActive(false);

        // A dug square touching the water fills straight away
        CheckForFlow();
    }

    // Works out whether the channel now reaches the vat
    void CheckForFlow()
    {
        // Flood outwards from any dug square on the shore row
        Queue<DigCell> queue = new Queue<DigCell>();
        HashSet<DigCell> reached = new HashSet<DigCell>();

        for (int x = 0; x < gridW; x++)
        {
            DigCell inlet = cells[x, shoreRow];
            if (inlet != null && inlet.dug && reached.Add(inlet)) queue.Enqueue(inlet);
        }

        // No inlet yet, so nothing flows
        if (queue.Count == 0) return;

        List<DigCell> order = new List<DigCell>();
        bool reachesVat = false;

        while (queue.Count > 0)
        {
            DigCell cell = queue.Dequeue();
            order.Add(cell);

            foreach (DigCell next in Neighbours(cell))
            {
                if (next == null) continue;

                // The vat is fed by any dug square beside it
                if (next.isVatCell)
                {
                    reachesVat = true;
                    continue;
                }

                if (!next.dug || !reached.Add(next)) continue;
                queue.Enqueue(next);
            }
        }

        if (!reachesVat) return;

        StartCoroutine(RunTheWater(order));
    }

    // The four squares around one cell
    IEnumerable<DigCell> Neighbours(DigCell cell)
    {
        if (cell.x + 1 < gridW) yield return cells[cell.x + 1, cell.y];
        if (cell.x - 1 >= 0) yield return cells[cell.x - 1, cell.y];
        if (cell.y + 1 < gridH) yield return cells[cell.x, cell.y + 1];
        if (cell.y - 1 >= 0) yield return cells[cell.x, cell.y - 1];
    }

    // Water spreads square by square, then fills the vat, then the door appears
    IEnumerator RunTheWater(List<DigCell> order)
    {
        flowing = true;
        Debug.Log("The sea finds the channel.");


        // The list is in flood order, so this reads as water travelling
        foreach (DigCell cell in order)
        {
            if (cell.flooded) continue;

            cell.flooded = true;
            if (cell.water != null) cell.water.gameObject.SetActive(true);

            yield return new WaitForSeconds(flowStepTime);
        }

        if (vat != null)
        {
            vat.Fill(vatFillTime);
            yield return new WaitForSeconds(vatFillTime);
        }

        // The vat is full, so the water settles

        yield return new WaitForSeconds(doorDelay);

        Debug.Log("The vat overflows. The door of light appears.");
        if (door != null) door.SetActive(true);


        finished = true;
        flowing = false;
    }
}
