using UnityEngine;
using UnityEngine.AI;

public class EnemyAttackState : EnemyState
{
    private EnemyAttackSettings attackSettings;
    private EnemyPartBreaks partBreaks;

    private float attackTimer = 0f;
    private float cachedAttackDuration = 0.1f;
    private bool cachedAllowMoveWhileAttacking = false;
    private bool cachedAllowRotateWhileAttacking = false;
    private float cachedAttackTurnSpeed = 0f;
    private float cachedMaxAttackTurnCorrectionAngle = 0f;

    private bool startedHitboxPlaceholder = false;

    private bool cachedAgentUpdateRotation = true;
    private bool cachedAgentIsStopped = false;
    private bool hasCachedAgentState = false;

    private Quaternion attackStartRotation;
    private bool hasAttackStartRotation = false;

    public EnemyAttackState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        attackTimer = 0f;
        startedHitboxPlaceholder = false;

        attackSettings = enemy != null ? enemy.GetComponent<EnemyAttackSettings>() : null;
        partBreaks = enemy != null ? enemy.GetComponent<EnemyPartBreaks>() : null;

        enemy.SetDebugColor(new Color(1f, 0.2f, 0.8f));
        Debug.Log($"{enemy.name} -> ATTACK");

        if (attackSettings == null)
        {
            Debug.LogWarning($"[{enemy.name}] EnemyAttackSettings missing. Back to Alert.");
            enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));
            return;
        }

        if (!attackSettings.CanAttackInCurrentState())
        {
            enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));
            return;
        }

        attackSettings.MarkAttackStarted();

        cachedAttackDuration = attackSettings.GetFixedAttackDuration();
        cachedAllowMoveWhileAttacking = attackSettings.AllowMoveWhileAttacking();
        cachedAllowRotateWhileAttacking = attackSettings.AllowRotateWhileAttacking();
        cachedAttackTurnSpeed = attackSettings.GetAttackTurnSpeed();
        cachedMaxAttackTurnCorrectionAngle = attackSettings.GetMaxAttackTurnCorrectionAngle();

        attackStartRotation = enemy.transform.rotation;
        hasAttackStartRotation = true;

        if (enemy.agent != null)
        {
            cachedAgentUpdateRotation = enemy.agent.updateRotation;
            cachedAgentIsStopped = enemy.agent.isStopped;
            hasCachedAgentState = true;

            enemy.agent.isStopped = !cachedAllowMoveWhileAttacking;

            // During attack, rotation is handled manually so angle cap and speed limit work predictably.
            enemy.agent.updateRotation = false;

            if (!cachedAllowMoveWhileAttacking)
                enemy.agent.ResetPath();
        }

        attackSettings.ApplyAttackAnimatorParameters();

        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsRunning", false);
        }

        attackSettings.BeginAttackHitboxPlaceholder();
        startedHitboxPlaceholder = true;
    }

    public override void Update()
    {
        if (attackSettings == null)
            return;

        if (cachedAllowRotateWhileAttacking && enemy.player != null)
        {
            ApplyLimitedAttackFacingCorrection(enemy.player);
        }

        if (enemy.agent != null)
        {
            if (!cachedAllowMoveWhileAttacking)
            {
                enemy.agent.isStopped = true;
                enemy.agent.ResetPath();
            }
            else
            {
                enemy.agent.isStopped = false;
            }

            enemy.agent.updateRotation = false;
        }

        attackTimer += Time.deltaTime;

        if (attackTimer >= cachedAttackDuration)
        {
            enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));
            return;
        }
    }

    public override void Exit()
    {
        if (attackSettings != null && startedHitboxPlaceholder)
        {
            attackSettings.EndAttackHitboxPlaceholder();
        }

        if (attackSettings != null)
        {
            attackSettings.ClearAttackAnimatorState();
        }
        else if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsAttack", false);
        }

        if (enemy.animator != null)
        {
            enemy.animator.ResetTrigger("Attacking");
        }

        if (enemy.agent != null)
        {
            if (hasCachedAgentState)
            {
                enemy.agent.isStopped = cachedAgentIsStopped;
                enemy.agent.updateRotation = cachedAgentUpdateRotation;
            }
            else
            {
                enemy.agent.isStopped = false;
                enemy.agent.updateRotation = true;
            }
        }
    }

    private void ApplyLimitedAttackFacingCorrection(Transform target)
    {
        if (target == null || !hasAttackStartRotation) return;

        Vector3 toTarget = target.position - enemy.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Quaternion desiredWorldRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

        Vector3 attackStartForward = attackStartRotation * Vector3.forward;
        attackStartForward.y = 0f;
        if (attackStartForward.sqrMagnitude < 0.0001f) return;
        attackStartForward.Normalize();

        float signedFromStart = Vector3.SignedAngle(
            attackStartForward,
            toTarget.normalized,
            Vector3.up
        );

        float clampedSigned = Mathf.Clamp(
            signedFromStart,
            -cachedMaxAttackTurnCorrectionAngle,
            cachedMaxAttackTurnCorrectionAngle
        );

        Quaternion clampedTargetRot =
            Quaternion.AngleAxis(clampedSigned, Vector3.up) * attackStartRotation;

        enemy.transform.rotation = Quaternion.RotateTowards(
            enemy.transform.rotation,
            clampedTargetRot,
            Mathf.Max(0f, cachedAttackTurnSpeed) * Time.deltaTime
        );
    }
}