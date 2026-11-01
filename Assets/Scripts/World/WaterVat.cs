using System.Collections;
using UnityEngine;

// The vat the stream fills. Once it's full, the door of light appears.
public class WaterVat : MonoBehaviour
{
    [Header("Glow")]
    [Tooltip("Colour of the vat's rim light once it's full.")]
    public Color fullColor = new Color(0.4f, 0.8f, 1f);
    public float lightRange = 9f;
    public float emptyIntensity = 0.3f;
    public float fullIntensity = 2f;

    [Header("Ripple")]
    [Tooltip("How much the surface wobbles once filled.")]
    public float rippleHeight = 0.05f;
    public float rippleSpeed = 1.5f;

    [HideInInspector] public Transform water;   // the water inside, set by the generator

    Light vatLight;
    Vector3 fullScale;
    Vector3 fullPosition;
    float fillLevel;
    bool filled;

    // Called by the generator once the water object exists
    public void SetUp(Transform waterInside)
    {
        water = waterInside;

        fullScale = water.localScale;
        fullPosition = water.localPosition;

        SetLevel(0f);

        GameObject lightObj = new GameObject("VatLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = Vector3.up * 1.5f;

        vatLight = lightObj.AddComponent<Light>();
        vatLight.type = LightType.Point;
        vatLight.color = fullColor;
        vatLight.range = lightRange;
        vatLight.intensity = emptyIntensity;
        vatLight.shadows = LightShadows.None;
    }

    public void Fill(float duration)
    {
        StartCoroutine(FillRoutine(duration));
    }

    IEnumerator FillRoutine(float duration)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duration))
        {
            SetLevel(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }

        SetLevel(1f);
        filled = true;
    }

    // Scales the water up from the bottom of the vat, rather than the middle
    void SetLevel(float level)
    {
        if (water == null) return;

        fillLevel = level;
        bool visible = level > 0.01f;

        if (water.gameObject.activeSelf != visible) water.gameObject.SetActive(visible);
        if (!visible) return;

        float height = fullScale.y * level;
        water.localScale = new Vector3(fullScale.x, height, fullScale.z);
        water.localPosition = fullPosition - Vector3.up * (fullScale.y - height) * 0.5f;

        if (vatLight != null)
            vatLight.intensity = Mathf.Lerp(emptyIntensity, fullIntensity, level);
    }

    void Update()
    {
        if (!filled || water == null) return;

        // A gentle wobble on the surface once it's full
        float ripple = Mathf.Sin(Time.time * rippleSpeed) * rippleHeight;
        water.localPosition = fullPosition + Vector3.up * ripple;
    }
}
