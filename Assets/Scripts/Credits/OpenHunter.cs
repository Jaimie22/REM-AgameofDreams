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
    [Tooltip("How far above or below it he can be and still be caught. Stops it grabbing him from the floor when he's up on a platform.")]
    public float catchHeight = 1.2f;

    [Tooltip("It only notices him while he's no higher than this above it. 0 means it always notices him. Lets it roam below a platform course and only give chase once he falls.")]
    public float chaseMaxHeight = 0f;

    [Header("Wandering")]
    public float wanderDistance = 7f;
    public float wanderTimeout = 5f;

    [Header("Bounds")]
    [Tooltip("How far from the centre of the face it may roam.")]
    public float faceLimit = 13f;
    [Tooltip("How high above the floor it stands.")]
    public float standHeight = 0.1f;

    [Header("Plank")]
    [Tooltip("How far it may walk out along the plank past the edge of the water. 0 keeps it in the water.")]
    public float plankReach = 0f;
    [Tooltip("Half the width of the plank, so it stays on it.")]
    public float plankHalfWidth = 0.8f;
    [Tooltip("How much higher the plank is than the water floor.")]
    public float plankRise = 0.7f;

    [Header("Which face")]
    public int faceIndex = 5;

    public State CurrentState { get; private set; } = State.Wandering;

    [HideInInspector] public Transform face;

    Vector3 target;
    float wanderStarted;
    // Kept relative to the face, because the cube turns and a world position would be wrong afterwards
    Vector3 homeLocal;
    Quaternion homeLocalRotation;
    bool hasHome;

    void Awake()
    {
        // The crawl animation carries its own movement, which pushed the hunters off the
        // edge while the player was in the cube view. The script does all the moving.
        foreach (Animator animator in GetComponentsInChildren<Animator>())
            animator.applyRootMotion = false;
    }

    void Start()
    {
        if (face == null) face = transform.parent;
        homeLocal = face.InverseTransformPoint(transform.position);
        homeLocalRotation = Quaternion.Inverse(face.rotation) * transform.rotation;
        hasHome = true;
        PickWanderTarget();
    }

    // Whatever else happens, it stays inside its bounds, even when it isn't the active face
    void LateUpdate()
    {
        if (face == null) return;
        transform.position = Settled(transform.position);
    }

    // Keeps a world position inside the bounds, standing at the right height
    Vector3 Settled(Vector3 worldSpot)
    {
        Vector3 local = face.InverseTransformPoint(ClampToFace(worldSpot));
        local.y = standHeight + (local.z > faceLimit ? plankRise : 0f);
        return face.TransformPoint(local);
    }

    // Puts it back where it started
    public void ResetToHome()
    {
        if (!hasHome || face == null) return;
        transform.position = face.TransformPoint(homeLocal);
        transform.rotation = face.rotation * homeLocalRotation;
        CurrentState = State.Wandering;
        PickWanderTarget();
    }

    void Update()
    {
        if (!IsActiveFace()) return;

        CubeWorld world = CubeWorld.Instance;
        Transform player = world.player.transform;

        float distance = Vector3.Distance(transform.position, player.position);

        // Judged along the ground and in height separately, so a hunter below a ledge can't reach up
        Vector3 toPlayer = player.position - transform.position;
        float height = Mathf.Abs(Vector3.Dot(toPlayer, face.up));
        float flat = Vector3.ProjectOnPlane(toPlayer, face.up).magnitude;

        if (flat < catchRange && height < catchHeight)
        {
            Catch(world);
            return;
        }

        // Open ground, so it simply sees him or it doesn't
        float above = Vector3.Dot(toPlayer, face.up);
        bool reachable = chaseMaxHeight <= 0f || above < chaseMaxHeight;

        if (distance < sightRange && reachable)
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

        // However it got here, it never leaves its bounds, and it stands on the floor
        transform.position = Settled(transform.position + direction * speed * Time.deltaTime);

        Quaternion wanted = Quaternion.LookRotation(direction, face.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted,
                                              turnSpeed * Time.deltaTime);
    }

    // Pulls a world position back inside the bounds of the face
    Vector3 ClampToFace(Vector3 worldSpot)
    {
        Vector3 local = face.InverseTransformPoint(worldSpot);

        // In the water it may go anywhere; the plank is only its width, and only part of the way out
        local.z = Mathf.Clamp(local.z, -faceLimit, faceLimit + plankReach);

        float sideways = local.z > faceLimit ? Mathf.Min(plankHalfWidth, faceLimit) : faceLimit;
        local.x = Mathf.Clamp(local.x, -sideways, sideways);

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

        // It costs the player a heart, the same as the maze hunter
        world.player.Audio.PlayDeath();
        world.player.Vitals.TakeDamage();

        CubeFace cubeFace = world.faces[world.CurrentFaceIndex];
        if (cubeFace != null && cubeFace.spawnPoint != null)
            world.player.Spawn(cubeFace.spawnPoint);

        // Everything goes back to where it started, so none are left piled up on his spawn
        foreach (OpenHunter other in FindObjectsByType<OpenHunter>(FindObjectsSortMode.None))
            if (other.faceIndex == faceIndex) other.ResetToHome();
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
