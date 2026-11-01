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

    // Looks for open floor far from the player, rather than just mirroring the spawn
    Vector3 PickSpot(CubeFace face, Transform parent)
    {
        Vector3 spawn = face.spawnPoint != null
            ? face.spawnPoint.localPosition
            : Vector3.zero;

        MazeGenerator maze = GetComponent<MazeGenerator>();
        float reach = maze != null ? maze.faceSize * 0.45f : 13f;

        Vector3 best = spawn;
        float bestDistance = -1f;

        for (int i = 0; i < placementAttempts; i++)
        {
            // Somewhere random on the face
            Vector3 candidate = new Vector3(
                Random.Range(-reach, reach),
                spawn.y,
                Random.Range(-reach, reach));

            float distance = Vector3.Distance(candidate, spawn);
            if (distance < minDistanceFromSpawn) continue;

            // Must be standing in open corridor, not inside a wall
            if (!IsClear(parent, candidate)) continue;

            best = candidate;
            bestDistance = distance;
            break;
        }

        // Nothing ideal turned up, so take the furthest clear spot we can find
        if (bestDistance < 0f)
        {
            for (int i = 0; i < placementAttempts; i++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(-reach, reach),
                    spawn.y,
                    Random.Range(-reach, reach));

                if (!IsClear(parent, candidate)) continue;

                float distance = Vector3.Distance(candidate, spawn);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }

        best.y += standHeight;
        return best;
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