using System.Collections.Generic;
using UnityEngine;

// The steampunk look for the Function face: warm flickering lamps, steam venting from
// pipes beside the machine, and dark smoke rising from chimneys round the edge.
// GearGenerator builds this after the room, so it's rebuilt with the level.
public class GearAtmosphere : MonoBehaviour
{
    [Header("Lamps")]
    public bool addLamps = true;
    [Tooltip("Colour of the lamplight. Warm amber reads as gas and brass.")]
    public Color lampColor = new Color(1f, 0.62f, 0.25f);
    public float lampIntensity = 2.5f;
    public float lampRange = 11f;
    [Tooltip("Height of the lamp posts by the machine.")]
    public float postHeight = 4.5f;
    [Tooltip("How many pillars get a lamp on top. Keep the total low, as every light costs.")]
    public int pillarLamps = 3;

    [Header("Steam Vents")]
    public bool addSteam = true;
    public int ventCount = 3;
    [Tooltip("Seconds between puffs, roughly. Each vent varies a little.")]
    public Vector2 puffInterval = new Vector2(3f, 6f);
    public Color steamColor = new Color(0.9f, 0.9f, 0.85f, 0.5f);

    [Header("Chimneys")]
    public bool addChimneys = true;
    public int chimneyCount = 4;
    public float chimneyHeight = 7f;
    public Color smokeColor = new Color(0.16f, 0.15f, 0.15f, 0.55f);

    [Header("Pipes")]
    [Tooltip("Copper pipes running along the back wall.")]
    public bool addPipes = true;

    [Header("Colours")]
    public Color brassColor = new Color(0.62f, 0.45f, 0.2f);
    public Color copperColor = new Color(0.55f, 0.3f, 0.18f);
    public Color ironColor = new Color(0.12f, 0.12f, 0.13f);

    Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();
    Material steamMaterial;
    Material smokeMaterial;
    Transform root;

    // Builds everything under the given parent
    public void Build(Transform parent, float floorY, float half, float machineZ,
                      Vector3 motorPos, Vector3 outputPos, List<Vector3> pillarSpots, float pillarTop)
    {
        materialCache.Clear();

        root = new GameObject("Atmosphere").transform;
        root.SetParent(parent, false);

        if (addPipes) BuildPipes(floorY, half);
        if (addChimneys) BuildChimneys(floorY, half);
        if (addSteam) BuildVents(floorY, machineZ, motorPos, outputPos);

        if (!addLamps) return;

        // One post at each end of the machine, a little in front of it
        float frontZ = machineZ - 2.6f;
        BuildLampPost("Lamp_Motor", new Vector3(motorPos.x - 1.5f, floorY, frontZ));
        BuildLampPost("Lamp_Output", new Vector3(outputPos.x + 1.5f, floorY, frontZ));

        // Lamps on top of a few pillars, so the room isn't lit only at the far end
        int count = Mathf.Min(pillarLamps, pillarSpots.Count);

        for (int i = 0; i < count; i++)
        {
            Vector3 spot = pillarSpots[i];
            BuildLamp("Lamp_Pillar_" + i, new Vector3(spot.x, pillarTop + 0.35f, spot.z));
        }
    }

    // --- Lamps ------------------------------------------------------

    // A thin iron post with a lamp on top
    void BuildLampPost(string name, Vector3 floorPos)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = name + "_Post";
        post.transform.SetParent(root, false);
        post.transform.localPosition = floorPos + Vector3.up * (postHeight * 0.5f);
        post.transform.localScale = new Vector3(0.18f, postHeight * 0.5f, 0.18f);
        Tint(post, ironColor);
        SafeDestroy(post.GetComponent<Collider>());

        BuildLamp(name, floorPos + Vector3.up * (postHeight + 0.3f));
    }

    // A glowing brass-capped bulb and its flickering light
    void BuildLamp(string name, Vector3 localPos)
    {
        GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = name;
        bulb.transform.SetParent(root, false);
        bulb.transform.localPosition = localPos;
        bulb.transform.localScale = Vector3.one * 0.5f;
        SafeDestroy(bulb.GetComponent<Collider>());

        Renderer rend = bulb.GetComponent<Renderer>();
        rend.sharedMaterial = GetGlowing(lampColor);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // A brass cap above the bulb
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = name + "_Cap";
        cap.transform.SetParent(root, false);
        cap.transform.localPosition = localPos + Vector3.up * 0.3f;
        cap.transform.localScale = new Vector3(0.45f, 0.06f, 0.45f);
        Tint(cap, brassColor);
        SafeDestroy(cap.GetComponent<Collider>());

        GameObject lightObj = new GameObject(name + "_Light");
        lightObj.transform.SetParent(root, false);
        lightObj.transform.localPosition = localPos;

        Light lamp = lightObj.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = lampColor;
        lamp.intensity = lampIntensity;
        lamp.range = lampRange;
        lamp.shadows = LightShadows.None;

        GearLamp flicker = lightObj.AddComponent<GearLamp>();
        flicker.bulb = rend;
        flicker.bulbColor = lampColor;
    }

    // --- Steam ------------------------------------------------------

    // Short pipes on the floor that puff steam now and then
    void BuildVents(float floorY, float machineZ, Vector3 motorPos, Vector3 outputPos)
    {
        steamMaterial = steamMaterial != null ? steamMaterial : MakeParticleMaterial(2f);

        for (int i = 0; i < ventCount; i++)
        {
            // Spread along the machine, behind it, so the gears show through the steam
            float t = ventCount > 1 ? (float)i / (ventCount - 1) : 0.5f;
            float x = Mathf.Lerp(motorPos.x, outputPos.x, t);
            Vector3 pos = new Vector3(x, floorY, machineZ + 3f);

            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = "Vent_" + i;
            pipe.transform.SetParent(root, false);
            pipe.transform.localPosition = pos + Vector3.up * 0.4f;
            pipe.transform.localScale = new Vector3(0.45f, 0.4f, 0.45f);
            Tint(pipe, copperColor);
            SafeDestroy(pipe.GetComponent<Collider>());

            // The puff comes out of the top
            float interval = Random.Range(puffInterval.x, puffInterval.y);
            MakeSteam("Steam_" + i, pos + Vector3.up * 0.85f, interval, Random.Range(0f, interval));
        }
    }

    void MakeSteam(string name, Vector3 localPos, float interval, float startDelay)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root, false);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // pointing up

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop();

        var main = ps.main;
        main.loop = true;
        main.duration = interval;
        main.startDelay = startDelay;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startColor = steamColor;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.05f;
        main.maxParticles = 60;

        // Stays with the face when the cube turns
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        // One puff each time round, rather than a steady stream
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 14, 22) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.12f;

        FadeAndSwell(ps, steamColor, 0.6f, 3f);
        SetUpRenderer(ps, steamMaterial);
        ps.Play();
    }

    // --- Chimneys ---------------------------------------------------

    // Tall iron stacks round the edge, breathing out dark smoke
    void BuildChimneys(float floorY, float half)
    {
        smokeMaterial = smokeMaterial != null ? smokeMaterial : MakeParticleMaterial(1.5f);

        // Corners of the room, but not the near ones where the player starts
        Vector3[] spots =
        {
            new Vector3(-half + 2f, floorY, half - 2f),
            new Vector3(half - 2f, floorY, half - 2f),
            new Vector3(-half + 2f, floorY, 2f),
            new Vector3(half - 2f, floorY, 2f)
        };

        int count = Mathf.Min(chimneyCount, spots.Length);

        for (int i = 0; i < count; i++)
        {
            GameObject stack = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stack.name = "Chimney_" + i;
            stack.transform.SetParent(root, false);
            stack.transform.localPosition = spots[i] + Vector3.up * (chimneyHeight * 0.5f);
            stack.transform.localScale = new Vector3(1.4f, chimneyHeight * 0.5f, 1.4f);
            Tint(stack, ironColor);

            // A brass band round the top
            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "Chimney_Band_" + i;
            band.transform.SetParent(root, false);
            band.transform.localPosition = spots[i] + Vector3.up * (chimneyHeight - 0.4f);
            band.transform.localScale = new Vector3(1.55f, 0.15f, 1.55f);
            Tint(band, brassColor);
            SafeDestroy(band.GetComponent<Collider>());

            MakeSmoke("Smoke_" + i, spots[i] + Vector3.up * chimneyHeight);
        }
    }

    void MakeSmoke(string name, Vector3 localPos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root, false);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // pointing up

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop();

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startColor = smokeColor;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 100;
        main.prewarm = true; // already smoking when the level loads
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.rateOverTime = 5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.4f;

        // A little wander, so the column bends instead of rising dead straight
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.25f;

        FadeAndSwell(ps, smokeColor, 0.8f, 3.5f);
        SetUpRenderer(ps, smokeMaterial);
        ps.Play();
    }

    // --- Pipes ------------------------------------------------------

    // Copper pipes along the back wall, with brass joints
    void BuildPipes(float floorY, float half)
    {
        float[] heights = { 1.6f, 2.4f };
        float length = half * 2f - 2f;

        for (int i = 0; i < heights.Length; i++)
        {
            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = "Pipe_" + i;
            pipe.transform.SetParent(root, false);
            pipe.transform.localPosition = new Vector3(0f, floorY + heights[i], half - 0.35f);
            pipe.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // lying along X
            pipe.transform.localScale = new Vector3(0.3f, length * 0.5f, 0.3f);
            Tint(pipe, copperColor);
            SafeDestroy(pipe.GetComponent<Collider>());

            // Joints at regular spacing
            for (float x = -half + 3f; x < half - 2f; x += 6f)
            {
                GameObject joint = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                joint.name = "Joint_" + i;
                joint.transform.SetParent(root, false);
                joint.transform.localPosition = new Vector3(x, floorY + heights[i], half - 0.35f);
                joint.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                joint.transform.localScale = new Vector3(0.42f, 0.12f, 0.42f);
                Tint(joint, brassColor);
                SafeDestroy(joint.GetComponent<Collider>());
            }
        }
    }

    // --- Particle helpers -------------------------------------------

    // Fades in, then away, and swells as it goes, so nothing pops into existence
    void FadeAndSwell(ParticleSystem ps, Color color, float startScale, float endScale)
    {
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, startScale, 1f, endScale));

        var rotationOverLife = ps.rotationOverLifetime;
        rotationOverLife.enabled = true;
        rotationOverLife.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
    }

    void SetUpRenderer(ParticleSystem ps, Material mat)
    {
        ParticleSystemRenderer rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        if (mat != null) rend.material = mat;
    }

    // A soft, normally-blended particle material, as steam and smoke should hide what's behind them
    Material MakeParticleMaterial(float softness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) return null;

        Material mat = new Material(shader);
        mat.SetTexture("_BaseMap", MakeSoftTexture(softness));
        mat.SetColor("_BaseColor", Color.white);

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        return mat;
    }

    // A soft round blob that fades out towards its edge
    Texture2D MakeSoftTexture(float softness)
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
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), softness);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    // --- Materials --------------------------------------------------

    void Tint(GameObject go, Color color)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = GetTinted(color);
    }

    // One material per colour, reused
    Material GetTinted(Color color)
    {
        if (materialCache.TryGetValue(color, out Material cached) && cached != null)
            return cached;

        Material mat = new Material(FindLit());
        mat.name = "Atmosphere_" + ColorUtility.ToHtmlStringRGBA(color);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);

        // Brass and copper should catch the light, iron should not
        mat.SetFloat("_Metallic", 0.6f);
        mat.SetFloat("_Smoothness", 0.45f);

        materialCache[color] = mat;
        return mat;
    }

    // The bulb's material glows, and GearLamp then varies how much
    Material GetGlowing(Color color)
    {
        Material mat = new Material(FindLit());
        mat.name = "Atmosphere_Bulb";
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);

        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 3f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        return mat;
    }

    void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    // Shader.Find fails in builds unless the shader is referenced by an asset,
    // so fall back to the one URP always includes
    Shader FindLit()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader;
    }
}
