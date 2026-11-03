using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The gear puzzle on the Function face. Loose gears are lying about the room.
// The player carries them, one at a time, to the pegs on the machine.
// When the gears mesh in a chain from the motor all the way to the output gear,
// everything turns, and the cage around the door of light sinks into the floor.
public class GearPuzzle : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("How close the player must be to pick up or put down a gear.")]
    public float interactRange = 2.5f;
    [Tooltip("Seconds between presses, so one tap can't pick a gear up and put it straight back.")]
    public float interactCooldown = 0.25f;

    [Header("Carrying")]
    [Tooltip("How high above the player a carried gear floats.")]
    public float carryHeight = 2.4f;
    [Tooltip("Size of a carried gear compared with a placed one.")]
    public float carryScale = 0.4f;

    [Header("Machine")]
    [Tooltip("How fast the motor turns, in degrees per second.")]
    public float motorSpeed = 60f;
    [Tooltip("How far out a pair of gears can be from touching and still count as meshing.")]
    public float meshTolerance = 0.05f;

    [Header("The Cage")]
    [Tooltip("Seconds the gears turn before the cage starts to open.")]
    public float openDelay = 1.2f;
    [Tooltip("Seconds the cage takes to sink into the floor.")]
    public float openTime = 1.8f;

    // Set by GearGenerator, which holds the clips
    [HideInInspector] public AudioClip placeClip;
    [HideInInspector] public AudioClip meshClip;
    [HideInInspector] public AudioClip startClip;
    [HideInInspector] public AudioClip cageClip;

    public bool Solved { get; private set; }

    // For the objective UI: how many gears are joined up, and how many pegs there are
    public int LinkedCount => linkedCount;
    public int PegCount => pegs.Count;

    GearPiece motor;
    GearPiece output;
    List<GearPeg> pegs = new List<GearPeg>();
    List<GearPiece> gears = new List<GearPiece>();   // every gear the player can pick up

    Transform cage;
    float cageDrop;
    Transform root;
    float pegGearHeight;
    float looseGearHeight;

    GearPiece carried;
    float lastPressTime;
    bool ready;
    int linkedCount;
    AudioSource source;

    // Called by the generator once the room is built
    public void SetUp(GearPiece motorGear, List<GearPeg> machinePegs, GearPiece outputGear,
                      List<GearPiece> looseGears, Transform cageRoot, float cageDropDistance,
                      Transform levelRoot, float onPegHeight, float onFloorHeight)
    {
        StopAllCoroutines();

        // A gear still in the player's hands from the last build would be left behind
        if (carried != null) Destroy(carried.gameObject);
        carried = null;

        motor = motorGear;
        output = outputGear;
        pegs = machinePegs;
        gears = looseGears;
        cage = cageRoot;
        cageDrop = cageDropDistance;
        root = levelRoot;
        pegGearHeight = onPegHeight;
        looseGearHeight = onFloorHeight;

        Solved = false;
        linkedCount = 0;
        ready = true;

        // The motor turns from the start, so there's something to connect to
        EvaluateChain();
    }

    void Update()
    {
        if (!ready || Solved) return;
        if (!IsActiveFace()) return;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        if (Time.time - lastPressTime < interactCooldown) return;
        lastPressTime = Time.time;

        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.player == null) return;

        // The player is in world space, the machine is local to the face
        Transform player = world.player.transform;
        Vector3 playerLocal = root.InverseTransformPoint(player.position);

        if (carried == null) TryPickUp(playerLocal, player);
        else TryPutDown(playerLocal, player);
    }

    // True when the player is actually on this face
    bool IsActiveFace()
    {
        CubeWorld world = CubeWorld.Instance;
        if (world == null || world.CurrentMode != CubeWorld.Mode.Playing) return false;
        if (world.CurrentFaceIndex < 0) return false;

        return world.faces[world.CurrentFaceIndex] == GetComponent<CubeFace>();
    }

    // --- Carrying -------------------------------------------------------

    // Picks up the nearest gear in reach, whether it's on the floor or on a peg
    void TryPickUp(Vector3 playerLocal, Transform player)
    {
        GearPiece best = null;
        float bestDistance = interactRange;

        foreach (GearPiece gear in gears)
        {
            if (gear == null) continue;

            float d = Flat(gear.transform.localPosition, playerLocal);

            // The closest gear within range is the one that gets picked up
            if (d < bestDistance)
            {
                bestDistance = d;
                best = gear;
            }
        }

        if (best == null)
        {
            Debug.Log("Nothing to pick up here.");
            return;
        }

        // Lifting it off a peg frees the peg up
        if (best.peg != null)
        {
            best.peg.placed = null;
            best.peg = null;
        }

        Carry(best, player);
        CubeWorld.Instance.player.Audio.PlayPickup();
        Debug.Log("Picked up a gear. Press E beside a peg to place it.");

        EvaluateChain();
    }

    // Puts the carried gear on the nearest peg, swapping if the peg is taken,
    // or drops it at his feet if there's no peg near
    void TryPutDown(Vector3 playerLocal, Transform player)
    {
        GearPeg peg = NearestPeg(playerLocal);

        if (peg == null)
        {
            DropAt(playerLocal);
            return;
        }

        GearPiece swapped = peg.placed;

        if (swapped != null)
        {
            peg.placed = null;
            swapped.peg = null;
        }

        PutOnPeg(carried, peg);
        carried = null;
        PlaySound(placeClip, 1f);
        Debug.Log("Placed a gear on the machine.");

        // The gear that was on the peg goes into his hands instead
        if (swapped != null) Carry(swapped, player);

        EvaluateChain();
    }

    // The empty-or-not peg closest to the player, within reach
    GearPeg NearestPeg(Vector3 playerLocal)
    {
        GearPeg best = null;
        float bestDistance = interactRange;

        foreach (GearPeg peg in pegs)
        {
            if (peg == null) continue;

            float d = Flat(peg.transform.localPosition, playerLocal);

            if (d < bestDistance)
            {
                bestDistance = d;
                best = peg;
            }
        }

        return best;
    }

    // The gear floats above the player's head
    void Carry(GearPiece gear, Transform player)
    {
        // The player might be scaled, so undo that or the gear is too
        float scale = Mathf.Max(0.01f, player.lossyScale.y);

        gear.transform.SetParent(player, false);
        gear.transform.localPosition = Vector3.up * (carryHeight / scale);
        gear.transform.localRotation = Quaternion.identity;
        gear.transform.localScale = Vector3.one * (carryScale / scale);

        gear.SetLoose(true);
        carried = gear;
    }

    void PutOnPeg(GearPiece gear, GearPeg peg)
    {
        gear.transform.SetParent(root, false);
        gear.transform.localPosition = peg.transform.localPosition + Vector3.up * pegGearHeight;
        gear.transform.localRotation = Quaternion.identity;
        gear.transform.localScale = Vector3.one;

        gear.SetLoose(false);
        gear.peg = peg;
        peg.placed = gear;
    }

    void DropAt(Vector3 playerLocal)
    {
        float floorY = pegs.Count > 0 ? pegs[0].transform.localPosition.y : 0f;

        carried.transform.SetParent(root, false);
        carried.transform.localPosition = new Vector3(playerLocal.x, floorY + looseGearHeight, playerLocal.z);
        carried.transform.localRotation = Quaternion.identity;
        carried.transform.localScale = Vector3.one;

        carried.SetLoose(true);
        carried = null;

        // Quieter than fitting it to a peg
        PlaySound(placeClip, 0.5f);
        Debug.Log("Put the gear down.");
    }

    // --- The chain --------------------------------------------------------

    // Works out which gears are turning. The motor always turns, and each gear in the
    // chain turns only if the one before it is turning and they're touching.
    void EvaluateChain()
    {
        foreach (GearPiece gear in gears)
            if (gear != null) gear.spinTarget = 0f;

        output.spinTarget = 0f;
        motor.spinTarget = motorSpeed;

        GearPiece previous = motor;
        float speed = motorSpeed;
        int linked = 0;
        bool complete = true;

        // Each peg in turn, then the output gear at the end of the line
        for (int i = 0; i <= pegs.Count; i++)
        {
            GearPiece next = i < pegs.Count ? pegs[i].placed : output;

            // A gap in the chain, so everything past it stays still
            if (next == null)
            {
                complete = false;
                break;
            }

            float apart = Flat(previous.transform.localPosition, next.transform.localPosition);
            float touching = previous.radius + next.radius;

            // Too far apart, or overlapping, so they don't mesh
            if (Mathf.Abs(apart - touching) > meshTolerance)
            {
                complete = false;
                break;
            }

            // Meshed gears turn opposite ways, and a smaller gear turns faster
            speed = -speed * previous.radius / next.radius;
            next.spinTarget = speed;

            previous = next;
            linked++;
        }

        // Another gear has joined the chain
        if (linked > linkedCount && !complete) PlaySound(meshClip, 0.8f);
        linkedCount = linked;

        if (!complete) return;

        // The chain reached the output gear
        Solved = true;
        PlaySound(startClip, 1f);
        Debug.Log("The gears mesh. The cage begins to open.");
        StartCoroutine(OpenTheCage());
    }

    // Lets the gears turn for a moment, then the cage sinks away
    IEnumerator OpenTheCage()
    {
        yield return new WaitForSeconds(openDelay);

        PlaySound(cageClip, 1f);

        if (cage != null)
        {
            Vector3 start = cage.localPosition;
            Vector3 end = start + Vector3.down * cageDrop;
            float duration = Mathf.Max(0.01f, openTime);

            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                cage.localPosition = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            cage.gameObject.SetActive(false);
        }

        PlayerAudio.PlayDoorSound();
        Debug.Log("The cage is open. The door of light is free.");
    }

    // A flat sound, heard the same wherever the player stands
    void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null) return;

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    // Distance along the floor, ignoring height
    float Flat(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
