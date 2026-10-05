using UnityEngine;

// Blends the scene's fog, lighting and background towards the current face's mood.
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

    FaceMood target;

    // Called by CubeWorld when a face is entered
    public void SetMood(FaceMood mood)
    {
        target = mood;
    }

    // Jumps straight to a mood with no blending (used at game start)
    public void SnapToMood(FaceMood mood)
    {
        target = mood;
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

    void Update()
    {
        if (target == null) return;

        float t = blendSpeed * Time.deltaTime;

        // Fog is either on or off, so switch it on as soon as any face wants it
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