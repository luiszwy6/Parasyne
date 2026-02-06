using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

// PlayerMovement: CharacterController locomotion.
// Key rules:
// - Movement is camera-relative, but facing is driven by PlayerAimSettings while aiming.
// - externalMovementLock blocks move/run/roll but keeps crouch + aiming available.

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerAimSettings))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Parameters")]
    public float walkSpeed   = 4f;
    public float runSpeed    = 6f;
    public float crouchSpeed = 2f;
    public float gravity     = -9.81f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("Camera transform")]
    public Transform cameraTransform;   // used ONLY for movement-relative direction

    [Header("Animation")]
    public float speedDampTime = 0.1f;

    [Header("Roll Settings")]
    public float rollDistance = 4f;
    public float rollDuration = 0.5f;
    public float rollCooldown = 0.8f;

    [Header("External Locks")]
    public bool externalMovementLock = false; // When true, block move/run/roll but keep crouch + aiming


    private Animator animator;
    private CharacterController controller;
    private PlayerInput playerInput;
    private PlayerAimSettings aimSettings;

    // Input actions (movement only)
    private InputAction moveAction;
    private InputAction runAction;
    private InputAction crouchAction;
    private InputAction rollAction;

    private InputAction stealthTakeDownAction;

    // State
    private Vector3 velocity;
    private bool isGrounded;
    private bool isCrouching;
    private bool isAiming;

    private bool isStealthy;
    private bool isTakingDown;
    private float takedownDuration = 5.0f;
    private float takedownTimer = 0f;

    private bool isRolling;
    private float rollTimer;
    private float rollRemainingDistance;
    private Vector3 rollDirection;
    private float lastRollTime = -999f;

    void Awake()
    {
        controller  = GetComponent<CharacterController>();
        animator    = GetComponentInChildren<Animator>();
        playerInput = GetComponent<PlayerInput>();
        aimSettings = GetComponent<PlayerAimSettings>();
    }

    void OnEnable()
    {
        var actions = playerInput.actions;

        moveAction   = actions["Move"];
        runAction    = actions["Run"];
        crouchAction = actions["Crouch"];
        rollAction   = actions["Roll"];
        stealthTakeDownAction = actions["StealthTakedown"];

        moveAction?.Enable();
        runAction?.Enable();
        crouchAction?.Enable();
        rollAction?.Enable();
        stealthTakeDownAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
        runAction?.Disable();
        crouchAction?.Disable();
        rollAction?.Disable();
        stealthTakeDownAction?.Disable();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // ----- Ground & gravity -----
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f)
            velocity.y = -0.5f;

        // Rolling state
        if (isRolling)
        {
            UpdateRoll(dt);
            return;
        }

        if(isTakingDown)
        {
            UpdateTakedown(dt);
            return;
        }

        // ----- Read input -----
        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        bool runHeld      = runAction != null && runAction.IsPressed();
        // External lock: block movement-related inputs only
        if (externalMovementLock)
        {
            moveInput = Vector2.zero;
            runHeld = false;
        }

        // Toggle crouch
        if (crouchAction != null && crouchAction.WasPerformedThisFrame() && isGrounded)
            isCrouching = !isCrouching;

        // ----- Movement input -----
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);
        bool wantsToMove = inputMagnitude > 0.1f;

        if (wantsToMove) inputDir.Normalize();
        else inputDir = Vector3.zero;

        // Determine aiming state from aimSettings (reads Aim action internally)
        isAiming = aimSettings != null && aimSettings.IsAiming;

        // Crouch + Run → stand up if moving
        if (isCrouching && runHeld && wantsToMove && isGrounded && !isAiming)
            isCrouching = false;

        // Only running when moving and not crouching / aiming
        bool wantsToRun = wantsToMove && runHeld && !isCrouching && !isAiming;

        // ===== Camera-relative movement direction (ONLY for movement) =====
        Vector3 moveDirWorld = inputDir;
        if (cameraTransform != null && wantsToMove)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight   = cameraTransform.right;

            camForward.y = 0f;
            camRight.y   = 0f;

            camForward.Normalize();
            camRight.Normalize();

            moveDirWorld = camForward * moveInput.y + camRight * moveInput.x;
            moveDirWorld.y = 0f;
            moveDirWorld.Normalize();
        }

        // Roll input
        bool rollPressed = !externalMovementLock && rollAction != null && rollAction.WasPerformedThisFrame();
        bool canRoll = Time.time >= lastRollTime + rollCooldown;

        if (rollPressed && isGrounded && !isRolling && canRoll)
        {
            StartRoll(moveDirWorld);
            UpdateRoll(dt);
            return;
        }
        // Stealth Takedown input
        bool takedownPressed = stealthTakeDownAction != null && stealthTakeDownAction.WasPerformedThisFrame();
        isStealthy = isCrouching;
        if(takedownPressed && isGrounded && !isTakingDown && isStealthy)
        {
            StartTakedown();
            UpdateTakedown(dt);
            return;
        }
        // ----- Base speed (non-aim) -----
        float baseSpeed;
        if (isCrouching) baseSpeed = crouchSpeed;
        else if (wantsToRun) baseSpeed = runSpeed;
        else baseSpeed = walkSpeed;

        // ----- Final speed (aim settings can override when aiming) -----
        float currentSpeed = aimSettings != null
            ? aimSettings.GetMoveSpeed(isCrouching, baseSpeed)
            : baseSpeed;

        // Final movement vector (independent of facing)
        Vector3 horizontal = moveDirWorld * currentSpeed * inputMagnitude;

        // ===== Facing (rotation) comes from aimSettings =====
        Vector3 facingDir = moveDirWorld; // fallback
        if (aimSettings != null)
        {
            facingDir = aimSettings.TickAimAndGetFacingDirection(transform, moveDirWorld, isCrouching);

            isAiming = aimSettings.IsAiming; // keep in sync
        }

        // ----- Apply rotation -----
        if (facingDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(facingDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * dt);
        }

        // ----- Move + gravity -----
        velocity.y += gravity * dt;
        Vector3 motion = horizontal;
        motion.y += velocity.y;
        controller.Move(motion * dt);

        // ----- Animator -----
        if (animator != null)
        {
            bool isMoving  = wantsToMove;
            bool isRunning = wantsToRun;
            bool isWalking = isMoving && !isRunning && !isCrouching;

            Vector3 localMove = Vector3.zero;
            if (horizontal.sqrMagnitude > 0.0001f)
                localMove = transform.InverseTransformDirection(horizontal).normalized * inputMagnitude;

            if (!isAiming)
            {
                animator.SetFloat("Speed", inputMagnitude, speedDampTime, dt);
                animator.SetFloat("SpeedX", 0f, speedDampTime, dt);
                animator.SetFloat("SpeedZ", 0f, speedDampTime, dt);
            }
            else
            {
                animator.SetFloat("Speed", 0f, speedDampTime, dt);
                animator.SetFloat("SpeedX", localMove.x, speedDampTime, dt);
                animator.SetFloat("SpeedZ", localMove.z, speedDampTime, dt);
            }

            animator.SetBool("IsCrouching", isCrouching);
            animator.SetBool("IsRunning",   isRunning);
            animator.SetBool("IsWalking",   isWalking);
            animator.SetBool("IsAiming",    isAiming);
            animator.SetBool("IsTakingDown", isTakingDown);
        }
    }

    // =============== Roll helpers ===============
    void StartRoll(Vector3 moveDirWorld)
    {
        Vector3 dir = moveDirWorld;

        if (aimSettings != null)
            dir = aimSettings.GetRollDirection(moveDirWorld);

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.Normalize();

        rollDirection = dir;
        rollRemainingDistance = rollDistance;
        rollTimer = rollDuration;
        isRolling = true;
        lastRollTime = Time.time;

        if (animator != null)
        {
            animator.ResetTrigger("RollTrigger");
            animator.SetTrigger("RollTrigger");
            animator.SetBool("IsRolling", true);

            Vector3 localRollDir = transform.InverseTransformDirection(dir);
            animator.SetFloat("RollDirX", localRollDir.x);
            animator.SetFloat("RollDirY", localRollDir.z);
        }
    }

    void UpdateRoll(float dt)
    {
        float rollSpeed = rollDistance / Mathf.Max(rollDuration, 0.0001f);

        float step = rollSpeed * dt;
        if (step > rollRemainingDistance)
            step = rollRemainingDistance;

        Vector3 motion = rollDirection * step;
        rollRemainingDistance -= step;
        controller.Move(motion);

        velocity.y += gravity * dt;
        controller.Move(new Vector3(0f, velocity.y, 0f) * dt);

        if (rollDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(rollDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * dt);
        }

        rollTimer -= dt;

        if (rollTimer <= 0f || rollRemainingDistance <= 0f)
        {
            isRolling = false;
            if (animator != null)
                animator.SetBool("IsRolling", false);
        }
    }
    void StartTakedown()
    {
        isTakingDown = true;
        takedownTimer = 0f;
        externalMovementLock = true;
        if (animator != null)
        {
            animator.ResetTrigger("TakeDown");
            animator.SetTrigger("TakeDown");
            animator.SetBool("IsTakingDown", true);
        }
    }
    void UpdateTakedown(float dt)
    {
        takedownTimer += dt;
        if (takedownTimer >= takedownDuration)
        {
            isTakingDown = false;
            takedownTimer = 0f;
            externalMovementLock = false;
            if (animator != null)
            {
                animator.SetBool("IsTakingDown", false);
            }
        }
    }
}
