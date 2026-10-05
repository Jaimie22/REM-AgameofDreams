using UnityEngine;

// Generation settings for one level. Create via Assets > Create > REM > Level Theme.
// Make one asset per level, then drag it onto that face's LevelGenerator.
[CreateAssetMenu(fileName = "LevelTheme", menuName = "REM/Level Theme")]
public class LevelTheme : ScriptableObject
{
    [Header("Identity")]
    public string levelName = "Unconscious";
    [TextArea(2, 4)]
    public string designNotes = "";

    [Header("Grid")]
    [Tooltip("Rough size of one grid cell in units. The generator stretches cells to fill the face exactly.")]
    public float cellSize = 5f;

    [Header("Coverage")]
    [Range(0.3f, 1f)]
    [Tooltip("How much of the face gets filled. 1 = every cell, 0.85 = a few voids left over.")]
    public float coverage = 0.9f;

    [Header("Main Route")]
    [Tooltip("Shortest acceptable main route, in cells. The generator retries to beat this.")]
    public int minRouteCells = 8;

    [Header("Branches (dead ends)")]
    [Tooltip("Longest a short spur can be before the fill pass takes over.")]
    public int maxBranchLength = 3;

    [Header("Rooms")]
    [Range(0f, 1f)]
    [Tooltip("Chance a cell opens out into a wider room.")]
    public float roomChance = 0.15f;

    [Header("Height")]
    [Tooltip("Height change between neighbouring cells. Keep at or below Step Offset.")]
    public float stepHeight = 0.3f;
    [Tooltip("How far above or below the start the level is allowed to drift.")]
    public float maxHeightDrift = 2f;

    [Header("Obstacles")]
    [Range(0f, 1f)] public float wallChance = 0.25f;
    public float wallHeight = 3f;
    [Tooltip("Width of the opening left in each wall.")]
    public float wallGap = 2.5f;
    public Vector2Int pillarsPerCell = new Vector2Int(0, 1);
    public float pillarSize = 1.2f;

    [Header("Gaps to Jump")]
    [Range(0f, 1f)]
    [Tooltip("Chance a cell's floor is split, leaving a gap to cross.")]
    public float gapChance = 0.15f;
    [Tooltip("Width of those gaps in units.")]
    public Vector2 gapSize = new Vector2(1.5f, 2.5f);

    [Header("Movement")]
    [Tooltip("Gravity on this level. -20 is normal, -6 is dreamlike and floaty.")]
    public float gravity = -20f;
    [Tooltip("Jump height in units. 0 means no jumping on this level.")]
    public float jumpHeight = 2f;

    [Header("Collectibles")]
    [Tooltip("Dream fragments to scatter. The door stays shut until they're all found.")]
    public int collectibles = 0;

    [Header("Materials")]
    public Material floorMaterial;
    public Material wallMaterial;
    public Material doorMaterial;
    public Material collectibleMaterial;
}