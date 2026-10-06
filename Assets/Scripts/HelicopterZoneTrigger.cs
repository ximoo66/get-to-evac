// Author: Omid Ameri
// Course: Multiplayer and Networking in Games and XR

using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class HelicopterZoneTrigger : NetworkBehaviour
{
    [SerializeField] private ExtractionManager extractionManager;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (!other.CompareTag("Player"))
            return;

        if (!other.TryGetComponent<NetworkObject>(out var playerNetworkObject))
            return;

        extractionManager?.NotifyPlayerEnteredHelicopterZone(playerNetworkObject.OwnerClientId);
        Debug.Log("Come in");
    }
}