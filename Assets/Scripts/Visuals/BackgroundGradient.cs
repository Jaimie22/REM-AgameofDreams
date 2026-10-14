using UnityEngine;

// A full-screen gradient drawn behind everything, used in the cube view.
// It fades in and out, so the level's own sky colour takes over on a face.
// Put this on the Main Camera, alongside DreamCamera.
// The execution order makes sure it runs after the camera has been positioned.
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(200)]
public class BackgroundGradient : MonoBehaviour
{
    [Header("Colours")]
    [Tooltip("Colour at the top of the screen.")]
    public Color topColor = new Color(0.18f, 0.08f, 0.35f);
    [Tooltip("Colour at the bottom of the screen.")]
    public Color bottomColor = new Color(0.55f, 0.2f, 0.45f);

    [Header("Blend")]
    [Tooltip("Where the two colours meet. 0.5 is the middle of the screen.")]
    [Range(0f, 1f)] public float midpoint = 0.5f;
    [Tooltip("How soft the transition is. Low = a hard band, high = a smooth wash.")]
    [Range(0.05f, 1f)] public float softness = 0.9f;

    [Header("Placement")]
    [Tooltip("How far behind the camera's view it sits. Must be further than the cube.")]
    public float distance = 150f;

    [Header("Fading")]
    [Tooltip("How quickly the gradient fades in and out.")]
    public float fadeSpeed = 2.5f;

    [Header("Stars")]
    [Tooltip("Scatters faint stars through the gradient.")]
    public bool showStars = true;
    [Range(0f, 1f)] public float starBrightness = 0.5f;
    public int starCount = 180;

    [Header("Debug")]
    [Tooltip("Shows the gradient all the time, so you can tune the colours.")]
    public bool alwaysVisible = false;

    Camera cam;
    Transform quad;
    Material gradientMaterial;
    Texture2D gradientTexture;

    float opacity;
    bool wanted;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        buildQuad();
    }

    // A quad parented to the camera, sitting behind the cube
    private void buildQuad()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = "BackgroundGradient";
        obj.transform.SetParent(transform, false);

        Destroy(obj.GetComponent<Collider>());

        MeshRenderer rend = obj.GetComponent<MeshRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        gradientMaterial = buildMaterial();
        rend.material = gradientMaterial;

        quad = obj.transform;
        quad.localRotation = Quaternion.identity;

        setOpacity(0f);
    }

    private Material buildMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material mat = new Material(shader);
        gradientTexture = buildGradientTexture();
        mat.SetTexture("_BaseMap", gradientTexture);
        mat.SetColor("_BaseColor", Color.white);

        // Ordinary transparent blending, drawn after the skybox.
        // Depth testing means the cube still appears in front of it.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 2990; // just before normal transparent objects

        return mat;
    }

    // Paints the gradient into a tall thin texture, with optional stars
    private Texture2D buildGradientTexture()
    {
        const int width = 64;
        const int height = 256;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);

            // Shift and soften the blend between the two colours
            float t = Mathf.InverseLerp(midpoint - softness * 0.5f,
                                        midpoint + softness * 0.5f, v);
            t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            Color c = Color.Lerp(bottomColor, topColor, t);
            c.a = 1f;

            for (int x = 0; x < width; x++) tex.SetPixel(x, y, c);
        }

        if (showStars) scatterStars(tex, width, height);

        tex.Apply();
        return tex;
    }

    // Faint dots, thicker towards the top where the sky is darker
    private void scatterStars(Texture2D tex, int width, int height)
    {
        System.Random rng = new System.Random(12345); // fixed, so stars never flicker

        for (int i = 0; i < starCount; i++)
        {
            int x = rng.Next(width);
            int y = rng.Next(height);

            // Fewer stars low down, where the gradient is brightest
            float heightFactor = (float)y / height;
            if (rng.NextDouble() > heightFactor) continue;

            float brightness = (float)rng.NextDouble() * starBrightness * heightFactor;

            Color existing = tex.GetPixel(x, y);
            Color star = Color.Lerp(existing, Color.white, brightness);
            star.a = 1f;
            tex.SetPixel(x, y, star);
        }
    }

    // Called by CubeWorld when entering or leaving the cube view
    public void setVisible(bool visible)
    {
        wanted = visible;
    }

    private void LateUpdate()
    {
        if (quad == null || cam == null) return;

        bool show = wanted || alwaysVisible;
        opacity = Mathf.MoveTowards(opacity, show ? 1f : 0f, fadeSpeed * Time.deltaTime);
        setOpacity(opacity);

        if (opacity <= 0.001f) return;

        // Keep the quad filling the view, at a fixed distance behind the cube
        float safeDistance = Mathf.Min(distance, cam.farClipPlane * 0.9f);
        float height = 2f * safeDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        quad.localPosition = new Vector3(0f, 0f, safeDistance);
        quad.localRotation = Quaternion.identity;

        // A little extra width, so nothing shows at the edges
        quad.localScale = new Vector3(height * cam.aspect * 1.1f, height * 1.1f, 1f);
    }

    private void setOpacity(float value)
    {
        if (gradientMaterial == null || quad == null) return;

        bool visible = value > 0.001f;
        if (quad.gameObject.activeSelf != visible) quad.gameObject.SetActive(visible);

        gradientMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, value));
    }

    // Rebuilds the texture when colours are changed during Play
    public void refreshColors()
    {
        if (gradientMaterial == null) return;

        if (gradientTexture != null) Destroy(gradientTexture);
        gradientTexture = buildGradientTexture();
        gradientMaterial.SetTexture("_BaseMap", gradientTexture);
    }
}
