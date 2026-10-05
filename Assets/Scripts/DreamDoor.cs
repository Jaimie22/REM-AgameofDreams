using UnityEngine;

// The door of light. Walking into it completes the level,
// but only once every dream fragment has been collected.
[RequireComponent(typeof(Collider))]
public class DreamDoor : MonoBehaviour
{
    [Tooltip("Colour while the door is still locked.")]
    public Color closedColor = new Color(0.2f, 0.2f, 0.3f);
    [Tooltip("Colour once every fragment has been collected.")]
    public Color openColor = new Color(1f, 0.95f, 0.7f);

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

    Renderer rend;
    bool wasOpen;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Start()
    {
        rend = GetComponent<Renderer>();
        UpdateLook(true);
    }

    void Update()
    {
        bool open = IsOpen();
        if (open != wasOpen) UpdateLook(open);
    }

    bool IsOpen()
    {
        // With no objectives in the scene, the door is always open
        return LevelObjectives.Instance == null || LevelObjectives.Instance.DoorOpen;
    }

    // Dim when locked, glowing when open
    void UpdateLook(bool open)
    {
        wasOpen = open;
        if (rend == null) return;

        Color c = open ? openColor : closedColor;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetColor(BaseColorID, c);
        block.SetColor(EmissionID, open ? c * 2f : Color.black);
        rend.SetPropertyBlock(block);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        if (!IsOpen())
        {
            int left = LevelObjectives.Instance.Required - LevelObjectives.Instance.Collected;
            Debug.Log("The door is sealed. " + left + " fragment(s) still out there.");
            return;
        }

        CubeWorld.Instance.CompleteCurrentLevel();
    }
}
