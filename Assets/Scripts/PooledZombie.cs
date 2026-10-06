using Unity.Netcode;
using UnityEngine;

public class PooledZombie : NetworkBehaviour
{
    private ZombieSpawner owner;
    private NetworkObject prefabRef;

    public void SetPool(ZombieSpawner spawner, NetworkObject prefab)
    {
        owner = spawner;
        prefabRef = prefab;
    }

    public void Despawn()
    {
        if (!IsServer) return;
        owner.ReturnToPool(this, prefabRef);
    }
}
