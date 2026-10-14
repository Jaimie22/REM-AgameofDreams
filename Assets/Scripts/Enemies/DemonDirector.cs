using UnityEngine;
using System.Collections.Generic;

// Decides when the demon appears on this face, and raises the tension as
// the level goes on. Put this on the face it belongs to, next to CubeFace.
[RequireComponent(typeof(CubeFace))]
public class DemonDirector : MonoBehaviour
{
    [Header("Which face")]
    [Tooltip("Index of this face in CubeWorld's Faces array (0 = Level 1).")]
    public int faceIndex = 2;

    [Header("Schedule")]
    [Tooltip("Seconds on the level before the first sighting can happen.")]
    public float firstSightingDelay = 25f;
    [Tooltip("Seconds between sightings early on.")]
    public float startingInterval = 45f;
    [Tooltip("Seconds between sightings once tension is high.")]
    public float minimumInterval = 18f;
    [Tooltip("Random wobble either side of the interval, so it's never predictable.")]
    public float intervalVariation = 8f;

    [Header("Placement")]
    [Tooltip("How far away it appears. In a maze, keep this short — corridors are tight.")]
    public Vector2 sightingDistance = new Vector2(8f, 14f);
    [Tooltip("How many spots to try before giving up this time round.")]
    public int placementAttempts = 25;
    [Tooltip("How high above the floor it stands.")]
    public float standHeight = 0.1f;

    [Header("Tension")]
    [Tooltip("Tension while simply exploring. Keep this low — it's the floor, not the ceiling.")]
    [Range(0f, 1f)] public float baseTension = 0.15f;
    [Tooltip("Extra tension added as the player delivers fragments.")]
    [Range(0f, 1f)] public float progressTension = 0.25f;
    [Tooltip("Spike added when the demon appears.")]
    [Range(0f, 1f)] public float sightingSpike = 0.3f;
    [Tooltip("How quickly a spike dies away.")]
    public float spikeDecay = 0.25f;
    [Tooltip("Tension can never go above this while playing the level.")]
    [Range(0f, 1f)] public float tensionCeiling = 0.75f;

    [Header("The Door")]
    [Tooltip("Tension once the door opens. Near zero makes the silence unnerving.")]
    [Range(0f, 1f)] public float doorOpenTension = 0f;

    [Header("The Demon")]
    [Tooltip("Optional. Leave empty and a placeholder silhouette is used.")]
    public GameObject demonPrefab;
    [Tooltip("Height of the placeholder, in units. Taller than the player is unsettling.")]
    public float placeholderHeight = 2.6f;
    [Tooltip("Faint glow of the placeholder's eyes.")]
    public Color eyeColor = new Color(0.9f, 0.2f, 0.2f);

    float timeOnFace;
    float nextSightingTime;
    float spike;
    int lastDeliveredCount;
    bool wasActive;

    private void OnEnable()
    {
        resetLevel();
    }

    private void resetLevel()
    {
        timeOnFace = 0f;
        spike = 0f;
        nextSightingTime = firstSightingDelay;
        lastDeliveredCount = 0;
    }

    private void Update()
    {
        bool active = isActiveFace();

        // Left the level, so hand the tension back to nothing.
        // This has to happen here, or the last value sticks and the camera never settles.
        if (!active)
        {
            if (wasActive)
            {
                wasActive = false;
                spike = 0f;
                HeartbeatSystem.Instance?.silence();
                resetLevel();
            }
            return;
        }

        wasActive = true;
        timeOnFace += Time.deltaTime;
        spike = Mathf.MoveTowards(spike, 0f, spikeDecay * Time.deltaTime);

        // Once the door is open the dream goes quiet, whatever else is going on.
        // The silence before the exit is more unsettling than the noise.
        if (LevelObjectives.Instance != null && LevelObjectives.Instance.DoorOpen)
        {
            spike = 0f;
            HeartbeatSystem.Instance?.setTension(doorOpenTension);
            return;
        }

        // Every delivery nudges the tension up a notch
        float progress = LevelObjectives.Instance != null
            ? LevelObjectives.Instance.Progress
            : 0f;

        if (LevelObjectives.Instance != null &&
            LevelObjectives.Instance.Delivered > lastDeliveredCount)
        {
            lastDeliveredCount = LevelObjectives.Instance.Delivered;
            spike = Mathf.Min(1f, spike + sightingSpike * 0.3f);
        }

        float tension = Mathf.Min(tensionCeiling,
                                  baseTension + progressTension * progress + spike);

        HeartbeatSystem.Instance?.setTension(tension);

        if (timeOnFace < nextSightingTime) return;

        trySighting(tension);

        // Sightings come closer together as the tension climbs
        float interval = Mathf.Lerp(startingInterval, minimumInterval, tension);
        nextSightingTime = timeOnFace + interval
                         + Random.Range(-intervalVariation, intervalVariation);
    }

    private bool isActiveFace()
    {
        CubeWorld world = CubeWorld.Instance;
        return world != null
            && world.CurrentMode == CubeWorld.Mode.Playing
            && world.CurrentFaceIndex == faceIndex;
    }

    // Finds a spot the player can actually see, and puts the demon there
    private void trySighting(float tension)
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null || world.dreamCamera == null) return;

        Transform player = world.player.transform;
        Camera cam = world.dreamCamera.Cam;

        if (!findSightingSpot(player, cam, out Vector3 spot)) return;

        GameObject demon = buildDemon(spot, player);
        DemonSighting sighting = demon.GetComponent<DemonSighting>();
        if (sighting == null) sighting = demon.AddComponent<DemonSighting>();

        // Higher tension means it lingers longer and shows itself more plainly
        sighting.lingerTime = Mathf.Lerp(1.5f, 3.5f, tension);
        sighting.maxOpacity = Mathf.Lerp(0.5f, 0.9f, tension);

        sighting.begin(player, cam);

        spike = Mathf.Min(1f, spike + sightingSpike);
    }

    // Looks for floor at the right distance that's on screen and in line of sight
    private bool findSightingSpot(Transform player, Camera cam, out Vector3 spot)
    {
        spot = Vector3.zero;

        for (int i = 0; i < placementAttempts; i++)
        {
            // A random direction on the level's plane, at a readable distance
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(sightingDistance.x, sightingDistance.y);

            Vector3 offset = transform.right * Mathf.Cos(angle) * distance
                           + transform.forward * Mathf.Sin(angle) * distance;

            Vector3 candidate = player.position + offset;

            // Find the floor under that point
            Vector3 rayStart = candidate + transform.up * 12f;
            if (!Physics.Raycast(rayStart, -transform.up, out RaycastHit floor, 30f,
                                 ~0, QueryTriggerInteraction.Ignore))
                continue;

            Vector3 standing = floor.point + transform.up * standHeight;

            // Must be on screen, or there's no sighting at all
            Vector3 chestHeight = standing + transform.up * placeholderHeight * 0.6f;
            Vector3 view = cam.WorldToViewportPoint(chestHeight);

            if (view.z <= 0f) continue;
            if (view.x < 0.1f || view.x > 0.9f) continue;
            if (view.y < 0.1f || view.y > 0.9f) continue;

            // And not hidden behind a wall
            Vector3 eyeLevel = player.position + transform.up * 1.5f;
            Vector3 toSpot = chestHeight - eyeLevel;

            if (Physics.Raycast(eyeLevel, toSpot.normalized, out RaycastHit blocker,
                                toSpot.magnitude - 0.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Something solid in the way, so try somewhere else
                if (blocker.distance < toSpot.magnitude - 1f) continue;
            }

            spot = standing;
            return true;
        }

        return false;
    }

    // Builds the demon, either from a prefab or as a placeholder silhouette
    private GameObject buildDemon(Vector3 position, Transform player)
    {
        if (demonPrefab != null)
        {
            GameObject fromPrefab = Instantiate(demonPrefab, position, Quaternion.identity);
            fromPrefab.transform.up = transform.up;
            return fromPrefab;
        }

        // A tall, thin, near-black figure reads well at distance in fog
        GameObject demon = new GameObject("DemonSighting");
        demon.transform.position = position;
        demon.transform.up = transform.up;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(demon.transform, false);
        body.transform.localPosition = Vector3.up * placeholderHeight * 0.5f;
        body.transform.localScale = new Vector3(0.5f, placeholderHeight * 0.5f, 0.5f);
        Destroy(body.GetComponent<Collider>());

        setSilhouetteMaterial(body.GetComponent<Renderer>(), new Color(0.02f, 0.02f, 0.03f));

        // Two faint eyes, which is what sells it at distance
        makeEye(demon.transform, new Vector3(-0.12f, placeholderHeight * 0.88f, 0.22f));
        makeEye(demon.transform, new Vector3(0.12f, placeholderHeight * 0.88f, 0.22f));

        return demon;
    }

    private void makeEye(Transform parent, Vector3 localPos)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        eye.transform.SetParent(parent, false);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * 0.09f;
        Destroy(eye.GetComponent<Collider>());

        Renderer rend = eye.GetComponent<Renderer>();
        setSilhouetteMaterial(rend, eyeColor);

        Material mat = rend.material;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", eyeColor * 2f);
    }

    // A see-through material, so the figure can fade in and out
    private void setSilhouetteMaterial(Renderer rend, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);

        // Transparent mode, so opacity can be animated
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
