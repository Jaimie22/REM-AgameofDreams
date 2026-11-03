using UnityEngine;

// A hunter for open ground, where there are no walls to navigate around.
// It wades toward the player across the pool, and is kept on the face
// so it can never wander off the edge.
public class OpenHunter : MonoBehaviour
{
    public enum State { Wandering, Chasing }

    [Header("Movement")]
    public float wanderSpeed = 1f;
    [Tooltip("Keep this below the player's move speed, so he can outrun it.")]
    public float chaseSpeed = 0.02f;
    public float turnSpeed = 3f;

    [Header("Senses")]
    [Tooltip("How far it notices him across the open water.")]
    public float sightRange = 14f;
    [Tooltip("How close it has to be to catch him.")]
    public float catchRange = 1.5f;

    [Header("Wandering")]
    public float wanderDistance = 7f;
    public float wanderTimeout = 5f;

    [Header("Bounds")]
    [Tooltip("How far from the centre of the face it may roam.")]
    public float faceLimit = 13f;
    [Tooltip("How high above the floor it stands.")]
    public float standHeight = 0.1f;

    [Header("Which face")]
    public int faceIndex = 5;

    public State CurrentState { get; private set; } = State.Wandering;

    [HideInInspector] public Transform face;

    Vector3 target;
    float wanderStarted;

    void Start()
    {
        if (face == null) face = transform.parent;
        PickWanderTarget();
    }

    void Update()
    {
        if (!IsActiveFace()) return;

        CubeWorld world = CubeWorld.Instance;
        Transform player = world.player.transform;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance < catchRange)
        {
            Catch(world);
            return;
        }

        // Open ground, so it simply sees him or it doesn't
        if (distance < sightRange)
        {
            CurrentState = State.Chasing;
            target = player.position;
        }
        else
        {
            CurrentState = State.Wandering;

            bool arrived = Vector3.Distance(transform.position, target) < 1.5f;
            if (arrived || Time.time - wanderStarted > wanderTimeout) PickWanderTarget();
        }

        Move();
    }

    void Move()
    {
        Vector3 toTarget = Vector3.ProjectOnPlane(target - transform.position, face.up);
        if (toTarget.sqrMagnitude < 0.01f) return;

        Vector3 direction = toTarget.normalized;
        float speed = CurrentState == State.Chasing ? chaseSpeed : wanderSpeed;

        transform.position += direction * speed * Time.deltaTime;

        // However it got here, it never leaves the face
        transform.position = ClampToFace(transform.position);

        // Settle it onto the pool floor
        Vector3 local = face.InverseTransformPoint(transform.position);
        local.y = standHeight;
        transform.position = face.TransformPoint(local);

        Quaternion wanted = Quaternion.LookRotation(direction, face.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted,
                                              turnSpeed * Time.deltaTime);
    }

    // Pulls a world position back inside the bounds of the face
    Vector3 ClampToFace(Vector3 worldSpot)
    {
        Vector3 local = face.InverseTransformPoint(worldSpot);

        local.x = Mathf.Clamp(local.x, -faceLimit, faceLimit);
        local.z = Mathf.Clamp(local.z, -faceLimit, faceLimit);

        return face.TransformPoint(local);
    }

    void PickWanderTarget()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

        Vector3 offset = face.right * Mathf.Cos(angle) * wanderDistance
                       + face.forward * Mathf.Sin(angle) * wanderDistance;

        target = ClampToFace(transform.position + offset);
        wanderStarted = Time.time;
    }

    void Catch(CubeWorld world)
    {
        Debug.Log("It caught him at the water's edge.");

        CubeFace cubeFace = world.faces[world.CurrentFaceIndex];
        if (cubeFace != null && cubeFace.spawnPoint != null)
            world.player.Spawn(cubeFace.spawnPoint);

        CurrentState = State.Wandering;
        PickWanderTarget();
    }

    bool IsActiveFace()
    {
        CubeWorld world = CubeWorld.Instance;
        return world != null
            && world.CurrentMode == CubeWorld.Mode.Playing
            && world.CurrentFaceIndex == faceIndex
            && world.player != null
            && face != null;
    }
}
