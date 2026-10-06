using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public sealed class ZombieSpawner : NetworkBehaviour
{
    // --------------------------------------------------
    // STAGE EVENTS
    // --------------------------------------------------

    public struct StageInfo
    {
        public int Index;
        public string Name;
        public float Duration;

        public StageInfo(int index, string name, float duration)
        {
            Index = index;
            Name = name;
            Duration = duration;
        }
    }

    // Fired on ALL machines (server + clients)
    public static System.Action<StageInfo> OnStageStarted;
    public static System.Action<StageInfo> OnStageEnded;

    // Syncs the currently active stage to all clients
    public NetworkVariable<int> CurrentStageIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // --------------------------------------------------
    // STAGE DATA
    // --------------------------------------------------

    [System.Serializable]
    private sealed class ZombieEntry
    {
        [SerializeField] private NetworkObject prefab;
        [SerializeField, Range(0f, 100f)] private float weight = 1f;

        public NetworkObject Prefab => prefab;
        public float Weight => weight;
    }

    [System.Serializable]
    private sealed class SpawnStage
    {
        [SerializeField] private string stageName;
        [SerializeField] private float duration = 60f;
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private List<ZombieEntry> zombies = new();

        public string StageName => stageName;
        public float Duration => duration;
        public float SpawnInterval => spawnInterval;
        public List<ZombieEntry> Zombies => zombies;
    }

    [Header("Stages")]
    [SerializeField] private List<SpawnStage> stages = new();

    [Header("Spawn Area")]
    [SerializeField] private bool useSpawnRadius = true;
    [SerializeField] private float spawnRadius = 5f;

    [Header("Pooling")]
    [SerializeField] private int prewarmPerType = 10;

    [Header("Scaling")]
    [SerializeField] private float minSpawnInterval = 0.35f;

    // prefab → pooled instances
    private readonly Dictionary<NetworkObject, Queue<PooledZombie>> _pool =
        new Dictionary<NetworkObject, Queue<PooledZombie>>();

    private Coroutine _stageRoutine;

    // ForceStage support
    private int _forcedStageIndex = -1;
    private bool _hasForcedStage;

    // --------------------------------------------------
    // NETWORK START
    // --------------------------------------------------

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        BuildPools();

        if (_stageRoutine != null)
            StopCoroutine(_stageRoutine);

        _stageRoutine = StartCoroutine(RunStages());
    }

    // --------------------------------------------------
    // STAGE CONTROL
    // --------------------------------------------------

    public void ForceStage(int stageIndex)
    {
        if (!IsServer) return;

        if (stageIndex < 0 || stageIndex >= stages.Count)
        {
            Debug.LogWarning($"[ZombieSpawner] ForceStage ignored. Invalid stage index: {stageIndex}");
            return;
        }

        _forcedStageIndex = stageIndex;
        _hasForcedStage = true;

        if (_stageRoutine != null)
            StopCoroutine(_stageRoutine);

        _stageRoutine = StartCoroutine(RunStages());
    }

    // --------------------------------------------------
    // POOL CREATION
    // --------------------------------------------------

    private void BuildPools()
    {
        _pool.Clear();

        for (int s = 0; s < stages.Count; s++)
        {
            var stage = stages[s];
            if (stage == null) continue;

            foreach (var entry in stage.Zombies)
            {
                if (entry == null) continue;
                var prefab = entry.Prefab;
                if (prefab == null) continue;

                if (_pool.ContainsKey(prefab))
                    continue;

                _pool.Add(prefab, new Queue<PooledZombie>());

                for (int i = 0; i < prewarmPerType; i++)
                {
                    // Instantiate ONLY — do NOT spawn yet
                    var obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);

                    var pooled = obj.GetComponent<PooledZombie>();
                    if (pooled == null)
                    {
                        Debug.LogError($"[ZombieSpawner] Prefab '{prefab.name}' is missing PooledZombie.");
                        Destroy(obj.gameObject);
                        continue;
                    }

                    pooled.SetPool(this, prefab);

                    obj.gameObject.SetActive(false);
                    _pool[prefab].Enqueue(pooled);
                }
            }
        }

        Debug.Log("[ZombieSpawner] Zombie pools prewarmed (not network spawned).");
    }

    // --------------------------------------------------
    // MAIN STAGE LOOP
    // --------------------------------------------------

    private IEnumerator RunStages()
    {
        int startIndex = 0;

        if (_hasForcedStage)
        {
            startIndex = _forcedStageIndex;
            _hasForcedStage = false;
        }

        if (stages.Count == 0)
        {
            Debug.LogWarning("[ZombieSpawner] No stages configured.");
            yield break;
        }

        for (int i = startIndex; i < stages.Count; i++)
        {
            var stage = stages[i];
            if (stage == null) continue;

            var info = new StageInfo(i, stage.StageName, stage.Duration);

            // Notify everyone the stage started
            CurrentStageIndex.Value = info.Index;
            BroadcastStageStartedClientRpc(info.Index, info.Name, info.Duration);

            Debug.Log($"[ZombieSpawner] Starting Stage: {stage.StageName}");

            float timer = 0f;

            while (timer < stage.Duration)
            {
                SpawnZombie(stage);

                float scaledInterval = GetScaledInterval(stage.SpawnInterval);
                yield return new WaitForSeconds(scaledInterval);
                timer += scaledInterval;
            }

            // Notify everyone the stage ended
            BroadcastStageEndedClientRpc(info.Index, info.Name, info.Duration);

            Debug.Log($"[ZombieSpawner] Finished Stage: {stage.StageName}");
        }

        Debug.Log("[ZombieSpawner] All stages complete.");
    }

    // --------------------------------------------------
    // PLAYER-COUNT SCALING
    // --------------------------------------------------

    private float GetScaledInterval(float baseInterval)
    {
        int players = 0;

        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (c.PlayerObject != null)
                players++;
        }

        players = Mathf.Max(players, 1);

        float alpha = 0.25f; //adjust the power of scaling
        float scaled = baseInterval / Mathf.Pow(players, alpha);
        return Mathf.Max(scaled, minSpawnInterval);
    }

    // --------------------------------------------------
    // SPAWNING
    // --------------------------------------------------

    private void SpawnZombie(SpawnStage stage)
    {
        if (stage.Zombies == null || stage.Zombies.Count == 0) return;

        NetworkObject prefab = ChooseWeighted(stage.Zombies);
        if (prefab == null) return;

        // 1️⃣ Get pooled instance (NOT spawned yet)
        PooledZombie zombie = GetFromPool(prefab);
        if (zombie == null) return;

        // 2️⃣ Calculate spawn position first
        Vector3 pos = GetSpawnPosition();

        zombie.transform.SetPositionAndRotation(pos, Quaternion.identity);

        // 3️⃣ Warp NavMeshAgent BEFORE spawning (prevents path-from-origin bug)
        var agent = zombie.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            // Ensure agent is enabled first
            if (!agent.enabled)
                agent.enabled = true;

            // Warp attempts to place it immediately
            agent.Warp(pos);

            // Only reset if Unity confirms it's actually on the NavMesh
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = false;
            }
        }

        // 4️⃣ Reset gameplay state BEFORE clients ever see it
        var health = zombie.GetComponent<Health>();
        if (health != null)
            health.ResetForReuse();

        // 5️⃣ Activate locally
        zombie.gameObject.SetActive(true);

        // 6️⃣ NOW tell Netcode this object exists
        if (!zombie.NetworkObject.IsSpawned)
            zombie.NetworkObject.Spawn(true);
    }

    private Vector3 GetSpawnPosition()
    {
        if (!useSpawnRadius)
            return transform.position;

        Vector3 random = transform.position + Random.insideUnitSphere * spawnRadius;

        if (NavMesh.SamplePosition(random, out NavMeshHit hit, spawnRadius, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    // --------------------------------------------------
    // POOL OPERATIONS
    // --------------------------------------------------

    private PooledZombie GetFromPool(NetworkObject prefab)
    {
        if (!_pool.ContainsKey(prefab))
            _pool.Add(prefab, new Queue<PooledZombie>());

        PooledZombie zombie;

        if (_pool[prefab].Count > 0)
        {
            zombie = _pool[prefab].Dequeue();
        }
        else
        {
            var obj = Instantiate(prefab);
            zombie = obj.GetComponent<PooledZombie>();

            if (zombie == null)
            {
                Debug.LogError($"[ZombieSpawner] Spawned prefab '{prefab.name}' is missing PooledZombie.");
                Destroy(obj.gameObject);
                return null;
            }

            zombie.SetPool(this, prefab);
        }

        // IMPORTANT: do NOT SetActive and do NOT Spawn here.
        return zombie;
    }

    public void ReturnToPool(PooledZombie zombie, NetworkObject prefab)
    {
        if (zombie == null || prefab == null) return;

        var agent = zombie.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        if (zombie.NetworkObject != null && zombie.NetworkObject.IsSpawned)
            zombie.NetworkObject.Despawn(false); // keeps instance alive

            zombie.gameObject.SetActive(false);

        if (!_pool.ContainsKey(prefab))
            _pool.Add(prefab, new Queue<PooledZombie>());

        _pool[prefab].Enqueue(zombie);
    }

    // --------------------------------------------------
    // WEIGHTED RANDOM
    // --------------------------------------------------

    private NetworkObject ChooseWeighted(List<ZombieEntry> list)
    {
        float total = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.Prefab == null) continue;
            total += e.Weight;
        }

        if (total <= 0f)
            return null;

        float roll = Random.Range(0f, total);
        float sum = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || e.Prefab == null) continue;

            sum += e.Weight;
            if (roll <= sum)
                return e.Prefab;
        }

        return null;
    }

    // --------------------------------------------------
    // NETWORK BROADCAST
    // --------------------------------------------------

    [ClientRpc]
    private void BroadcastStageStartedClientRpc(int index, string stageName, float duration)
    {
        StageInfo info = new StageInfo(index, stageName, duration);
        OnStageStarted?.Invoke(info);
    }

    [ClientRpc]
    private void BroadcastStageEndedClientRpc(int index, string stageName, float duration)
    {
        StageInfo info = new StageInfo(index, stageName, duration);
        OnStageEnded?.Invoke(info);
    }
}
