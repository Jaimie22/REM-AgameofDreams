using UnityEngine;

// A dream fragment. Collect them all to open the level's door of light.
// Lights itself so it stays visible in dark levels.
public class DreamFragment : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Degrees per second it spins, so it catches the eye.")]
    public float spinSpeed = 60f;
    [Tooltip("How far it bobs up and down.")]
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;

    [Header("Glow")]
    public Color glowColor = new Color(0.6f, 0.85f, 1f);
    [Tooltip("How much brighter than the base colour the glow is.")]
    public float emissionStrength = 4f;
    [Tooltip("Pulse speed of the glow. 0 for a steady light.")]
    public float pulseSpeed = 2f;
    [Tooltip("How much the glow dips during a pulse.")]
    [Range(0f, 1f)] public float pulseDepth = 0.3f;

    [Header("Light")]
    [Tooltip("Adds a small point light so it lights the floor around it.")]
    public bool addLight = true;
    public float lightRange = 7f;
    public float lightIntensity = 3f;

    static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int colorId = Shader.PropertyToID("_Color");
    static readonly int emissionId = Shader.PropertyToID("_EmissionColor");

    Vector3 startPos;
    Material instanceMaterial;
    Light pointLight;
    bool collected;

    private void Start()
    {
        startPos = transform.localPosition;
        setUpGlow();
    }

    // Gives this fragment its own glowing material and a little light of its own
    private void setUpGlow()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // .material makes a copy, so changing it affects only this fragment
            instanceMaterial = rend.material;
            instanceMaterial.SetColor(baseColorId, glowColor);
            instanceMaterial.SetColor(colorId, glowColor);

            // Emission has to be switched on per material, not just set
            instanceMaterial.EnableKeyword("_EMISSION");
            instanceMaterial.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
            instanceMaterial.SetColor(emissionId, glowColor * emissionStrength);
        }

        if (!addLight) return;

        GameObject lightObj = new GameObject("FragmentLight");
        lightObj.transform.SetParent(transform, false);

        pointLight = lightObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = glowColor;
        pointLight.range = lightRange;
        pointLight.intensity = lightIntensity;
        pointLight.shadows = LightShadows.None; // cheaper, and softer in fog
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = startPos + Vector3.up * bob;

        if (pulseSpeed <= 0f) return;

        // A slow breath in the brightness, so it reads as alive
        float pulse = 1f - pulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));

        if (instanceMaterial != null)
            instanceMaterial.SetColor(emissionId, glowColor * emissionStrength * pulse);

        if (pointLight != null)
            pointLight.intensity = lightIntensity * pulse;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.GetComponent<PlayerController>() == null) return;

        collected = true;
        LevelObjectives.Instance?.fragmentCollected();
        gameObject.SetActive(false);
    }
}
