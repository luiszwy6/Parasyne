using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class OcclusionPitchSwitch : CinemachineExtension
{
    [Header("Pitch")]
    public float normalPitch = 45f;
    public float occludedPitch = 60f;

    [Header("Damping")]
    public float smoothIn = 0.20f;
    public float smoothOut = 0.20f;

    [Header("Y - axis lock")]
    public bool lockYaw = true;
    public float lockedYaw = 0f;

    [Header("Z - axis lock")]
    public bool lockRoll = true;
    public float lockedRoll = 0f;

    [Header("Switch Cooldown")]
    public float switchCooldown = 0.3f;

    [Header("Trigger Occlusion (recommended)")]
    [Tooltip("Put this on the SAME object as probeReference (e.g., cameraPivot/AimPivot).")]
    public OcclusionTriggerReporter triggerReporter;

    [Tooltip("Optional: if triggerReporter is null, try GetComponent on probeReference.")]
    public Transform probeReference;

    float _currentPitch;
    float _vel;

    bool _occludedState;
    float _cooldownTimer;

    protected override void OnEnable()
    {
        base.OnEnable();
        _currentPitch = NormalizeAngle(transform.eulerAngles.x);
        _vel = 0f;
        _occludedState = false;
        _cooldownTimer = 0f;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Aim) return;

        if (deltaTime <= 0f) deltaTime = Time.unscaledDeltaTime;

        // Auto-find reporter if not assigned
        if (triggerReporter == null && probeReference != null)
            triggerReporter = probeReference.GetComponent<OcclusionTriggerReporter>();

        bool wantOccluded = (triggerReporter != null) && triggerReporter.IsOccluded;

        // Cooldown to prevent rapid switching
        _cooldownTimer -= deltaTime;
        if (wantOccluded != _occludedState && _cooldownTimer <= 0f)
        {
            _occludedState = wantOccluded;
            _cooldownTimer = switchCooldown;
        }

        float targetPitch = _occludedState ? occludedPitch : normalPitch;
        float smooth = targetPitch > _currentPitch ? smoothIn : smoothOut;

        _currentPitch = Mathf.SmoothDampAngle(
            _currentPitch,
            targetPitch,
            ref _vel,
            smooth,
            Mathf.Infinity,
            deltaTime
        );

        var e = state.RawOrientation.eulerAngles;

        float yaw = lockYaw ? lockedYaw : e.y;
        float roll = lockRoll ? lockedRoll : e.z;

        state.RawOrientation = Quaternion.Euler(_currentPitch, yaw, roll);
    }

    static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }
}
