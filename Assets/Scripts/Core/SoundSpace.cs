using UnityEngine;

// Works out how loud and from which side a sound should be heard, for things in the level.
// The camera can be a long way from the player, so loudness comes from how far the sound
// is from the player, and the stereo side comes from where it is on screen.
public static class SoundSpace
{
    // 0 to 1. Full volume right beside the player, fading to silence at the hearing range.
    public static float Loudness(Vector3 position, float hearingRange)
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null) return 0f;

        float distance = Vector3.Distance(position, world.player.transform.position);
        float near = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, hearingRange));

        // Squared, so it drops away quickly and nearby sounds stand out
        return near * near;
    }

    // -1 is the left speaker, 1 is the right, from where the sound is on screen
    public static float Pan(Vector3 position)
    {
        Camera cam = Camera.main;
        CubeWorld world = CubeWorld.Instance;
        if (cam == null || world == null || world.player == null) return 0f;

        Vector3 offset = position - world.player.transform.position;
        offset.y = 0f;

        Vector3 right = cam.transform.right;
        right.y = 0f;
        right.Normalize();

        // Gentle, as a hard pan to one ear sounds odd
        float side = Vector3.Dot(offset, right) / Mathf.Max(1f, offset.magnitude);
        return Mathf.Clamp(side, -0.8f, 0.8f);
    }

    // True if this object is part of the face the player is on, and the game is being played
    public static bool IsOnActiveFace(Transform thing)
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.CurrentMode != CubeWorld.Mode.Playing) return false;
        if (world.CurrentFaceIndex < 0) return false;

        CubeFace face = world.faces[world.CurrentFaceIndex];
        return face != null && thing.IsChildOf(face.transform);
    }
}
