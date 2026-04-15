using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAttackSettings : MonoBehaviour
{
    public enum AttackPoseType
    {
        Normal,
        Hurt,
        Crawl
    }

    [System.Serializable]
    public class AttackProfile
    {
        [Header("Decision / Hit Range")]
        [Tooltip("How close the player must be for AI to start an attack.")]
        public float attackEnterRange = 1.8f;

        [Tooltip("Actual hit range checked for future hitbox/hurtbox validation.")]
        public float attackHitRange = 1.4f;

        [Tooltip("Target must be within this angle to attack (degrees).")]
        [Range(0f, 180f)] public float attackAngle = 80f;

        [Header("Timing")]
        [Tooltip("Minimum time between attack starts.")]
        public float attackCooldown = 1.2f;

        [Tooltip("Total attack-state duration. During this time IsAttack stays true.")]
        public float fixedAttackDuration = 0.7f;

        [Header("Damage")]
        public float baseDamage = 10f;

        [Header("Movement During Attack")]
        [Tooltip("If false, movement should be blocked for the whole attack duration.")]
        public bool allowMoveWhileAttacking = false;

        [Tooltip("Optional attack lunge distance (code-driven movement, not root motion).")]
        public float attackMoveDistance = 0f;

        [Tooltip("How long the attack move/lunge lasts.")]
        public float attackMoveDuration = 0.10f;

        [Tooltip("Delay before attack lunge starts, to sync with animation.")]
        public float attackMoveStartDelay = 0f;

        [Header("Rotation During Attack")]
        [Tooltip("If false, no facing correction is applied during the attack.")]
        public bool allowRotateWhileAttacking = false;

        [Tooltip("Maximum total yaw correction allowed during this attack, relative to facing at attack start.")]
        [Range(0f, 180f)] public float maxAttackTurnCorrectionAngle = 0f;

        [Tooltip("How fast the facing correction rotates during the attack (deg/s).")]
        public float attackTurnSpeed = 180f;

        [Header("Animation")]
        [Tooltip("Sets Animator bool IsAttack while this attack profile is active.")]
        public bool setIsAttack = true;
    }

    [Header("Refs")]
    public Enemy enemy;
    public EnemyPartBreaks partBreaks;

    [Header("Profiles")]
    public AttackProfile normalAttack = new AttackProfile
    {
        attackEnterRange = 1.8f,
        attackHitRange = 1.4f,
        attackAngle = 80f,
        attackCooldown = 1.2f,
        fixedAttackDuration = 0.70f,
        baseDamage = 12f,
        allowMoveWhileAttacking = false,
        attackMoveDistance = 0.20f,
        attackMoveDuration = 0.10f,
        attackMoveStartDelay = 0.05f,
        allowRotateWhileAttacking = true,
        maxAttackTurnCorrectionAngle = 20f,
        attackTurnSpeed = 240f,
        setIsAttack = true
    };

    public AttackProfile hurtAttack = new AttackProfile
    {
        attackEnterRange = 1.6f,
        attackHitRange = 1.25f,
        attackAngle = 75f,
        attackCooldown = 1.35f,
        fixedAttackDuration = 0.85f,
        baseDamage = 10f,
        allowMoveWhileAttacking = false,
        attackMoveDistance = 0.10f,
        attackMoveDuration = 0.10f,
        attackMoveStartDelay = 0.06f,
        allowRotateWhileAttacking = true,
        maxAttackTurnCorrectionAngle = 12f,
        attackTurnSpeed = 160f,
        setIsAttack = true
    };

    public AttackProfile crawlAttack = new AttackProfile
    {
        attackEnterRange = 1.2f,
        attackHitRange = 1.0f,
        attackAngle = 90f,
        attackCooldown = 1.6f,
        fixedAttackDuration = 1.00f,
        baseDamage = 8f,
        allowMoveWhileAttacking = false,
        attackMoveDistance = 0.15f,
        attackMoveDuration = 0.12f,
        attackMoveStartDelay = 0.08f,
        allowRotateWhileAttacking = true,
        maxAttackTurnCorrectionAngle = 8f,
        attackTurnSpeed = 120f,
        setIsAttack = true
    };

    [Header("Global Attack Rules")]
    [Tooltip("If true, target must be visible by line trace to start attack.")]
    public bool requireLineOfSight = false;

    [Tooltip("Layers that block attack LOS. Usually world/obstacles. Exclude enemy/player layers if needed.")]
    public LayerMask lineOfSightMask = ~0;

    [Tooltip("Height offset used for LOS checks.")]
    public float lineOfSightHeightOffset = 1.2f;

    [Header("State Restrictions")]
    public bool disallowAttackWhenStunned = true;
    public bool disallowAttackWhenTakenDown = true;
    public bool disallowAttackWhenDead = true;
    public bool disallowAttackDuringLegBreakReact = true;

    [Header("Debug")]
    public bool logAttackDecisions = false;

    private float _nextAllowedAttackTime = 0f;

    private void Reset()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (partBreaks == null) partBreaks = GetComponent<EnemyPartBreaks>();
    }

    private void Awake()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (partBreaks == null) partBreaks = GetComponent<EnemyPartBreaks>();
    }

    public AttackPoseType GetAttackPoseType()
    {
        bool isCrawl = false;
        bool useHurtAttack = false;

        if (partBreaks != null)
        {
            isCrawl = partBreaks.GetIsCrawl();
            useHurtAttack = partBreaks.ShouldUseHurtAttackAnimation();
        }
        else if (enemy != null && enemy.animator != null)
        {
            isCrawl = enemy.animator.GetBool("IsCrawl");
            bool hurtWalkL = enemy.animator.GetBool("IsHurtWalk_L");
            bool hurtWalkR = enemy.animator.GetBool("IsHurtWalk_R");
            bool hurtArm = enemy.animator.GetBool("IsHurtArm");
            useHurtAttack = hurtWalkL || hurtWalkR || hurtArm;
        }

        if (isCrawl) return AttackPoseType.Crawl;
        if (useHurtAttack) return AttackPoseType.Hurt;
        return AttackPoseType.Normal;
    }

    public AttackProfile GetActiveProfile()
    {
        switch (GetAttackPoseType())
        {
            case AttackPoseType.Crawl:
                return crawlAttack;
            case AttackPoseType.Hurt:
                return hurtAttack;
            default:
                return normalAttack;
        }
    }

    public bool CanStartAttackByCooldown()
    {
        return Time.time >= _nextAllowedAttackTime;
    }

    public void MarkAttackStarted()
    {
        AttackProfile p = GetActiveProfile();
        float cd = (p != null) ? Mathf.Max(0f, p.attackCooldown) : 0f;
        _nextAllowedAttackTime = Time.time + cd;
    }

    public float GetFixedAttackDuration()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Max(0.01f, p.fixedAttackDuration) : 0.01f;
    }

    public float GetAttackEnterRange()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Max(0f, p.attackEnterRange) : 0f;
    }

    public float GetAttackHitRange()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Max(0f, p.attackHitRange) : 0f;
    }

    public float GetAttackAngle()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Clamp(p.attackAngle, 0f, 180f) : 0f;
    }

    public bool IsTargetWithinEnterRange(Transform target)
    {
        if (target == null) return false;
        return GetHorizontalDistanceTo(target) <= GetAttackEnterRange();
    }

    public bool IsTargetWithinHitRange(Transform target)
    {
        if (target == null) return false;
        return GetHorizontalDistanceTo(target) <= GetAttackHitRange();
    }

    public bool IsTargetWithinAttackAngle(Transform target)
    {
        if (target == null) return false;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return true;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return true;

        float angle = Vector3.Angle(fwd.normalized, toTarget.normalized);
        return angle <= GetAttackAngle() * 0.5f;
    }

    public bool HasLineOfSightToTarget(Transform target)
    {
        if (target == null) return false;
        if (!requireLineOfSight) return true;

        Vector3 origin = transform.position + Vector3.up * lineOfSightHeightOffset;
        Vector3 dest = target.position + Vector3.up * lineOfSightHeightOffset;
        Vector3 dir = dest - origin;
        float dist = dir.magnitude;

        if (dist <= 0.001f) return true;

        bool blocked = Physics.Raycast(
            origin,
            dir.normalized,
            dist,
            lineOfSightMask,
            QueryTriggerInteraction.Ignore
        );

        return !blocked;
    }

    public bool CanAttackTargetNow(Transform target)
    {
        if (target == null) return false;
        if (!CanStartAttackByCooldown()) return false;
        if (!CanAttackInCurrentState()) return false;

        if (!IsTargetWithinEnterRange(target)) return false;
        if (!IsTargetWithinAttackAngle(target)) return false;
        if (!HasLineOfSightToTarget(target)) return false;

        if (logAttackDecisions)
        {
            Debug.Log($"[{name}] CanAttackTargetNow = true ({GetAttackPoseType()})");
        }

        return true;
    }

    public float GetBaseDamageForCurrentPose()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Max(0f, p.baseDamage) : 0f;
    }

    public float GetFinalDamage()
    {
        float dmg = GetBaseDamageForCurrentPose();

        if (partBreaks != null)
        {
            dmg *= Mathf.Max(0f, partBreaks.GetAttackDamageMultiplierFromArms());
        }

        return Mathf.Max(0f, dmg);
    }

    public bool AllowMoveWhileAttacking()
    {
        AttackProfile p = GetActiveProfile();
        return p != null && p.allowMoveWhileAttacking;
    }

    public bool AllowRotateWhileAttacking()
    {
        AttackProfile p = GetActiveProfile();
        return p != null && p.allowRotateWhileAttacking;
    }

    public float GetAttackTurnSpeed()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Max(0f, p.attackTurnSpeed) : 0f;
    }

    public float GetMaxAttackTurnCorrectionAngle()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Clamp(p.maxAttackTurnCorrectionAngle, 0f, 180f) : 0f;
    }

    public float GetAttackMoveDistance()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Max(0f, p.attackMoveDistance) : 0f;
    }

    public float GetAttackMoveDuration()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Max(0.001f, p.attackMoveDuration) : 0.001f;
    }

    public float GetAttackMoveStartDelay()
    {
        AttackProfile p = GetActiveProfile();
        return p != null ? Mathf.Max(0f, p.attackMoveStartDelay) : 0f;
    }

    public void BeginAttackHitboxPlaceholder()
    {
        Debug.Log($"[{name}] Begin attack hitbox (placeholder)");
    }

    public void EndAttackHitboxPlaceholder()
    {
        Debug.Log($"[{name}] End attack hitbox (placeholder)");
    }

    public void ApplyAttackAnimatorParameters()
    {
        if (enemy == null || enemy.animator == null) return;

        bool hurtArm = (partBreaks != null) ? partBreaks.HasAnyHurtArm() : false;
        enemy.animator.SetBool("IsHurtArm", hurtArm);

        AttackProfile p = GetActiveProfile();
        bool isAttack = (p != null) && p.setIsAttack;

        enemy.animator.SetBool("IsAttack", isAttack);
        enemy.animator.ResetTrigger("Attacking");
        enemy.animator.SetTrigger("Attacking");
    }

    public void ClearAttackAnimatorState()
    {
        if (enemy == null || enemy.animator == null) return;
        enemy.animator.SetBool("IsAttack", false);
    }

    public void SyncHurtArmAnimatorBoolOnly()
    {
        if (enemy == null || enemy.animator == null) return;
        bool hurtArm = (partBreaks != null) ? partBreaks.HasAnyHurtArm() : false;
        enemy.animator.SetBool("IsHurtArm", hurtArm);
    }

    public bool CanAttackInCurrentState()
    {
        if (enemy == null) return true;

        if (enemy.animator != null)
        {
            if (disallowAttackWhenDead && enemy.animator.GetBool("IsDead"))
                return false;

            if (disallowAttackWhenTakenDown && enemy.animator.GetBool("BeingTakenDown"))
                return false;
        }

        if (enemy.stateMachine != null && enemy.stateMachine.currentState != null)
        {
            EnemyState s = enemy.stateMachine.currentState;

            if (disallowAttackWhenStunned && s is EnemyStunState)
                return false;

            if (disallowAttackWhenTakenDown && s is EnemyTakeDownState)
                return false;

            if (disallowAttackWhenDead && s.GetType().Name == "EnemyDeadState")
                return false;
        }

        if (disallowAttackDuringLegBreakReact && partBreaks != null && partBreaks.IsLegBreakReactMoveBlocked())
            return false;

        return true;
    }

    public float GetHorizontalDistanceTo(Transform target)
    {
        if (target == null) return float.PositiveInfinity;

        Vector3 a = transform.position;
        Vector3 b = target.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    [Header("Gizmos")]
    public bool drawAttackGizmos = true;
    public bool drawAllProfiles = true;
    public bool drawCurrentProfileOnly = false;
    public bool drawAttackAngle = true;
    public bool drawTargetLine = true;
    public float gizmoHeight = 0.05f;

    private void OnDrawGizmosSelected()
    {
        if (!drawAttackGizmos) return;

        if (enemy == null) enemy = GetComponent<Enemy>();
        if (partBreaks == null) partBreaks = GetComponent<EnemyPartBreaks>();

        Vector3 origin = transform.position + Vector3.up * gizmoHeight;

        if (drawCurrentProfileOnly)
        {
            DrawCurrentProfileGizmos(origin);
        }
        else if (drawAllProfiles)
        {
            DrawProfileGizmos(origin, normalAttack, new Color(0.2f, 1f, 0.2f, 1f));
            DrawProfileGizmos(origin, hurtAttack, new Color(1f, 0.85f, 0.15f, 1f));
            DrawProfileGizmos(origin, crawlAttack, new Color(0.2f, 0.9f, 1f, 1f));
        }
        else
        {
            DrawCurrentProfileGizmos(origin);
        }

        if (drawTargetLine && enemy != null && enemy.player != null)
        {
            Gizmos.color = Color.white;
            Vector3 targetPos = enemy.player.position + Vector3.up * gizmoHeight;
            Gizmos.DrawLine(origin, targetPos);
            Gizmos.DrawWireSphere(targetPos, 0.08f);
        }
    }

    private void DrawCurrentProfileGizmos(Vector3 origin)
    {
        AttackProfile p = GetActiveProfile();
        AttackPoseType pose = GetAttackPoseType();

        Color c;
        switch (pose)
        {
            case AttackPoseType.Crawl:
                c = new Color(0.2f, 0.9f, 1f, 1f);
                break;
            case AttackPoseType.Hurt:
                c = new Color(1f, 0.85f, 0.15f, 1f);
                break;
            default:
                c = new Color(0.2f, 1f, 0.2f, 1f);
                break;
        }

        DrawProfileGizmos(origin, p, c);
    }

    private void DrawProfileGizmos(Vector3 origin, AttackProfile p, Color baseColor)
    {
        if (p == null) return;

        Gizmos.color = baseColor;
        Gizmos.DrawWireSphere(origin, Mathf.Max(0f, p.attackEnterRange));

        Color hitColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.65f);
        Gizmos.color = hitColor;
        Gizmos.DrawWireSphere(origin, Mathf.Max(0f, p.attackHitRange));

        if (drawAttackAngle)
        {
            DrawAttackAngleGizmos(origin, p, baseColor);
        }
    }

    private void DrawAttackAngleGizmos(Vector3 origin, AttackProfile p, Color color)
    {
        float enterR = Mathf.Max(0f, p.attackEnterRange);
        float hitR = Mathf.Max(0f, p.attackHitRange);
        float halfAngle = Mathf.Clamp(p.attackAngle * 0.5f, 0f, 180f);

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 leftDir = Quaternion.AngleAxis(-halfAngle, Vector3.up) * fwd;
        Vector3 rightDir = Quaternion.AngleAxis(halfAngle, Vector3.up) * fwd;

        Gizmos.color = color;
        Gizmos.DrawLine(origin, origin + fwd * enterR);
        Gizmos.DrawLine(origin, origin + leftDir * enterR);
        Gizmos.DrawLine(origin, origin + rightDir * enterR);

        Color hitLineColor = new Color(color.r, color.g, color.b, 0.65f);
        Gizmos.color = hitLineColor;
        Gizmos.DrawLine(origin, origin + leftDir * hitR);
        Gizmos.DrawLine(origin, origin + rightDir * hitR);

        DrawArc(origin, fwd, enterR, halfAngle, color, 28);
        DrawArc(origin, fwd, hitR, halfAngle, hitLineColor, 20);
    }

    private void DrawArc(Vector3 origin, Vector3 forward, float radius, float halfAngle, Color color, int segments)
    {
        if (radius <= 0f || segments < 2) return;

        Gizmos.color = color;

        float start = -halfAngle;
        float end = halfAngle;
        float step = (end - start) / segments;

        Vector3 prev = origin + (Quaternion.AngleAxis(start, Vector3.up) * forward) * radius;

        for (int i = 1; i <= segments; i++)
        {
            float a = start + step * i;
            Vector3 next = origin + (Quaternion.AngleAxis(a, Vector3.up) * forward) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}