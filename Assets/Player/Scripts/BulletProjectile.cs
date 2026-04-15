using UnityEngine;

[DisallowMultipleComponent]
public class BulletProjectile : MonoBehaviour
{
    [Header("Projectile (Shared)")]
    public float speed = 120f;
    public float maxDistance = 50f;
    public float radius = 0.04f;

    [Header("Hit Query (Shared)")]
    public LayerMask hitMask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Behavior (Shared)")]
    public bool destroyOnAnyHit = true;

    [Header("Debug")]
    public bool drawDebugLine = false;
    public Color debugColor = Color.red;

    [Header("Gizmos / Debug View")]
    public bool drawGizmos = true;
    public bool drawOnlyWhenSelected = false;
    public bool drawSweepSegment = true;
    public Color gizmoSphereColor = Color.red;
    public Color gizmoSweepColor = Color.yellow;

    // Runtime projectile state
    private Vector3 _dir;
    private float _traveled = 0f;
    private bool _inited = false;

    // Runtime damage injected from weapon at fire time (not inspector config)
    private float _runtimePartDamage = 0f;
    private float _runtimeBaseDamage = 0f; // reserved for future enemy HP system

    // Gizmo sweep visualization
    private Vector3 _prevPosForGizmo;
    private bool _hasPrevPosForGizmo = false;

    public void Init(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float maxDistance,
        float radius,
        float runtimePartDamage,
        float runtimeBaseDamage,
        LayerMask hitMask,
        QueryTriggerInteraction triggerInteraction)
    {
        transform.position = origin;

        _dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        this.speed = Mathf.Max(0.01f, speed);
        this.maxDistance = Mathf.Max(0.01f, maxDistance);
        this.radius = Mathf.Max(0.001f, radius);

        _runtimePartDamage = Mathf.Max(0f, runtimePartDamage);
        _runtimeBaseDamage = Mathf.Max(0f, runtimeBaseDamage);

        this.hitMask = hitMask;
        this.triggerInteraction = triggerInteraction;

        _traveled = 0f;
        _inited = true;

        transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);

        _prevPosForGizmo = transform.position;
        _hasPrevPosForGizmo = true;
    }

    private void Update()
    {
        if (!_inited) return;

        float step = speed * Time.deltaTime;
        if (step <= 0f) return;

        Vector3 from = transform.position;
        Vector3 to = from + _dir * step;

        _prevPosForGizmo = from;
        _hasPrevPosForGizmo = true;

        if (Physics.SphereCast(from, radius, _dir, out RaycastHit hit, step, hitMask, triggerInteraction))
        {
            transform.position = hit.point;

            bool hitEnemyHurtbox = TryDamageEnemyBodyPart(hit);

            if (destroyOnAnyHit || hitEnemyHurtbox)
            {
                Destroy(gameObject);
                return;
            }
        }

        transform.position = to;
        _traveled += step;

        if (drawDebugLine)
        {
            Debug.DrawLine(from, to, debugColor, 0f, false);
        }

        if (_traveled >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private bool TryDamageEnemyBodyPart(RaycastHit hit)
    {
        Collider hitCol = hit.collider;
        if (hitCol == null) return false;

        // Find enemy root systems
        EnemyHurtBoxSettings hurtBoxSettings = hitCol.GetComponentInParent<EnemyHurtBoxSettings>();
        if (hurtBoxSettings == null)
            return false; // not an enemy hurtbox target

        EnemyPartBreaks partBreaks = hurtBoxSettings.GetComponent<EnemyPartBreaks>();
        if (partBreaks == null)
            partBreaks = hurtBoxSettings.GetComponentInParent<EnemyPartBreaks>();

        if (partBreaks == null)
        {
            Debug.LogWarning($"[BulletProjectile] EnemyPartBreaks missing on {hurtBoxSettings.name}");
            return true;
        }

        if (!TryResolveBodyPartTypeFromTag(hitCol, out EnemyHurtBoxSettings.EnemyBodyPartType partType))
        {
            Debug.LogWarning($"[BulletProjectile] Hurtbox tag not recognized on '{hitCol.name}' (tag={hitCol.tag})");
            return true;
        }

        // Same damage entry path as BodyPartDebugger (part damage only)
        partBreaks.ApplyPartDamage(partType, _runtimePartDamage);

        EnemyHealth hp = hurtBoxSettings.GetComponent<EnemyHealth>();
        if (hp == null) hp = hurtBoxSettings.GetComponentInParent<EnemyHealth>();

        if (hp != null && _runtimeBaseDamage > 0f)
        {
           hp.TakeDamage(_runtimeBaseDamage);
        }

        // Hit VFX (blood)
        EnemyHitVFX hitVFX = hurtBoxSettings.GetComponent<EnemyHitVFX>();
        if (hitVFX == null)
            hitVFX = hurtBoxSettings.GetComponentInParent<EnemyHitVFX>();

        if (hitVFX != null)
        {
            hitVFX.PlayHit(
                partType,
                hit.point,
                hit.normal,
                _runtimePartDamage,
                _runtimeBaseDamage
            );
        }

        HitPartShakeReaction shake = hurtBoxSettings.GetComponentInParent<HitPartShakeReaction>();
        if (shake != null)
        shake.PlayHurtbox(hit.collider);

        Debug.Log($"[BulletProjectile] Hit {hurtBoxSettings.name} -> {partType}, part_dmg={_runtimePartDamage}");

        // Future:
        // Enemy enemy = hurtBoxSettings.GetComponent<Enemy>() ?? hurtBoxSettings.GetComponentInParent<Enemy>();
        // enemy?.TakeDamage(_runtimeBaseDamage);

        return true;
    }

    private bool TryResolveBodyPartTypeFromTag(Collider hitCol, out EnemyHurtBoxSettings.EnemyBodyPartType partType)
    {
        partType = default;

        if (hitCol == null) return false;

        // Core tags
        if (hitCol.CompareTag("E_Head"))
        {
            partType = EnemyHurtBoxSettings.EnemyBodyPartType.Head;
            return true;
        }

        if (hitCol.CompareTag("E_Torso"))
        {
            partType = EnemyHurtBoxSettings.EnemyBodyPartType.Torso;
            return true;
        }

        if (hitCol.CompareTag("E_Abdomen"))
        {
            partType = EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen;
            return true;
        }

        // Preferred left/right tags
        if (hitCol.CompareTag("E_LeftArm"))
        {
            partType = EnemyHurtBoxSettings.EnemyBodyPartType.LeftArm;
            return true;
        }

        if (hitCol.CompareTag("E_RightArm"))
        {
            partType = EnemyHurtBoxSettings.EnemyBodyPartType.RightArm;
            return true;
        }

        if (hitCol.CompareTag("E_LeftLeg"))
        {
            partType = EnemyHurtBoxSettings.EnemyBodyPartType.LeftLeg;
            return true;
        }

        if (hitCol.CompareTag("E_RightLeg"))
        {
            partType = EnemyHurtBoxSettings.EnemyBodyPartType.RightLeg;
            return true;
        }

        // Fallback for grouped tags (older setup)
        if (hitCol.CompareTag("E_Arm"))
        {
            string n = hitCol.name.ToLowerInvariant();
            if (n.Contains("_l") || n.Contains("left"))
                partType = EnemyHurtBoxSettings.EnemyBodyPartType.LeftArm;
            else if (n.Contains("_r") || n.Contains("right"))
                partType = EnemyHurtBoxSettings.EnemyBodyPartType.RightArm;
            else
                partType = EnemyHurtBoxSettings.EnemyBodyPartType.LeftArm;

            return true;
        }

        if (hitCol.CompareTag("E_Leg"))
        {
            string n = hitCol.name.ToLowerInvariant();
            if (n.Contains("_l") || n.Contains("left"))
                partType = EnemyHurtBoxSettings.EnemyBodyPartType.LeftLeg;
            else if (n.Contains("_r") || n.Contains("right"))
                partType = EnemyHurtBoxSettings.EnemyBodyPartType.RightLeg;
            else
                partType = EnemyHurtBoxSettings.EnemyBodyPartType.LeftLeg;

            return true;
        }

        return false;
    }

    // ---------------------------------------------------------------------
    // Gizmos
    // ---------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (!drawGizmos || drawOnlyWhenSelected) return;
        DrawProjectileGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !drawOnlyWhenSelected) return;
        DrawProjectileGizmos();
    }

    private void DrawProjectileGizmos()
    {
        float r = Mathf.Max(0.001f, radius);

        // Current projectile sphere
        Gizmos.color = gizmoSphereColor;
        Gizmos.DrawWireSphere(transform.position, r);

        if (!drawSweepSegment || !_hasPrevPosForGizmo) return;

        // Sweep segment (previous -> current)
        Gizmos.color = gizmoSweepColor;
        Gizmos.DrawLine(_prevPosForGizmo, transform.position);

        // End caps
        Gizmos.DrawWireSphere(_prevPosForGizmo, r);
        Gizmos.DrawWireSphere(transform.position, r);
    }
}