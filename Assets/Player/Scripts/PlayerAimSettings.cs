using UnityEngine;
using UnityEngine.InputSystem;

// PlayerAimSettings: reads Aim/Look input and produces aiming outputs.
// - Delegates aim point calculation to PlayerCrossHairSettings.
// - Provides facing direction while aiming and optionally draws a sight line.

[RequireComponent(typeof(PlayerInput))]
public class PlayerAimSettings : MonoBehaviour
{
    [Header("Aim Move Speeds")]
    public float aimWalkSpeed = 2.5f;
    public float aimCrouchSpeed = 1.5f;

    [Header("Cameras")]
    public Transform cameraTransform;     // for stick aiming basis
    public Camera aimCamera;              // for mouse ray

    [Header("Crosshair Provider (Weapon)")]
    public PlayerCrossHairSettings crosshairSettings; // weapon/empty that computes aim point

    [Header("Aim Sight Line (Play Mode)")]
    public bool showAimLine = true;
    public LineRenderer aimLine; 

    [Tooltip("Line start when standing (optional). If null, fallback is used.")]
    public Transform aimLineStartStanding;

    [Tooltip("Line start when crouching (optional). If null, fallback is used.")]
    public Transform aimLineStartCrouching;

    [Tooltip("Fallback height used only when no start points are assigned.")]
    public float aimLineStartHeight = 0.8f;

    [Tooltip("Max line length (finite for performance).")]
    public float aimLineLength = 25f;

    [Tooltip("If true, raycast and truncate the line at wall hit. Uses aimLineStopLayers if set; else falls back to crosshairSettings.stopLayers.")]
    public bool aimLineStopsByLayer = true;

    [Tooltip("Optional: override layers for aim line stopping. If zero, uses crosshairSettings.stopLayers.")]
    public LayerMask aimLineStopLayers = 0;

    public float aimLinePadding = 0.02f;

    [Tooltip("If true, force the line to stay on the start point's Y (useful for top-down readability).")]
    public bool keepAimLineFlat = true;

    private PlayerInput playerInput;

    // Input actions
    private InputAction lookAction;
    private InputAction aimAction;

    // Input state
    public bool IsAiming { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool UsingMouseScheme { get; private set; }

    // Outputs (from crosshairSettings)
    public Vector3 AimWorldDir { get; private set; }
    public Vector3 AimPointClamped { get; private set; }
    public bool HasMouseAimPoint { get; private set; }
    public Vector3 MouseAimPoint { get; private set; }

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        SetAimLineVisible(false);
    }

    void OnEnable()
    {
        var actions = playerInput.actions;
        lookAction = actions["Look"];
        aimAction  = actions["Aim"];
        lookAction?.Enable();
        aimAction?.Enable();
    }

    void OnDisable()
    {
        lookAction?.Disable();
        aimAction?.Disable();
        SetAimLineVisible(false);
    }

    public float GetMoveSpeed(bool isCrouching, float fallbackSpeed)
    {
        if (!IsAiming) return fallbackSpeed;
        return isCrouching ? aimCrouchSpeed : aimWalkSpeed;
    }

    public Vector3 TickAimAndGetFacingDirection(Transform actor, Vector3 moveDirWorld, bool isCrouching)
    {
        IsAiming = aimAction != null && aimAction.IsPressed();
        LookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

        UsingMouseScheme = playerInput != null &&
                           playerInput.currentControlScheme == "Keyboard&Mouse";

        // default facing dir
        Vector3 facingDir = moveDirWorld;

        // CrosshairSettings computes aim point/dir for mouse or gamepad.
        if (crosshairSettings != null)
        {
            crosshairSettings.Tick(
                actor: actor,
                isCrouching: isCrouching,
                isAiming: IsAiming,
                lookInput: LookInput,
                usingMouseScheme: UsingMouseScheme,
                aimCamera: aimCamera,
                cameraTransform: cameraTransform
            );

            AimWorldDir       = crosshairSettings.AimWorldDir;
            AimPointClamped   = crosshairSettings.AimPointClamped;
            HasMouseAimPoint  = crosshairSettings.HasMouseAimPoint;
            MouseAimPoint     = crosshairSettings.MouseAimPoint;

            if (IsAiming && AimWorldDir.sqrMagnitude > 0.0001f)
                facingDir = AimWorldDir;
        }
        else
        {
            // Fallback if no crosshair provider is assigned.
            AimWorldDir = Vector3.zero;
            AimPointClamped = actor.position;
            HasMouseAimPoint = false;
            MouseAimPoint = Vector3.zero;
        }

        UpdateAimLine(actor, isCrouching);
        return facingDir;
    }

    public Vector3 GetRollDirection(Vector3 moveDirWorld)
    {
        if (IsAiming && AimWorldDir.sqrMagnitude > 0.001f) return AimWorldDir;
        if (moveDirWorld.sqrMagnitude > 0.001f) return moveDirWorld;

        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
    }

    void UpdateAimLine(Transform actor, bool isCrouching)
    {
        if (!showAimLine || aimLine == null)
            return;

        bool visible = IsAiming && AimWorldDir.sqrMagnitude > 0.0001f && aimLineLength > 0.01f;
        SetAimLineVisible(visible);

        if (!visible)
            return;

        // start transform: crouch/stand
        Transform startTf = isCrouching ? aimLineStartCrouching : aimLineStartStanding;
        if (startTf == null)
            startTf = isCrouching ? aimLineStartStanding : aimLineStartCrouching;

        Vector3 start = (startTf != null)
            ? startTf.position
            : actor.position + Vector3.up * aimLineStartHeight;

        // direction: use AimWorldDir on XZ
        Vector3 dir = AimWorldDir;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = actor.forward;
            dir.y = 0f;
        }
        dir.Normalize();

        Vector3 end = start + dir * aimLineLength;

        // stop layers: prefer override, otherwise use crosshairSettings.stopLayers
        LayerMask layers = aimLineStopLayers.value != 0
            ? aimLineStopLayers
            : (crosshairSettings != null ? crosshairSettings.stopLayers : 0);

        if (aimLineStopsByLayer && layers.value != 0)
        {
            if (Physics.Raycast(start, dir, out RaycastHit hit, aimLineLength, layers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point - dir * Mathf.Max(0f, aimLinePadding);
            }
        }

        if (keepAimLineFlat)
            end.y = start.y;

        aimLine.positionCount = 2;
        aimLine.SetPosition(0, start);
        aimLine.SetPosition(1, end);
    }

    void SetAimLineVisible(bool visible)
    {
        if (aimLine != null)
            aimLine.enabled = visible;
    }
}
