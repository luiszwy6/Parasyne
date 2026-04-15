using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

// PlayerTPS: third-person aiming camera mode.
// - Drives yaw/pitch pivots from mouse delta.
// - Casts a center-screen ray to compute an aim point.
// - Smooths aim point to reduce jitter, and rotates actor towards it on XZ.

[DisallowMultipleComponent]
public class PlayerTPS : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Transform actorRoot;               // Player root transform (for rotation)
    [SerializeField] private Camera outputCamera;               // TPS output camera (with CinemachineBrain)
    [SerializeField] private PlayerMovement playerMovement;     // Used for externalMovementLock (do NOT disable PlayerMovement)
    [SerializeField] private Animator animator;                 // Optional (PlayerMovement already drives IsAiming/IsCrouching)

    [Header("TPS Camera (Cinemachine 3)")]
    [SerializeField] private CinemachineCamera tpsVirtualCamera;
    [SerializeField] private int activePriority = 50;
    [SerializeField] private int inactivePriority = 10;

    [Header("TPS Rig (Yaw/Pitch)")]
    [Tooltip("Yaw pivot. Usually placed at player center.")]
    [SerializeField] private Transform yawPivot;
    [Tooltip("Pitch pivot. Should be a child of yawPivot, placed near shoulder/head.")]
    [SerializeField] private Transform pitchPivot;

    [Header("Look Input")]
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private float sensitivity = 0.08f;
    [SerializeField] private float pitchMin = -35f;
    [SerializeField] private float pitchMax = 70f;
    [SerializeField] private bool lockCursorWhenActive = true;

    [Header("Look Filter")]
    [SerializeField] private float lookDeadzone = 0.0f;   // per-axis mouse delta deadzone
    [SerializeField] private float maxLookDeltaX = 40f;   // clamp mouse delta X per frame
    [SerializeField] private float maxLookDeltaY = 40f;   // clamp mouse delta Y per frame

    [Header("Look Reset")]
    [SerializeField] private string crouchBoolName = "IsCrouching";

    [Header("Aim (Center Screen Ray)")]
    [SerializeField] private float aimRayDistance = 200f;
    [SerializeField] private LayerMask aimHitMask = ~0;
    [SerializeField] private float rotateToAimSpeed = 15f;

    [Header("Aim Smoothing")]
    [Tooltip("Smooth time for the aim point to reduce raycast snapping jitter. Smaller = snappier.")]
    [SerializeField] private float aimPointSmoothTime = 0.06f;

    [Tooltip("Do not rotate actor towards aim point when the aim point is too close (prevents micro jitter).")]
    [SerializeField] private float minAimTurnDistance = 1.0f;

    [Header("Optional Visual")]
    [SerializeField] private LineRenderer tpsAimLine;
    [SerializeField] private Transform aimLineStart;
    [SerializeField] private float aimLineStartHeight = 1.4f;
    [SerializeField] private bool stopAimLineByHit = true;
    [SerializeField] private float aimLinePadding = 0.02f;

    public enum RoomOverrideMode
    {
        None,
        ForceAllRoomsVisible
    }

    [Header("Room Rendering (Debug Only)")]
    [Tooltip("Recommended: keep None for gameplay. Use ForceAllRoomsVisible only when debugging TPS.")]
    [SerializeField] private RoomOverrideMode roomOverride = RoomOverrideMode.None;
    [SerializeField] private RoomManager roomManager;

    [Header("TPS Action Lock")]
    [Tooltip("When enabled, TPS will block move/run/roll but still allow aiming + crouching (via PlayerMovement.externalMovementLock).")]
    [SerializeField] private bool lockActionsExceptAimingAndCrouching = true;

    // runtime
    private InputAction lookAction;
    private float yaw;
    private float pitch;
    private bool active;
    private bool isAiming;

    // outputs
    public Vector3 AimPoint { get; private set; }
    public Vector3 AimDirXZ { get; private set; }

    // aim smoothing runtime
    private Vector3 smoothedAimPoint;
    private Vector3 aimPointVelocity;
    private bool hasSmoothedAimPoint;

    // look reset runtime
    private bool suppressLookOnce;
    private bool lastCrouch;
    private int crouchBoolHash;
    private bool hasCrouchBool;

    // room override cache (visual visibility only)
    private readonly Dictionary<Room, bool> roomVisibilityCache = new();
    public Camera OutputCamera => outputCamera;
    public bool IsActive => active;

    private void Reset()
    {
        actorRoot = transform;
        playerInput = GetComponent<PlayerInput>();
        playerMovement = GetComponent<PlayerMovement>();
        animator = GetComponentInChildren<Animator>();

        if (roomManager == null) roomManager = FindFirstObjectByType<RoomManager>();
    }

    private void Awake()
    {
        if (actorRoot == null) actorRoot = transform;
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (outputCamera == null) outputCamera = Camera.main;

        if (playerInput != null)
            lookAction = playerInput.actions[lookActionName];

        crouchBoolHash = Animator.StringToHash(crouchBoolName);
        hasCrouchBool = false;

        if (animator != null)
        {
            var ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].type == AnimatorControllerParameterType.Bool && ps[i].nameHash == crouchBoolHash)
                {
                    hasCrouchBool = true;
                    break;
                }
            }

            if (hasCrouchBool)
                lastCrouch = animator.GetBool(crouchBoolHash);
        }

        // Ensure a clean start in top-down mode
        SetActive(false);
        SetAiming(false);
    }

    /// <summary>
    /// Called by PlayerTPSToggle to enable/disable TPS camera mode.
    /// </summary>
    public void SetActive(bool on)
    {
        active = on;

        // camera priority
        if (tpsVirtualCamera != null)
            tpsVirtualCamera.Priority = on ? activePriority : inactivePriority;

        // input
        if (on) lookAction?.Enable();
        else lookAction?.Disable();

        // cursor
        if (lockCursorWhenActive)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        // room override (debug only)
        if (on) ApplyRoomOverride();
        else RestoreRoomOverride();

        // lock player actions but keep aiming/crouching
        if (playerMovement != null && lockActionsExceptAimingAndCrouching)
            playerMovement.externalMovementLock = on;

        // reset smoothing state when entering TPS
        if (on)
        {
            hasSmoothedAimPoint = false;
            aimPointVelocity = Vector3.zero;
            suppressLookOnce = true; // reset look delta on TPS enter
        }

        // visuals
        if (tpsAimLine != null)
            tpsAimLine.enabled = on && isAiming;
    }

    /// <summary>
    /// Called by PlayerTPSToggle to tell TPS whether we are currently aiming (Aim pressed).
    /// </summary>
    public void SetAiming(bool aiming)
    {
        isAiming = aiming;

        // visuals
        if (tpsAimLine != null)
            tpsAimLine.enabled = active && isAiming;
    }

    public void ZeroLookDeltaOnce()
    {
        suppressLookOnce = true;
    }

    private void LateUpdate()
    {
        if (!active) return;

        // 1) TPS Look: drive yaw/pitch from mouse delta
        Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        // reset look once when needed (TPS enter / crouch enter)
        if (suppressLookOnce)
        {
            suppressLookOnce = false;
            delta = Vector2.zero;
        }

        // detect crouch enter -> reset next look once
        if (hasCrouchBool)
        {
            bool crouchNow = animator.GetBool(crouchBoolHash);
            if (crouchNow && !lastCrouch)
                suppressLookOnce = true;
            lastCrouch = crouchNow;
        }

        delta = FilterLookDelta(delta);

        yaw += delta.x * sensitivity;
        pitch -= delta.y * sensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        if (yawPivot != null) yawPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Only compute aiming direction / rotate actor while Aim is held
        if (!isAiming)
        {
            AimDirXZ = Vector3.zero;
            return;
        }

        // 2) Choose the output camera used for center-screen ray
        Camera cam = outputCamera != null ? outputCamera : Camera.main;
        if (cam == null) return;

        // 3) TPS aim: center screen raycast
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 hitPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, aimRayDistance, aimHitMask, QueryTriggerInteraction.Ignore))
        {
            hitPoint = hit.point;
        }
        else
        {
            // Stable fallback: intersect with a horizontal plane (prevents jitter above horizon)
            float planeY = actorRoot != null ? actorRoot.position.y + 1.4f : transform.position.y + 1.4f;
            Plane p = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

            if (p.Raycast(ray, out float enter))
                hitPoint = ray.GetPoint(Mathf.Clamp(enter, 0f, aimRayDistance));
            else
                hitPoint = ray.origin + ray.direction * 10f; // ultra fallback (rare)
        }

        // 4) Smooth aim point (reduces snapping jitter)
        if (!hasSmoothedAimPoint)
        {
            smoothedAimPoint = hitPoint;
            aimPointVelocity = Vector3.zero;
            hasSmoothedAimPoint = true;
        }
        else
        {
            smoothedAimPoint = Vector3.SmoothDamp(
                smoothedAimPoint,
                hitPoint,
                ref aimPointVelocity,
                Mathf.Max(0.0001f, aimPointSmoothTime)
            );
        }

        AimPoint = smoothedAimPoint;

        // 5) Rotate actor towards the (smoothed) aim point on XZ
        Vector3 toAim = AimPoint - actorRoot.position;
        toAim.y = 0f;

        if (toAim.sqrMagnitude > minAimTurnDistance * minAimTurnDistance)
        {
            AimDirXZ = toAim.normalized;

            Quaternion target = Quaternion.LookRotation(AimDirXZ, Vector3.up);
            actorRoot.rotation = Quaternion.Slerp(
                actorRoot.rotation,
                target,
                1f - Mathf.Exp(-rotateToAimSpeed * Time.deltaTime)
            );
        }

        UpdateAimLine(cam, ray, AimPoint);
    }

    private Vector2 FilterLookDelta(Vector2 d)
    {
        if (Mathf.Abs(d.x) < lookDeadzone) d.x = 0f;
        if (Mathf.Abs(d.y) < lookDeadzone) d.y = 0f;

        d.x = Mathf.Clamp(d.x, -Mathf.Abs(maxLookDeltaX), Mathf.Abs(maxLookDeltaX));
        d.y = Mathf.Clamp(d.y, -Mathf.Abs(maxLookDeltaY), Mathf.Abs(maxLookDeltaY));

        return d;
    }

    private void UpdateAimLine(Camera cam, Ray centerRay, Vector3 aimPoint)
    {
        if (tpsAimLine == null) return;

        Vector3 start = aimLineStart != null
            ? aimLineStart.position
            : (actorRoot != null ? actorRoot.position : transform.position) + Vector3.up * aimLineStartHeight;

        Vector3 dir = (aimPoint - start);
        float dist = dir.magnitude;
        if (dist < 0.001f) dist = 0.001f;
        dir /= dist;

        Vector3 end = aimPoint;

        if (stopAimLineByHit)
        {
            if (Physics.Raycast(start, dir, out RaycastHit hit, dist, aimHitMask, QueryTriggerInteraction.Ignore))
                end = hit.point - dir * Mathf.Max(0f, aimLinePadding);
        }

        tpsAimLine.positionCount = 2;
        tpsAimLine.SetPosition(0, start);
        tpsAimLine.SetPosition(1, end);
    }

    // ---------------- Room override (debug only) ----------------
    private void ApplyRoomOverride()
    {
        if (roomOverride == RoomOverrideMode.None) return;

        if (roomManager == null)
            roomManager = FindFirstObjectByType<RoomManager>();

        if (roomManager == null) return;

        if (roomOverride == RoomOverrideMode.ForceAllRoomsVisible)
        {
            var allRooms = roomManager.GetRooms();
            if (allRooms == null) return;

            roomVisibilityCache.Clear();

            foreach (var r in allRooms)
            {
                if (r == null) continue;

                roomVisibilityCache[r] = r.IsVisible;
                r.SetVisible(true);
            }
        }
    }

    private void RestoreRoomOverride()
    {
        if (roomOverride == RoomOverrideMode.None) return;

        if (roomVisibilityCache.Count > 0)
        {
            foreach (var kv in roomVisibilityCache)
            {
                if (kv.Key == null) continue;
                kv.Key.SetVisible(kv.Value);
            }
            roomVisibilityCache.Clear();
        }
    }
}