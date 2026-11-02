using UnityEngine;

// One peg on the gear machine. A single gear can sit on it.
// The puzzle keeps track of what's placed here.
public class GearPeg : MonoBehaviour
{
    [Tooltip("The size of gear that fits this peg's gap in the chain. Only used for the hint ring on the floor.")]
    public float requiredRadius = 1.5f;

    [HideInInspector] public GearPiece placed;
}
