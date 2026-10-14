using UnityEngine;

// A giant, near-transparent PJBoy standing behind the cube in the zoomed-out
// and map views: the dreamer, with his dream in front of him.
// Each view has its own placement, because the two cameras are very different.
// Put this on DreamCube and assign the player prefab.
public class DreamerBackdrop : MonoBehaviour
{
    [Header("The Dreamer")]
    [Tooltip("The PJBoy prefab, or any model to stand behind the cube.")]
    public GameObject dreamerPrefab;
    [Tooltip("Optional. An Animator Controller playing idle only. Leave empty to use the prefab's own.")]
    public RuntimeAnimatorController idleController;

    [Header("Cube View Placement")]
    [Tooltip("How far in front of the camera he stands in the rotate-world view.")]
    public float cubeDistance = 120f;
    [Tooltip("Sideways on screen. Positive = right, negative = left.")]
    public float cubeSideOffset = 25f;
    [Tooltip("Up or down the screen. Negative drops him lower in the frame.")]
    public float cubeHeightOffset = -15f;
    public float cubeScale = 26f;
    [Tooltip("Turns him slightly, so he isn't dead square to the camera.")]
    public float cubeYaw = -25f;

    [Header("Map View Placement")]
    [Tooltip("How far in front of the camera he stands in the top-down map view.")]
    public float mapDistance = 90f;
    [Tooltip("Sideways on screen. Positive = right, negative = left.")]
    public float mapSideOffset = 45f;
    [Tooltip("Up or down the screen. Negative drops him lower in the frame.")]
    public float mapHeightOffset = 0f;
    public float mapScale = 20f;
    [Tooltip("Turns him slightly, so he isn't dead square to the camera.")]
    public float mapYaw = -25f;

    [Header("Look")]
    [Tooltip("How solid he is. Keep this low — he should be a presence, not a character.")]
    [Range(0f, 1f)] public float opacity = 0.16f;
    [Tooltip("Tints him towards this colour, so he sits in the background.")]
    public Color tint = new Color(0.75f, 0.7f, 0.95f);
    [Tooltip("How quickly he fades in and out.")]
    public float fadeSpeed = 1.5f;
    [Tooltip("How quickly he slides between the two placements when the view changes.")]
    public float moveSpeed = 4f;

    [Header("Drift")]
    [Tooltip("A slow sway, so he never looks like a cardboard cutout.")]
    public float swayAmount = 0.6f;
    public float swaySpeed = 0.35f;

    [Header("Debug")]
    [Tooltip("Shows him all the time, so you can position him.")]
    public bool alwaysVisible = false;
    [Tooltip("While tuning, forces the map placement so you can see it from any view.")]
    public bool previewMapPlacement = false;

    static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int colorId = Shader.PropertyToID("_Color");

    GameObject dreamer;
    Renderer[] renderers;
    Camera cam;

    float currentOpacity;
    bool wanted;

    // Smoothed values, so switching views slides rather than snaps
    float distance, side, height, currentScale, yaw;
    bool initialised;

    private void Start()
    {
        cam = Camera.main;
        if (dreamerPrefab != null) buildDreamer();
    }

    // Builds the giant figure once, then just fades him in and out
    private void buildDreamer()
    {
        dreamer = Instantiate(dreamerPrefab);
        dreamer.name = "DreamerBackdrop";
        dreamer.transform.localScale = Vector3.one * cubeScale;

        stripGameplayComponents(dreamer);

        // Idle only, so he breathes but never moves
        Animator animator = dreamer.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            if (idleController != null) animator.runtimeAnimatorController = idleController;
            animator.applyRootMotion = false;
        }

        renderers = dreamer.GetComponentsInChildren<Renderer>();
        makeTransparent();

        setOpacity(0f);
        dreamer.SetActive(false);
    }

    // Switches off anything that would make him behave like the player.
    // Disabling rather than destroying avoids RequireComponent conflicts,
    // and stops the scripts ever running their Start methods.
    private void stripGameplayComponents(GameObject obj)
    {
        // Every script except the Animator, which isn't a MonoBehaviour
        foreach (MonoBehaviour script in obj.GetComponentsInChildren<MonoBehaviour>(true))
            script.enabled = false;

        CharacterController controller = obj.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        foreach (Collider c in obj.GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        foreach (Light l in obj.GetComponentsInChildren<Light>(true))
            l.enabled = false;
    }

    // Switches every material on him to transparent, so he can be faded
    private void makeTransparent()
    {
        if (renderers == null) return;

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;

            // .materials makes copies, so the real player isn't affected
            Material[] mats = rend.materials;

            foreach (Material mat in mats)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 2995; // after the gradient, before the cube
            }

            rend.materials = mats;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }

    // Called by CubeWorld when entering or leaving the cube and map views
    public void setVisible(bool visible)
    {
        wanted = visible;
    }

    // True when the top-down map is open
    private bool inMapView()
    {
        if (previewMapPlacement) return true;

        CubeWorld world = CubeWorld.Instance;
        return world != null && world.CurrentMode == CubeWorld.Mode.Map;
    }

    private void LateUpdate()
    {
        if (dreamer == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        bool show = wanted || alwaysVisible;
        currentOpacity = Mathf.MoveTowards(currentOpacity, show ? opacity : 0f,
                                           fadeSpeed * opacity * Time.deltaTime);

        bool visible = currentOpacity > 0.001f;
        if (dreamer.activeSelf != visible) dreamer.SetActive(visible);
        if (!visible) return;

        setOpacity(currentOpacity);
        updatePlacement();

        // Place him in the camera's own space, so he sits in the same part of
        // the frame whichever way the camera is looking
        Transform camT = cam.transform;

        Vector3 basePosition = camT.position
                             + camT.forward * distance
                             + camT.right * side
                             + camT.up * height;

        // A slow sway, like breathing
        float sway = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
        dreamer.transform.position = basePosition + camT.up * sway;

        // Stand him upright relative to the camera, facing it, turned slightly off square
        dreamer.transform.rotation = camT.rotation * Quaternion.Euler(0f, 180f + yaw, 0f);
        dreamer.transform.localScale = Vector3.one * currentScale;
    }

    // Eases between the two sets of placement values as the view changes
    private void updatePlacement()
    {
        bool map = inMapView();

        float wantedDistance = map ? mapDistance : cubeDistance;
        float wantedSide = map ? mapSideOffset : cubeSideOffset;
        float wantedHeight = map ? mapHeightOffset : cubeHeightOffset;
        float wantedScale = map ? mapScale : cubeScale;
        float wantedYaw = map ? mapYaw : cubeYaw;

        // Snap into place the first time, so he doesn't fly in from nowhere
        if (!initialised)
        {
            distance = wantedDistance;
            side = wantedSide;
            height = wantedHeight;
            currentScale = wantedScale;
            yaw = wantedYaw;
            initialised = true;
            return;
        }

        float t = moveSpeed * Time.deltaTime;
        distance = Mathf.Lerp(distance, wantedDistance, t);
        side = Mathf.Lerp(side, wantedSide, t);
        height = Mathf.Lerp(height, wantedHeight, t);
        currentScale = Mathf.Lerp(currentScale, wantedScale, t);
        yaw = Mathf.Lerp(yaw, wantedYaw, t);
    }

    // Fades every part of him together, tinted towards the background
    private void setOpacity(float value)
    {
        if (renderers == null) return;

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;

            foreach (Material mat in rend.materials)
            {
                Color c = tint;
                c.a = value;

                if (mat.HasProperty(baseColorId)) mat.SetColor(baseColorId, c);
                if (mat.HasProperty(colorId)) mat.SetColor(colorId, c);
            }
        }
    }

    private void OnDestroy()
    {
        if (dreamer != null) Destroy(dreamer);
    }
}
