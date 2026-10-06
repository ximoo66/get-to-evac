using UnityEngine;
using UnityEngine.AI;

public class State
{
    public enum STATE { WANDER, PURSUE, ATTACK, STUNNED }
    public enum EVENT { ENTER, UPDATE, EXIT }

    public STATE name;
    protected EVENT stage;

    protected AI npc;
    protected Animator anim;
    protected Transform player;
    protected NavMeshAgent agent;

    protected State nextState;
    protected float wanderTimer;

    public State(AI _npc, NavMeshAgent _agent, Animator _anim, Transform _player)
    {
        npc = _npc;
        agent = _agent;
        anim = _anim;
        player = _player;
        stage = EVENT.ENTER;
    }

    public virtual void Enter() { stage = EVENT.UPDATE; }
    public virtual void Update() { }
    public virtual void Exit() { stage = EVENT.EXIT; }

    public State Process()
    {
        if (stage == EVENT.ENTER) Enter();
        if (stage == EVENT.UPDATE) Update();
        if (stage == EVENT.EXIT)
        {
            Exit();
            return nextState;
        }
        return this;
    }

    protected void SetAnimation(bool wander, bool run, bool attack, bool stunned)
    {
        anim.SetBool("IsWandering", wander);
        anim.SetBool("IsRunning", run);
        anim.SetBool("IsAttacking", attack);
        anim.SetBool("IsStunned", stunned);
    }

    protected bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 dir = player.position - npc.transform.position;
        float angle = Vector3.Angle(dir, npc.transform.forward);

        return dir.magnitude < npc.visionDistance && angle < npc.visionAngle;
    }

    protected bool CanAttackPlayer()
    {
        if (player == null) return false;

        return Vector3.Distance(npc.transform.position, player.position)
        < npc.attackDistance;
    }

    protected Vector3 RandomNavSphere(Vector3 origin, float dist)
    {
        Vector3 rand = Random.insideUnitSphere * dist + origin;
        NavMesh.SamplePosition(rand, out NavMeshHit hit, dist, NavMesh.AllAreas);
        return hit.position;
    }
}

///////////////////////////////////////////////////////////////
/// WANDER
///////////////////////////////////////////////////////////////
public class Wander : State
{
    public Wander(AI _npc, NavMeshAgent _agent, Animator _anim, Transform _player)
    : base(_npc, _agent, _anim, _player)
    {
        name = STATE.WANDER;
        agent.speed = npc.wanderSpeed;
        agent.isStopped = false;
    }

    public override void Enter()
    {
        SetAnimation(true, false, false, false);
        wanderTimer = 0f;
        base.Enter();
    }

    public override void Update()
    {
        wanderTimer += Time.deltaTime;

        if (wanderTimer >= npc.wanderInterval || !agent.hasPath)
        {
            agent.SetDestination(RandomNavSphere(npc.transform.position, npc.wanderRadius));
            wanderTimer = 0f;
        }

        if (CanSeePlayer())
        {
            nextState = new Pursue(npc, agent, anim, player);
            stage = EVENT.EXIT;
        }
    }
}

///////////////////////////////////////////////////////////////
/// PURSUE
///////////////////////////////////////////////////////////////
public class Pursue : State
{
    float baseSpeed;

    public Pursue(AI _npc, NavMeshAgent _agent, Animator _anim, Transform _player)
    : base(_npc, _agent, _anim, _player)
    {
        name = STATE.PURSUE;
        baseSpeed = npc.wanderSpeed;
        agent.speed = baseSpeed * npc.pursueSpeedMultiplier;
    }

    public override void Enter()
    {
        SetAnimation(false, true, false, false);
        base.Enter();
    }

    public override void Update()
    {
        agent.SetDestination(player.position);

        if (CanAttackPlayer())
        {
            nextState = new Attack(npc, agent, anim, player);
            stage = EVENT.EXIT;
        }
        else if (!CanSeePlayer())
        {
            nextState = new Wander(npc, agent, anim, player);
            stage = EVENT.EXIT;
        }
    }

    public override void Exit()
    {
        agent.speed = baseSpeed;
        base.Exit();
    }
}

///////////////////////////////////////////////////////////////
/// ATTACK
///////////////////////////////////////////////////////////////
public class Attack : State
{
    float attackTimer;
    float rotationSpeed = 6f;

    public Attack(AI _npc, NavMeshAgent _agent, Animator _anim, Transform _player)
    : base(_npc, _agent, _anim, _player)
    {
        name = STATE.ATTACK;
    }

    public override void Enter()
    {
        // Stop moving so we don't slide past the player
        agent.isStopped = true;
        agent.ResetPath();

        attackTimer = 0f;

        SetAnimation(false, false, true, false);

        base.Enter();
    }

    public override void Update()
    {
        if (player == null)
        {
            nextState = new Wander(npc, agent, anim, player);
            stage = EVENT.EXIT;
            return;
        }

        // Always face the target while attacking
        Vector3 dir = player.position - npc.transform.position;
        dir.y = 0;

        npc.transform.rotation = Quaternion.Slerp(
            npc.transform.rotation,
            Quaternion.LookRotation(dir),
                                                  Time.deltaTime * rotationSpeed);

        attackTimer += Time.deltaTime;
        Debug.Log(attackTimer);
        // This is the key: repeat attack instead of leaving state
        if (attackTimer >= npc.attackCooldown)
        {
            attackTimer = 0f;

            // Trigger the actual damage event here
            DealDamage();
        }

        // Leave ONLY if player escaped range
        if (!CanAttackPlayer())
        {
            nextState = new Pursue(npc, agent, anim, player);
            stage = EVENT.EXIT;
        }
    }

    void DealDamage()
    {
        // Replace with your real health system
        var health = player.GetComponent<Health>();
        if (health != null)
            health.TakeDamage(10);
            Debug.Log("wtf");
    }

    public override void Exit()
    {
        agent.isStopped = false;
        base.Exit();
    }
}

///////////////////////////////////////////////////////////////
/// STUNNED
///////////////////////////////////////////////////////////////
public class Stunned : State
{
    float timer;

    public Stunned(AI _npc, NavMeshAgent _agent, Animator _anim, Transform _player)
    : base(_npc, _agent, _anim, _player)
    {
        name = STATE.STUNNED;
    }

    public override void Enter()
    {
        SetAnimation(false, false, false, true);
        agent.isStopped = true;
        timer = 0f;
        base.Enter();
    }

    public override void Update()
    {
        timer += Time.deltaTime;

        if (timer >= npc.stunDuration)
        {
            nextState = new Wander(npc, agent, anim, player);
            stage = EVENT.EXIT;
        }
    }

    public override void Exit()
    {
        agent.isStopped = false;
        base.Exit();
    }
}
