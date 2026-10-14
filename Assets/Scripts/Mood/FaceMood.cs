using UnityEngine;

// The look of one face: fog, ambient light, sun and background colour.
// Put this on a face alongside its CubeFace component.
public class FaceMood : MonoBehaviour
{
    [Header("Fog")]
    public bool fogEnabled = true;
    public Color fogColor = Color.black;
    [Tooltip("Higher = thicker fog. 0.03 is heavy, 0.005 is a light haze.")]
    public float fogDensity = 0.03f;

    [Header("Lighting")]
    [Tooltip("Overall light bouncing around the scene.")]
    public Color ambientColor = new Color(0.05f, 0.05f, 0.08f);

    [Tooltip("Brightness of the Directional Light on this face.")]
    public float sunIntensity = 0.1f;
    public Color sunColor = Color.white;

    [Header("Background")]
    [Tooltip("Colour behind everything. Fades towards the fog colour at the horizon.")]
    public Color skyColor = Color.black;
}
