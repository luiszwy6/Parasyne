// Enemy.cs
// Central "data + perception" component used by Enemy AI states.
// - Holds tunable parameters (vision/hearing/thresholds/timers).
// - Provides helper methods to test vision rings + LOS + score.
// - Stores investigation memory (last seen/heard/known positions).

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
    [Tooltip("If set, use this transform for eye position + orientation (head/eyes bone is best).")]
    public Transform eyeTransform;

    [Tooltip("Used only when eyeTransform is null. Offset from enemy root to approximate eyes.")]
    public Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Legacy Vision (unused by score system)")]
    // NOTE: Kept for backward compatibility / inspector migration.
    // The current system uses outer/inner ring values below.
    public float viewRadius = 10f;
    [Range(0, 360)] public float viewAngle = 120f;

    [Header("Vision Rings (Base)")]
    // Two-layer vision:
    // - Outer ring: "suspicion" (can lead to Aware)
    // - Inner ring: "high confidence" (often boosts score or triggers instant alert)
    public float outerRadius = 10f;
    [Range(0, 360)] public float outerAngle = 120f;
    [Range(0, 180)] public float outerVerticalAngle = 60f;

    public float innerRadius = 4f;
    [Range(0, 360)] public float innerAngle = 160f;
    [Range(0, 180)] public float innerVerticalAngle = 90f;

    [Header("Vision Occlusion (raycast)")]
    // If enabled, enemy must have a clear ray to the playerVisionTarget (or player).
    // This prevents "seeing through walls".
    public bool useVisionOcclusion = true;

    [Tooltip("Layers that can block vision (Environment/Props). Exclude Player/Enemy layers.")]
    public LayerMask visionObstructionMask = ~0;

    [Tooltip("Shorten the ray slightly so we don't hit the target collider due to precision/self-intersection.")]
    public float visionOcclusionPadding = 0.05f;

    public QueryTriggerInteraction visionOcclusionTriggers = QueryTriggerInteraction.Ignore;

    [Header("Debug Draw (Play Mode)")]
    [Tooltip("Draw vision ray when the cone test passes (needs Gizmos on).")]
    public bool debugDrawVisionRay = true;

    [Tooltip("0 = only this frame; >0 keeps line visible for seconds.")]
    public float debugRayDuration = 0f;

    [Header("Debug Draw (Edit Mode)")]
    [Tooltip("Draw a gizmo ray in Edit Mode when cone test passes.")]
    public bool debugDrawEditorVisionRay = true;

    [Tooltip("If true, editor ray color indicates blocked/clear. If false, always draws yellow.")]
    public bool debugEditorRayShowsOcclusion = true;

    public enum PlayerPosture { Stand = 0, Crouch = 1, Run = 2 }

    [Header("Vision Posture Multipliers (reserved)")]
    // These multipliers scale radii depending on player posture.
    // Example: crouching reduces effective vision distance.
    public float postureOuterRadiusMul_Stand = 1.0f;
    public float postureOuterRadiusMul_Crouch = 0.75f;
    public float postureOuterRadiusMul_Run = 1.0f;

    public float postureInnerRadiusMul_Stand = 1.0f;
    public float postureInnerRadiusMul_Crouch = 0.75f;
    public float postureInnerRadiusMul_Run = 1.0f;

    [Tooltip("Used only if PlayerAwareness doesn't provide posture and playerAnimator is null.")]
    public PlayerPosture debugPosture = PlayerPosture.Stand;

    [Header("Hearing")]
    public float hearingRadius = 8f;

    [Header("Noise sensitivity (0-2)")]
    [Tooltip("Min noise that makes the enemy become Aware when player is in hearing range.")]
    [Range(0, 2)] public int noiseSensitivity = 2;

    [Header("Vision Score (0-4 lightness)")]
    // lightScore[l] is the base score coming from player "lightness" (0..4).
    // This score only applies if the player is inside the view cone (outer or inner).
    [Tooltip("Score added by player lightness level 0..4 (applies only when in view cone/ring).")]
    public float[] lightScore = new float[5] { 0f, 1f, 2f, 3f, 4f };

    [Header("Ring Bonus (per lightness, 0-4)")]
    // Extra score depending on which ring the player is in.
    // Arrays allow tuning per-lightness level (bright players can get bigger bonuses).
    [Tooltip("Score bonus when player is in outer ring. Index 0..4")]
    public float[] outerRingBonusByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };

    [Tooltip("Score bonus when player is in inner ring. Index 0..4")]
    public float[] innerRingBonusByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };

    [Header("Legacy Ring Bonus (fallback if arrays are missing/invalid)")]
    // If arrays are not set correctly, use these constant bonuses.
    public float outerRingBonus = 1f;
    public float innerRingBonus = 3f;

    [Header("Vision Score Thresholds")]
    [Tooltip("Score >= this -> Aware can start accumulating.")]
    public float scoreToAware = 3f;

    [Tooltip("Score >= this -> instant Alert.")]
    public float scoreToAlert = 5f;

    [Header("Force To Alert (per lightness)")]
    // Special case: within a certain distance (depends on lightness) and inside INNER cone -> Alert immediately.
    // This is useful for "too close" situations even if score thresholds are tuned high.
    [Tooltip("If >0, within this distance and inside INNER cone -> instant Alert regardless of score. Index 0..4")]
    public float[] forceToAlertByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };

    [Header("State timers")]
    [Tooltip("How long enemy stays Aware without seeing or hearing the player.")]
    public float awareForgetTime = 5f;

    [Header("Check state")]
    [Tooltip("If suspicion is continuous for this long, switch from Aware -> Check.")]
    public float awareToCheckTime = 1.5f;

    [Tooltip("When enemy reaches last known position, wait/check for this many seconds.")]
    public float checkingTime = 2.0f;

    [Header("Last known info (legacy)")]
    // Kept for compatibility; we also store lastSeen/lastHeard separately.
    public Vector3 lastKnownPosition;
    public bool hasLastKnownPosition = false;

    [Header("Investigation Memory")]
    // Investigation priority:
    // lastSeen -> lastHeard -> lastKnown (legacy)
    public Vector3 lastSeenPosition;
    public bool hasLastSeenPosition = false;

    public Vector3 lastHeardPosition;
    public bool hasLastHeardPosition = false;

    [Header("Patrol")]
    [Tooltip("Waypoints for Patrol state (empty GameObjects in scene).")]
    public Transform[] patrolWaypoints;

    [Tooltip("Current waypoint index. Kept across states so patrol can resume.")]
    public int patrolIndex = 0;

    [Tooltip("Wait time at each waypoint before moving on.")]
    public float patrolWaitTime = 0.5f;

    [Tooltip("If true, loop back to index 0 after last waypoint.")]
    public bool patrolLoop = true;

    [Header("Navigation")]
    public NavMeshAgent agent;

    [Header("Animation")]
    public Animator animator;

    [Header("Alert Movement")]
    [Tooltip("If true, use Run blend tree + faster speed in ALERT. If false, use Walk blend tree.")]
    public bool runOnAlert = true;

    [Tooltip("NavMeshAgent speed in ALERT if runOnAlert = false.")]
    public float alertWalkSpeed = 2.0f;

    [Tooltip("NavMeshAgent speed in ALERT if runOnAlert = true.")]
    public float alertRunSpeed = 4.0f;

    // Small wrapper methods keep states simple and avoid null checks everywhere.
    public void SetAnimRunning(bool v)
    {
        if (animator != null) animator.SetBool("IsRunning", v);
    }

    public void SetAnimAware(bool v)
    {
        if (animator != null) animator.SetBool("IsAware", v);
    }

    public void SetAnimSpeed(float v)
    {
        if (animator != null) animator.SetFloat("Speed", v);
    }

    public void SetAnimTakeDown(bool v)
    {
        if (animator != null) animator.SetBool("BeingTakenDown", v);
    }
    public void ForceTakeDown()
    {

        SetAnimTakeDown(true);

    }

    [HideInInspector] public EnemyStateMachine stateMachine;
    [HideInInspector] public float stateTimer;

    Renderer debugRenderer;

    void Awake()
    {
        // Initialize state machine and cache common components.
        stateMachine = new EnemyStateMachine();
        debugRenderer = GetComponentInChildren<Renderer>();
        if (!agent) agent = GetComponent<NavMeshAgent>();

        // If outer ring values were not set, fallback to legacy view values.
        if (outerRadius <= 0f) outerRadius = viewRadius;
        if (outerAngle <= 0f) outerAngle = viewAngle;

        // Ensure score arrays always have length 5 to avoid IndexOutOfRange.
        EnsureScoreArrays();
    }

    void Start()
    {
        // Default behavior: start patrolling.
        stateMachine.Initialize(new EnemyPatrolState(this));
    }

    void Update()
    {
        // Main loop: let the active state handle behavior.
        stateMachine.currentState?.Update();
    }

    #region Perception helpers

    // Read light/noise levels from PlayerAwareness.
    // We clamp later, but these should already be small enums:
    // lightness: 0..4, noisiness: 0..2
    public int CurrentLightLevel() => playerAwareness ? playerAwareness.lightness : 0;
    public int CurrentNoiseLevel() => playerAwareness ? playerAwareness.noisiness : 0;

    // Eye position is used for cone checks and LOS raycasts.
    public Vector3 GetEyePosition()
    {
        if (eyeTransform != null) return eyeTransform.position;
        return transform.position + eyeOffset;
    }

    // Use eyeTransform orientation when available, otherwise enemy root orientation.
    private Vector3 EyeForward => (eyeTransform != null ? eyeTransform.forward : transform.forward).normalized;
    private Vector3 EyeRight => (eyeTransform != null ? eyeTransform.right : transform.right).normalized;
    private Vector3 EyeUp => (eyeTransform != null ? eyeTransform.up : transform.up).normalized;

    // Where we consider the "visible point" of the player (target point for LOS/cone).
    public Vector3 GetPlayerVisionPoint()
    {
        if (playerVisionTarget != null) return playerVisionTarget.position;
        if (player != null) return player.position;
        return default;
    }

    // Determine player posture, with fallbacks:
    // 1) Read animator bool params (most stable for gameplay)
    // 2) If PlayerAwareness has posture (field or property), use it via reflection
    // 3) Use debugPosture for testing
    public PlayerPosture CurrentPosture()
    {
        if (playerAnimator != null)
        {
            bool crouching = (!string.IsNullOrEmpty(crouchParam)) && playerAnimator.GetBool(crouchParam);
            bool running = (!string.IsNullOrEmpty(runParam)) && playerAnimator.GetBool(runParam);

            if (crouching) return PlayerPosture.Crouch;
            if (running) return PlayerPosture.Run;
            return PlayerPosture.Stand;
        }

        if (!playerAwareness) return debugPosture;

        var t = playerAwareness.GetType();

        // Reflection: supports either a public field "posture" or a property "posture".
        // This avoids hard dependency if PlayerAwareness is implemented differently.
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

    // Effective radii apply posture multipliers.
    // These are used by cone checks (outer/inner).
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

    // If the raycast hits the player (or the vision target), we treat it as NOT blocked.
    // This helps when the player has multiple colliders / child transforms.
    private bool IsHitPlayerOrVisionTarget(Transform hitT)
    {
        if (hitT == null) return false;

        if (player != null && (hitT == player || hitT.IsChildOf(player)))
            return true;

        if (playerVisionTarget != null && (hitT == playerVisionTarget || hitT.IsChildOf(playerVisionTarget)))
            return true;

        return false;
    }

    // Line-of-sight test from eye to playerVisionTarget.
    // maxDistance usually matches the ring radius used by the cone test.
    public bool HasLineOfSightToPlayerVisionPoint(float maxDistance)
    {
        if (!useVisionOcclusion) return true;
        if (!player) return false;

        Vector3 origin = GetEyePosition();
        Vector3 target = GetPlayerVisionPoint();

        Vector3 toTarget = target - origin;
        float dist = toTarget.magnitude;
        if (dist <= 0.0001f) return true;

        // Shorten ray slightly to avoid hitting the target collider due to float precision.
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

        // If the raycast "hit" is actually player/target, we consider LOS clear.
        if (blocked && IsHitPlayerOrVisionTarget(hit.transform))
            blocked = false;

        // Optional runtime debug: green = clear, red/gray = blocked.
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

    // 3D view cone test using yaw + pitch relative to the eye orientation.
    // Steps:
    // 1) distance check
    // 2) forward-facing check (z > 0)
    // 3) yaw/pitch within angles
    // 4) optional LOS raycast
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

        // z is how much the target is in front of the enemy.
        float z = Vector3.Dot(dir, EyeForward);
        if (z <= 0f) return false;

        float x = Vector3.Dot(dir, EyeRight);
        float y = Vector3.Dot(dir, EyeUp);

        // Convert direction to yaw/pitch angles around the eye basis.
        float yawDeg = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
        float pitchDeg = Mathf.Atan2(y, z) * Mathf.Rad2Deg;

        if (Mathf.Abs(yawDeg) > horizontalAngleDeg * 0.5f) return false;
        if (Mathf.Abs(pitchDeg) > verticalAngleDeg * 0.5f) return false;

        // Occlusion check should match the radius used here.
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

    // Compute "vision score" for this frame.
    // IMPORTANT RULE:
    // - If player is not in view (not outer/inner), score MUST be 0.
    // - This prevents awareness from building just because the player is bright elsewhere.
    public float GetVisionScore(out bool inOuter, out bool inInner)
    {
        EnsureScoreArrays();

        inInner = IsPlayerInInnerRing();
        inOuter = inInner || IsPlayerInOuterRing();

        if (!inOuter)
            return 0f;

        int l = Mathf.Clamp(CurrentLightLevel(), 0, 4);

        float score = lightScore[l];

        // Inner ring usually gives stronger bonus than outer ring.
        if (inInner)
            score += (innerRingBonusByLight != null && innerRingBonusByLight.Length >= 5) ? innerRingBonusByLight[l] : innerRingBonus;
        else
            score += (outerRingBonusByLight != null && outerRingBonusByLight.Length >= 5) ? outerRingBonusByLight[l] : outerRingBonus;

        return score;
    }

    // Check the special "force alert" rule.
    // If configured distance > 0 for the current lightness,
    // and the player is inside INNER cone within that distance -> alert immediately.
    public bool IsForceAlertTriggered()
    {
        if (!player) return false;

        int l = Mathf.Clamp(CurrentLightLevel(), 0, 4);
        if (forceToAlertByLight == null || forceToAlertByLight.Length < 5) return false;

        float r = forceToAlertByLight[l];
        if (r <= 0f) return false;

        return IsPlayerInViewCone3DFromEye(r, innerAngle, innerVerticalAngle);
    }

    // Hearing is simple radius check. Noise level gating happens in states.
    public bool IsPlayerInHearingRange()
    {
        if (!player) return false;
        float distance = Vector3.Distance(transform.position, player.position);
        return distance <= hearingRadius;
    }

    // Store "seen" position and also update legacy lastKnownPosition.
    // We project to NavMesh so agents can actually navigate there.
    public void SetLastSeenPosition(Vector3 pos)
    {
        pos = ProjectPointToNavMesh(pos);
        lastSeenPosition = pos;
        hasLastSeenPosition = true;

        lastKnownPosition = pos;
        hasLastKnownPosition = true;
    }

    // Store "heard" position and also update legacy lastKnownPosition.
    public void SetLastHeardPosition(Vector3 pos)
    {
        pos = ProjectPointToNavMesh(pos);
        lastHeardPosition = pos;
        hasLastHeardPosition = true;

        lastKnownPosition = pos;
        hasLastKnownPosition = true;
    }

    // Provide a single investigation point for Check/Search states.
    // Priority is: seen -> heard -> known.
    public bool TryGetInvestigationPoint(out Vector3 point)
    {
        if (hasLastSeenPosition) { point = lastSeenPosition; return true; }
        if (hasLastHeardPosition) { point = lastHeardPosition; return true; }
        if (hasLastKnownPosition) { point = lastKnownPosition; return true; }

        point = default;
        return false;
    }

    // Clear all memory so enemy returns to normal behavior (usually Patrol).
    public void ClearInvestigationMemory()
    {
        hasLastSeenPosition = false;
        hasLastHeardPosition = false;
        hasLastKnownPosition = false;
    }

    // Legacy API mapping.
    public void SetLastKnownPosition(Vector3 pos) => SetLastSeenPosition(pos);
    public void ClearLastKnownPosition() => ClearInvestigationMemory();

    // Project a point to the NavMesh near it, so pathing won't fail.
    // If agent isn't on NavMesh, we return the original position.
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

    // Ensure arrays always have length 5 so all indexing is safe.
    private void EnsureScoreArrays()
    {
        if (lightScore == null || lightScore.Length != 5) lightScore = new float[5] { 0f, 1f, 2f, 3f, 4f };
        if (outerRingBonusByLight == null || outerRingBonusByLight.Length != 5) outerRingBonusByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };
        if (innerRingBonusByLight == null || innerRingBonusByLight.Length != 5) innerRingBonusByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };
        if (forceToAlertByLight == null || forceToAlertByLight.Length != 5) forceToAlertByLight = new float[5] { 0f, 0f, 0f, 0f, 0f };
    }

    #endregion

    #region Debug visuals (Gizmos)

    // Quick visual indicator on the enemy mesh/material (optional usage by states).
    public void SetDebugColor(Color c)
    {
        if (debugRenderer != null) debugRenderer.material.color = c;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 root = transform.position;

        // Hearing radius visualization.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(root, hearingRadius);

#if UNITY_EDITOR
        // Draw outer/inner 3D cone wireframes in editor.
        Vector3 eye = (eyeTransform != null ? eyeTransform.position : transform.position + eyeOffset);

        Vector3 f = (eyeTransform != null ? eyeTransform.forward : transform.forward).normalized;
        Vector3 r = (eyeTransform != null ? eyeTransform.right : transform.right).normalized;
        Vector3 u = (eyeTransform != null ? eyeTransform.up : transform.up).normalized;

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
            // Editor ray: only draw if cone test passes (outer or inner).
            bool inOuter = IsPlayerInOuterRing();
            bool inInner = inOuter && IsPlayerInInnerRing();

            if (inOuter || inInner)
            {
                Vector3 target = GetPlayerVisionPoint();

                // Match LOS distance to the ring the player is in.
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
    // Draw a simple wireframe "boxy cone" plus arc grid for visualization.
    // This is editor-only and does not affect gameplay logic.
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

        // Build a direction in the eye basis given yaw/pitch.
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

        // Four corners of the cone "frustum" at max radius.
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

        // Draw yaw arcs at fixed pitch.
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

        // Draw pitch arcs at fixed yaw.
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

        // Basic grid.
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
}
