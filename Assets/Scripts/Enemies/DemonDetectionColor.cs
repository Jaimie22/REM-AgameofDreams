using System.Collections.Generic;
using UnityEngine;

// Detection drives the demon's whole face:
//   green  = happy      (spiral spins slowly)
//   yellow = no expression
//   red    = angry      (brows down, frown, fangs, spiral spins fast)
// Put this on the demon's root object. It finds the face, the glowing materials and the Spiral bone by itself.
// Call SetDetection(0..1) from your detection code (0 = unaware, 1 = chasing you).
public class DemonDetectionColor : MonoBehaviour
{
    [Tooltip("Materials whose name contains one of these glow with the detection colour.")]
    [SerializeField] string[] glowMaterialNames = { "Spiral", "Eye" };
    [Tooltip("Local axis the spiral turns around. Try (0,0,1), then (0,1,0) or (1,0,0) if it spins sideways.")]
    [SerializeField] Vector3 spinAxis = Vector3.forward;
    [SerializeField] Color happy = new Color(0.42f, 0.84f, 0.54f);
    [SerializeField] Color neutral = new Color(1f, 0.76f, 0.35f);
    [SerializeField] Color angry = new Color(1f, 0.30f, 0.30f);
    [SerializeField] float emission = 2.5f;
    [SerializeField] float calmSpin = 40f, huntSpin = 420f;
    [SerializeField] float smoothing = 6f;

    [Range(0f, 1f)] public float detection;
    float shownDetection;
    SkinnedMeshRenderer face;
    int neutralShape = -1, angryShape = -1;
    Transform spiralBone;
    readonly List<(Renderer renderer, int index)> glow = new List<(Renderer, int)>();
    MaterialPropertyBlock block;

    public void SetDetection(float value) { detection = Mathf.Clamp01(value); }

    void Awake()
    {
        block = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!mats[i]) continue;
                foreach (var key in glowMaterialNames)
                    if (mats[i].name.Contains(key)) { glow.Add((r, i)); break; }
            }
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh && face == null)
            {
                int n = smr.sharedMesh.blendShapeCount;
                for (int i = 0; i < n; i++)
                {
                    string s = smr.sharedMesh.GetBlendShapeName(i);
                    if (s.EndsWith("Neutral")) neutralShape = i;
                    if (s.EndsWith("Angry")) angryShape = i;
                }
                if (neutralShape >= 0 || angryShape >= 0) face = smr;
            }
        }
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == "Spiral") { spiralBone = t; break; }
        if (face == null) Debug.LogWarning("DemonDetectionColor: no mesh with Neutral/Angry blend shapes found", this);
        if (spiralBone == null) Debug.LogWarning("DemonDetectionColor: no bone named Spiral found", this);
    }

    void Update()
    {
        shownDetection = Mathf.Lerp(shownDetection, detection, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        float d = shownDetection;

        float wAngry = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, d));
        float wNeutral = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.45f, d)) * (1f - wAngry);
        if (face)
        {
            if (neutralShape >= 0) face.SetBlendShapeWeight(neutralShape, wNeutral * 100f);
            if (angryShape >= 0) face.SetBlendShapeWeight(angryShape, wAngry * 100f);
        }

        Color c = Color.Lerp(Color.Lerp(happy, neutral, Mathf.Clamp01(wNeutral + wAngry)), angry, wAngry);
        foreach (var (r, i) in glow)
        {
            if (!r) continue;
            r.GetPropertyBlock(block, i);
            block.SetColor("_EmissionColor", c * emission);
            r.SetPropertyBlock(block, i);
        }
        if (spiralBone)
            spiralBone.Rotate(spinAxis, Mathf.Lerp(calmSpin, huntSpin, d) * Time.deltaTime, Space.Self);
    }
}
