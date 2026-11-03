using UnityEngine;

// A television's restless glow: brightness and colour shifting as the
// picture changes, with the occasional brighter cut.
[RequireComponent(typeof(Light))]
public class TelevisionGlow : MonoBehaviour
{
    [Header("Brightness")]
    public float baseIntensity = 1.2f;
    [Tooltip("How much the brightness wavers as the picture changes.")]
    [Range(0f, 1f)] public float flicker = 0.35f;
    [Tooltip("How quickly it changes. Higher is more restless.")]
    public float flickerSpeed = 6f;

    [Header("Colour")]
    [Tooltip("The two colours it drifts between, as scenes change.")]
    public Color coolTint = new Color(0.6f, 0.75f, 1f);
    public Color warmTint = new Color(0.9f, 0.8f, 0.7f);
    public float colorSpeed = 0.4f;

    [Header("Cuts")]
    [Tooltip("Occasional brighter flashes, as the picture cuts to something new.")]
    public bool allowCuts = true;
    public Vector2 secondsBetweenCuts = new Vector2(2f, 7f);
    public float cutBrightness = 1.8f;

    Light tv;
    float seed;
    float nextCut;
    float cutAmount;

    void Start()
    {
        tv = GetComponent<Light>();
        seed = Random.value * 100f;
        ScheduleCut();
    }

    void Update()
    {
        // A wandering flicker rather than a regular pulse
        float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
        float wave = 1f + (noise - 0.5f) * 2f * flicker;

        // The occasional cut to a brighter scene, fading away after
        if (allowCuts && Time.time > nextCut)
        {
            cutAmount = 1f;
            ScheduleCut();
        }

        cutAmount = Mathf.MoveTowards(cutAmount, 0f, Time.deltaTime * 3f);
        float cut = 1f + cutAmount * (cutBrightness - 1f);

        tv.intensity = baseIntensity * wave * cut;

        // Drift between cool and warm, as the picture changes
        float blend = Mathf.PerlinNoise(seed + 50f, Time.time * colorSpeed);
        tv.color = Color.Lerp(coolTint, warmTint, blend);
    }

    void ScheduleCut()
    {
        nextCut = Time.time + Random.Range(secondsBetweenCuts.x, secondsBetweenCuts.y);
    }
}
