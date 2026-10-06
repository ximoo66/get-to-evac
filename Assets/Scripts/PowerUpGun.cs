using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class PowerUpGun : NetworkBehaviour
{
    [Header("Auto Fire Settings")]
    [SerializeField] private float fireInterval = 0.2f;
    [SerializeField] private float duration = 3f;

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSfx;
    [SerializeField] private float volume = 1f;

    private bool consumed = false;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || consumed)
            return;

        if (!other.CompareTag("Player"))
            return;

        ProjectileLaunch weapon = other.GetComponentInChildren<ProjectileLaunch>();
        if (weapon == null)
            return;

        consumed = true;

        // Activate the buff on the server-owned weapon
        weapon.ActivateAutoFire(fireInterval, duration);

        PlayPickupClientRpc();

        NetworkObject.Despawn(true);
    }

    [ClientRpc]
    private void PlayPickupClientRpc()
    {
        if (pickupSfx != null)
            AudioSource.PlayClipAtPoint(pickupSfx, transform.position, volume);
    }
}
