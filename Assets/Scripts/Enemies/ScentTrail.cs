using System.Collections.Generic;
using UnityEngine;

// The trail the player leaves behind. Each mark fades over time, so an old
// route goes cold while a fresh one is easy to follow.
// Put this on DreamCube.
public class ScentTrail : MonoBehaviour
{
    public static ScentTrail Instance { get; private set; }

    public class Mark
    {
        public Vector3 position;   // world space, since the hunter walks in world space
        public float dropped;      // when it was laid
    }

    [Header("Trail")]
    [Tooltip("How far the player moves before dropping another mark.")]
    public float markSpacing = 1.2f;
    [Tooltip("Seconds before a mark goes completely cold.")]
    public float markLifetime = 25f;
    [Tooltip("Most marks kept at once. Older ones are dropped first.")]
    public int maxMarks = 120;

    [Header("Debug")]
    [Tooltip("Draws the trail in the Scene view, newest brightest.")]
    public bool showTrail = false;

    readonly List<Mark> marks = new List<Mark>();
    Vector3 lastDrop;
    bool started;

    void Awake()
    {
        Instance = this;
    }

    // Wipes the trail, for when the player changes level or respawns
    public void Clear()
    {
        marks.Clear();
        started = false;
    }

    void Update()
    {
        // Only track the player while he's playing, not in the map or cutscenes
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null) return;
        if (world.CurrentMode != CubeWorld.Mode.Playing) return;

        Vector3 here = world.player.transform.position;

        // Drop a mark once he's moved far enough from the last one
        if (!started || Vector3.Distance(here, lastDrop) >= markSpacing)
        {
            marks.Add(new Mark { position = here, dropped = Time.time });
            lastDrop = here;
            started = true;

            if (marks.Count > maxMarks) marks.RemoveAt(0);
        }

        // Clear out anything that's gone cold
        for (int i = marks.Count - 1; i >= 0; i--)
            if (Time.time - marks[i].dropped > markLifetime) marks.RemoveAt(i);
    }

    // The freshest mark within range of a spot, or null if the trail is cold there
    public Mark FindFreshest(Vector3 from, float range)
    {
        Mark best = null;
        float bestAge = float.MaxValue;

        // Find the freshest mark within range
        foreach (Mark mark in marks)
        {
            if (Vector3.Distance(from, mark.position) > range) continue;

            float age = Time.time - mark.dropped;
            if (age < bestAge)
            {
                bestAge = age;
                best = mark;
            }
        }

        return best;
    }

    // How strong the scent is at a spot, 0 (nothing) to 1 (just walked here)
    public float StrengthAt(Vector3 spot, float range)
    {
        Mark mark = FindFreshest(spot, range);
        if (mark == null) return 0f;

        float age = Time.time - mark.dropped;
        return Mathf.Clamp01(1f - age / markLifetime);
    }

    // Draw the trail in the Scene view for debugging
    void OnDrawGizmos()
    {
        if (!showTrail) return;

        // Draw the marks, newest brightest
        foreach (Mark mark in marks)
        {
            float age = Time.time - mark.dropped;
            float freshness = Mathf.Clamp01(1f - age / markLifetime);

            // Draw a wireframe sphere at the mark's position, with color based on freshness
            Gizmos.color = new Color(1f, freshness, 0f, freshness);
            Gizmos.DrawWireSphere(mark.position, 0.3f);
        }
    }
}
