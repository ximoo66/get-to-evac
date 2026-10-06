// Author: Omid Ameri
// Course: Multiplayer and Networking in Games and XR

using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Health : NetworkBehaviour
{
    [field: SerializeField] public int MaximumHealth { get; private set; } = 100;

    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();
    public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);

    // ✅ Server-controlled: if true, this object ignores all damage.
    public NetworkVariable<bool> IsInvulnerable = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private bool isProcessingDeath = false;

    public Action<Health> OnDie;

    private PooledZombie pooledZombie;
    private Animator anim;
    private NavMeshAgent agent;
    private AI ai;

    [Header("Zombie Settings")]
    [SerializeField] private bool isZombie = false;
    [SerializeField] private float deathDelay = 3.5f;
    [SerializeField] private NetworkObject worldCanvasNetworkObject;

    [Header("Player Settings")]
    [SerializeField] private bool isPlayer = false; // ✅ set true on player prefab only

    private void Awake()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        ai = GetComponent<AI>();
        pooledZombie = GetComponent<PooledZombie>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = MaximumHealth;
            IsDead.Value = false;
            IsInvulnerable.Value = false;
        }

        IsDead.OnValueChanged += OnDeadStateChanged;

        if (IsDead.Value)
            OnDeadStateChanged(false, true);
    }

    public override void OnNetworkDespawn()
    {
        IsDead.OnValueChanged -= OnDeadStateChanged;
    }

    // --------------------------------------------------
    // DAMAGE / HEAL
    // --------------------------------------------------

    public void TakeDamage(int damageValue, bool applyStun = true)
    {
        if (!IsServer) return;

        // ✅ Winners (or any invulnerable object) ignore damage completely
        if (IsInvulnerable.Value) return;

        if (IsDead.Value || isProcessingDeath) return;

        ModifyHealth(-damageValue);

        if (applyStun && isZombie && !IsDead.Value && ai != null)
            ai.ApplyStun();
    }

    public void RestoreHealth(int healingValue)
    {
        if (!IsServer) return;
        if (IsDead.Value) return;

        ModifyHealth(healingValue);
    }

    private void ModifyHealth(int value)
    {
        if (IsDead.Value) return;

        int newHealth = CurrentHealth.Value + value;
        CurrentHealth.Value = Mathf.Clamp(newHealth, 0, MaximumHealth);

        if (CurrentHealth.Value <= 0)
            HandleDeath();
    }

    // --------------------------------------------------
    // INVULNERABILITY (SERVER)
    // --------------------------------------------------

    public void SetInvulnerable(bool value)
    {
        if (!IsServer) return;
        IsInvulnerable.Value = value;
    }

    // --------------------------------------------------
    // DEATH
    // --------------------------------------------------

    private void HandleDeath()
    {
        if (isProcessingDeath) return;

        isProcessingDeath = true;
        IsDead.Value = true;

        OnDie?.Invoke(this);

        if (isZombie)
            StartCoroutine(ZombieDeathRoutine());
    }

    private IEnumerator ZombieDeathRoutine()
    {
        if (agent != null) agent.isStopped = true;
        if (ai != null) ai.enabled = false;

        yield return new WaitForSeconds(deathDelay);

        if (worldCanvasNetworkObject != null && worldCanvasNetworkObject.IsSpawned)
            worldCanvasNetworkObject.Despawn(true);

        if (pooledZombie != null)
        {
            pooledZombie.Despawn();
        }
        else
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
            else
                gameObject.SetActive(false);
        }
    }

    public void ResetForReuse()
    {
        if (!IsServer) return;

        isProcessingDeath = false;

        CurrentHealth.Value = MaximumHealth;
        IsDead.Value = false;
        IsInvulnerable.Value = false;

        if (ai != null) ai.enabled = true;
        if (agent != null)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }

        if (anim != null)
        {
            anim.SetBool("IsDead", false);
            anim.SetBool("IsStunned", false);
            anim.Rebind();
        }
    }

    // --------------------------------------------------
    // CLIENT VISUAL + UI
    // --------------------------------------------------

    private void OnDeadStateChanged(bool oldValue, bool newValue)
    {
        if (!newValue) return;

        if (anim != null)
        {
            anim.SetBool("IsStunned", false);
            anim.SetBool("IsDead", true);
        }

        // Show death UI only for the local owner of the player
        if (!isPlayer) return;
        if (!IsClient) return;
        if (NetworkObject == null) return;

        bool isLocalOwner = NetworkObject.OwnerClientId == NetworkManager.Singleton.LocalClientId;
        if (!isLocalOwner) return;

        PlayerEndScreenUI ui = null;
#if UNITY_2023_1_OR_NEWER
        ui = UnityEngine.Object.FindFirstObjectByType<PlayerEndScreenUI>(FindObjectsInactive.Include);
#else
        ui = UnityEngine.Object.FindObjectOfType<PlayerEndScreenUI>(true);
#endif
        if (ui != null)
            ui.ShowDeath("YOU DIED\nWaiting for teammates...");
    }
}