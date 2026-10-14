using UnityEngine;

// Blends the scene's fog, lighting and background towards the current face's mood.
// Also lifts the fog while the player is in the cube or map view, so the
// backdrop and the level layout can actually be seen.
// Put this anywhere in the scene, for example on DreamCube.
public class MoodController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The scene's Directional Light.")]
    public Light sun;
    [Tooltip("The Main Camera, so its background colour can change too.")]
    public Camera mainCamera;

    [Header("Blending")]
    [Tooltip("How fast the mood changes. Lower = slower, dreamier.")]
    public float blendSpeed = 1.5f;

    [Header("Away From The Level")]
    [Tooltip("How fast the fog lifts and returns when leaving or entering a level.")]
    public float fogLiftSpeed = 3f;
    [Tooltip("Brightness of the sun while away from a level, so the cube is visible.")]
    public float awaySunIntensity = 0.8f;
    [Tooltip("Ambient light while away from a level.")]
    public Color awayAmbientColor = new Color(0.35f, 0.3f, 0.4f);

    // true in the cube and map views
    FaceMood target;
    bool awayFromLevel;

    [Tooltip("Colour of the thin haze in the cube view. Match it to your gradient.")]
    public Color awayFogColor = new Color(0.4f, 0.25f, 0.5f);
    [Tooltip("Density of that haze. Very low — just enough to soften the distance.")]
    public float awayFogDensity = 0.004f;

    // Called by CubeWorld when a face is entered
    public void setMood(FaceMood mood)
    {
        target = mood;
    }

    // Jumps straight to a mood with no blending (used at game start)
    public void snapToMood(FaceMood mood)
    {
        target = mood;
        awayFromLevel = false;

        if (mood == null) return;

        RenderSettings.fog = mood.fogEnabled;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = mood.fogColor;
        RenderSettings.fogDensity = mood.fogDensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = mood.ambientColor;

        if (sun != null)
        {
            sun.intensity = mood.sunIntensity;
            sun.color = mood.sunColor;
        }

        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = mood.skyColor;
        }
    }

    // Called when leaving a level for the cube or map view, and when coming back
    public void setAwayFromLevel(bool away)
    {
        awayFromLevel = away;
    }

    private void Update()
    {
        // Away from the level: swap the heavy level fog for a thin dreamy haze,
        // so the cube reads clearly but still sits in an atmosphere
        if (awayFromLevel)
        {
            float lift = fogLiftSpeed * Time.deltaTime;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, awayFogColor, lift);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, awayFogDensity, lift);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight =
                Color.Lerp(RenderSettings.ambientLight, awayAmbientColor, lift);

            if (sun != null)
                sun.intensity = Mathf.Lerp(sun.intensity, awaySunIntensity, lift);

            return;
        }

        if (target == null) return;

        // In a level: blend towards that face's mood
        float t = blendSpeed * Time.deltaTime;

        RenderSettings.fog = target.fogEnabled || RenderSettings.fogDensity > 0.001f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, target.fogColor, t);

        float wantedDensity = target.fogEnabled ? target.fogDensity : 0f;
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, wantedDensity, t);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, target.ambientColor, t);

        if (sun != null)
        {
            sun.intensity = Mathf.Lerp(sun.intensity, target.sunIntensity, t);
            sun.color = Color.Lerp(sun.color, target.sunColor, t);
        }

        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.Lerp(mainCamera.backgroundColor, target.skyColor, t);
        }
    }
}
