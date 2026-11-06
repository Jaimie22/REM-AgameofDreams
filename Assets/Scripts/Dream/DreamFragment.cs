using UnityEngine;

// A dream fragment. Collect them all to open the level's door of light.
// Greybox: a plain coloured shape that turns and bobs so it can be spotted. No glow or light.
public class DreamFragment : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Degrees per second it spins, so it catches the eye. 0 for none.")]
    public float spinSpeed = 60f;
    [Tooltip("How far it bobs up and down. 0 for none.")]
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;

    [Header("Colour")]
    public Color glowColor = new Color(0.6f, 0.85f, 1f);

    static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int colorId = Shader.PropertyToID("_Color");

    Vector3 startPos;
    bool collected;

    private void Start()
    {
        startPos = transform.localPosition;

        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // .material makes a copy, so changing it affects only this fragment
            Material mat = rend.material;
            mat.SetColor(baseColorId, glowColor);
            mat.SetColor(colorId, glowColor);
        }
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = startPos + Vector3.up * bob;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        collected = true;
        LevelObjectives.Instance?.FragmentCollected();
        gameObject.SetActive(false);
    }
}
