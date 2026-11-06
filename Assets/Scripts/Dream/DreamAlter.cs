using UnityEngine;

// Where the player brings the dream fragments.
// Greybox: it just changes from the empty colour to the full colour as fragments arrive.
// No light, glow or orbiting shards.
public class DreamAltar : MonoBehaviour
{
    [Header("Colour")]
    public Color emptyColor = new Color(0.15f, 0.15f, 0.25f);
    public Color fullColor = new Color(0.7f, 0.9f, 1f);
    [Tooltip("Dims the colours. Lower this if the altar reads as pure white.")]
    [Range(0f, 1f)] public float colorStrength = 0.8f;
    [Tooltip("How fast the colour catches up when a fragment is delivered.")]
    public float blendSpeed = 3f;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");

    Material instanceMaterial;
    float progress;      // smoothed 0 to 1

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null) instanceMaterial = rend.material; // a copy, so this altar has its own colour

        UpdateLook();
    }

    // Called by LevelObjectives when a fragment is delivered
    public void OnFragmentDelivered()
    {
    }

    void Update()
    {
        float target = LevelObjectives.Instance != null
            ? LevelObjectives.Instance.Progress
            : 0f;

        progress = Mathf.Lerp(progress, target, blendSpeed * Time.deltaTime);
        UpdateLook();
    }

    void UpdateLook()
    {
        if (instanceMaterial == null) return;

        Color c = Color.Lerp(emptyColor, fullColor, progress) * colorStrength;
        instanceMaterial.SetColor(BaseColorID, c);
        instanceMaterial.SetColor(ColorID, c);
    }
}
