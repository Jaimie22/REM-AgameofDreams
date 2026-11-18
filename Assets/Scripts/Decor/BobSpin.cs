using UnityEngine;

// Slowly spins and bobs an object (the dream shard).
public class BobSpin : MonoBehaviour
{
    public float spinSpeed = 40f;
    public float bobHeight = 0.1f;
    public float bobSpeed = 1.5f;

    Vector3 start;
    float phase;

    void Start()
    {
        start = transform.localPosition;
        phase = Random.value * 6.28f;          // random offset per shard
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

        if (bobHeight > 0f)
            transform.localPosition = start + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
    }
}
