using UnityEngine;

// Editor helper: builds the dream cube, all six faces, and placeholder levels for you.
// Add this to the DreamCube object (the one with CubeWorld), then right-click
// this component's title bar in the Inspector and choose "Build Dream Cube".
public class DreamCubeBuilder : MonoBehaviour
{
    [Header("Size")]
    [Tooltip("Width of the whole cube in units.")]
    public float cubeSize = 20f;

    [Header("Materials (optional)")]
    public Material bodyMaterial;
    public Material floorMaterial;
    public Material doorMaterial;
    [Tooltip("Grey material used as the fog on locked faces.")]
    public Material fogMaterial;

    [ContextMenu("Build Dream Cube")]
    void Build()
    {
        // Remove anything built previously so you can rebuild safely
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        // The cube must start unrotated for the face maths to work
        transform.rotation = Quaternion.identity;
        float half = cubeSize / 2f;

        // --- The solid cube body ---
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "CubeBody";
        body.transform.SetParent(transform, false);
        body.transform.localScale = Vector3.one * cubeSize;
        SetMaterial(body, bodyMaterial);

        // --- The six faces, in level order ---
        string[] names =
        {
            "Face_1_Top", "Face_2_Front", "Face_3_Right",
            "Face_4_Back", "Face_5_Left", "Face_6_Bottom"
        };

        // Where each face sits (centre of each side of the cube)
        Vector3[] positions =
        {
            Vector3.up * half, Vector3.back * half, Vector3.right * half,
            Vector3.forward * half, Vector3.left * half, Vector3.down * half
        };

        // How each face is turned so its "up" points outward
        Vector3[] rotations =
        {
            new Vector3(0, 0, 0), new Vector3(-90, 0, 0), new Vector3(0, 0, -90),
            new Vector3(90, 0, 0), new Vector3(0, 0, 90), new Vector3(180, 0, 0)
        };

        CubeFace[] faces = new CubeFace[6];

        for (int i = 0; i < 6; i++)
        {
            // Face anchor
            GameObject faceObj = new GameObject(names[i]);
            faceObj.transform.SetParent(transform, false);
            faceObj.transform.localPosition = positions[i];
            faceObj.transform.localEulerAngles = rotations[i];
            CubeFace face = faceObj.AddComponent<CubeFace>();

            // Level content: everything the player sees when the face is unlocked
            GameObject content = new GameObject("LevelContent");
            content.transform.SetParent(faceObj.transform, false);

            // Placeholder floor (a Plane is 10x10 units at scale 1)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(content.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            floor.transform.localScale = Vector3.one * (cubeSize / 10f) * 0.95f;
            SetMaterial(floor, floorMaterial);

            // Door of light at the far end of the face
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "DreamDoor";
            door.transform.SetParent(content.transform, false);
            door.transform.localPosition = new Vector3(0f, 1.5f, half * 0.7f);
            door.transform.localScale = new Vector3(2f, 3f, 0.5f);
            door.GetComponent<Collider>().isTrigger = true;
            door.AddComponent<DreamDoor>();
            SetMaterial(door, doorMaterial);

            // Spawn point at the near end, facing the door
            GameObject spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(faceObj.transform, false);
            spawn.transform.localPosition = new Vector3(0f, 1f, -half * 0.7f);

            // Fog cover shown while locked (no collider, it's just visual)
            GameObject fog = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fog.name = "LockedCover";
            fog.transform.SetParent(faceObj.transform, false);
            fog.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            fog.transform.localScale = Vector3.one * (cubeSize / 10f);
            DestroyImmediate(fog.GetComponent<Collider>());
            SetMaterial(fog, fogMaterial);

            // Hook everything up in the Inspector slots
            face.spawnPoint = spawn.transform;
            face.levelContent = content;
            face.lockedCover = fog;
            faces[i] = face;
        }

        // Fill in CubeWorld's Faces array automatically
        CubeWorld world = GetComponent<CubeWorld>();
        if (world != null) world.faces = faces;

#if UNITY_EDITOR
        // Tell Unity the scene changed so the build gets saved
        if (world != null) UnityEditor.EditorUtility.SetDirty(world);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif

        Debug.Log("Dream cube built!");
    }

    // Applies a material only if one was assigned
    void SetMaterial(GameObject obj, Material mat)
    {
        if (mat != null) obj.GetComponent<Renderer>().sharedMaterial = mat;
    }
}