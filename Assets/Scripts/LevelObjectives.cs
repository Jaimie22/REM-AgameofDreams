using UnityEngine;

// Keeps count of the dream fragments on the current level,
// and reveals the door of light once they've all been delivered to the altar.
// Put this anywhere in the scene, for example on DreamCube.
public class LevelObjectives : MonoBehaviour
{
    public static LevelObjectives Instance { get; private set; }

    public int Required { get; private set; }
    public int Carried { get; private set; }
    public int Delivered { get; private set; }

    public bool AllCollected => Carried >= Required && Required > 0;
    public bool DoorOpen => Delivered >= Required && Required > 0;

    // 0 to 1, used by the altar for its glow
    public float Progress => Required > 0 ? (float)Delivered / Required : 0f;

    DreamAltar altar;
    GameObject door;

    void Awake()
    {
        Instance = this;
    }

    // Called when a level is generated
    public void SetUpLevel(int required, DreamAltar levelAltar, GameObject levelDoor)
    {
        Required = required;
        Carried = 0;
        Delivered = 0;

        altar = levelAltar;
        door = levelDoor;

        // The door stays hidden until every fragment is delivered
        if (door != null) door.SetActive(false);
    }

    // Kept so older calls still work
    public void SetRequired(int count)
    {
        SetUpLevel(count, altar, door);
    }

    // A fragment was picked up off the floor
    public void FragmentCollected()
    {
        Carried++;
        Debug.Log("Dream fragment " + Carried + " of " + Required +
                  (AllCollected ? " — take them to the altar." : ""));
    }

    // The player reached the altar carrying fragments
    public void DeliverToAltar()
    {
        if (Carried <= 0) return;

        while (Carried > 0)
        {
            Carried--;
            Delivered++;
            if (altar != null) altar.OnFragmentDelivered();
        }

        Debug.Log("Delivered " + Delivered + " of " + Required + " to the altar.");

        if (DoorOpen) RevealDoor();
    }

    // Brings the door of light into existence
    void RevealDoor()
    {
        Debug.Log("The door of light appears.");
        if (door != null) door.SetActive(true);
    }
}
