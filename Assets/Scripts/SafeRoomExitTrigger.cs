// Author: Omid Ameri
// Course: Multiplayer and Networking in Games and XR

using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class SafeRoomExitTrigger : NetworkBehaviour
{
    [SerializeField] private ExtractionManager extractionManager;

    private bool _alreadyTriggered;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (_alreadyTriggered) return;

        if (!other.CompareTag("Player"))
            return;

        if (!other.TryGetComponent<NetworkObject>(out var playerNetworkObject))
            return;

        _alreadyTriggered = true;
        extractionManager?.NotifySafeRoomExited(playerNetworkObject.OwnerClientId);
    }
}