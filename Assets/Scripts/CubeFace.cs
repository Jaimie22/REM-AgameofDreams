using UnityEngine;

// One face of the dream cube.
// Build the level as a child of this object as if it were lying flat on top.
// This object's green Y arrow points "outward" from the cube = "up" for the level.
public class CubeFace : MonoBehaviour
{
    [Header("Level")]
    [Tooltip("Where the player appears when entering this face.")]
    public Transform spawnPoint;

    [Tooltip("Parent object holding all of this face's level geometry.")]
    public GameObject levelContent;

    [Tooltip("Grey 'fog' object shown while this face is locked.")]
    public GameObject lockedCover;

    [Header("Face Colour")]
    [Tooltip("Untick to leave materials exactly as they are — needed for glass or any textured material.")]
    public bool tintFloors = false;

    [Tooltip("Identifying colour for this face. Only used when Tint Floors is ticked.")]
    public Color faceColor = Color.white;

    [Tooltip("Objects tinted with the face colour. Filled in automatically by the generator.")]
    public Renderer[] colorRenderers;

    [Tooltip("Optional. The giant dreamer standing behind the cube.")]
    public DreamerBackdrop dreamerBackdrop;

    [Header("Scene View")]
    [Tooltip("Colour of the gizmo line, so faces are still tellable apart in the editor.")]
    public Color gizmoColor = Color.cyan;

    // Colour property names: URP shaders use _BaseColor, older shaders use _Color
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");

    public bool IsUnlocked { get; private set; }

    void Awake()
    {
        ApplyColor();
    }

    // Runs in the editor whenever you change a value, so colours update instantly
    void OnValidate()
    {
        ApplyColor();
    }

    // Tints each renderer without touching the shared material.
    // With Tint Floors off, it clears any tint so the real material shows through.
    public void ApplyColor()
    {
        if (colorRenderers == null) return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (Renderer r in colorRenderers)
        {
            if (r == null) continue;

            if (!tintFloors)
            {
                // An empty block removes the override entirely
                r.SetPropertyBlock(null);
                continue;
            }

            r.GetPropertyBlock(block);
            block.SetColor(BaseColorID, faceColor);
            block.SetColor(ColorID, faceColor);
            r.SetPropertyBlock(block);
        }
    }

    // Shows the level or the fog, depending on lock state
    public void SetUnlocked(bool unlocked)
    {
        IsUnlocked = unlocked;
        if (levelContent != null) levelContent.SetActive(unlocked);
        if (lockedCover != null) lockedCover.SetActive(!unlocked);
    }

    // Scene view helpers: line = face "up", green sphere = spawn point
    void OnDrawGizmos()
    {
        Gizmos.color = tintFloors ? faceColor : gizmoColor;
        Gizmos.DrawLine(transform.position, transform.position + transform.up * 3f);

        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
        }
    }
}