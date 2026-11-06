using System.Collections;
using UnityEngine;

// The vat the stream fills. Once it's full, the door of light appears.
public class WaterVat : MonoBehaviour
{
    [HideInInspector] public Transform water;   // the water inside, set by the generator

    Vector3 fullScale;
    Vector3 fullPosition;
    float fillLevel;

    // Called by the generator once the water object exists
    public void SetUp(Transform waterInside)
    {
        water = waterInside;

        fullScale = water.localScale;
        fullPosition = water.localPosition;

        SetLevel(0f);
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
    }
}
