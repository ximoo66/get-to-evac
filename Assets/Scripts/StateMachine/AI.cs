using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class AI : NetworkBehaviour
{
    private NavMeshAgent agent;
    private Animator anim;

    public Transform player;
    private State currentState;

    private readonly List<GameObject> players = new List<GameObject>();
    private float retargetTimer = 0f;

    [Header("Zombie Settings")]

    [SerializeField] public float wanderSpeed = 1.3f;
    [SerializeField] public float pursueSpeedMultiplier = 2.2f;

    [SerializeField] public float visionDistance = 12f;
    [SerializeField] public float visionAngle = 120f;

    [SerializeField] public float attackDistance = 2.2f;
    [SerializeField] public float attackCooldown = 1.2f;

    [SerializeField] public float wanderRadius = 8f;
    [SerializeField] public float wanderInterval = 3f;

    [SerializeField] public float stunDuration = 1.2f;

    private const float retargetInterval = 2f;

    void Awake()
    {
        // Always cache components in Awake for Netcode safety
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        player = FindNextPlayer();
        currentState = new Wander(this, agent, anim, player);
    }

    void Update()
    {
        if (!IsServer || currentState == null) return;

        currentState = currentState.Process();

        // periodically re-evaluate closest player
        retargetTimer += Time.deltaTime;
        if (retargetTimer >= retargetInterval)
        {
            retargetTimer = 0f;

            Transform newPlayer = FindNextPlayer();
            if (newPlayer != player)
            {
                player = newPlayer;
                RebuildState();
            }
        }
    }

    private Transform FindNextPlayer()
    {
        players.Clear();
        players.AddRange(GameObject.FindGameObjectsWithTag("Player"));

        float closestDist = float.MaxValue;
        Transform closest = null;

        foreach (var obj in players)
        {
            float dist = Vector3.Distance(obj.transform.position, transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = obj.transform;
            }
        }

        return closest;
    }

    private void RebuildState()
    {
        switch (currentState.name)
        {
            case State.STATE.WANDER:
                currentState = new Wander(this, agent, anim, player);
                break;
            case State.STATE.PURSUE:
                currentState = new Pursue(this, agent, anim, player);
                break;
            case State.STATE.ATTACK:
                currentState = new Attack(this, agent, anim, player);
                break;
            case State.STATE.STUNNED:
                currentState = new Stunned(this, agent, anim, player);
                break;
        }
    }

    // Called by weapon/damage system
    public void ApplyStun()
    {
        if (!IsServer) return;
        currentState = new Stunned(this, agent, anim, player);
    }
}
