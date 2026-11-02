using UnityEngine;

// One gear: the loose ones lying about the face, the ones sitting on the machine's pegs,
// and the fixed motor and output gears.
// It turns about its own up axis, and the puzzle decides how fast.
// All motion is in local space, so it stays attached to its face
// however the cube is turned.
public class GearPiece : MonoBehaviour
{
    [Tooltip("Radius at the teeth. Two gears mesh when their centres are exactly the sum of their radii apart.")]
    public float radius = 1.5f;

    [Header("Spin")]
    [Tooltip("How quickly it speeds up and slows down, in degrees per second, each second.")]
    public float spinUpRate = 240f;

    [Header("Loose Gear")]
    [Tooltip("Slow spin while it's lying on the floor or being carried, so it catches the eye.")]
    public float idleSpinSpeed = 25f;
    public float bobHeight = 0.08f;
    public float bobSpeed = 1.6f;

    // Set by the puzzle
    [HideInInspector] public GearPeg peg;          // the peg it's sitting on, if any
    [HideInInspector] public float spinTarget;     // degrees per second it should be turning at

    bool loose;
    float restHeight;
    float spinSpeed;

    // Loose means lying on the floor, or in the player's hands.
    // It bobs and turns gently until it's put on a peg.
    public void SetLoose(bool isLoose)
    {
        loose = isLoose;
        restHeight = transform.localPosition.y;
    }

    void Update()
    {
        // Loose gears turn gently on their own, placed ones turn as fast as the chain says
        float target = loose ? idleSpinSpeed : spinTarget;
        spinSpeed = Mathf.MoveTowards(spinSpeed, target, spinUpRate * Time.deltaTime);

        // Spin around its own axis, not the world's
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

        if (!loose) return;

        // Local, so the bob doesn't drag it off the face when the cube turns
        Vector3 position = transform.localPosition;
        position.y = restHeight + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = position;
    }
}
