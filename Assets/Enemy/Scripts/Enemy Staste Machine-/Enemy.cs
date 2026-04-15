// Enemy.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Enemy : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public PlayerAwareness playerAwareness;

    [Tooltip("Where the enemy aims its visibility check. If null, uses player.position.")]
    public Transform playerVisionTarget;

    [Header("Player Animator (Posture)")]
    public Animator playerAnimator;
    public string crouchParam = "IsCrouching";
    public string runParam = "IsRunning";

    [Header("Vision Origin (for 3D cone + raycast)")]
    public Transform eyeTransform;
    public Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Legacy Vision (unused by score system)")]
    public float viewRadius = 10f;
    [Range(0, 360)] public float viewAngle = 120f;

    [Header("Vision Rings (Base)")]
    // Outer ring: large cone = "soft detection" (low bonus, farther range)
    public float outerRadius = 10f;
    [Range(0, 360)] public float outerAngle = 120f;
    [Range(0, 180)] public float outerVerticalAngle = 60f;

    // Inner ring: tighter/stronger cone = "strong detection" (higher bonus, closer range)
    public float innerRadius = 4f;
    [Range(0, 360)] public float innerAngle = 160f;
    [Range(0, 180)] public float innerVerticalAngle = 90f;

    [Header("Vision Occlusion (raycast)")]
    // If enabled: LOS must be clear (raycast) to count as "seen"
    public bool useVisionOcclusion = true;
    public LayerMask visionObstructionMask = ~0;
    public float visionOcclusionPadding = 0.05f;
    public QueryTriggerInteraction visionOcclusionTriggers = QueryTriggerInteraction.Ignore;

    [Header("Debug Draw (Play Mode)")]
    public bool debugDrawVisionRay = true;
    public float debugRayDuration = 0f;

    [Header("Debug Draw (Edit Mode)")]
    public bool debugDrawEditorVisionRay = true;
    public bool debugEditorRayShowsOcclusion = true;

    public enum PlayerPosture { Stand = 0, Crouch = 1, Run = 2 }

    [Header("Vision Posture Multipliers (reserved)")]
    // Posture scaling: shrink/enlarge effective detection radii based on posture
    public float postureOuterRadiusMul_Stand = 1.0f;
    public float postureOuterRadiusMul_Crouch = 0.75f;
    public float postureOuterRadiusMul_Run = 1.0f;

    public float postureInnerRadiusMul_Stand = 1.0f;
    public float postureInnerRadiusMul_Crouch = 0.75f;
    public float postureInnerRadiusMul_Run = 1.0f;

    [Tooltip("Used only if PlayerAwareness doesn't provide posture and playerAnimator is null.")]
    public PlayerPosture debugPosture = PlayerPosture.Stand;

    // -------------------- HEARING --------------------

    [Header("Hearing")]
    // Hearing is based on overlap with player's noise rings (distance-attenuation model)
    public float hearingRadius = 8f;

    [Header("Noise sensitivity (0-2)")]
    // Enemy triggers only if perceived noise >= this threshold
    [Range(0, 2)] public int noiseSensitivity = 2;

    [Header("Sound Occlusion (-1)")]
    // Simple occlusion: if a wall blocks LOS, perceived noise is reduced by 1
    public LayerMask soundObstructionMask = ~0;
    public QueryTriggerInteraction soundOcclusionTriggers = QueryTriggerInteraction.Ignore;

    [Header("Sound Reaction")]
    public float soundReactionTime = 0.35f;
    public float soundPreTurnDelay = 0.12f;
    public float soundTurnSpeed = 10f;

    // Cached settings used for noise ring attenuation queries
    private PlayerNoiseSettings playerNoiseSettings;

    // -------------------- VISION SCORE (lightness + ring bonus) --------------------

    [Header("Vision Score (0-4 lightness)")]
    // Base score by light level (PlayerAwareness.lightness)
    public float[] lightScore = new float[5] { 0f, 1f, 2f, 3f, 4f };

    [Header("Ring Bonus (per lightness, 0-4)")]
    // Extra score added if in outer/inner ring at a given light level
    public float[] outerRingBonusByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };
    public float[] innerRingBonusByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };

    [Header("Legacy Ring Bonus (fallback if arrays are missing/invalid)")]
    public float outerRingBonus = 1f;
    public float innerRingBonus = 3f;

    [Header("Vision Score Thresholds")]
    // Score gates used by FSM states (e.g., Patrol->Aware->Alert)
    public float scoreToAware = 3f;
    public float scoreToAlert = 5f;

    [Header("Force To Alert (per lightness)")]
    // Optional: "instant alert" if player is within a forced radius for a light level
    public float[] forceToAlertByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };

    [Header("State timers")]
    public float awareForgetTime = 5f;
    public float surpriseDuration = 0.6f;
    [Header("Stun")]
    public float shortStunDuration = 0.35f;   // Head -> short stun
    public float longStunDuration = 0.75f;    // Torso/Abdomen -> long stun
    public float stunCooldown = 0.5f;
    private float _nextAllowedStunTime = 0f;
    public float shortStunKnockbackStartDelay = 0.02f;
    public float longStunKnockbackStartDelay = 0.04f;
    [Header("Stun When Crawling")]
    public string crawlParam = "IsCrawl";

    [Header("Stun Knockback")]
    public float longStunKnockbackDistance = 0.45f;
    public float longStunKnockbackDuration = 0.12f;
    public float shortStunKnockbackDistance = 0.10f;
    public float shortStunKnockbackDuration = 0.06f;

    [Header("Check state")]
    public float awareToCheckTime = 1.5f;
    public float checkingTime = 2.0f;

    [Header("Last known info (legacy)")]
    public Vector3 lastKnownPosition;
    public bool hasLastKnownPosition = false;

    [Header("Investigation Memory")]
    // Separate memory buckets for sight vs sound investigation
    public Vector3 lastSeenPosition;
    public bool hasLastSeenPosition = false;

    public Vector3 lastHeardPosition;
    public bool hasLastHeardPosition = false;

    [Header("Aware Trigger")]
    // Helps FSM decide which "aware" behavior to play (heard vs seen)
    [HideInInspector] public bool lastAwareTriggerWasSound = false;

    [Header("Patrol")]
    public Transform[] patrolWaypoints;
    public int patrolIndex = 0;
    public float patrolWaitTime = 0.5f;
    public bool patrolLoop = true;

    [Header("Navigation")]
    public NavMeshAgent agent;
    public CharacterController cc;

    [Header("Animation")]
    public Animator animator;

    [Header("Alert Movement")]
    public bool runOnAlert = true;
    public float alertWalkSpeed = 2.0f;
    public float alertRunSpeed = 4.0f;

    [Header("Takedown")]
    public string takedownParam = "BeingTakenDown";
    [Header("Health")] 
    public EnemyHealth health;

    [Header("Debug Color")]
    public bool allowStateColorChange = true;

    // Animator helpers (kept minimal for state code readability)
    public void SetAnimRunning(bool v) { if (animator != null) animator.SetBool("IsRunning", v); }
    public void SetAnimAware(bool v) { if (animator != null) animator.SetBool("IsAware", v); }
    public void SetAnimSpeed(float v) { if (animator != null) animator.SetFloat("Speed", v); }
    public void SetAnimTakeDown(bool v) { if (animator != null) animator.SetBool("BeingTakenDown", v); }

    public void ForceTakeDown()
    {
        SetAnimTakeDown(true);
        stateMachine.ChangeState(new EnemyTakeDownState(this));
    }

    [HideInInspector] public EnemyStateMachine stateMachine;
    [HideInInspector] public float stateTimer;

    Renderer debugRenderer;

    void Awake()
    {
        stateMachine = new EnemyStateMachine();
        debugRenderer = GetComponentInChildren<Renderer>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!cc) cc = GetComponent<CharacterController>();
        health = GetComponent<EnemyHealth>();

        // Backward-compat defaults: if new params not set, fall back to legacy ones
        if (outerRadius <= 0f) outerRadius = viewRadius;
        if (outerAngle <= 0f) outerAngle = viewAngle;

        EnsureScoreArrays();

        // Cache PlayerNoiseSettings for hearing checks
        if (player != null)
            playerNoiseSettings = player.GetComponent<PlayerNoiseSettings>();
    }

    void Start()
    {
        stateMachine.Initialize(new EnemyPatrolState(this));
    }

    void Update()
    {
        stateMachine.currentState?.Update();
    }

    #region Perception helpers

    // Pull current perception values from PlayerAwareness (light/noise are integer bands)
    public int CurrentLightLevel() => playerAwareness ? playerAwareness.lightness : 0;
    public int CurrentNoiseLevel() => playerAwareness ? playerAwareness.noisiness : 0;

    public Vector3 GetEyePosition()
    {
        if (eyeTransform != null) return eyeTransform.position;
        return transform.position + eyeOffset;
    }

    // Eye basis vectors used for 3D cone checks (yaw/pitch)
    private Vector3 EyeForward => (eyeTransform != null ? eyeTransform.forward : transform.forward).normalized;
    private Vector3 EyeRight => (eyeTransform != null ? eyeTransform.right : transform.right).normalized;
    private Vector3 EyeUp => (eyeTransform != null ? eyeTransform.up : transform.up).normalized;

    public Vector3 GetPlayerVisionPoint()
    {
        if (playerVisionTarget != null) return playerVisionTarget.position;
        if (player != null) return player.position;
        return default;
    }

    public bool IsDeadPlaceholder()
    {
    if (health != null) return health.IsDead;
    if (animator != null && animator.GetBool("IsDead")) return true;
    return false;
    }

    public PlayerPosture CurrentPosture()
    {
        // Primary source: playerAnimator bool params
        if (playerAnimator != null)
        {
            bool crouching = (!string.IsNullOrEmpty(crouchParam)) && playerAnimator.GetBool(crouchParam);
            bool running = (!string.IsNullOrEmpty(runParam)) && playerAnimator.GetBool(runParam);

            if (crouching) return PlayerPosture.Crouch;
            if (running) return PlayerPosture.Run;
            return PlayerPosture.Stand;
        }

        // Secondary source: reflect posture from PlayerAwareness (if it exists)
        if (!playerAwareness) return debugPosture;

        var t = playerAwareness.GetType();

        var f = t.GetField("posture");
        if (f != null)
        {
            object v = f.GetValue(playerAwareness);
            if (v is int vi) return (PlayerPosture)Mathf.Clamp(vi, 0, 2);
            if (v != null && v.GetType().IsEnum) return (PlayerPosture)Mathf.Clamp((int)v, 0, 2);
        }

        var p = t.GetProperty("posture");
        if (p != null)
        {
            object v = p.GetValue(playerAwareness);
            if (v is int vi) return (PlayerPosture)Mathf.Clamp(vi, 0, 2);
            if (v != null && v.GetType().IsEnum) return (PlayerPosture)Mathf.Clamp((int)v, 0, 2);
        }

        return debugPosture;
    }

    // Effective ring radii after posture multipliers
    public float GetOuterRadiusEffective()
    {
        var posture = CurrentPosture();
        float mul =
            posture == PlayerPosture.Crouch ? postureOuterRadiusMul_Crouch :
            posture == PlayerPosture.Run ? postureOuterRadiusMul_Run :
            postureOuterRadiusMul_Stand;

        return Mathf.Max(0f, outerRadius * mul);
    }

    public float GetInnerRadiusEffective()
    {
        var posture = CurrentPosture();
        float mul =
            posture == PlayerPosture.Crouch ? postureInnerRadiusMul_Crouch :
            posture == PlayerPosture.Run ? postureInnerRadiusMul_Run :
            postureInnerRadiusMul_Stand;

        return Mathf.Max(0f, innerRadius * mul);
    }

    // Treat hits on player or its child transforms as "not blocked"
    private bool IsHitPlayerOrVisionTarget(Transform hitT)
    {
        if (hitT == null) return false;

        if (player != null && (hitT == player || hitT.IsChildOf(player)))
            return true;

        if (playerVisionTarget != null && (hitT == playerVisionTarget || hitT.IsChildOf(playerVisionTarget)))
            return true;

        return false;
    }

    // LOS raycast from eye to player vision point (used by vision cones)
    public bool HasLineOfSightToPlayerVisionPoint(float maxDistance)
    {
        if (!useVisionOcclusion) return true;
        if (!player) return false;

        Vector3 origin = GetEyePosition();
        Vector3 target = GetPlayerVisionPoint();

        Vector3 toTarget = target - origin;
        float dist = toTarget.magnitude;
        if (dist <= 0.0001f) return true;

        float rayDist = Mathf.Min(maxDistance, dist - visionOcclusionPadding);
        if (rayDist <= 0f) return true;

        Vector3 dir = toTarget / dist;

        bool blocked = Physics.Raycast(
            origin,
            dir,
            out RaycastHit hit,
            rayDist,
            visionObstructionMask,
            visionOcclusionTriggers
        );

        if (blocked && IsHitPlayerOrVisionTarget(hit.transform))
            blocked = false;

        if (debugDrawVisionRay && Application.isPlaying)
        {
            if (blocked)
            {
                Debug.DrawLine(origin, hit.point, Color.red, debugRayDuration, true);
                Debug.DrawLine(hit.point, origin + dir * rayDist, Color.gray, debugRayDuration, true);
            }
            else
            {
                Debug.DrawLine(origin, target, Color.green, debugRayDuration, true);
            }
        }

        return !blocked;
    }

    // 3D cone check using yaw/pitch in eye space, plus LOS test
    public bool IsPlayerInViewCone3DFromEye(float radius, float horizontalAngleDeg, float verticalAngleDeg)
    {
        if (!player) return false;

        Vector3 origin = GetEyePosition();
        Vector3 target = GetPlayerVisionPoint();
        Vector3 toTarget = target - origin;

        float distance = toTarget.magnitude;
        if (distance > radius) return false;
        if (distance < 0.0001f) return true;

        Vector3 dir = toTarget / distance;

        float z = Vector3.Dot(dir, EyeForward);
        if (z <= 0f) return false;

        float x = Vector3.Dot(dir, EyeRight);
        float y = Vector3.Dot(dir, EyeUp);

        float yawDeg = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
        float pitchDeg = Mathf.Atan2(y, z) * Mathf.Rad2Deg;

        if (Mathf.Abs(yawDeg) > horizontalAngleDeg * 0.5f) return false;
        if (Mathf.Abs(pitchDeg) > verticalAngleDeg * 0.5f) return false;

        if (!HasLineOfSightToPlayerVisionPoint(radius)) return false;

        return true;
    }

    public bool IsPlayerInOuterRing()
    {
        return IsPlayerInViewCone3DFromEye(GetOuterRadiusEffective(), outerAngle, outerVerticalAngle);
    }

    public bool IsPlayerInInnerRing()
    {
        return IsPlayerInViewCone3DFromEye(GetInnerRadiusEffective(), innerAngle, innerVerticalAngle);
    }

    // Core vision scoring: lightness base + ring bonus (inner > outer)
    public float GetVisionScore(out bool inOuter, out bool inInner)
    {
        EnsureScoreArrays();

        inInner = IsPlayerInInnerRing();
        inOuter = inInner || IsPlayerInOuterRing();

        if (!inOuter)
            return 0f;

        int l = Mathf.Clamp(CurrentLightLevel(), 0, 4);

        float score = lightScore[l];

        if (inInner)
            score += (innerRingBonusByLight != null && innerRingBonusByLight.Length >= 5) ? innerRingBonusByLight[l] : innerRingBonus;
        else
            score += (outerRingBonusByLight != null && outerRingBonusByLight.Length >= 5) ? outerRingBonusByLight[l] : outerRingBonus;

        return score;
    }

    // Optional "hard trigger" for alert at close range per lightness
    public bool IsForceAlertTriggered()
    {
        if (!player) return false;

        int l = Mathf.Clamp(CurrentLightLevel(), 0, 4);
        if (forceToAlertByLight == null || forceToAlertByLight.Length < 5) return false;

        float r = forceToAlertByLight[l];
        if (r <= 0f) return false;

        return IsPlayerInViewCone3DFromEye(r, innerAngle, innerVerticalAngle);
    }

    // -------------------- HEARING --------------------

    // Ring check: is enemy inside any player noise ring at all?
    public bool IsPlayerInHearingRange()
    {
        if (!player) return false;

        if (playerNoiseSettings == null)
            playerNoiseSettings = player.GetComponent<PlayerNoiseSettings>();

        if (playerNoiseSettings == null) return false;

        int atten = playerNoiseSettings.GetRingAttenuation(transform.position, hearingRadius);
        return atten != int.MaxValue;
    }

    // Perceived noise = baseNoise - ringAttenuation - (wall? 1 : 0)
    public int GetPerceivedNoiseLevel()
    {
        if (!player) return 0;

        if (playerNoiseSettings == null)
            playerNoiseSettings = player.GetComponent<PlayerNoiseSettings>();

        if (playerNoiseSettings == null) return 0;

        int baseNoise = Mathf.Clamp(CurrentNoiseLevel(), 0, 2);
        if (baseNoise <= 0) return 0;

        int atten = playerNoiseSettings.GetRingAttenuation(transform.position, hearingRadius);
        if (atten == int.MaxValue) return 0;

        int perceived = Mathf.Max(0, baseNoise - atten);

        if (perceived > 0 && IsSoundBlockedByWall())
            perceived = Mathf.Max(0, perceived - 1);

        return perceived;
    }

    // Simple "one wall = -1" occlusion using a linecast
    private bool IsSoundBlockedByWall()
    {
        if (!player) return false;

        Vector3 origin = GetEyePosition();
        Vector3 target = player.position;

        bool hit = Physics.Linecast(
            origin,
            target,
            out RaycastHit hitInfo,
            soundObstructionMask,
            soundOcclusionTriggers
        );

        if (!hit) return false;

        Transform ht = hitInfo.transform;
        if (ht == player || ht.IsChildOf(player)) return false;

        return true;
    }

    // -------------------- Investigation memory --------------------

    // Store investigation points projected to NavMesh (so agent can path to it)
    public void SetLastSeenPosition(Vector3 pos)
    {
        pos = ProjectPointToNavMesh(pos);
        lastSeenPosition = pos;
        hasLastSeenPosition = true;

        lastKnownPosition = pos;
        hasLastKnownPosition = true;
    }

    public void SetLastHeardPosition(Vector3 pos)
    {
        pos = ProjectPointToNavMesh(pos);
        lastHeardPosition = pos;
        hasLastHeardPosition = true;

        lastKnownPosition = pos;
        hasLastKnownPosition = true;
    }

    public bool TryGetInvestigationPoint(out Vector3 point)
    {
        if (hasLastSeenPosition) { point = lastSeenPosition; return true; }
        if (hasLastHeardPosition) { point = lastHeardPosition; return true; }
        if (hasLastKnownPosition) { point = lastKnownPosition; return true; }

        point = default;
        return false;
    }

    public void ClearInvestigationMemory()
    {
        hasLastSeenPosition = false;
        hasLastHeardPosition = false;
        hasLastKnownPosition = false;
    }

    public void SetLastKnownPosition(Vector3 pos) => SetLastSeenPosition(pos);
    public void ClearLastKnownPosition() => ClearInvestigationMemory();

    private Vector3 ProjectPointToNavMesh(Vector3 pos)
    {
        if (agent != null && agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(pos, out hit, 2.0f, NavMesh.AllAreas))
                return hit.position;
        }
        return pos;
    }

    // Ensure arrays exist and are exactly length 5 (indexed by lightness 0..4)
    private void EnsureScoreArrays()
    {
        if (lightScore == null || lightScore.Length != 5) lightScore = new float[5] { 0f, 1f, 2f, 3f, 4f };
        if (outerRingBonusByLight == null || outerRingBonusByLight.Length != 5) outerRingBonusByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };
        if (innerRingBonusByLight == null || innerRingBonusByLight.Length != 5) innerRingBonusByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };
        if (forceToAlertByLight == null || forceToAlertByLight.Length != 5) forceToAlertByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };
    }

    #endregion

    #region Debug visuals (Gizmos)

    public void SetDebugColor(Color c)
    {
      if (!allowStateColorChange) return;
      if (debugRenderer != null) debugRenderer.material.color = c;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 root = transform.position;

        // Hearing radius visualization.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(root, hearingRadius);

#if UNITY_EDITOR
        Vector3 eye = (eyeTransform != null ? eyeTransform.position : transform.position + eyeOffset);

        Vector3 f = (eyeTransform != null ? eyeTransform.forward : transform.forward).normalized;
        Vector3 r = (eyeTransform != null ? eyeTransform.right : transform.right).normalized;
        Vector3 u = (eyeTransform != null ? eyeTransform.up : transform.up).normalized;

        // Visualize outer/inner vision cones in Scene view (editor-only).
        DrawVisionCone3DWire(
            eye, f, r, u,
            Application.isPlaying ? GetOuterRadiusEffective() : outerRadius,
            outerAngle, outerVerticalAngle,
            new Color(0.1f, 1f, 0.1f, 1f),
            "Outer 3D Cone"
        );

        DrawVisionCone3DWire(
            eye, f, r, u,
            Application.isPlaying ? GetInnerRadiusEffective() : innerRadius,
            innerAngle, innerVerticalAngle,
            new Color(0.2f, 0.6f, 1f, 1f),
            "Inner 3D Cone"
        );

        if (debugDrawEditorVisionRay && player != null)
        {
            bool inOuter = IsPlayerInOuterRing();
            bool inInner = inOuter && IsPlayerInInnerRing();

            if (inOuter || inInner)
            {
                Vector3 target = GetPlayerVisionPoint();

                float losDist = inInner
                    ? (Application.isPlaying ? GetInnerRadiusEffective() : innerRadius)
                    : (Application.isPlaying ? GetOuterRadiusEffective() : outerRadius);

                bool blocked = useVisionOcclusion && !HasLineOfSightToPlayerVisionPoint(losDist);

                if (!debugEditorRayShowsOcclusion)
                    Gizmos.color = Color.yellow;
                else
                    Gizmos.color = blocked ? Color.red : Color.green;

                Gizmos.DrawLine(eye, target);
                Gizmos.DrawSphere(target, 0.05f);
            }
        }
#endif
    }

#if UNITY_EDITOR
    private void DrawVisionCone3DWire(
        Vector3 origin,
        Vector3 forward,
        Vector3 right,
        Vector3 up,
        float radius,
        float horizontalAngleDeg,
        float verticalAngleDeg,
        Color color,
        string label
    )
    {
        if (radius <= 0f) return;

        Handles.color = color;

        float hHalf = horizontalAngleDeg * 0.5f * Mathf.Deg2Rad;
        float vHalf = verticalAngleDeg * 0.5f * Mathf.Deg2Rad;

        // Converts (yaw, pitch) in eye-space to a world direction.
        Vector3 Dir(float yawRad, float pitchRad)
        {
            float cy = Mathf.Cos(yawRad);
            float sy = Mathf.Sin(yawRad);
            float cp = Mathf.Cos(pitchRad);
            float sp = Mathf.Sin(pitchRad);

            Vector3 d =
                (forward * cp * cy) +
                (right * cp * sy) +
                (up * sp);

            return d.normalized;
        }

        Vector3 c1 = Dir(-hHalf, -vHalf) * radius;
        Vector3 c2 = Dir(+hHalf, -vHalf) * radius;
        Vector3 c3 = Dir(+hHalf, +vHalf) * radius;
        Vector3 c4 = Dir(-hHalf, +vHalf) * radius;

        Handles.DrawLine(origin, origin + c1);
        Handles.DrawLine(origin, origin + c2);
        Handles.DrawLine(origin, origin + c3);
        Handles.DrawLine(origin, origin + c4);

        Handles.DrawLine(origin + c1, origin + c2);
        Handles.DrawLine(origin + c2, origin + c3);
        Handles.DrawLine(origin + c3, origin + c4);
        Handles.DrawLine(origin + c4, origin + c1);

        int arcSeg = 32;

        void DrawArcYaw(float pitchRad)
        {
            Vector3 prev = origin + Dir(-hHalf, pitchRad) * radius;
            for (int i = 1; i <= arcSeg; i++)
            {
                float t = (float)i / arcSeg;
                float yaw = Mathf.Lerp(-hHalf, +hHalf, t);
                Vector3 p = origin + Dir(yaw, pitchRad) * radius;
                Handles.DrawLine(prev, p);
                prev = p;
            }
        }

        void DrawArcPitch(float yawRad)
        {
            Vector3 prev = origin + Dir(yawRad, -vHalf) * radius;
            for (int i = 1; i <= arcSeg; i++)
            {
                float t = (float)i / arcSeg;
                float pitch = Mathf.Lerp(-vHalf, +vHalf, t);
                Vector3 p = origin + Dir(yawRad, pitch) * radius;
                Handles.DrawLine(prev, p);
                prev = p;
            }
        }

        DrawArcYaw(-vHalf);
        DrawArcYaw(0f);
        DrawArcYaw(+vHalf);

        DrawArcPitch(-hHalf);
        DrawArcPitch(0f);
        DrawArcPitch(+hHalf);

        int grid = 3;
        for (int i = 1; i <= grid; i++)
        {
            float pt = Mathf.Lerp(-vHalf, +vHalf, (float)i / (grid + 1));
            DrawArcYaw(pt);

            float yt = Mathf.Lerp(-hHalf, +hHalf, (float)i / (grid + 1));
            DrawArcPitch(yt);
        }

        Handles.Label(origin + up * 0.15f, label);
    }
#endif

    #endregion

    public bool CanEnterStunNow()
{
    return Time.time >= _nextAllowedStunTime;
}

// Returns false if stun is rejected (cooldown / dead / invalid part / dead state).
public bool TryEnterStunFromBodyPart(EnemyHurtBoxSettings.EnemyBodyPartType partType)
{
    // Only Head / Torso / Abdomen trigger stun in current design.
    bool isHead = partType == EnemyHurtBoxSettings.EnemyBodyPartType.Head;
    bool isTorsoOrAbdomen =
        partType == EnemyHurtBoxSettings.EnemyBodyPartType.Torso ||
        partType == EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen;

    if (!isHead && !isTorsoOrAbdomen)
        return false;

    if (IsDeadPlaceholder())
        return false;
        
    if (animator != null && !string.IsNullOrEmpty(crawlParam) && animator.GetBool(crawlParam))
    return false;

    if (animator != null && animator.GetBool("IsDead"))
        return false;

    if (!CanEnterStunNow())
        return false;

    // Do not stack/re-enter stun while already in stun.
    if (stateMachine != null && stateMachine.currentState is EnemyStunState)
        return false;

    // Do not interrupt dead state (future-proof for when you add EnemyDeadState).
    if (stateMachine != null &&
        stateMachine.currentState != null &&
        stateMachine.currentState.GetType().Name == "EnemyDeadState")
    {
        return false;
    }

    EnemyStunState.StunKind stunKind = isHead
        ? EnemyStunState.StunKind.Short
        : EnemyStunState.StunKind.Long;

    float duration = (stunKind == EnemyStunState.StunKind.Short) ? shortStunDuration : longStunDuration;

    // Save the interrupted state instance so stun can restore it.
    EnemyState interruptedState = stateMachine != null ? stateMachine.currentState : null;

    // Safety: do not restore null / self.
    if (interruptedState is EnemyStunState)
        interruptedState = null;

    _nextAllowedStunTime = Time.time + stunCooldown;

    stateMachine.ChangeState(new EnemyStunState(this, stunKind, duration, interruptedState));
    return true;
}
}
