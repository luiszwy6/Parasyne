using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [Min(0f)] public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Hit Escalation")]
    public bool escalateCheckThenAlert = true;

    [Tooltip("If no hit happens within this time, hit count resets to 0.")]
    [Min(0f)] public float hitMemoryDuration = 4f;

    [Tooltip("On the first hit, turn toward player for this many seconds before entering Check.")]
    [Min(0f)] public float turnBeforeCheckSeconds = 0.25f;

    [Tooltip("Turn speed while reacting to hit.")]
    [Min(0f)] public float turnSpeed = 14f;
    [Tooltip("Delay before starting turn to player")]
    [Min(0f)] public float turnStartDelay = 0.08f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isDeadParam = "IsDead";

    public bool IsDead { get; private set; }

    private Enemy enemy;
    private int isDeadHash;

    private int hitCountForEscalation = 0;
    private float hitMemoryTimer = 0f;

    private Coroutine turnToPlayerRoutine;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        enemy = GetComponent<Enemy>();
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        currentHealth = Mathf.Clamp(currentHealth, 0f, Mathf.Max(0f, maxHealth));
        isDeadHash = Animator.StringToHash(isDeadParam);

        if (currentHealth <= 0f)
            Die();
    }

    private void Update()
    {
        if (!escalateCheckThenAlert) return;
        if (hitMemoryDuration <= 0f) return;

        if (hitMemoryTimer > 0f)
        {
            hitMemoryTimer -= Time.deltaTime;
            if (hitMemoryTimer <= 0f)
            {
                hitMemoryTimer = 0f;
                hitCountForEscalation = 0;

                if (turnToPlayerRoutine != null)
                {
                    StopCoroutine(turnToPlayerRoutine);
                    turnToPlayerRoutine = null;
                }
            }
        }
    }

    public void TakeDamage(float dmg)
    {
        if (IsDead) return;
        if (dmg <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - dmg);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        if (escalateCheckThenAlert)
            EscalateOnHit();
    }

    public void Kill()
    {
        if (IsDead) return;
        currentHealth = 0f;
        Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        if (turnToPlayerRoutine != null)
        {
            StopCoroutine(turnToPlayerRoutine);
            turnToPlayerRoutine = null;
        }

        if (animator != null)
            animator.SetBool(isDeadHash, true);

        if (enemy != null && enemy.stateMachine != null)
            enemy.stateMachine.ChangeState(new EnemyDeadState(enemy));
    }

    private void EscalateOnHit()
    {
        if (enemy == null || enemy.stateMachine == null) return;
        if (enemy.player == null) return;

        if (enemy.IsDeadPlaceholder())
            return;

        EnemyState cs = enemy.stateMachine.currentState;

        // Do not override these states
        if (cs is EnemyDeadState) return;
        if (cs is EnemyAttackState) return;
        if (cs is EnemySurpriseState) return;

        // refresh memory window
        hitMemoryTimer = Mathf.Max(0f, hitMemoryDuration);

        hitCountForEscalation++;

        enemy.lastAwareTriggerWasSound = false;
        enemy.SetLastSeenPosition(enemy.player.position);

        // second+ hit within window -> Alert now
        if (hitCountForEscalation >= 2)
        {
            if (turnToPlayerRoutine != null)
            {
                StopCoroutine(turnToPlayerRoutine);
                turnToPlayerRoutine = null;
            }

            if (!(cs is EnemyAlertState))
                enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));

            return;
        }

        // first hit -> turn then Check (unless already Check/Alert)
        if (cs is EnemyCheckState || cs is EnemyAlertState)
            return;

        if (turnToPlayerRoutine != null)
            StopCoroutine(turnToPlayerRoutine);

        turnToPlayerRoutine = StartCoroutine(TurnToPlayerThenCheck());
    }

     private IEnumerator TurnToPlayerThenCheck()
    {
     float delay = Mathf.Max(0f, turnStartDelay);
      if (delay > 0f)
      {
            float d = delay;
            while (d > 0f)
           {
                if (IsDead) yield break;
                if (enemy == null || enemy.player == null || enemy.stateMachine == null) yield break;

            // second hit escalates immediately
                  if (hitCountForEscalation >= 2) yield break;

            EnemyState cs = enemy.stateMachine.currentState;
            if (cs is EnemyDeadState || cs is EnemyAttackState || cs is EnemySurpriseState || cs is EnemyAlertState)
                yield break;

            d -= Time.deltaTime;
            yield return null;
        }
    }

    float t = Mathf.Max(0f, turnBeforeCheckSeconds);

    while (t > 0f)
    {
        if (IsDead) yield break;
        if (enemy == null || enemy.player == null || enemy.stateMachine == null) yield break;

        if (hitCountForEscalation >= 2) yield break;

        EnemyState cs = enemy.stateMachine.currentState;
        if (cs is EnemyDeadState || cs is EnemyAttackState || cs is EnemySurpriseState || cs is EnemyAlertState)
            yield break;

        Vector3 dir = enemy.player.position - enemy.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            float k = 1f - Mathf.Exp(-Mathf.Max(0f, turnSpeed) * Time.deltaTime);
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, target, k);
        }

        t -= Time.deltaTime;
        yield return null;
    }

    if (!IsDead && enemy != null && enemy.stateMachine != null)
    {
        EnemyState cs = enemy.stateMachine.currentState;
        if (!(cs is EnemyCheckState) && !(cs is EnemyAlertState) && hitCountForEscalation == 1)
            enemy.stateMachine.ChangeState(new EnemyCheckState(enemy));
    }

    turnToPlayerRoutine = null;
}
}