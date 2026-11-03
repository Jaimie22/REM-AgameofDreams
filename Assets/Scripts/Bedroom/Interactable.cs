using UnityEngine;
using UnityEngine.InputSystem;

// Something in the room the player can interact with by walking up
// and pressing E. The lamp, a toy, the bed.
[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    public enum Kind { ToggleLight, PickUp, GoToBed }

    [Header("What it is")]
    public Kind kind = Kind.PickUp;
    [Tooltip("Shown in the Console, and later on screen. Keep it short.")]
    public string prompt = "Pick up";

    [Header("Reach")]
    [Tooltip("How close he has to be.")]
    public float reach = 2f;

    [Header("Toggle Light")]
    [Tooltip("The light this switches, for a lamp.")]
    public Light lamp;

    [Header("Pick Up")]
    [Tooltip("Where it goes when carried. Leave empty to find his hand.")]
    public Transform carryPoint;
    public Vector3 carryPosition;
    public Vector3 carryRotation;

    [Header("Go To Bed")]
    [Tooltip("Called when he turns in. Hook up whatever starts the dream.")]
    public UnityEngine.Events.UnityEvent onSleep;

    bool used;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (used && kind != Kind.ToggleLight) return;

        CubeWorld world = CubeWorld.Instance;
        Transform player = world != null && world.player != null
            ? world.player.transform
            : null;

        // The opening scene may not have a CubeWorld, so fall back to a search
        if (player == null)
        {
            PlayerController found = FindFirstObjectByType<PlayerController>();
            if (found == null) return;
            player = found.transform;
        }

        if (Vector3.Distance(transform.position, player.position) > reach) return;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        Use(player);
    }

    void Use(Transform player)
    {
        switch (kind)
        {
            case Kind.ToggleLight:
                if (lamp != null)
                {
                    lamp.enabled = !lamp.enabled;
                    Debug.Log(lamp.enabled ? "The lamp comes on." : "The lamp goes out.");
                }
                break;

            case Kind.PickUp:
                Carry(player);
                used = true;
                break;

            case Kind.GoToBed:
                Debug.Log("He climbs into bed.");
                used = true;
                onSleep?.Invoke();
                break;
        }
    }

    // Puts it in his hands
    void Carry(Transform player)
    {
        Transform hand = carryPoint != null ? carryPoint : FindHand(player);
        if (hand == null) hand = player;

        transform.SetParent(hand, false);
        transform.localPosition = carryPosition;
        transform.localRotation = Quaternion.Euler(carryRotation);

        // Carried now, so it shouldn't be pickable
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Debug.Log("He picks up the " + name + ".");
    }

    Transform FindHand(Transform player)
    {
        Animator animator = player.GetComponentInChildren<Animator>();

        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone != null) return bone;
        }

        foreach (Transform t in player.GetComponentsInChildren<Transform>())
            if (t.name.ToLower().Contains("hand")) return t;

        return null;
    }
}
