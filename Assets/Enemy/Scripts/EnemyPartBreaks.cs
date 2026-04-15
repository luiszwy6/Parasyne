using UnityEngine;

public class EnemyPartBreaks : MonoBehaviour
{
    [Header("Part Health")] // independent from enemy health
    public float headPartHealth = 90f;
    public float torsoPartHealth = 90f;
    public float abdomenPartHealth = 90f;

    public float leftArmPartHealth = 90f;
    public float rightArmPartHealth = 90f;

    public float leftLegPartHealth = 90f;
    public float rightLegPartHealth = 90f;

    [Header("Arm Break Thresholds (percent, 0-1)")]
    [Range(0f, 1f)] public float hurtArmThreshold = 0.5f;

    [Header("Arm Health Max (for percent conversion)")]
    public float armMaxHealth = 90f;

    [Header("Arm Break Attack Damage Multipliers")]
    [Range(0.05f, 1f)] public float hurtArmDamageMultiplier = 0.75f;
    [Range(0.05f, 1f)] public float bothArmsHurtDamageMultiplier = 0.55f;

    [Header("Leg Break Thresholds (percent, 0-1)")]
    [Range(0f, 1f)] public float hurtWalkThreshold = 0.5f;
    [Range(0f, 1f)] public float crawlThreshold = 0.2f;

    [Header("Leg Health Max (for percent conversion)")]
    public float legMaxHealth = 90f;

    [Header("Leg Break React (before hurt-walk/crawl layers activate)")]
    public bool enableLegBreakTransitionAnim = true;

    public float legBreakReactDuration = 0.25f;
    public float legBreakLayerDelay = 0.25f;

    [Header("Movement Slowdown")]
    [Range(0.1f, 1f)] public float hurtWalkSpeedMultiplier = 0.65f;
    [Range(0.05f, 1f)] public float crawlSpeedMultiplier = 0.35f;

    [Header("Layer Weight Targets")]
    [Range(0f, 1f)] public float hurtWalkLayerWeight = 1f;
    [Range(0f, 1f)] public float crawlLayerWeight = 1f;

    [Header("Audio (optional)")]
    [SerializeField] private EnemyAudioEvents audioEvents;

    [Tooltip("Play hurt SFX when an arm crosses the hurt threshold.")]
    [SerializeField] private bool playArmBreakSfx = true;

    [Tooltip("Play hurt SFX when a leg crosses the hurt-walk threshold.")]
    [SerializeField] private bool playLegBreakSfx = true;

    [Tooltip("Play hurt SFX when entering Crawl state.")]
    [SerializeField] private bool playEnterCrawlSfx = true;

    public enum LegLocomotionState
    {
        Normal,
        HurtWalkLeft,
        HurtWalkRight,
        Crawl
    }

    private LegLocomotionState _lastEvaluatedLegState = LegLocomotionState.Normal;
    private bool _initializedLegState = false;

    private bool _pendingLegBreakReactTrigger = false;
    private float _legBreakReactMoveBlockTimer = 0f;
    private float _legBreakLayerBlockTimer = 0f;

    // caches to detect threshold crossings
    private bool _wasLeftArmHurt = false;
    private bool _wasRightArmHurt = false;

    private bool _wasLeftLegHurtWalk = false;
    private bool _wasRightLegHurtWalk = false;

    private bool _wasCrawl = false;

    private void Reset()
    {
        if (audioEvents == null) audioEvents = GetComponent<EnemyAudioEvents>();
        if (audioEvents == null) audioEvents = GetComponentInParent<EnemyAudioEvents>();
    }

    private void Awake()
    {
        if (audioEvents == null) audioEvents = GetComponent<EnemyAudioEvents>();
        if (audioEvents == null) audioEvents = GetComponentInParent<EnemyAudioEvents>();

        SyncThresholdCaches();
    }

    // -------------------------
    // Damage / Part Health API
    // -------------------------

    public void ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType partType, float partDmg)
    {
        if (partDmg <= 0f) return;

        switch (partType)
        {
            case EnemyHurtBoxSettings.EnemyBodyPartType.Head:
                headPartHealth = Mathf.Max(0f, headPartHealth - partDmg);
                break;

            case EnemyHurtBoxSettings.EnemyBodyPartType.Torso:
                torsoPartHealth = Mathf.Max(0f, torsoPartHealth - partDmg);
                break;

            case EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen:
                abdomenPartHealth = Mathf.Max(0f, abdomenPartHealth - partDmg);
                break;

            case EnemyHurtBoxSettings.EnemyBodyPartType.LeftArm:
                leftArmPartHealth = Mathf.Max(0f, leftArmPartHealth - partDmg);
                break;

            case EnemyHurtBoxSettings.EnemyBodyPartType.RightArm:
                rightArmPartHealth = Mathf.Max(0f, rightArmPartHealth - partDmg);
                break;

            case EnemyHurtBoxSettings.EnemyBodyPartType.LeftLeg:
                leftLegPartHealth = Mathf.Max(0f, leftLegPartHealth - partDmg);
                break;

            case EnemyHurtBoxSettings.EnemyBodyPartType.RightLeg:
                rightLegPartHealth = Mathf.Max(0f, rightLegPartHealth - partDmg);
                break;
        }

        EvaluateBreakSfx();
    }

    public float GetPartHealth(EnemyHurtBoxSettings.EnemyBodyPartType partType)
    {
        switch (partType)
        {
            case EnemyHurtBoxSettings.EnemyBodyPartType.Head: return headPartHealth;
            case EnemyHurtBoxSettings.EnemyBodyPartType.Torso: return torsoPartHealth;
            case EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen: return abdomenPartHealth;
            case EnemyHurtBoxSettings.EnemyBodyPartType.LeftArm: return leftArmPartHealth;
            case EnemyHurtBoxSettings.EnemyBodyPartType.RightArm: return rightArmPartHealth;
            case EnemyHurtBoxSettings.EnemyBodyPartType.LeftLeg: return leftLegPartHealth;
            case EnemyHurtBoxSettings.EnemyBodyPartType.RightLeg: return rightLegPartHealth;
            default: return 0f;
        }
    }

    // -------------------------
    // Arm State Evaluation
    // -------------------------

    public float GetLeftArmPercent()
    {
        float maxHp = Mathf.Max(0.0001f, armMaxHealth);
        return Mathf.Clamp01(leftArmPartHealth / maxHp);
    }

    public float GetRightArmPercent()
    {
        float maxHp = Mathf.Max(0.0001f, armMaxHealth);
        return Mathf.Clamp01(rightArmPartHealth / maxHp);
    }

    public bool IsLeftArmHurt() => GetLeftArmPercent() < hurtArmThreshold;
    public bool IsRightArmHurt() => GetRightArmPercent() < hurtArmThreshold;

    public bool HasAnyHurtArm() => IsLeftArmHurt() || IsRightArmHurt();
    public bool HasBothHurtArms() => IsLeftArmHurt() && IsRightArmHurt();

    public float GetAttackDamageMultiplierFromArms()
    {
        if (HasBothHurtArms()) return bothArmsHurtDamageMultiplier;
        if (HasAnyHurtArm()) return hurtArmDamageMultiplier;
        return 1f;
    }

    // -------------------------
    // Leg State Evaluation
    // -------------------------

    public float GetLeftLegPercent()
    {
        float maxHp = Mathf.Max(0.0001f, legMaxHealth);
        return Mathf.Clamp01(leftLegPartHealth / maxHp);
    }

    public float GetRightLegPercent()
    {
        float maxHp = Mathf.Max(0.0001f, legMaxHealth);
        return Mathf.Clamp01(rightLegPartHealth / maxHp);
    }

    public bool IsLeftLegHurtWalk() => GetLeftLegPercent() < hurtWalkThreshold;
    public bool IsRightLegHurtWalk() => GetRightLegPercent() < hurtWalkThreshold;

    public bool IsLeftLegCrawlCritical() => GetLeftLegPercent() < crawlThreshold;
    public bool IsRightLegCrawlCritical() => GetRightLegPercent() < crawlThreshold;

    public bool ShouldCrawl()
    {
        bool leftHurt = IsLeftLegHurtWalk();
        bool rightHurt = IsRightLegHurtWalk();
        bool leftCritical = IsLeftLegCrawlCritical();
        bool rightCritical = IsRightLegCrawlCritical();

        return leftCritical || rightCritical || (leftHurt && rightHurt);
    }

    public LegLocomotionState GetLegLocomotionState()
    {
        bool leftHurt = IsLeftLegHurtWalk();
        bool rightHurt = IsRightLegHurtWalk();

        if (ShouldCrawl()) return LegLocomotionState.Crawl;
        if (leftHurt && !rightHurt) return LegLocomotionState.HurtWalkLeft;
        if (!leftHurt && rightHurt) return LegLocomotionState.HurtWalkRight;

        return LegLocomotionState.Normal;
    }

    // -------------------------
    // Leg Break React Runtime Flow
    // -------------------------

    public void TickLegBreakReact(float deltaTime)
    {
        if (deltaTime < 0f) deltaTime = 0f;

        if (_legBreakReactMoveBlockTimer > 0f)
        {
            _legBreakReactMoveBlockTimer -= deltaTime;
            if (_legBreakReactMoveBlockTimer < 0f) _legBreakReactMoveBlockTimer = 0f;
        }

        if (_legBreakLayerBlockTimer > 0f)
        {
            _legBreakLayerBlockTimer -= deltaTime;
            if (_legBreakLayerBlockTimer < 0f) _legBreakLayerBlockTimer = 0f;
        }

        LegLocomotionState current = GetLegLocomotionState();

        if (!_initializedLegState)
        {
            _lastEvaluatedLegState = current;
            _initializedLegState = true;
            return;
        }

        if (current != _lastEvaluatedLegState)
        {
            bool enteringLegOverlayState =
                current == LegLocomotionState.HurtWalkLeft ||
                current == LegLocomotionState.HurtWalkRight ||
                current == LegLocomotionState.Crawl;

            if (enteringLegOverlayState && enableLegBreakTransitionAnim)
            {
                _pendingLegBreakReactTrigger = true;
                _legBreakReactMoveBlockTimer = Mathf.Max(0f, legBreakReactDuration);
                _legBreakLayerBlockTimer = Mathf.Max(0f, legBreakLayerDelay);
            }
        }

        _lastEvaluatedLegState = current;
    }

    public bool ConsumeLegBreakReactTriggerRequest()
    {
        if (!_pendingLegBreakReactTrigger) return false;
        _pendingLegBreakReactTrigger = false;
        return true;
    }

    public bool IsLegBreakReactMoveBlocked() => _legBreakReactMoveBlockTimer > 0f;
    public bool IsLegBreakLayerBlocked() => _legBreakLayerBlockTimer > 0f;

    // -------------------------
    // Driver-friendly outputs
    // -------------------------

    public float GetLegMoveSpeedMultiplier()
    {
        if (IsLegBreakReactMoveBlocked()) return 0f;

        switch (GetLegLocomotionState())
        {
            case LegLocomotionState.Crawl: return crawlSpeedMultiplier;
            case LegLocomotionState.HurtWalkLeft:
            case LegLocomotionState.HurtWalkRight: return hurtWalkSpeedMultiplier;
            default: return 1f;
        }
    }

    public bool GetIsHurtWalkL() => GetLegLocomotionState() == LegLocomotionState.HurtWalkLeft;
    public bool GetIsHurtWalkR() => GetLegLocomotionState() == LegLocomotionState.HurtWalkRight;
    public bool GetIsCrawl() => GetLegLocomotionState() == LegLocomotionState.Crawl;

    public bool ShouldUseHurtAttackAnimation()
    {
        return GetIsHurtWalkL() || GetIsHurtWalkR() || HasAnyHurtArm();
    }

    public float GetHurtWalkLeftLayerWeight()
    {
        if (IsLegBreakLayerBlocked()) return 0f;
        return GetIsHurtWalkL() ? hurtWalkLayerWeight : 0f;
    }

    public float GetHurtWalkRightLayerWeight()
    {
        if (IsLegBreakLayerBlocked()) return 0f;
        return GetIsHurtWalkR() ? hurtWalkLayerWeight : 0f;
    }

    public float GetCrawlLayerWeight()
    {
        if (IsLegBreakLayerBlocked()) return 0f;
        return GetIsCrawl() ? crawlLayerWeight : 0f;
    }

    // -------------------------
    // SFX logic
    // -------------------------

    private void SyncThresholdCaches()
    {
        _wasLeftArmHurt = IsLeftArmHurt();
        _wasRightArmHurt = IsRightArmHurt();

        _wasLeftLegHurtWalk = IsLeftLegHurtWalk();
        _wasRightLegHurtWalk = IsRightLegHurtWalk();

        _wasCrawl = (GetLegLocomotionState() == LegLocomotionState.Crawl);
    }

private void EvaluateBreakSfx()
{
    if (audioEvents == null)
        audioEvents = GetComponent<EnemyAudioEvents>() ?? GetComponentInParent<EnemyAudioEvents>();

    bool leftArmHurt = IsLeftArmHurt();
    bool rightArmHurt = IsRightArmHurt();

    bool leftLegHurt = IsLeftLegHurtWalk();
    bool rightLegHurt = IsRightLegHurtWalk();

    bool crawlNow = (GetLegLocomotionState() == LegLocomotionState.Crawl);

    if (playArmBreakSfx && audioEvents != null)
    {
        if (!_wasLeftArmHurt && leftArmHurt) audioEvents.Enemy_PlayArmBreakSfx();
        else if (!_wasRightArmHurt && rightArmHurt) audioEvents.Enemy_PlayArmBreakSfx();
    }

    if (playLegBreakSfx && audioEvents != null)
    {
        if (!_wasLeftLegHurtWalk && leftLegHurt) audioEvents.Enemy_PlayLegBreakSfx();
        else if (!_wasRightLegHurtWalk && rightLegHurt) audioEvents.Enemy_PlayLegBreakSfx();
    }

    if (playEnterCrawlSfx && audioEvents != null)
    {
        if (!_wasCrawl && crawlNow) audioEvents.Enemy_PlayCrawlEnterSfx();
    }

    _wasLeftArmHurt = leftArmHurt;
    _wasRightArmHurt = rightArmHurt;
    _wasLeftLegHurtWalk = leftLegHurt;
    _wasRightLegHurtWalk = rightLegHurt;
    _wasCrawl = crawlNow;
}
}