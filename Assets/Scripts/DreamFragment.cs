using UnityEngine;

// A dream fragment. Collect them all to open the level's door of light.
public class DreamFragment : MonoBehaviour
{
    [Tooltip("Degrees per second it spins, so it catches the eye.")]
    public float spinSpeed = 60f;
    [Tooltip("How far it bobs up and down.")]
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;

    Vector3 startPos;
    bool collected;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = startPos + Vector3.up * bob;
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.GetComponent<PlayerController>() == null) return;

        collected = true;
        LevelObjectives.Instance?.FragmentCollected();
        gameObject.SetActive(false);
    }
}
