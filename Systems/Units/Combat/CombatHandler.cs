using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health), typeof(NavMeshAgent), typeof(FactionMember))]
public class CombatHandler : MonoBehaviour
{
    [Header("Combat Settings")]
    [Tooltip("Radius in dem diese Einheit nach Gegnern sucht.")]
    public float detectionRadius = 8f;
    [Tooltip("Reichweite für den Angriff.")]
    public float attackRange = 2f;
    [Tooltip("Cooldown zwischen Angriffen.")]
    public float attackCooldown = 1f;
    [Tooltip("Schaden pro Angriff.")]
    public int damagePerAttack = 1;
    [Tooltip("Distanz, ab der das Ziel wieder vergessen wird.")]
    public float loseTargetDistance = 15f;

    private NavMeshAgent agent;
    private Health health;
    private FactionMember factionMember;

    private Transform currentTarget;
    private Vector3 defendPosition;
    private float lastAttackTime;
    private Coroutine defendRoutine;
    private Coroutine aggroRoutine;

    public delegate void CombatEvent(Transform target);
    public event CombatEvent OnUnderAttack;
    public event CombatEvent OnAcquireTarget;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        factionMember = GetComponent<FactionMember>();
        health.OnDamagedBy += HandleDamage;
    }

    void Start()
    {
        aggroRoutine = StartCoroutine(AggroLoop());
    }

    void OnDestroy()
    {
        health.OnDamagedBy -= HandleDamage;
        if (defendRoutine != null) StopCoroutine(defendRoutine);
        if (aggroRoutine != null) StopCoroutine(aggroRoutine);
    }

    private void HandleDamage(GameObject source)
    {
        var otherFaction = source.GetComponent<FactionMember>()?.faction;
        if (otherFaction == null || otherFaction == factionMember.faction)
            return;

        currentTarget = source.transform;
        Vector3 dirOffset = (transform.position - currentTarget.position).normalized;
        defendPosition = currentTarget.position + dirOffset * attackRange;

        OnUnderAttack?.Invoke(currentTarget);
        if (defendRoutine != null) StopCoroutine(defendRoutine);
        defendRoutine = StartCoroutine(DefendLoop());
    }

    private IEnumerator DefendLoop()
    {
        while (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            var tgtHealth = currentTarget.GetComponent<Health>();
            if (tgtHealth == null || tgtHealth.CurrentHealth <= 0 || dist > loseTargetDistance)
                break;

            if (dist > attackRange)
                agent.SetDestination(defendPosition);
            else
            {
                agent.ResetPath();
                Vector3 dir = (currentTarget.position - transform.position).normalized;
                dir.y = 0f;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    Time.deltaTime * 8f);

                if (Time.time - lastAttackTime >= attackCooldown)
                {
                    tgtHealth.TakeDamage(damagePerAttack, gameObject);
                    lastAttackTime = Time.time;
                }
            }
            yield return null;
        }
        currentTarget = null;
    }

    private IEnumerator AggroLoop()
    {
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            if (currentTarget == null && health.CurrentHealth > 0)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius);
                Transform nearest = null;
                float bestDist = float.MaxValue;

                foreach (var hit in hits)
                {
                    var fm = hit.GetComponent<FactionMember>();
                    if (fm != null && fm.faction != factionMember.faction)
                    {
                        float d = Vector3.Distance(transform.position, hit.transform.position);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            nearest = hit.transform;
                        }
                    }
                }

                if (nearest != null)
                {
                    currentTarget = nearest;
                    Vector3 dirOffset = (transform.position - currentTarget.position).normalized;
                    defendPosition = currentTarget.position + dirOffset * attackRange;

                    OnAcquireTarget?.Invoke(currentTarget);
                    if (defendRoutine != null) StopCoroutine(defendRoutine);
                    defendRoutine = StartCoroutine(DefendLoop());
                }
            }
            yield return wait;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
