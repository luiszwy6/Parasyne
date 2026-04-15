using UnityEngine;
using UnityEngine.AI;

public class EnemyStunState : EnemyState
{
    public enum StunKind
    {
        Short = 0,
        Long = 1
    }

    private readonly StunKind stunKind;
    private readonly float duration;
    private readonly EnemyState returnState;

    private float timer;

    private Vector3 knockbackDir;
    private float knockbackDistance;
    private float knockbackDuration;
    private float knockbackStartDelay;
    private float knockbackMoved;

    public EnemyStunState(Enemy enemy, StunKind stunKind, float duration, EnemyState returnState)
        : base(enemy)
    {
        this.stunKind = stunKind;
        this.duration = Mathf.Max(0f, duration);
        this.returnState = returnState;
    }

    public override void Enter()
    {
        timer = 0f;
        knockbackMoved = 0f;

        enemy.SetDebugColor(new Color(1f, 0.55f, 0f)); // orange
        Debug.Log($"{enemy.name} -> STUN ({stunKind}, {duration:F2}s)");

        if (enemy.agent != null)
        {
            enemy.agent.isStopped = true;
            enemy.agent.ResetPath();
            enemy.agent.updateRotation = false;

            // Prevent agent from fighting CC knockback movement.
            enemy.agent.updatePosition = false;
        }

        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRunning", false);
            enemy.animator.SetFloat("Speed", 0f);

            // Prevent awareness transitions from stealing stun animation.
            enemy.animator.SetBool("IsAware", false);
            enemy.animator.SetBool("IsCheck", false);
            enemy.animator.SetBool("IsAlert", false);

            enemy.animator.ResetTrigger("IsShortStun");
            enemy.animator.ResetTrigger("IsLongStun");

            if (stunKind == StunKind.Short)
                enemy.animator.SetTrigger("IsShortStun");
            else
                enemy.animator.SetTrigger("IsLongStun");
        }

        // Fallback knockback direction: backward from current facing
        knockbackDir = -enemy.transform.forward;
        knockbackDir.y = 0f;
        if (knockbackDir.sqrMagnitude > 0.0001f)
            knockbackDir.Normalize();
        else
            knockbackDir = Vector3.back;

        if (stunKind == StunKind.Long)
        {
            knockbackDistance = Mathf.Max(0f, enemy.longStunKnockbackDistance);
            knockbackDuration = Mathf.Max(0.001f, enemy.longStunKnockbackDuration);
            knockbackStartDelay = Mathf.Max(0f, enemy.longStunKnockbackStartDelay);
        }
        else
        {
            knockbackDistance = Mathf.Max(0f, enemy.shortStunKnockbackDistance);
            knockbackDuration = Mathf.Max(0.001f, enemy.shortStunKnockbackDuration);
            knockbackStartDelay = Mathf.Max(0f, enemy.shortStunKnockbackStartDelay);
        }

        if (enemy.animator != null)
    {
    }
    }

    public override void Update()
    {
        if (enemy.IsDeadPlaceholder())
        {
            return;
        }

        timer += Time.deltaTime;

        // Delay knockback slightly so animation pose starts first
        float kbTimer = timer - knockbackStartDelay;

        if (kbTimer > 0f && kbTimer <= knockbackDuration && knockbackDistance > 0f)
        {
            float t = Mathf.Clamp01(kbTimer / knockbackDuration);
            float targetMovedByNow = Mathf.Lerp(0f, knockbackDistance, t);
            float deltaMove = targetMovedByNow - knockbackMoved;

            if (deltaMove > 0f)
            {
                Vector3 delta = knockbackDir * deltaMove;

                if (enemy.cc != null)
                {
                    enemy.cc.Move(delta);
                }
                else
                {
                    // Fallback if CC ref is missing
                    enemy.transform.position += delta;
                }

                knockbackMoved += deltaMove;
            }
        }

        if (timer < duration) return;

        ClearStunAnimatorTriggers();

        if (returnState != null)
        {
            enemy.stateMachine.ChangeState(returnState);
        }
        else
        {
            enemy.stateMachine.ChangeState(new EnemyPatrolState(enemy));
        }
    }

    public override void Exit()
    {
        ClearStunAnimatorTriggers();

        if (enemy.agent != null)
        {
            enemy.agent.updatePosition = true;
            enemy.agent.isStopped = false;

            if (enemy.agent.isOnNavMesh)
                enemy.agent.Warp(enemy.transform.position);
        }
        
        if (enemy.animator != null)
        {
        }
    }

    private void ClearStunAnimatorTriggers()
    {
        if (enemy.animator == null) return;

        enemy.animator.ResetTrigger("IsShortStun");
        enemy.animator.ResetTrigger("IsLongStun");
    }
}