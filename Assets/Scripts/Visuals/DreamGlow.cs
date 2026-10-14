using UnityEngine;

// A soft aura around the player, so he glows in the dark rather than
// carrying a torch. Builds its own billboard, so there's nothing to set up.
public class DreamGlow : MonoBehaviour
{
    [Header("Glow")]
    public Color glowColor = new Color(0.7f, 0.85f, 1f);
    [Tooltip("Overall brightness of the aura.")]
    [Range(0f, 3f)] public float strength = 0.8f;
    [Tooltip("Width of the halo in units. Roughly twice his height works well.")]
    public float size = 3.2f;
    [Tooltip("Height above his feet that the halo is centred on.")]
    public float centerHeight = 0.9f;

    [Header("Softness")]
    [Tooltip("How quickly the glow fades towards its edge. Higher = tighter, softer halo.")]
    [Range(1f, 8f)] public float falloff = 3f;
    [Tooltip("Pushes the halo behind him, so his body masks the middle of it.")]
    public float depthOffset = 0.4f;

    [Header("Breathing")]
    [Tooltip("Slow rise and fall, like a sleeping breath. 0 for a steady glow.")]
    public float breathSpeed = 0.8f;
    [Range(0f, 1f)] public float breathDepth = 0.15f;

    [Header("Movement")]
    [Tooltip("The glow swells a little while he's moving.")]
    [Range(0f, 1f)] public float moveBoost = 0.2f;

    [Header("Optional Light")]
    [Tooltip("Adds a very dim light so the floor at his feet is still readable in fog.")]
    public bool addFaintLight = true;
    public float lightRange = 5f;
    public float lightIntensity = 0.4f;

    Transform halo;
    Material haloMaterial;
    CharacterController controller;
    Light faintLight;
    Camera cam;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main;

        buildHalo();
        if (addFaintLight) buildLight();
    }

    // A quad with a soft radial texture, blended additively
    private void buildHalo()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = "DreamGlow";

        // Purely visual: no collider, casts nothing, receives nothing
        Destroy(obj.GetComponent<Collider>());

        MeshRenderer rend = obj.GetComponent<MeshRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        haloMaterial = buildMaterial();
        rend.material = haloMaterial;

        halo = obj.transform;
        halo.SetParent(null); // world space, so it never inherits his rotation
    }

    private Material buildMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material mat = new Material(shader);
        mat.SetTexture("_BaseMap", buildGlowTexture());

        // Additive blending, so it brightens whatever is behind it
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 1f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        return mat;
    }

    // Paints a circle that fades smoothly out to nothing
    private Texture2D buildGlowTexture()
    {
        const int res = 128;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 centre = new Vector2(res * 0.5f, res * 0.5f);
        float radius = res * 0.5f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), centre) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.Pow(alpha, falloff); // the curve that makes it soft
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    private void buildLight()
    {
        GameObject obj = new GameObject("GlowLight");
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = Vector3.up * centerHeight;

        faintLight = obj.AddComponent<Light>();
        faintLight.type = LightType.Point;
        faintLight.color = glowColor;
        faintLight.range = lightRange;
        faintLight.intensity = lightIntensity;
        faintLight.shadows = LightShadows.None;
    }

    private void LateUpdate()
    {
        if (halo == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // How fast he's moving, as 0 to 1
        float speed = 0f;
        if (controller != null)
        {
            Vector3 flat = controller.velocity;
            flat.y = 0f;
            speed = Mathf.Clamp01(flat.magnitude / 5f);
        }

        // Sit at his middle, nudged away from the camera so his body masks the centre
        Vector3 centre = transform.position + Vector3.up * centerHeight;
        halo.position = centre + cam.transform.forward * depthOffset;

        // Always face the camera square on
        halo.rotation = cam.transform.rotation;

        // A slow breath, with a lift while he's moving
        float breath = 1f - breathDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * breathSpeed));
        float boost = 1f + moveBoost * speed;

        halo.localScale = Vector3.one * size * breath * boost;

        Color c = glowColor * strength * breath * boost;
        c.a = 1f;
        haloMaterial.SetColor("_BaseColor", c);

        if (faintLight != null)
        {
            faintLight.color = glowColor;
            faintLight.intensity = lightIntensity * breath * boost;
        }
    }

    // The halo lives outside him, so tidy it up when he's hidden or destroyed
    private void OnDisable()
    {
        if (halo != null) halo.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (halo != null) halo.gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        if (halo != null) Destroy(halo.gameObject);
    }
}
