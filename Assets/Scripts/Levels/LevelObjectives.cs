using UnityEngine;

// Keeps count of the dream fragments on the current level,
// and reveals the door of light once they've all been delivered to the altar.
// Put this anywhere in the scene, for example on DreamCube.
// Each face registers its own set, and the active one is switched on entry.
public class LevelObjectives : MonoBehaviour
{
    public static LevelObjectives Instance { get; private set; }

    // One level's worth of objectives
    class FaceObjectives
    {
        public int required;
        public int carried;
        public int delivered;
        public DreamAltar altar;
        public GameObject door;
    }

    // One entry per face, keyed by the face's own transform
    readonly System.Collections.Generic.Dictionary<Transform, FaceObjectives> byFace
        = new System.Collections.Generic.Dictionary<Transform, FaceObjectives>();

    FaceObjectives active;

    public int Required => active != null ? active.required : 0;
    public int Carried => active != null ? active.carried : 0;
    public int Delivered => active != null ? active.delivered : 0;

    public bool AllCollected => active != null && active.carried >= active.required && active.required > 0;
    public bool DoorOpen => active != null && active.delivered >= active.required && active.required > 0;

    // 0 to 1, used by the altar for its glow
    public float Progress => Required > 0 ? (float)Delivered / Required : 0f;

    void Awake()
    {
        Instance = this;
    }

    // Called by a generator when its face is built.
    // The face is worked out from the altar, so each level keeps its own count.
    public void SetUpLevel(int required, DreamAltar levelAltar, GameObject levelDoor)
    {
        Transform face = FindFace(levelAltar != null ? levelAltar.transform : levelDoor.transform);
        if (face == null) return;

        FaceObjectives objectives = new FaceObjectives
        {
            required = required,
            carried = 0,
            delivered = 0,
            altar = levelAltar,
            door = levelDoor
        };

        byFace[face] = objectives;

        // The door stays hidden until every fragment is delivered
        if (levelDoor != null) levelDoor.SetActive(false);
    }

    // Called by CubeWorld when a face is entered, so the right level is counted
    public void SetActiveFace(Transform face)
    {
        active = byFace.TryGetValue(face, out FaceObjectives objectives) ? objectives : null;
    }

    // Walks up the hierarchy to find which face this object belongs to
    Transform FindFace(Transform child)
    {
        Transform t = child;
        while (t != null)
        {
            if (t.GetComponent<CubeFace>() != null) return t;
            t = t.parent;
        }
        return null;
    }

    // A fragment was picked up off the floor
    public void FragmentCollected()
    {
        if (active == null) return;

        active.carried++;
        Debug.Log("Dream fragment " + active.carried + " of " + active.required +
                  (AllCollected ? " — take them to the altar." : ""));
    }

    // The player reached the altar carrying fragments
    public void DeliverToAltar()
    {
        if (active == null || active.carried <= 0) return;

        while (active.carried > 0)
        {
            active.carried--;
            active.delivered++;
            if (active.altar != null) active.altar.OnFragmentDelivered();
        }

        Debug.Log("Delivered " + active.delivered + " of " + active.required + " to the altar.");

        if (DoorOpen) RevealDoor();
    }

    // Brings this level's door of light into existence
    void RevealDoor()
    {
        Debug.Log("The door of light appears.");
        if (active != null && active.door != null) active.door.SetActive(true);
    }
}