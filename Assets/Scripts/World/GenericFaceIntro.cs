using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A crystal entry animation for any level. Crystal pillars gather, then fly out and each one
// brings a piece of the level into being. The style changes how they gather and arrive.
[RequireComponent(typeof(CubeFace))]
public class GenericFaceIntro : MonoBehaviour, IFaceIntro
{
    public enum Style
    {
        Arc,      // Level 1: the crystals rise from the dark and arc down onto the level
        Rain,     // Level 2: slow rain from high above
        Helix,    // Level 4: crystals spiral in, like a machine starting up
        Float,    // Level 5: crystals drift in low and gentle
        Outward   // Level 6: crystals burst outwards from the middle
    }

    public Style style = Style.Arc;
    public bool playEntryAnimation = true;

    [Tooltip("The level is built in this many groups, one crystal for each. Lower is quicker.")]
    public int maxGroups = 28;
    public float gatherTime = 2.5f;
    public float releaseDuration = 2.5f;
    [Tooltip("Seconds a crystal takes to reach its place. 0 uses the style's own speed.")]
    public float flyTime = 0f;
    public float popTime = 0.5f;
    public Vector3 crystalSize = new Vector3(0.8f, 2.2f, 0.8f);

    [Header("Camera")]
    [Tooltip("The camera looks straight down from above the middle of the face, to show all of it.")]
    public float introFov = 55f;
    [Tooltip("Extra space left round the edge of the face.")]
    public float introCameraMargin = 3f;

    class Item
    {
        public Transform t;
        public Vector3 scale;
    }

    class Group
    {
        public List<Item> items = new List<Item>();
        public Vector3 centre;        // in the world, worked out when the animation plays
        public Vector3 localCentre;   // relative to the face, because the cube turns after this is recorded
    }

    readonly List<Group> groups = new List<Group>();

    // The area the level covers, relative to the face, recorded before the cube turns
    Vector3 localMin, localMax;

    [Tooltip("The camera never frames more than this much of the face from the middle, in case something huge sits under it.")]
    public float maxFramedHalfSize = 20f;
    CubeFace face;

    void Awake()
    {
        face = GetComponent<CubeFace>();
    }

    // The things that make up the level: everything solid under the generated content
    Transform ContentParent()
    {
        if (face == null) face = GetComponent<CubeFace>();
        if (face == null || face.levelContent == null) return null;
        Transform content = face.levelContent.transform;
        Transform generated = content.Find("Generated");
        return generated != null ? generated : content;
    }

    static bool IsEnemy(Transform t)
    {
        return t.GetComponentInChildren<Hunter>(true) != null ||
               t.GetComponentInChildren<OpenHunter>(true) != null;
    }

    void SetEnemiesActive(bool active)
    {
        Transform parent = ContentParent();
        if (parent == null) return;
        foreach (Hunter h in parent.GetComponentsInChildren<Hunter>(true)) h.gameObject.SetActive(active);
        foreach (OpenHunter h in parent.GetComponentsInChildren<OpenHunter>(true)) h.gameObject.SetActive(active);
    }

    public void HideForIntro()
    {
        groups.Clear();
        if (!playEntryAnimation) return;

        Transform parent = ContentParent();
        if (parent == null) return;

        List<Transform> items = new List<Transform>();
        foreach (Transform child in parent)
        {
            if (!child.gameObject.activeSelf) continue;
            if (child.GetComponentInChildren<Renderer>(true) == null) continue;
            if (IsEnemy(child)) continue;
            items.Add(child);
        }
        if (items.Count == 0) return;

        // Pieces nearest the start come first, so the level grows outwards from the player
        Vector3 start = face.spawnPoint != null ? face.spawnPoint.position : face.transform.position;
        items.Sort((a, b) => (a.position - start).sqrMagnitude.CompareTo((b.position - start).sqrMagnitude));

        // How far the level reaches, measured from what's actually there, not just the centre of each piece
        localMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        localMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (Transform item in items)
        {
            foreach (Renderer r in item.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                for (int cx = -1; cx <= 1; cx += 2)
                    for (int cy = -1; cy <= 1; cy += 2)
                        for (int cz = -1; cz <= 1; cz += 2)
                        {
                            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(cx, cy, cz));
                            Vector3 local = face.transform.InverseTransformPoint(corner);
                            localMin = Vector3.Min(localMin, local);
                            localMax = Vector3.Max(localMax, local);
                        }
            }
        }

        int perGroup = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)Mathf.Max(1, maxGroups)));
        for (int i = 0; i < items.Count; i += perGroup)
        {
            Group g = new Group();
            for (int k = i; k < Mathf.Min(items.Count, i + perGroup); k++)
            {
                g.items.Add(new Item { t = items[k], scale = items[k].localScale });
                g.centre += items[k].position;
            }
            g.centre /= g.items.Count;
            g.localCentre = face.transform.InverseTransformPoint(g.centre);
            groups.Add(g);
        }

        foreach (Group g in groups)
            foreach (Item it in g.items)
                it.t.gameObject.SetActive(false);

        SetEnemiesActive(false);
    }

    void ShowEverything()
    {
        foreach (Group g in groups)
            foreach (Item it in g.items)
                if (it.t != null)
                {
                    it.t.localScale = it.scale;
                    it.t.gameObject.SetActive(true);
                }
        SetEnemiesActive(true);
    }

    // How the style gathers its crystals: ring height, radius, spin speed
    void Gathering(out float hover, out float startRadius, out float spin)
    {
        switch (style)
        {
            case Style.Rain:    hover = 11f; startRadius = 5f; spin = 12f; break;
            case Style.Helix:   hover = 9f;  startRadius = 4f; spin = 70f; break;
            case Style.Float:   hover = 4f;  startRadius = 5f; spin = 18f; break;
            case Style.Outward: hover = 3f;  startRadius = 1.5f; spin = 60f; break;
            default:            hover = 6f;  startRadius = 4f; spin = 35f; break;
        }
    }

    float FlightTime()
    {
        if (flyTime > 0f) return flyTime;
        switch (style)
        {
            case Style.Rain: return 1.6f;
            case Style.Float: return 1.4f;
            case Style.Outward: return 0.6f;
            default: return 1f;
        }
    }

    Vector3 FlightPosition(Vector3 from, Vector3 to, float u, Vector3 right, Vector3 fwd, Vector3 up)
    {
        switch (style)
        {
            case Style.Rain:
                return Vector3.Lerp(from, to, u * u);

            case Style.Helix:
            {
                float a = u * 720f * Mathf.Deg2Rad;
                float r = (1f - u) * 3f;
                return Vector3.Lerp(from, to, u) + (right * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * r;
            }

            case Style.Float:
                return Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, u)) + up * Mathf.Sin(u * Mathf.PI) * 1.2f;

            case Style.Outward:
                return Vector3.Lerp(from, to, 1f - (1f - u) * (1f - u));

            default: // Arc
                return Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, u)) + up * Mathf.Sin(u * Mathf.PI) * 3f;
        }
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float k = x - 1f;
        return 1f + c3 * k * k * k + c1 * k * k;
    }

    public IEnumerator PlayIntro(DreamCamera cam, Color tint, Color previousTint)
    {
        if (!playEntryAnimation || groups.Count == 0)
        {
            ShowEverything();
            yield break;
        }

        Transform ft = face.transform;
        Vector3 up = ft.up, right = ft.right, fwd = ft.forward;

        // The cube has turned since the pieces were recorded, so find where they are now
        foreach (Group g in groups) g.centre = ft.TransformPoint(g.localCentre);

        int n = groups.Count;
        Vector3 centre = Vector3.zero;
        foreach (Group g in groups) centre += g.centre;
        centre /= n;

        Gathering(out float hover, out float startRadius, out float spin);

        // The crystals start in the colour of the level before and settle into this level's colour
        Material mat = CrystalKit.Make(previousTint);

        // Rings of crystals, filling the inner ring first, neighbouring rings turning opposite ways
        float[] radius = new float[n];
        float[] angle0 = new float[n];
        float[] dir = new float[n];
        int placed = 0, ring = 0;
        while (placed < n)
        {
            float r = startRadius + ring * 2.6f;
            int capacity = Mathf.Max(3, Mathf.FloorToInt(2f * Mathf.PI * r / (crystalSize.x * 2.4f)));
            int take = Mathf.Min(capacity, n - placed);
            for (int j = 0; j < take; j++)
            {
                radius[placed + j] = r;
                angle0[placed + j] = 360f * j / take + ring * 23f;
                dir[placed + j] = (ring % 2 == 0) ? 1f : -1f;
            }
            placed += take;
            ring++;
        }

        Transform[] crystals = new Transform[n];
        for (int i = 0; i < n; i++)
            crystals[i] = CrystalKit.Pillar("IntroCrystal", mat, Vector3.zero).transform;

        // 0 circling, 1 flying, 2 popping in, 3 finished
        int[] state = new int[n];
        Vector3[] fromPos = new Vector3[n];
        float[] landedAt = new float[n];
        float fly = FlightTime();
        int finished = 0;

        // The camera hangs straight above the middle of the area the level covers, high enough to see all of it
        Vector3 localMiddle = (localMin + localMax) * 0.5f;
        localMiddle.y = 0f;
        Vector3 middle = ft.TransformPoint(localMiddle);
        float outerRing = startRadius + Mathf.Max(0, ring - 1) * 2.6f;
        float extRight = Mathf.Max(Mathf.Min((localMax.x - localMin.x) * 0.5f, maxFramedHalfSize), outerRing) + introCameraMargin;
        float extForward = Mathf.Max(Mathf.Min((localMax.z - localMin.z) * 0.5f, maxFramedHalfSize), outerRing) + introCameraMargin;
        float aspect = (cam != null && cam.Cam != null) ? cam.Cam.aspect : 16f / 9f;
        CrystalKit.TopDownPose(middle, up, fwd, extRight, extForward, introFov, aspect,
                               out Vector3 camPos, out Quaternion camRot);

        float t = 0f;
        while (finished < n)
        {
            t += Time.deltaTime;

            if (cam != null)
                cam.SetIntroPose(camPos, camRot, introFov);

            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1f));
            CrystalKit.SetTint(mat, Color.Lerp(previousTint, tint, Mathf.Clamp01(t / (gatherTime + releaseDuration))));

            for (int i = 0; i < n; i++)
            {
                if (state[i] == 3) continue;
                Transform c = crystals[i];
                float releaseAt = gatherTime + (n > 1 ? releaseDuration * i / (n - 1) : 0f);
                float ct = Mathf.Min(t, releaseAt);

                float a = (angle0[i] + dir[i] * spin * ct) * Mathf.Deg2Rad;
                float bob = Mathf.Sin(ct * 2f + i) * 0.3f;
                Vector3 orbitPos = centre
                                   + right * Mathf.Cos(a) * radius[i]
                                   + fwd * Mathf.Sin(a) * radius[i]
                                   + up * (hover + bob - (1f - appear) * 3f);
                Quaternion spinRot = Quaternion.AngleAxis(ct * 30f + i * 20f, up) * ft.rotation;

                if (state[i] == 0)
                {
                    c.position = orbitPos;
                    c.rotation = spinRot;
                    c.localScale = crystalSize * appear;
                    if (t >= releaseAt)
                    {
                        state[i] = 1;
                        fromPos[i] = orbitPos;
                    }
                    continue;
                }

                Group g = groups[i];

                if (state[i] == 1)
                {
                    float u = Mathf.Clamp01((t - releaseAt) / Mathf.Max(0.1f, fly));
                    c.position = FlightPosition(fromPos[i], g.centre + up * 0.6f, u, right, fwd, up);
                    c.rotation = Quaternion.AngleAxis(u * 360f, up) * spinRot;
                    c.localScale = crystalSize;

                    if (u >= 1f)
                    {
                        state[i] = 2;
                        landedAt[i] = t;
                        foreach (Item it in g.items)
                            if (it.t != null)
                            {
                                it.t.localScale = it.scale * 0.001f;
                                it.t.gameObject.SetActive(true);
                            }
                    }
                    continue;
                }

                // Landed: the crystal sinks into its group as the pieces pop up
                float p = Mathf.Clamp01((t - landedAt[i]) / Mathf.Max(0.1f, popTime));
                c.localScale = crystalSize * (1f - p);
                float pop = Mathf.Max(0.001f, EaseOutBack(p));
                foreach (Item it in g.items)
                    if (it.t != null) it.t.localScale = it.scale * pop;

                if (p >= 1f)
                {
                    foreach (Item it in g.items)
                        if (it.t != null) it.t.localScale = it.scale;
                    Destroy(c.gameObject);
                    state[i] = 3;
                    finished++;
                }
            }

            yield return null;
        }

        Destroy(mat);
        Physics.SyncTransforms();
        // The camera is released by whoever brings the player in, so it can glide to the player camera
        SetEnemiesActive(true);
    }
}
