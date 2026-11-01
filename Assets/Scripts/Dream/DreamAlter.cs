using UnityEngine;

// Where the player brings the dream fragments.
// Once it's full, it reveals the door of light.
public class DreamAltar : MonoBehaviour
{
    [Header("Glow")]
    public Color emptyColor = new Color(0.15f, 0.15f, 0.25f);
    public Color fullColor = new Color(0.7f, 0.9f, 1f);
    [Tooltip("How brightly it glows when full. 0 = flat colour, 1 = soft, 3+ = blazing.")]
    [Range(0f, 8f)] public float emissionStrength = 2f;
    [Tooltip("Dims the base colour too. Lower this if the altar reads as pure white.")]
    [Range(0f, 1f)] public float colorStrength = 0.8f;

    [Header("Light")]
    public bool addLight = true;
    public float lightRange = 10f;
    [Tooltip("Light brightness when empty.")]
    public float minIntensity = 0.3f;
    [Tooltip("Light brightness when every fragment has been delivered.")]
    public float maxIntensity = 3f;

    [Header("Feedback")]
    [Tooltip("How fast the glow catches up when a fragment is delivered.")]
    public float blendSpeed = 3f;
    [Tooltip("Extra flash when a fragment lands.")]
    public float pulseStrength = 1f;

    [Header("Floating Fragments")]
    [Tooltip("Delivered fragments orbit above the altar.")]
    public bool showDeliveredFragments = true;
    public float orbitRadius = 0.9f;
    public float orbitHeight = 1.8f;
    public float orbitSpeed = 40f;
    [Tooltip("How brightly the orbiting shards glow.")]
    [Range(0f, 8f)] public float shardEmission = 1.5f;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");
    static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

    Material instanceMaterial;
    Light altarLight;
    Transform orbitRoot;

    float progress;      // smoothed 0 to 1
    float pulse;         // extra brightness that fades away
    int shownFragments;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // .material makes a copy, so this altar has its own glow
            instanceMaterial = rend.material;
            instanceMaterial.EnableKeyword("_EMISSION");
            instanceMaterial.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        if (addLight)
        {
            GameObject lightObj = new GameObject("AltarLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = Vector3.up * 1.5f;

            altarLight = lightObj.AddComponent<Light>();
            altarLight.type = LightType.Point;
            altarLight.range = lightRange;
            altarLight.shadows = LightShadows.None;
        }

        orbitRoot = new GameObject("DeliveredFragments").transform;
        orbitRoot.SetParent(transform, false);

        UpdateLook();
    }

    // Called by LevelObjectives when a fragment is delivered
    public void OnFragmentDelivered()
    {
        pulse = pulseStrength;
        if (showDeliveredFragments) AddOrbitingFragment();
    }

    // Adds one small glowing shard circling above the altar
    void AddOrbitingFragment()
    {
        GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = "Delivered_" + shownFragments;
        shard.transform.SetParent(orbitRoot, false);
        shard.transform.localScale = Vector3.one * 0.25f;
        shard.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);

        Destroy(shard.GetComponent<Collider>());

        Renderer rend = shard.GetComponent<Renderer>();
        Material mat = rend.material;
        mat.SetColor(BaseColorID, fullColor * colorStrength);
        mat.SetColor(ColorID, fullColor * colorStrength);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor(EmissionID, fullColor * shardEmission);

        // Spread them evenly around the circle
        shownFragments++;
        LayOutOrbit();
    }

    // Spaces the delivered shards evenly around the ring
    void LayOutOrbit()
    {
        int count = orbitRoot.childCount;
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            orbitRoot.GetChild(i).localPosition = new Vector3(
                Mathf.Cos(angle) * orbitRadius,
                orbitHeight,
                Mathf.Sin(angle) * orbitRadius);
        }
    }

    void Update()
    {
        // Ease towards however full the altar actually is
        float target = LevelObjectives.Instance != null
            ? LevelObjectives.Instance.Progress
            : 0f;

        progress = Mathf.Lerp(progress, target, blendSpeed * Time.deltaTime);
        pulse = Mathf.Lerp(pulse, 0f, blendSpeed * Time.deltaTime);

        UpdateLook();

        if (orbitRoot != null)
            orbitRoot.Rotate(Vector3.up, orbitSpeed * Time.deltaTime, Space.Self);
    }

    void UpdateLook()
    {
        Color c = Color.Lerp(emptyColor, fullColor, progress);

        if (instanceMaterial != null)
        {
            Color baseColor = c * colorStrength;
            instanceMaterial.SetColor(BaseColorID, baseColor);
            instanceMaterial.SetColor(ColorID, baseColor);
            instanceMaterial.SetColor(EmissionID, c * (emissionStrength * progress + pulse));
        }

        if (altarLight != null)
        {
            altarLight.color = c;
            altarLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, progress) + pulse;
        }
    }
}