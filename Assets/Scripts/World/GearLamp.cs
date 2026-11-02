using UnityEngine;

// A lamp that flickers like an old gas mantle or a tired bulb.
// It also keeps the glowing bulb in step with the light, so they never disagree.
[RequireComponent(typeof(Light))]
public class GearLamp : MonoBehaviour
{
    [Header("Flicker")]
    [Tooltip("How fast the light wavers.")]
    public float flickerSpeed = 3f;
    [Tooltip("How much the brightness drops at its dimmest, as a share of full. 0 = steady.")]
    [Range(0f, 0.8f)] public float flickerDepth = 0.25f;
    [Tooltip("Now and then the lamp stutters, dipping sharply for a moment.")]
    [Range(0f, 1f)] public float stutterChance = 0.02f;

    [Header("Bulb")]
    [Tooltip("The glowing bulb that goes with this light. Optional.")]
    public Renderer bulb;
    public Color bulbColor = new Color(1f, 0.62f, 0.25f);
    public float bulbGlow = 3f;

    static readonly int emissionId = Shader.PropertyToID("_EmissionColor");

    Light lamp;
    float fullIntensity;
    float seed;
    float stutterTimer;
    MaterialPropertyBlock block;

    void Awake()
    {
        lamp = GetComponent<Light>();
        fullIntensity = lamp.intensity;
        seed = Random.value * 100f;
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        // Smooth noise, so it wavers rather than strobes
        float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
        float level = 1f - flickerDepth * (1f - noise);

        // A sudden dip, which is what makes it feel like it might fail
        stutterTimer -= Time.deltaTime;
        if (stutterTimer <= 0f && Random.value < stutterChance * Time.deltaTime * 60f)
            stutterTimer = Random.Range(0.05f, 0.15f);

        if (stutterTimer > 0f) level *= 0.3f;

        lamp.intensity = fullIntensity * level;

        if (bulb == null) return;

        bulb.GetPropertyBlock(block);
        block.SetColor(emissionId, bulbColor * bulbGlow * level);
        bulb.SetPropertyBlock(block);
    }
}
