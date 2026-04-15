using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHitVFX : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private ParticleSystem bloodImpactPrefab;

    [Header("Placement")]
    [SerializeField] private float surfaceOffset = 0.01f;
    [SerializeField] private float destroyAfter = 2f;

    [Header("Scale")]
    [SerializeField] private float baseScale = 1f;
    [SerializeField] private float headScale = 1.15f;
    [SerializeField] private float torsoScale = 1f;
    [SerializeField] private float abdomenScale = 1f;
    [SerializeField] private float armScale = 0.9f;
    [SerializeField] private float legScale = 0.9f;

    public void PlayHit(
        EnemyHurtBoxSettings.EnemyBodyPartType part,
        Vector3 hitPoint,
        Vector3 hitNormal,
        float partDamage,
        float baseDamage)
    {
        if (bloodImpactPrefab == null) return;

        Vector3 pos = hitPoint + hitNormal * surfaceOffset;
        Quaternion rot = Quaternion.LookRotation(-hitNormal, Vector3.up);

        ParticleSystem ps = Instantiate(bloodImpactPrefab, pos, rot);

        float s = GetScale(part);
        float dmg = Mathf.Max(0f, partDamage + baseDamage);
        float dmgScale = Mathf.Clamp(0.9f + dmg * 0.01f, 0.9f, 1.6f);

        ps.transform.localScale = Vector3.one * (s * dmgScale);

        ps.Play(true);
        Destroy(ps.gameObject, destroyAfter);
    }

    private float GetScale(EnemyHurtBoxSettings.EnemyBodyPartType part)
    {
        switch (part)
        {
            case EnemyHurtBoxSettings.EnemyBodyPartType.Head: return baseScale * headScale;
            case EnemyHurtBoxSettings.EnemyBodyPartType.Torso: return baseScale * torsoScale;
            case EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen: return baseScale * abdomenScale;
            case EnemyHurtBoxSettings.EnemyBodyPartType.LeftArm:
            case EnemyHurtBoxSettings.EnemyBodyPartType.RightArm: return baseScale * armScale;
            case EnemyHurtBoxSettings.EnemyBodyPartType.LeftLeg:
            case EnemyHurtBoxSettings.EnemyBodyPartType.RightLeg: return baseScale * legScale;
            default: return baseScale;
        }
    }
}