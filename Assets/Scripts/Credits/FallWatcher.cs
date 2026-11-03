using UnityEngine;

// Watches for the player falling off the end of the dream, and starts
// the awakening sequence when he does.
// Put this on the final face, next to AwakeningGenerator.
[RequireComponent(typeof(CubeFace))]
public class FallWatcher : MonoBehaviour
{
    [Header("Which face")]
    [Tooltip("Index of this face in CubeWorld's Faces array (0 = Level 1).")]
    public int faceIndex = 5;

    [Header("The Fall")]
    [Tooltip("How far below the face he must drop before it counts as the jump.")]
    public float fallThreshold = 4f;

    [Tooltip("The ending sequence. Leave empty to find one in the scene.")]
    public AwakeningSequence sequence;

    bool triggered;

    void Start()
    {
        if (sequence == null) sequence = FindFirstObjectByType<AwakeningSequence>();
    }

    void Update()
    {
        if (triggered) return;

        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null) return;
        if (world.CurrentMode != CubeWorld.Mode.Playing) return;
        if (world.CurrentFaceIndex != faceIndex) return;

        // How far below the face's surface he is, along the face's own up
        Vector3 local = transform.InverseTransformPoint(world.player.transform.position);

        if (local.y > -fallThreshold) return;

        triggered = true;
        Debug.Log("He steps off the edge of the dream.");

        if (sequence != null) sequence.Begin(world.player.transform);
        else Debug.LogWarning("FallWatcher: no AwakeningSequence found in the scene.");
    }
}
