using UnityEngine;

// Keeps count of the dream fragments on the current level.
// Put this anywhere in the scene, for example on DreamCube.
public class LevelObjectives : MonoBehaviour
{
    public static LevelObjectives Instance { get; private set; }

    public int Required { get; private set; }
    public int Collected { get; private set; }
    public bool DoorOpen => Collected >= Required;

    void Awake()
    {
        Instance = this;
    }

    // Called when a level is generated
    public void SetRequired(int count)
    {
        Required = count;
        Collected = 0;
    }

    public void FragmentCollected()
    {
        Collected++;
        Debug.Log("Dream fragment " + Collected + " of " + Required);

        if (DoorOpen) Debug.Log("The door of light opens.");
    }
}
