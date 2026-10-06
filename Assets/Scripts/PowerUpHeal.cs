using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class PowerUpHeal : NetworkBehaviour
{
    [Header("Healing Settings")]
    [SerializeField] private int healAmount = 50;   // Editable in Inspector

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSfx;   // Assign in Inspector
    [SerializeField] private float volume = 1f;

    private bool consumed = false; // Prevent double-trigger race conditions

    private void Awake()
    {
        // Ensure this is a trigger collider
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only the SERVER is allowed to process the pickup
        if (!IsServer || consumed)
            return;

        if (!other.CompareTag("Player"))
            return;

        // Check for Health component
        Health health = other.GetComponent<Health>();
        if (health == null)
            return;

        consumed = true;

        // Heal the player (server-authoritative)
        health.RestoreHealth(healAmount);

        // Tell every client to play the sound
        PlayPickupSfxClientRpc();

        // Despawn the object for everyone
        NetworkObject.Despawn(true);
    }

    [ClientRpc]
    private void PlayPickupSfxClientRpc()
    {
        if (pickupSfx != null)
        {
            AudioSource.PlayClipAtPoint(pickupSfx, transform.position, volume);
        }
    }
}
