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
    [Tooltip("Identifying colour for this face.")]
    public Color faceColor = Color.white;

    [Tooltip("Objects to tint with the face colour (e.g. the Floor). Drag them in here.")]
    public Renderer[] colorRenderers;

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

    // Tints each renderer without touching the shared material,
    // so every face can have its own colour
    public void ApplyColor()
    {
        if (colorRenderers == null) return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer r in colorRenderers)
        {
            if (r == null) continue;
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

    // Scene view helpers: line in the face colour = face "up", green sphere = spawn point
    void OnDrawGizmos()
    {
        Gizmos.color = faceColor;
        Gizmos.DrawLine(transform.position, transform.position + transform.up * 3f);

        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.4f);
        }
    }
}