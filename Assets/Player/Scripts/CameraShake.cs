using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3.x

public class CameraNoiseByMovement : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private CinemachineCamera cmCamera;
    [SerializeField] private CinemachineBasicMultiChannelPerlin noise;

    [Header("Noise Settings")]
    [SerializeField] private float walkAmplitude = 0.25f;
    [SerializeField] private float runAmplitude = 0.6f;
    [SerializeField] private float frequency = 2.0f;

    [Header("Ortho Size Offset")]
    [SerializeField] private float walkSizeOffset = -0.15f;
    [SerializeField] private float crouchSizeOffset = -0.45f;
    [SerializeField] private float runSizeOffset = -0.30f;

    [Header("Aiming Hold Zoom")]
    [SerializeField] private float aimZoomDelay = 5f;
    [SerializeField] private float aimHoldZoomOffsetStanding = -0.20f;
    [SerializeField] private float aimHoldZoomOffsetCrouching = -0.10f;
    [SerializeField] private float aimZoomInDamping = 0.18f;
    [SerializeField] private float aimZoomOutDamping = 0.12f;

    [Header("Size Damping")]
    [SerializeField] private float enterDamping = 0.12f;
    [SerializeField] private float exitDamping = 0.20f;

    [Header("Noise Smoothing")]
    [SerializeField] private float noiseBlendSpeed = 5f;

    private static readonly int IsWalkingHash   = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningHash   = Animator.StringToHash("IsRunning");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsAimingHash    = Animator.StringToHash("IsAiming");
    private static readonly int SpeedHash       = Animator.StringToHash("Speed");

    private float defaultOrthoSize;
    private float orthoSizeVelocity;

    private bool wasAiming = false;
    private bool wasAimHoldZoomActive = false;
    private float aimTimer = 0f;
    private float lockedAimSize = 0f;
    private bool aimEnteredFromCrouch = false;

    private void Reset()
    {
        animator = GetComponentInParent<Animator>();

        if (cmCamera == null)
            cmCamera = GetComponent<CinemachineCamera>();

        if (noise == null)
            noise = GetComponent<CinemachineBasicMultiChannelPerlin>();
    }

    private void Awake()
    {
        if (cmCamera == null)
            cmCamera = GetComponent<CinemachineCamera>();

        if (cmCamera != null)
            defaultOrthoSize = cmCamera.Lens.OrthographicSize;

        if (noise != null)
            noise.FrequencyGain = frequency;
    }

    private void Update()
    {
        if (animator == null || cmCamera == null)
            return;

        bool isWalking   = animator.GetBool(IsWalkingHash);
        bool isRunning   = animator.GetBool(IsRunningHash);
        bool isCrouching = animator.GetBool(IsCrouchingHash);
        bool isAiming    = animator.GetBool(IsAimingHash);
        float speed      = animator.GetFloat(SpeedHash);

        // -------- Noise --------
        float targetAmplitude = 0f;

        if (isAiming)
        {
            targetAmplitude = 0f;
        }
        else if (isRunning)
        {
            targetAmplitude = runAmplitude;
        }
        else if (isWalking)
        {
            targetAmplitude = walkAmplitude;
        }

        if (noise != null)
        {
            noise.AmplitudeGain = Mathf.MoveTowards(
                noise.AmplitudeGain,
                targetAmplitude,
                noiseBlendSpeed * Time.deltaTime
            );

            noise.FrequencyGain = frequency;
        }

        // -------- Aiming timer / size lock --------
        LensSettings lens = cmCamera.Lens;

        if (isAiming)
        {
            if (!wasAiming)
            {
                lockedAimSize = lens.OrthographicSize;   // lock current size when entering aim
                aimEnteredFromCrouch = isCrouching;      // remember crouch state at aim enter
                aimTimer = 0f;
                orthoSizeVelocity = 0f;
                wasAimHoldZoomActive = false;
            }

            aimTimer += Time.deltaTime;
        }
        else
        {
            aimTimer = 0f;
            wasAimHoldZoomActive = false;
        }

        // -------- Orthographic Size --------
        float targetSize;
        float smoothTime;

        if (isAiming)
        {
            bool shouldAimHoldZoom =
                aimTimer >= aimZoomDelay &&
                speed < 0.1f &&
                !isWalking &&
                !isRunning;

            float holdOffset = aimEnteredFromCrouch
                ? aimHoldZoomOffsetCrouching
                : aimHoldZoomOffsetStanding;

            targetSize = shouldAimHoldZoom
                ? lockedAimSize + holdOffset
                : lockedAimSize;

            if (shouldAimHoldZoom)
            {
                smoothTime = wasAimHoldZoomActive ? aimZoomInDamping : aimZoomInDamping;
            }
            else
            {
                smoothTime = wasAimHoldZoomActive ? aimZoomOutDamping : 0.0001f;
            }

            wasAimHoldZoomActive = shouldAimHoldZoom;
        }
        else
        {
            targetSize = defaultOrthoSize;

            if (isRunning)
                targetSize = defaultOrthoSize + runSizeOffset;
            else if (isCrouching)
                targetSize = defaultOrthoSize + crouchSizeOffset;
            else if (isWalking)
                targetSize = defaultOrthoSize + walkSizeOffset;

            bool isMovementCameraState = isRunning || isWalking || isCrouching;
            smoothTime = isMovementCameraState ? enterDamping : exitDamping;
        }

        smoothTime = Mathf.Max(0.0001f, smoothTime);

        lens.OrthographicSize = Mathf.SmoothDamp(
            lens.OrthographicSize,
            targetSize,
            ref orthoSizeVelocity,
            smoothTime
        );
        cmCamera.Lens = lens;

        wasAiming = isAiming;
    }
}