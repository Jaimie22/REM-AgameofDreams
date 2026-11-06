using UnityEngine;

// The door of light. It only exists once every fragment has been
// delivered to the altar, so walking into it always completes the level.
// Greybox: a plain coloured block that appears instantly. No glow, light or mist.
[RequireComponent(typeof(Collider))]
public class DreamDoor : MonoBehaviour
{
    [Header("Colour")]
    public Color doorColor = new Color(1f, 0.95f, 0.7f);
    [Tooltip("Dims the colour. Lower this if the door reads as pure white.")]
    [Range(0f, 1f)] public float colorStrength = 0.8f;

    static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int colorId = Shader.PropertyToID("_Color");

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) return;

        // .material makes a copy, so this door has its own colour
        Color baseColor = doorColor * colorStrength;
        rend.material.SetColor(baseColorId, baseColor);
        rend.material.SetColor(colorId, baseColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        CubeWorld.Instance.CompleteCurrentLevel();
    }
}
