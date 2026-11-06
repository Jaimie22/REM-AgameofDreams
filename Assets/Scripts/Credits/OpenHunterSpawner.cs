using System.Collections;
using UnityEngine;

// Puts hunters onto an open face, spread around the edges so the player
// has to pick a route through them to reach the board.
// Put this on the face, next to AwakeningGenerator.
[RequireComponent(typeof(CubeFace))]
public class OpenHunterSpawner : MonoBehaviour
{
    [Header("The Hunters")]
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

    [Header("Where They Can Go")]
    [Tooltip("How far from the centre they can go in the water. Just inside the edge of the pool.")]
    public float waterLimit = 14f;
    [Tooltip("How far out along the plank they can walk, past the water's edge. About half the board's length out.")]
    public float plankReach = 3f;
    public float plankHalfWidth = 0.8f;
    [Tooltip("How much higher the plank is than the water floor (board height plus half its thickness).")]
    public float plankRise = 0.7f;

    [Header("Chase")]
    [Tooltip("Start in a pack just behind the player, already after him. Off puts them on a ring instead.")]
    public bool startBehindPlayer = true;
    [Tooltip("How far behind the player's start the pack begins.")]
    public float behindDistance = 5f;
    [Tooltip("How far apart they stand side to side.")]
    public float behindSpread = 2.2f;
    [Tooltip("Their running speed as a fraction of the player's walking speed. Below 1 they can't catch him from behind, because he can't sprint here.")]
    [Range(0.3f, 0.95f)] public float chaseSpeedOfPlayer = 0.7f;
    public float wanderSpeed = 1f;
    [Tooltip("How far away they notice him. Large, so the chase is on from the start.")]
    public float sightRange = 60f;

    [Header("Difficulty")]
    [Tooltip("Each one is a little faster than the last, so they don't move as a block.")]
    public float speedVariation = 0.3f;

    const string HunterName = "OpenHunter";

    IEnumerator Start()
    {
        // Wait for the face to be built, so the player's start spot is where it's going to be
        yield return null;
        yield return null;
        SpawnHunters();
    }

    public void SpawnHunters()
    {

        CubeFace face = GetComponent<CubeFace>();
        if (face == null || face.levelContent == null) return;

        Transform parent = face.levelContent.transform;
        ClearHunters(parent);

        Vector3 spawn = face.spawnPoint != null
            ? face.spawnPoint.localPosition
            : Vector3.zero;

        if (startBehindPlayer)
        {
            for (int i = 0; i < hunterCount; i++)
            {
                float side = (i - (hunterCount - 1) * 0.5f) * behindSpread;
                float back = behindDistance + (i % 2) * 1.5f + Random.Range(0f, 0.6f);

                Vector3 packSpot = new Vector3(
                    Mathf.Clamp(spawn.x + side, -waterLimit, waterLimit),
                    standHeight,
                    Mathf.Clamp(spawn.z - back, -waterLimit, waterLimit));

                BuildHunter(parent, packSpot, i);
            }
            return;
        }

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

    // A plain red capsule
    GameObject MakePlaceholder(Transform parent)
    {
        GameObject hunter = new GameObject("Hunter");
        hunter.transform.SetParent(parent, false);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(hunter.transform, false);
        body.transform.localPosition = Vector3.up * 0.9f;
        body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
        Destroy(body.GetComponent<Collider>());

        Renderer rend = body.GetComponent<Renderer>();
        rend.material.SetColor("_BaseColor", new Color(0.7f, 0.12f, 0.12f));
        rend.material.SetColor("_Color", new Color(0.7f, 0.12f, 0.12f));

        return hunter;
    }

    void BuildHunter(Transform parent, Vector3 spot, int number)
    {
        GameObject hunter = MakePlaceholder(parent);
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
        brain.faceLimit = waterLimit;
        brain.plankReach = plankReach;
        brain.plankHalfWidth = plankHalfWidth;
        brain.plankRise = plankRise;

        // A little variety, so they don't advance in lockstep
        float variation = 1f + Random.Range(-speedVariation, speedVariation);
        float playerSpeed = CubeWorld.Instance != null && CubeWorld.Instance.player != null
            ? CubeWorld.Instance.player.moveSpeed : 5f;
        brain.chaseSpeed = Mathf.Min(playerSpeed * chaseSpeedOfPlayer * variation, playerSpeed * 0.92f);
        brain.wanderSpeed = wanderSpeed * variation;
        brain.sightRange = sightRange;
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