using UnityEngine;

// Draws a soft dark circle under the player, projected onto whatever is below.
// Works regardless of lighting, so the player can always read their position.
// Put this on the player.
public class BlobShadow : MonoBehaviour
{
    [Header("Size")]
    [Tooltip("Width of the shadow when standing on the ground.")]
    public float size = 1.2f;
    [Tooltip("How much smaller it gets at maximum height.")]
    [Range(0f, 1f)] public float shrinkWithHeight = 0.5f;

    [Header("Fade")]
    [Tooltip("Darkness of the shadow when standing on the ground.")]
    [Range(0f, 1f)] public float strength = 0.5f;
    [Tooltip("Height at which the shadow fades out completely.")]
    public float maxHeight = 8f;

    [Header("Raycast")]
    [Tooltip("How far down to look for a surface.")]
    public float rayDistance = 20f;
    [Tooltip("Started slightly above the player's feet, so it works on slopes.")]
    public float rayStartOffset = 0.5f;
    [Tooltip("Which layers count as ground. Default is everything.")]
    public LayerMask groundLayers = ~0;

    [Header("Look")]
    public Color shadowColor = Color.black;
    [Tooltip("Optional. Leave empty and a soft circle is generated automatically.")]
    public Material shadowMaterial;

    Transform blob;
    Material instanceMaterial;
    MeshRenderer blobRenderer;

    void Start()
    {
        CreateBlob();
    }

    // Builds the quad and its material once at startup
    void CreateBlob()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "BlobShadow";

        // The quad is just a visual, so it needs no collider and casts nothing itself
        Destroy(go.GetComponent<Collider>());

        blobRenderer = go.GetComponent<MeshRenderer>();
        blobRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        blobRenderer.receiveShadows = false;

        instanceMaterial = shadowMaterial != null
            ? new Material(shadowMaterial)
            : BuildDefaultMaterial();

        blobRenderer.material = instanceMaterial;

        blob = go.transform;
        blob.SetParent(null); // kept in world space so it doesn't inherit our rotation
    }

    // A see-through unlit material with a soft circle painted into it
    Material BuildDefaultMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material mat = new Material(shader);
        mat.SetTexture("_BaseMap", BuildCircleTexture());
        mat.SetColor("_BaseColor", shadowColor);

        // Switch the material into transparent mode
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        return mat;
    }

    // Paints a circle that fades out towards its edge
    Texture2D BuildCircleTexture()
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
                alpha = alpha * alpha; // squared gives a softer edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    void LateUpdate()
    {
        if (blob == null) return;

        // Look straight down for a surface, ignoring triggers like doors and fragments
        Vector3 origin = transform.position + Vector3.up * rayStartOffset;
        bool hitSomething = Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                            rayDistance, groundLayers,
                                            QueryTriggerInteraction.Ignore);

        if (!hitSomething)
        {
            blobRenderer.enabled = false; // nothing below, e.g. over a gap
            return;
        }

        blobRenderer.enabled = true;

        // Sit just above the surface and lie flat against it
        blob.position = hit.point + hit.normal * 0.02f;
        blob.rotation = Quaternion.LookRotation(-hit.normal, Vector3.forward);

        // The higher he is, the smaller and fainter the shadow
        float height = Mathf.Clamp01(hit.distance / maxHeight);
        float scale = size * Mathf.Lerp(1f, 1f - shrinkWithHeight, height);
        blob.localScale = new Vector3(scale, scale, 1f);

        Color c = shadowColor;
        c.a = strength * (1f - height);
        instanceMaterial.SetColor("_BaseColor", c);
    }

    // The blob lives outside the player, so tidy it up when he's hidden or destroyed
    void OnDisable()
    {
        if (blobRenderer != null) blobRenderer.enabled = false;
    }

    void OnDestroy()
    {
        if (blob != null) Destroy(blob.gameObject);
    }
}
