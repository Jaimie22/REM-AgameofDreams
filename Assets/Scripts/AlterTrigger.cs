using UnityEngine;

// The area around the altar. Walking in with fragments delivers them.
[RequireComponent(typeof(Collider))]
public class AltarTrigger : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        LevelObjectives.Instance?.DeliverToAltar();
    }

    // Also deliver if the player is already standing there when they collect one
    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        LevelObjectives.Instance?.DeliverToAltar();
    }
}
