using UnityEngine;

// The area around the altar. Walking in with fragments delivers them.
[RequireComponent(typeof(Collider))]
public class AltarTrigger : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        LevelObjectives.Instance?.deliverToAltar();
    }

    // Also deliver if the player is already standing there when they collect one
    private void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        LevelObjectives.Instance?.deliverToAltar();
    }
}
