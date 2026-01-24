using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed Multipliers")]
    //The movement speeds of the players are based on animation root speed, so these are multipliers for the root motion speed.
    public float walkSpeed      = 1f;
    public float runSpeed       = 1.5f;
    public float crouchSpeed    = 0.6f;
    public float aimWalkSpeed   = 0.8f;
    public float aimCrouchSpeed = 0.5f;
    public float rollRMScale    = 1.0f;

    [Header("Gravity")]
    public float gravity = -9.81f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("Camera")]
    public Transform cameraTransform;   // pixel camera transform
    public Camera   aimCamera;          // separate camera for mouse ray

    [Header("Animation")]
    public Animator animator;
    public float   speedDampTime = 0.1f;

    [Header("Crosshair")]
    public Transform crosshair;
    public float     crosshairDistance   = 4f;
    public LayerMask crosshairGroundMask = ~0;

    private CharacterController controller;
    private PlayerInput         playerInput;

    // Input actions
    private InputAction moveAction;
    private InputAction runAction;
    private InputAction crouchAction;
    private InputAction lookAction;
    private InputAction aimAction;
    private InputAction rollAction;

    // State
    private bool isGrounded;
    private bool isCrouching;
    private bool isAiming;
    private bool isRolling;
    private bool wantsToMove;
    private bool wantsToRun;

    private Vector3 verticalVelocity;     // only Y used
    private Vector3 moveDirWorld;         // desired move direction (world)
    private Vector3 rollDirection;        // stored roll direction

    void Awake()
    {
        controller  = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (!animator)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = true; // use root motion
    }

    void OnEnable()
    {
        var actions = playerInput.actions;

        moveAction   = actions["Move"];
        runAction    = actions["Run"];
        crouchAction = actions["Crouch"];
        lookAction   = actions["Look"];
        aimAction    = actions["Aim"];
        rollAction   = actions["Roll"];

        moveAction?.Enable();
        runAction?.Enable();
        crouchAction?.Enable();
        lookAction?.Enable();
        aimAction?.Enable();
        rollAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
        runAction?.Disable();
        crouchAction?.Disable();
        lookAction?.Disable();
        aimAction?.Disable();
        rollAction?.Disable();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // ----- Ground & gravity -----
        isGrounded = controller.isGrounded;
        if (isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -0.5f;

        verticalVelocity.y += gravity * dt;

        // ----- Read input -----
        Vector2 moveInput = moveAction  != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        bool    runHeld   = runAction   != null && runAction.IsPressed();
        Vector2 lookInput = lookAction  != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        isAiming          = aimAction   != null && aimAction.IsPressed();

        // crouch toggle (ignore during roll)
        if (!isRolling && crouchAction != null && crouchAction.WasPerformedThisFrame() && isGrounded)
            isCrouching = !isCrouching;

        // movement input
        Vector3 inputDir       = new Vector3(moveInput.x, 0f, moveInput.y);
        float   inputMagnitude = Mathf.Clamp01(inputDir.magnitude);
        wantsToMove = !isRolling && inputMagnitude > 0.1f;

        if (wantsToMove)
            inputDir.Normalize();
        else
            inputDir = Vector3.zero;

        // crouch + run → stand up
        if (!isRolling && isCrouching && runHeld && wantsToMove && isGrounded)
            isCrouching = false;

        // run only when moving, not crouching, not aiming, not rolling
        wantsToRun = wantsToMove && runHeld && !isCrouching && !isAiming && !isRolling;

        // ----- Roll input -----
        bool rollPressed = rollAction != null && rollAction.WasPerformedThisFrame();
        if (rollPressed && isGrounded && !isRolling)
        {
            StartRoll(moveInput, lookInput);
        }

        // ----- Camera-relative move direction (for animation & SpeedX/Z) -----
        moveDirWorld = Vector3.zero;
        if (wantsToMove)
        {
            if (cameraTransform != null)
            {
                Vector3 camForward = cameraTransform.forward;
                Vector3 camRight   = cameraTransform.right;

                camForward.y = 0f;
                camRight.y   = 0f;

                camForward.Normalize();
                camRight.Normalize();

                moveDirWorld   = camForward * moveInput.y + camRight * moveInput.x;
                moveDirWorld.y = 0f;
                moveDirWorld.Normalize();
            }
            else
            {
                moveDirWorld = inputDir;
            }
        }

        // ----- Root Motion & animation speed multiplier -----
        float locomotionSpeedMult = 1f;

        if (isRolling)
        {
            locomotionSpeedMult = rollRMScale;
        }
        else if (isAiming)
        {
            locomotionSpeedMult = isCrouching ? aimCrouchSpeed : aimWalkSpeed;
        }
        else
        {
            if (isCrouching)
                locomotionSpeedMult = crouchSpeed;
            else if (wantsToRun)
                locomotionSpeedMult = runSpeed;
            else
                locomotionSpeedMult = walkSpeed;
        }

        if (animator != null)
        {
            animator.SetFloat("LocomotionSpeedMult", locomotionSpeedMult);
        }

        // ===== Aiming: facingDir (only rotation & crosshair) =====
        Vector3 facingDir        = moveDirWorld;   // default facing = move direction
        Vector3 aimWorldDir      = Vector3.zero;
        bool    hasMouseAimPoint = false;
        Vector3 mouseAimPoint    = Vector3.zero;

        if (isAiming)
        {
            Camera camForStick = cameraTransform != null
                ? cameraTransform.GetComponent<Camera>()
                : null;

            bool usingMouseScheme = playerInput != null &&
                                    playerInput.currentControlScheme == "Keyboard&Mouse";

            if (usingMouseScheme && aimCamera != null && Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Ray     ray      = aimCamera.ScreenPointToRay(mousePos);

                if (Physics.Raycast(ray, out RaycastHit hit, 100f, crosshairGroundMask))
                {
                    mouseAimPoint    = hit.point;
                    hasMouseAimPoint = true;

                    aimWorldDir   = mouseAimPoint - transform.position;
                    aimWorldDir.y = 0f;

                    if (aimWorldDir.sqrMagnitude > 0.001f)
                        facingDir = aimWorldDir.normalized;
                }
            }
            else if (lookInput.sqrMagnitude > 0.01f && camForStick != null)
            {
                Vector3 camForward = camForStick.transform.forward;
                Vector3 camRight   = camForStick.transform.right;
                camForward.y = 0f;
                camRight.y   = 0f;
                camForward.Normalize();
                camRight.Normalize();

                aimWorldDir   = camForward * lookInput.y + camRight * lookInput.x;
                aimWorldDir.y = 0f;

                if (aimWorldDir.sqrMagnitude > 0.001f)
                    facingDir = aimWorldDir.normalized;
            }
        }

        // during roll, force facing to rollDirection
        if (isRolling && rollDirection.sqrMagnitude > 0.001f)
            facingDir = rollDirection;

        // ----- Apply rotation -----
        if (facingDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(facingDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotationSpeed * dt
            );
        }

        // ----- Crosshair -----
        UpdateCrosshair(isAiming, aimWorldDir, hasMouseAimPoint, mouseAimPoint);

        // ----- Animator parameters (for blend trees) -----
        if (animator != null)
        {
            bool isMoving  = wantsToMove;
            bool isWalking = isMoving && !wantsToRun && !isCrouching;
            bool isRunning = wantsToRun;

            // local move dir for 2D blend tree（瞄准时）
            Vector3 localMove = Vector3.zero;
            if (moveDirWorld.sqrMagnitude > 0.0001f)
                localMove = transform.InverseTransformDirection(moveDirWorld) * inputMagnitude;

            if (!isAiming)
            {
                // 1D locomotion
                animator.SetFloat("Speed", inputMagnitude, speedDampTime, Time.deltaTime);
                animator.SetFloat("SpeedX", 0f, speedDampTime, Time.deltaTime);
                animator.SetFloat("SpeedZ", 0f, speedDampTime, Time.deltaTime);
            }
            else
            {
                // aiming locomotion uses 2D blend tree
                animator.SetFloat("Speed", 0f, speedDampTime, Time.deltaTime);
                animator.SetFloat("SpeedX", localMove.x, speedDampTime, Time.deltaTime);
                animator.SetFloat("SpeedZ", localMove.z, speedDampTime, Time.deltaTime);
            }

            animator.SetBool("IsCrouching", isCrouching);
            animator.SetBool("IsRunning",   isRunning);
            animator.SetBool("IsWalking",   isWalking);
            animator.SetBool("IsAiming",    isAiming);
            animator.SetBool("IsRolling",   isRolling);

            // 检查 roll 动画是否结束（确保 roll 状态有 tag "Roll"）
            if (isRolling)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsTag("Roll"))
                {
                    isRolling = false;
                    animator.SetBool("IsRolling", false);
                }
            }
        }
    }

    // ===== Root Motion application =====
    void OnAnimatorMove()
    {
        if (animator == null || controller == null)
            return;

        Vector3 delta = animator.deltaPosition;

        if (!isRolling && !wantsToMove)
            delta = Vector3.zero;

        // add gravity on Y
        delta.y += verticalVelocity.y * Time.deltaTime;

        controller.Move(delta);
    }

    // =============== Roll helpers ===============
    void StartRoll(Vector2 moveInput, Vector2 lookInput)
    {
        Vector3 dir = transform.forward;

        // move-based dir
        Vector3 moveDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight   = cameraTransform.right;
            camForward.y = 0f;
            camRight.y   = 0f;
            camForward.Normalize();
            camRight.Normalize();

            moveDir = camForward * moveInput.y + camRight * moveInput.x;
        }

        // aim-based dir
        Vector3 aimDir = moveDir;
        if (isAiming && cameraTransform != null)
        {
            Camera  cam        = cameraTransform.GetComponent<Camera>();
            Vector3 camForward = (cam ? cam.transform.forward : cameraTransform.forward);
            Vector3 camRight   = (cam ? cam.transform.right   : cameraTransform.right);
            camForward.y = 0f;
            camRight.y   = 0f;
            camForward.Normalize();
            camRight.Normalize();

            aimDir = camForward * lookInput.y + camRight * lookInput.x;
        }

        if (aimDir.sqrMagnitude > 0.001f)
            dir = aimDir.normalized;
        else if (moveDir.sqrMagnitude > 0.001f)
            dir = moveDir.normalized;

        dir.y = 0f;
        dir.Normalize();

        rollDirection = dir;
        isRolling     = true;

        // face roll direction immediately
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

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

    // =============== Crosshair helper ===============
    void UpdateCrosshair(bool aiming, Vector3 aimWorldDir, bool hasMouseAimPoint, Vector3 mouseAimPoint)
    {
        if (crosshair == null)
            return;

        if (!aiming)
        {
            crosshair.gameObject.SetActive(false);
            return;
        }

        crosshair.gameObject.SetActive(true);

        // Mouse: use ray hit point
        if (hasMouseAimPoint)
        {
            crosshair.position = mouseAimPoint + Vector3.up * 0.02f;
            return;
        }

        // Controller: use direction ray
        if (aimWorldDir.sqrMagnitude < 0.001f)
        {
            crosshair.gameObject.SetActive(false);
            return;
        }

        Vector3 start = transform.position + Vector3.up * 0.5f;
        Vector3 dir   = aimWorldDir.normalized;

        if (Physics.Raycast(start, dir, out RaycastHit hit, 100f, crosshairGroundMask))
        {
            crosshair.position = hit.point + Vector3.up * 0.02f;
        }
        else
        {
            crosshair.position = start + dir * crosshairDistance;
        }
    }
}
