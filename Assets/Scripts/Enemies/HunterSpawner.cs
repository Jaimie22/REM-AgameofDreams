using UnityEngine;

// Puts the hunter into the maze, far from where the player starts
// and somewhere it isn't buried in a wall.
// Put this on the face, next to MazeGenerator.
[RequireComponent(typeof(CubeFace))]
public class HunterSpawner : MonoBehaviour
{
    [Header("The Hunter")]
    [Tooltip("Your hunter model. Leave empty for a placeholder figure.")]
    public GameObject hunterPrefab;
    [Tooltip("Index of this face in CubeWorld's Faces array (0 = Level 1).")]
    public int faceIndex = 2;

    [Header("Placement")]
    [Tooltip("How far from the player's spawn it starts, in units.")]
    public float minDistanceFromSpawn = 20f;
    [Tooltip("It always starts on the opposite half of the maze from the player. This is how far past the middle, in units.")]
    public float oppositeMargin = 4f;
    public float standHeight = 0.2f;
    [Tooltip("How much clear space it needs, so it never starts inside a wall.")]
    public float clearance = 1f;
    [Tooltip("How many spots to try before settling for the best one found.")]
    public int placementAttempts = 60;

    [Header("Placeholder")]
    public float placeholderHeight = 2.4f;
    public Color bodyColor = new Color(0.05f, 0.04f, 0.06f);
    public Color eyeColor = new Color(0.9f, 0.15f, 0.1f);

    const string HunterName = "Hunter";

    // Called after the maze is built
    public void SpawnHunter()
    {
        CubeFace face = GetComponent<CubeFace>();
        if (face == null || face.levelContent == null) return;

        Transform content = face.levelContent.transform;
        Transform generated = content.Find("Generated");
        Transform parent = generated != null ? generated : content;

        // Clear by name, so an orphan from a previous build can't survive
        ClearHunters(content);

        // The new colliders need registering before we can test for walls
        Physics.SyncTransforms();

        Vector3 spot = PickSpot(face, parent);

        GameObject hunter = hunterPrefab != null
            ? Instantiate(hunterPrefab, parent)
            : BuildPlaceholder(parent);

        hunter.name = HunterName;
        hunter.transform.localPosition = spot;
        hunter.transform.localRotation = Quaternion.identity;

        Hunter brain = hunter.GetComponent<Hunter>();
        if (brain == null) brain = hunter.AddComponent<Hunter>();

        brain.face = transform;
        brain.faceIndex = faceIndex;
    }

    // Removes every hunter anywhere under this face, however it got there
    public void ClearHunters(Transform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);

            if (child.name == HunterName || child.GetComponent<Hunter>() != null)
            {
                SafeDestroy(child.gameObject);
                continue;
            }

            // Also look one level down, inside Generated
            for (int j = child.childCount - 1; j >= 0; j--)
            {
                Transform grandchild = child.GetChild(j);

                if (grandchild.name == HunterName || grandchild.GetComponent<Hunter>() != null)
                    SafeDestroy(grandchild.gameObject);
            }
        }
    }

    // Looks for open floor on the far side of the maze from the player
    Vector3 PickSpot(CubeFace face, Transform parent)
    {
        Vector3 spawn = face.spawnPoint != null
            ? face.spawnPoint.localPosition
            : Vector3.zero;

        MazeGenerator maze = GetComponent<MazeGenerator>();
        float reach = maze != null ? maze.faceSize * 0.45f : 13f;

        // Best: the far side, and a long way from the player
        if (TryFindSpot(parent, spawn, reach, true, true, out Vector3 spot)) return Raise(spot);

        // Next: still the far side, taking the furthest spot found
        if (TryFindSpot(parent, spawn, reach, true, false, out spot)) return Raise(spot);

        // Last resort, so it always ends up somewhere: the furthest clear spot anywhere
        TryFindSpot(parent, spawn, reach, false, false, out spot);
        return Raise(spot);
    }

    Vector3 Raise(Vector3 spot)
    {
        spot.y += standHeight;
        return spot;
    }

    // Tries random spots until one fits. With needDistance it stops at the first spot that is
    // far enough away, otherwise it keeps the furthest one found.
    bool TryFindSpot(Transform parent, Vector3 spawn, float reach,
                     bool farSideOnly, bool needDistance, out Vector3 found)
    {
        found = spawn;
        float bestDistance = -1f;

        // Which way the player's side of the maze lies, measured from the middle of the face
        Vector2 playerSide = new Vector2(spawn.x, spawn.z);
        bool hasSide = playerSide.magnitude > 1f;
        if (hasSide) playerSide.Normalize();

        for (int i = 0; i < placementAttempts * 3; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(-reach, reach),
                spawn.y,
                Random.Range(-reach, reach));

            // Never on the player's half of the maze
            if (farSideOnly && hasSide)
            {
                float along = Vector2.Dot(new Vector2(candidate.x, candidate.z), playerSide);
                if (along > -oppositeMargin) continue;
            }

            float distance = Vector3.Distance(candidate, spawn);
            if (needDistance && distance < minDistanceFromSpawn) continue;

            // Must be standing in open corridor, not inside a wall
            if (!IsClear(parent, candidate)) continue;

            if (needDistance)
            {
                found = candidate;
                return true;
            }

            if (distance > bestDistance)
            {
                bestDistance = distance;
                found = candidate;
            }
        }

        return bestDistance >= 0f;
    }

    // True if there's room to stand here without overlapping a wall
    bool IsClear(Transform parent, Vector3 localSpot)
    {
        Vector3 world = parent.TransformPoint(localSpot + Vector3.up * 1.2f);

        Collider[] hits = Physics.OverlapSphere(world, clearance);

        foreach (Collider col in hits)
        {
            if (col.isTrigger) continue;

            // Floors are fine, walls are not
            if (col.name.StartsWith("Wall")) return false;
        }

        return true;
    }

    // A tall thin silhouette, the same language as the demon sightings
    GameObject BuildPlaceholder(Transform parent)
    {
        GameObject obj = new GameObject(HunterName);
        obj.transform.SetParent(parent, false);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(obj.transform, false);
        body.transform.localPosition = Vector3.up * placeholderHeight * 0.5f;
        body.transform.localScale = new Vector3(0.6f, placeholderHeight * 0.5f, 0.6f);

        SafeDestroy(body.GetComponent<Collider>());
        TintObject(body, bodyColor, 0f);

        MakeEye(obj.transform, new Vector3(-0.14f, placeholderHeight * 0.85f, 0.26f));
        MakeEye(obj.transform, new Vector3(0.14f, placeholderHeight * 0.85f, 0.26f));

        return obj;
    }

    void MakeEye(Transform parent, Vector3 localPos)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        eye.transform.SetParent(parent, false);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * 0.11f;

        SafeDestroy(eye.GetComponent<Collider>());
        TintObject(eye, eyeColor, 2.5f);
    }

    void TintObject(GameObject go, Color color, float emission)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return;

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);

        if (emission > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emission);
        }

        rend.sharedMaterial = mat;
    }

    void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}