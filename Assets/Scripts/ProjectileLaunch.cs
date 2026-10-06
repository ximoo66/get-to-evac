using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ProjectileLaunch : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader myReader;
    [SerializeField] private Transform ProjectileSpawnPoint;

    [Header("Normal Projectiles")]
    [SerializeField] private GameObject ServerProjectilePrefab;
    [SerializeField] private GameObject ClientProjectilePrefab;

    [Header("PowerUp Projectiles")]
    [SerializeField] private GameObject ServerPowerUpProjectilePrefab;
    [SerializeField] private GameObject ClientPowerUpProjectilePrefab;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip ShootSfx;
    [SerializeField] private AudioClip PowerUpShootSfx;   // NEW
    [SerializeField] private AudioClip ReloadSfx;

    [Header("Weapon Settings")]
    [SerializeField] private float ProjectileSpeed = 20.0f;
    [SerializeField] private float ForceMultiplier = 1.0f;

    [Header("Gun Timing")]
    [SerializeField] private float ShotGap = 0.15f;      // time between normal shots
    [SerializeField] private float ReloadTime = 1.25f;   // reload duration

    [Header("Magazine")]
    [SerializeField] private int MagazineSize = 10;

    [Header("Inspector Tweaks")]
    [SerializeField] private Vector3 ProjectileRotationOffset = Vector3.zero;

    // SERVER authoritative ammo and reload state (per-player)
    private NetworkVariable<int> CurrentAmmo = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> IsReloading = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool attackHeld = false;

    // local-only prediction timers (visual)
    private float shotTimer = 0f;
    private float reloadTimer = 0f;

    // PowerUp auto-fire (server authoritative)
    private Coroutine autoFireCoroutine;

    public int GetCurrentAmmo() => CurrentAmmo.Value;
    public int GetMagazineSize() => MagazineSize;

    public System.Action<int, int> OnAmmoChanged;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentAmmo.Value = MagazineSize;
            IsReloading.Value = false;
        }

        CurrentAmmo.OnValueChanged += HandleAmmoChanged;

        if (!IsOwner) return;
        myReader.PrimaryAttackEvent += AttackEvent;

        HandleAmmoChanged(0, CurrentAmmo.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            myReader.PrimaryAttackEvent -= AttackEvent;

        CurrentAmmo.OnValueChanged -= HandleAmmoChanged;

        if (IsServer && autoFireCoroutine != null)
        {
            StopCoroutine(autoFireCoroutine);
            autoFireCoroutine = null;
        }
    }

    private void HandleAmmoChanged(int oldValue, int newValue)
    {
        OnAmmoChanged?.Invoke(newValue, MagazineSize);
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (reloadTimer > 0f)
        {
            reloadTimer -= Time.deltaTime;
            if (reloadTimer <= 0f)
                reloadTimer = 0f;
        }

        if (!attackHeld) return;

        shotTimer -= Time.deltaTime;
        if (shotTimer > 0f) return;

        RequestShootServerRpc();
        shotTimer = ShotGap;
    }

    private void AttackEvent(bool pressed)
    {
        attackHeld = pressed;
    }

    // =========================================================
    // POWERUP: AUTO FIRE (ANOTHER KIND OF BULLET, UNLIMITED AMMO)
    // =========================================================
    public void ActivateAutoFire(float fireInterval, float duration)
    {
        if (!IsServer) return;

        if (autoFireCoroutine != null)
            StopCoroutine(autoFireCoroutine);

        autoFireCoroutine = StartCoroutine(AutoFireRoutine(fireInterval, duration));
    }

    private IEnumerator AutoFireRoutine(float fireInterval, float duration)
    {
        if (ProjectileSpawnPoint == null)
        {
            autoFireCoroutine = null;
            yield break;
        }

        float interval = Mathf.Max(0.01f, fireInterval);
        float endTime = Time.time + Mathf.Max(0f, duration);

        while (Time.time < endTime)
        {
            Vector3 spawnPos = ProjectileSpawnPoint.position;
            Vector3 direction = ProjectileSpawnPoint.forward;

            SpawnServerPowerUpProjectile(spawnPos, direction);

            PowerUpAttackClientRpc(spawnPos, direction);

            // Owner gets instant prediction + POWERUP SFX
            PlayPowerUpShootClientRpc(OwnerClientId);

            yield return new WaitForSeconds(interval);
        }

        autoFireCoroutine = null;
    }

    private void SpawnServerPowerUpProjectile(Vector3 spawnPos, Vector3 direction)
    {
        if (ServerPowerUpProjectilePrefab == null) return;

        Quaternion rot =
        Quaternion.LookRotation(direction.normalized, Vector3.up) *
        Quaternion.Euler(ProjectileRotationOffset);

        GameObject projectile = Instantiate(ServerPowerUpProjectilePrefab, spawnPos, rot);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = direction.normalized * ProjectileSpeed * ForceMultiplier;
    }

    [ClientRpc]
    private void PowerUpAttackClientRpc(Vector3 spawnPos, Vector3 direction)
    {
        if (IsOwner) return;
        SpawnFakeClientPowerUpProjectile(spawnPos, direction);
    }

    private void SpawnFakeClientPowerUpProjectile(Vector3 spawnPos, Vector3 direction)
    {
        if (ClientPowerUpProjectilePrefab == null) return;

        Quaternion rot =
        Quaternion.LookRotation(direction.normalized, Vector3.up) *
        Quaternion.Euler(ProjectileRotationOffset);

        GameObject projectile = Instantiate(ClientPowerUpProjectilePrefab, spawnPos, rot);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = direction.normalized * ProjectileSpeed * ForceMultiplier;
    }

    [ClientRpc]
    private void PlayPowerUpShootClientRpc(ulong ownerId)
    {
        if (NetworkManager.Singleton.LocalClientId != ownerId) return;

        if (audioSource)
        {
            AudioClip clipToPlay = PowerUpShootSfx != null ? PowerUpShootSfx : ShootSfx;
            if (clipToPlay != null)
                audioSource.PlayOneShot(clipToPlay);
        }

        SpawnFakeClientPowerUpProjectile(ProjectileSpawnPoint.position, ProjectileSpawnPoint.forward);
    }

    // ============================
    // SERVER VALIDATION (NORMAL FIRE)
    // ============================

    [ServerRpc]
    private void RequestShootServerRpc(ServerRpcParams rpcParams = default)
    {
        if (IsReloading.Value) return;

        if (CurrentAmmo.Value <= 0)
        {
            StartReloadOnServer(rpcParams.Receive.SenderClientId);
            return;
        }

        CurrentAmmo.Value--;

        Vector3 spawnPos = ProjectileSpawnPoint.position;
        Vector3 direction = ProjectileSpawnPoint.forward;

        SpawnServerProjectile(spawnPos, direction);
        PrimaryAttackClientRpc(spawnPos, direction);

        PlayShootClientRpc(rpcParams.Receive.SenderClientId);
    }

    private void SpawnServerProjectile(Vector3 spawnPos, Vector3 direction)
    {
        if (ServerProjectilePrefab == null) return;

        Quaternion rot =
        Quaternion.LookRotation(direction.normalized, Vector3.up) *
        Quaternion.Euler(ProjectileRotationOffset);

        GameObject projectile = Instantiate(ServerProjectilePrefab, spawnPos, rot);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = direction.normalized * ProjectileSpeed * ForceMultiplier;
    }

    [ClientRpc]
    private void PrimaryAttackClientRpc(Vector3 spawnPos, Vector3 direction)
    {
        if (IsOwner) return;
        SpawnFakeClientProjectile(spawnPos, direction);
    }

    private void SpawnFakeClientProjectile(Vector3 spawnPos, Vector3 direction)
    {
        if (ClientProjectilePrefab == null) return;

        Quaternion rot =
        Quaternion.LookRotation(direction.normalized, Vector3.up) *
        Quaternion.Euler(ProjectileRotationOffset);

        GameObject projectile = Instantiate(ClientProjectilePrefab, spawnPos, rot);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = direction.normalized * ProjectileSpeed * ForceMultiplier;
    }

    // ============================
    // RELOAD SYSTEM
    // ============================

    [ServerRpc]
    private void StartReloadServerRpc(ServerRpcParams rpcParams = default)
    {
        StartReloadOnServer(rpcParams.Receive.SenderClientId);
    }

    private void StartReloadOnServer(ulong clientId)
    {
        if (IsReloading.Value) return;

        IsReloading.Value = true;

        BeginReloadClientRpc(clientId);

        StartCoroutine(FinishReloadCoroutine(clientId));
    }

    private IEnumerator FinishReloadCoroutine(ulong clientId)
    {
        yield return new WaitForSeconds(ReloadTime);

        CurrentAmmo.Value = MagazineSize;
        IsReloading.Value = false;
    }

    [ClientRpc]
    private void BeginReloadClientRpc(ulong ownerId)
    {
        if (NetworkManager.Singleton.LocalClientId != ownerId) return;

        reloadTimer = ReloadTime;

        if (audioSource && ReloadSfx)
            audioSource.PlayOneShot(ReloadSfx);
    }

    // ============================
    // AUDIO & PREDICTION (NORMAL FIRE)
    // ============================

    [ClientRpc]
    private void PlayShootClientRpc(ulong ownerId)
    {
        if (NetworkManager.Singleton.LocalClientId != ownerId) return;

        if (audioSource && ShootSfx)
            audioSource.PlayOneShot(ShootSfx);

        SpawnFakeClientProjectile(ProjectileSpawnPoint.position, ProjectileSpawnPoint.forward);
    }
}
