using UnityEngine;

// The player's health and energy.
// Health is hearts. One is lost each time the hunter catches the player.
// Energy drains while sprinting and slowly comes back once the player eases off.
// This is added to the player automatically the first time it's asked for, so there's no setup.
public class PlayerVitals : MonoBehaviour
{
    [Header("Health")]
    public int maxHearts = 3;
    [Tooltip("Seconds after being hurt when the player can't be hurt again, so one catch can't count twice.")]
    public float hurtCooldown = 1.5f;
    [Tooltip("When the last heart goes, this many seconds pass before the hearts refill.")]
    public float refillDelay = 1.2f;

    [Header("Energy")]
    public float maxEnergy = 100f;
    [Tooltip("Energy used per second while sprinting. Lower is slower.")]
    public float sprintDrain = 10f;
    [Tooltip("Energy gained per second while not sprinting.")]
    public float regenRate = 8f;
    [Tooltip("Seconds after sprinting before energy starts coming back.")]
    public float regenDelay = 1.5f;
    [Tooltip("After running right out, sprinting is locked until energy climbs back to this fraction.")]
    [Range(0f, 1f)]
    public float exhaustedRecover = 0.2f;

    public int Hearts { get; private set; }
    public float Energy { get; private set; }
    public float EnergyFraction => maxEnergy > 0f ? Energy / maxEnergy : 0f;
    public bool Exhausted { get; private set; }
    public bool Sprinting { get; private set; }

    // A level can switch running off altogether (the final chase). The energy bar hides while it's locked.
    public bool SprintLocked { get; set; }

    // For the UI, so it can flash when the player is hurt
    public float LastHurtTime { get; private set; } = -999f;

    float lastSprintTime = -999f;
    float refillAt = -1f;

    void Awake()
    {
        RestoreAll();
    }

    void Update()
    {
        // Out of hearts, so they come back after a short pause (the hunter has already reset the player)
        if (refillAt > 0f && Time.time >= refillAt)
        {
            RestoreAll();
        }
    }

    // Called by the player every frame. Returns true if the player is really sprinting.
    public bool UpdateSprint(bool wantsSprint, float deltaTime)
    {
        bool sprinting = wantsSprint && !SprintLocked && !Exhausted && Energy > 0f;

        if (sprinting)
        {
            Energy -= sprintDrain * deltaTime;
            lastSprintTime = Time.time;

            // Running dry locks sprinting for a bit
            if (Energy <= 0f)
            {
                Energy = 0f;
                Exhausted = true;
                sprinting = false;
            }
        }
        else if (Time.time - lastSprintTime >= regenDelay)
        {
            Energy = Mathf.Min(maxEnergy, Energy + regenRate * deltaTime);

            if (Exhausted && Energy >= maxEnergy * exhaustedRecover) Exhausted = false;
        }

        Sprinting = sprinting;
        return sprinting;
    }

    // Takes a heart. Returns false if the player was still protected from the last hit.
    public bool TakeDamage()
    {
        if (Time.time - LastHurtTime < hurtCooldown) return false;

        LastHurtTime = Time.time;
        Hearts = Mathf.Max(0, Hearts - 1);

        // No hearts left, so they refill after a moment
        if (Hearts <= 0) refillAt = Time.time + refillDelay;

        return true;
    }

    // Full hearts and full energy, used when a level starts
    public void RestoreAll()
    {
        Hearts = maxHearts;
        Energy = maxEnergy;
        Exhausted = false;
        Sprinting = false;
        refillAt = -1f;
    }
}
