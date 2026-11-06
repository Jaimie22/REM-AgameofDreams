using UnityEngine;

// The spade lying on the sand. Walk into it to pick it up.
// Tells the face's DigPuzzle that the player is now armed.
// All motion is in local space, so it stays attached to its face
// however the cube is turned.
[RequireComponent(typeof(Collider))]
public class SpadePickup : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Slow spin so it catches the eye. 0 to leave it still.")]
    public float spinSpeed = 30f;
    public float bobHeight = 0.15f;
    public float bobSpeed = 1.5f;

    [Header("Carried Spade")]
    [Tooltip("Where on the player it attaches. Leave empty to hold it at his side.")]
    public Transform handOverride;
    public Vector3 carryPosition = new Vector3(0f, 0.05f, 0.1f);
    [Tooltip("Lays the spade horizontally in the grip. Tune from here.")]
    public Vector3 carryRotation = new Vector3(0f, 0f, 90f);
    public float carryScale = 1f;

    Vector3 startPos;
    bool collected;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Start()
    {
        // Local, so the bob doesn't drag it off the face when the cube turns
        startPos = transform.localPosition;
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (collected) return;

        // Spin around its own axis, not the world's
        if (spinSpeed > 0f)
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = startPos + Vector3.up * bob;
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        collected = true;

        GiveToPlayer(player.transform);

        // Tell the dig puzzle on this face that he's got the tool
        DigPuzzle puzzle = GetComponentInParent<DigPuzzle>();
        if (puzzle == null) puzzle = FindFirstObjectByType<DigPuzzle>();
        if (puzzle != null) puzzle.GiveSpade();

        Debug.Log("Picked up the spade. Press E near the ground to dig.");

        gameObject.SetActive(false);
    }

    // Puts a spade in the player's hand
    void GiveToPlayer(Transform player)
    {
        // The greybox player is a plain capsule, so the spade is held at his side
        // unless a hand is assigned
        bool inHand = handOverride != null;
        Transform hand = inHand ? handOverride : player;

        GameObject carried = Instantiate(gameObject);

        carried.name = "CarriedSpade";
        carried.transform.SetParent(hand, false);
        carried.transform.localPosition = inHand ? carryPosition : new Vector3(0.45f, 0.5f, 0.15f);
        carried.transform.localRotation = Quaternion.Euler(carryRotation);
        carried.transform.localScale = Vector3.one * carryScale;

        // It's held now, so it shouldn't be pickable or bobbing
        SpadePickup pickup = carried.GetComponent<SpadePickup>();
        if (pickup != null) Destroy(pickup);

        foreach (Collider c in carried.GetComponentsInChildren<Collider>())
            Destroy(c);

        carried.SetActive(true);
    }
}
