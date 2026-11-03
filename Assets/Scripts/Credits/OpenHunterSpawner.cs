using UnityEngine;

// Puts hunters onto an open face, spread around the edges so the player
// has to pick a route through them to reach the board.
// Put this on the face, next to AwakeningGenerator.
[RequireComponent(typeof(CubeFace))]
public class OpenHunterSpawner : MonoBehaviour
{
    [Header("The Hunters")]
    [Tooltip("The same hunter prefab used on the maze level.")]
    public GameObject hunterPrefab;
    [Tooltip("How many wade across the pool.")]
    [Range(1, 8)] public int hunterCount = 4;
    public int faceIndex = 5;

    [Header("Placement")]
    [Tooltip("How far from the centre they start, so none are on top of him.")]
    public float ringRadius = 12f;
    [Tooltip("How far apart they're spread. 1 is an even ring, lower clusters them.")]
    [Range(0.2f, 1f)] public float spread = 1f;
    [Tooltip("Nothing spawns closer than this to the player's start.")]
    public float clearOfSpawn = 12f;
    public float standHeight = 1.2f;

    [Header("Difficulty")]
    [Tooltip("Each one is a little faster than the last, so they don't move as a block.")]
    public float speedVariation = 0.3f;

    const string HunterName = "OpenHunter";

    void Start()
    {
        SpawnHunters();
    }

    public void SpawnHunters()
    {
        if (hunterPrefab == null) return;

        CubeFace face = GetComponent<CubeFace>();
        if (face == null || face.levelContent == null) return;

        Transform parent = face.levelContent.transform;
        ClearHunters(parent);

        Vector3 spawn = face.spawnPoint != null
            ? face.spawnPoint.localPosition
            : Vector3.zero;

        int placed = 0;
        int attempts = 0;

        while (placed < hunterCount && attempts < hunterCount * 10)
        {
            attempts++;

            // Spread around a ring, with a little scatter so it isn't a pattern
            float angle = (360f / hunterCount) * placed * spread
                        + Random.Range(-15f, 15f);

            float radians = angle * Mathf.Deg2Rad;
            float radius = ringRadius + Random.Range(-2f, 2f);

            Vector3 spot = new Vector3(
                Mathf.Cos(radians) * radius,
                standHeight,
                Mathf.Sin(radians) * radius);

            // Never right on top of where he starts
            if (Vector3.Distance(spot, spawn) < clearOfSpawn) continue;

            BuildHunter(parent, spot, placed);
            placed++;
        }

        if (placed < hunterCount)
            Debug.LogWarning("OpenHunterSpawner: placed " + placed + " of " + hunterCount +
                             ". Try a smaller Clear Of Spawn.");
    }

    void BuildHunter(Transform parent, Vector3 spot, int number)
    {
        GameObject hunter = Instantiate(hunterPrefab, parent);
        hunter.name = HunterName + "_" + number;
        hunter.transform.localPosition = spot;
        hunter.transform.localRotation = Quaternion.identity;

        // The maze hunter's brain won't work here, so make sure it's gone
        Hunter mazeBrain = hunter.GetComponent<Hunter>();
        if (mazeBrain != null) Destroy(mazeBrain);

        OpenHunter brain = hunter.GetComponent<OpenHunter>();
        if (brain == null) brain = hunter.AddComponent<OpenHunter>();

        brain.face = transform;
        brain.faceIndex = faceIndex;
        brain.standHeight = standHeight;

        // A little variety, so they don't advance in lockstep
        float variation = 1f + Random.Range(-speedVariation, speedVariation);
        brain.chaseSpeed *= variation;
        brain.wanderSpeed *= variation;
    }

    void ClearHunters(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);

            if (child.name.StartsWith(HunterName) || child.GetComponent<OpenHunter>() != null)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }
    }
}