using UnityEngine;

// The door of light. It only exists once every fragment has been
// delivered to the altar, so walking into it always completes the level.
[RequireComponent(typeof(Collider))]
public class DreamDoor : MonoBehaviour
{
    [Header("Appearance")]
    public Color doorColor = new Color(1f, 0.95f, 0.7f);
    [Tooltip("How brightly the surface glows. 0 = flat colour, 1 = soft, 3+ = blazing.")]
    [Range(0f, 8f)] public float emissionStrength = 2f;
    [Tooltip("Dims the base colour too. Lower this if the door reads as pure white.")]
    [Range(0f, 1f)] public float colorStrength = 0.8f;

    [Header("Arrival")]
    [Tooltip("Seconds the door takes to grow into place when it appears.")]
    public float appearDuration = 0.6f;

    [Header("Light")]
    public bool addLight = true;
    public float lightRange = 12f;
    [Tooltip("Brightness of the light it casts. Try 1 to 2 for a subtle glow.")]
    public float lightIntensity = 2.5f;
    [Tooltip("Gentle breathing of the light. 0 for a steady glow.")]
    public float pulseSpeed = 1.2f;
    [Range(0f, 1f)] public float pulseDepth = 0.2f;

    [Header("Mist")]
    [Tooltip("Mist drifting out of the doorway.")]
    public bool addMist = true;
    [Tooltip("Colour of the mist. Usually a paler version of the door colour.")]
    public Color mistColor = new Color(0.9f, 0.95f, 1f);
    [Range(0f, 1f)] public float mistOpacity = 0.25f;
    [Tooltip("How far the mist drifts from the doorway.")]
    public float mistReach = 3f;
    [Tooltip("Puffs per second. Keep it low, mist should be lazy.")]
    public float mistRate = 14f;
    [Tooltip("Size of each puff of mist.")]
    public Vector2 mistSize = new Vector2(1.2f, 2.5f);
    [Tooltip("Mist also pools and spreads along the floor.")]
    public bool mistPoolsAtBase = true;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");
    static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

    Vector3 fullScale;
    float appearTimer;
    Light doorLight;
    Material instanceMaterial;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        fullScale = transform.localScale;
    }

    void OnEnable()
    {
        // Restart the grow-in animation each time the door is revealed
        appearTimer = 0f;
        transform.localScale = Vector3.zero;
    }

    void Start()
    {
        SetUpMaterial();
        if (addLight) SetUpLight();
        if (addMist) SetUpMist();
    }

    void SetUpMaterial()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) return;

        // .material makes a copy, so this door has its own glow
        instanceMaterial = rend.material;

        Color baseColor = doorColor * colorStrength;
        instanceMaterial.SetColor(BaseColorID, baseColor);
        instanceMaterial.SetColor(ColorID, baseColor);

        instanceMaterial.EnableKeyword("_EMISSION");
        instanceMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        instanceMaterial.SetColor(EmissionID, doorColor * emissionStrength);
    }

    void SetUpLight()
    {
        GameObject lightObj = new GameObject("DoorLight");
        lightObj.transform.SetParent(transform, false);

        doorLight = lightObj.AddComponent<Light>();
        doorLight.type = LightType.Point;
        doorLight.color = doorColor;
        doorLight.range = lightRange;
        doorLight.intensity = lightIntensity;
        doorLight.shadows = LightShadows.None;
    }

    // Builds the mist drifting out of the doorway
    void SetUpMist()
    {
        // Spilling outward through the opening
        MakeMist("DoorMist", Vector3.zero, Vector3.forward, 25f, mistReach, mistSize);

        // Pooling and spreading along the ground at the base
        if (mistPoolsAtBase)
            MakeMist("DoorMistBase", Vector3.down * 1.3f, Vector3.forward, 80f,
                     mistReach * 0.5f, mistSize * 1.4f);
    }

    // One mist emitter, built in code so there's nothing to set up
    void MakeMist(string name, Vector3 offset, Vector3 direction, float spreadAngle,
                  float reach, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = offset;
        obj.transform.localRotation = Quaternion.LookRotation(direction);

        // The door is a squashed cube, so undo its scale or the mist is squashed too
        Vector3 s = transform.localScale;
        obj.transform.localScale = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            s.z != 0f ? 1f / s.z : 1f);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop();

        var main = ps.main;
        main.startLifetime = 3.5f;
        main.startSpeed = reach / 3.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new Color(mistColor.r, mistColor.g, mistColor.b, mistOpacity);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var emission = ps.emission;
        emission.rateOverTime = mistRate;

        // Creates the mist that emits from the door, and its chosen shape is a Hemispheric shape.
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 1.2f;
        shape.radiusThickness = 0.4f;

        // Fade in, then away, so nothing pops into existence
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(mistColor, 0f),
                new GradientColorKey(mistColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

        // Swell as it drifts, which reads as mist thinning out
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));

        // A slow turn stops the puffs looking like repeated stamps
        var rotationOverLife = ps.rotationOverLifetime;
        rotationOverLife.enabled = true;
        rotationOverLife.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        SetUpMistRenderer(ps);
        ps.Play();
    }

    // Soft additive billboards, with no shadows
    void SetUpMistRenderer(ParticleSystem ps)
    {
        ParticleSystemRenderer rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sortingFudge = 10f; // keeps mist behind the door surface

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) return;

        Material mat = new Material(shader);
        mat.SetTexture("_BaseMap", BuildSoftTexture());
        mat.SetColor("_BaseColor", Color.white);

        // Additive blending, so mist brightens what's behind it rather than hiding it
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 1f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        rend.material = mat;
    }

    // A soft round blob that fades out towards its edge
    Texture2D BuildSoftTexture()
    {
        const int res = 64;
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
                alpha = alpha * alpha * alpha; // cubed gives a very soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    void Update()
    {
        // Grow in with a little overshoot, so it lands with some weight
        if (appearTimer < 1f)
        {
            appearTimer += Time.deltaTime / Mathf.Max(0.01f, appearDuration);
            float t = Mathf.Clamp01(appearTimer);
            float eased = Mathf.Sin(t * Mathf.PI * 0.5f) * (1f + 0.15f * (1f - t));

            transform.localScale = fullScale * eased;
        }

        if (pulseSpeed <= 0f) return;

        // A slow breath, so the doorway feels alive
        float pulse = 1f - pulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));

        if (instanceMaterial != null)
            instanceMaterial.SetColor(EmissionID, doorColor * emissionStrength * pulse);

        if (doorLight != null)
            doorLight.intensity = lightIntensity * pulse;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        CubeWorld.Instance.CompleteCurrentLevel();
    }
}