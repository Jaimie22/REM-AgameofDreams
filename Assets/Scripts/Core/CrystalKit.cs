using UnityEngine;

// Shared bits for the crystal pillars that build the levels and mark finished ones.
public static class CrystalKit
{
    // A pale see-through crystal in the given colour
    public static Material Make(Color tint, float alpha = 0.35f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material m = new Material(shader);
        if (m.HasProperty("_Surface"))   // URP: transparent, alpha blended
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f);
        m.renderQueue = 3000;
        SetTint(m, tint, alpha);
        return m;
    }

    // Changes a crystal material's colour (and the faint glow it gives off)
    public static void SetTint(Material m, Color tint, float alpha = 0.35f)
    {
        Color c = new Color(tint.r, tint.g, tint.b, alpha);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", tint * 0.35f);
        }
    }

    // A camera straight above the middle of a face, looking down, with the face's forward as screen up.
    // extentRight and extentForward are how far the area reaches from the middle in each direction.
    public static void TopDownPose(Vector3 centre, Vector3 up, Vector3 forward,
                                   float extentRight, float extentForward,
                                   float fov, float aspect,
                                   out Vector3 position, out Quaternion rotation)
    {
        float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float needed = Mathf.Max(extentForward, extentRight / Mathf.Max(0.5f, aspect)) * 1.1f;
        position = centre + up * (needed / tan);
        rotation = Quaternion.LookRotation(-up, forward);
    }

    // A tall crystal pillar with no collider, so it never gets in the way
    public static GameObject Pillar(string name, Material material, Vector3 size, Transform parent = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localScale = size;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }
}
