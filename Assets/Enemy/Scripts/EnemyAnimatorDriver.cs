using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyAnimatorDriver : MonoBehaviour
{
    [Header("Refs")]
    public Enemy enemy;
    public NavMeshAgent agent;
    public EnemyPartBreaks partBreaks;

    [Header("Animator Speed Feed")]
    public float speedMultiplier = 1f;
    public float moveEpsilon = 0.02f;
    public Transform locomotionSpace;

    [Header("Turn Feed")]
    public string turnParam = "Turn";
    public float fullTurnAngle = 90f;
    public float turnDeadZone = 3f;
    public float turnDampTime = 0.08f;

    [Header("Pre-Turn Gate")]
    [Tooltip("If angle to desired path direction exceeds this, stop moving and rotate first.")]
    public float stopMoveTurnAngle = 35f;

    [Tooltip("When angle falls below this, movement resumes.")]
    public float resumeMoveTurnAngle = 10f;

    [Tooltip("Manual rotate speed while pre-turning, in deg/s.")]
    public float preTurnRotateSpeed = 240f;

    [Header("Animator Param Names")]
    public string speedParam = "Speed";
    public string speedXParam = "SpeedX";
    public string speedZParam = "SpeedZ";

    [Header("Debug")]
    public bool logLegStateChanges = false;
    public bool debugPreTurning = false;
    public float debugSignedAngle = 0f;

    private EnemyPartBreaks.LegLocomotionState _currentLegState = EnemyPartBreaks.LegLocomotionState.Normal;

    private float _baseRequestedAgentSpeed = 0f;
    private float _lastAppliedAgentSpeed = -1f;
    private bool _baseSpeedInitialized = false;

    private bool _isPreTurning = false;

    void Awake()
    {
        if (!enemy) enemy = GetComponent<Enemy>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!partBreaks) partBreaks = GetComponent<EnemyPartBreaks>();

        if (enemy && !enemy.animator) enemy.animator = GetComponentInChildren<Animator>(true);
        if (!locomotionSpace) locomotionSpace = transform;

        // We want full control over "move now or rotate first".
        if (agent != null)
            agent.updateRotation = false;
    }

    void Update()
    {
        if (!enemy || !enemy.animator || !agent) return;

        UpdateLegBreakAnimatorAndMovement();

        Vector3 desiredDir = GetDesiredDirection();
        UpdatePreTurnState(desiredDir);
        ApplyRotationIfNeeded(desiredDir);
        ApplyMovementGate();
        UpdateLocomotionParams(desiredDir);
    }

    private Vector3 GetDesiredDirection()
    {
        Vector3 desiredDir = agent.desiredVelocity;
        desiredDir.y = 0f;

        if (desiredDir.sqrMagnitude < 0.0001f && agent.hasPath)
        {
            desiredDir = agent.steeringTarget - locomotionSpace.position;
            desiredDir.y = 0f;
        }

        return desiredDir.sqrMagnitude > 0.0001f ? desiredDir.normalized : Vector3.zero;
    }

    private void UpdatePreTurnState(Vector3 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.0001f)
        {
            _isPreTurning = false;
            debugSignedAngle = 0f;
            debugPreTurning = false;
            return;
        }

        float signedAngle = Vector3.SignedAngle(
            locomotionSpace.forward,
            desiredDir,
            Vector3.up
        );

        debugSignedAngle = signedAngle;

        float absAngle = Mathf.Abs(signedAngle);

        if (!_isPreTurning)
        {
            if (absAngle >= stopMoveTurnAngle)
                _isPreTurning = true;
        }
        else
        {
            if (absAngle <= resumeMoveTurnAngle)
                _isPreTurning = false;
        }

        debugPreTurning = _isPreTurning;
    }

    private void ApplyRotationIfNeeded(Vector3 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);

        if (_isPreTurning)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                preTurnRotateSpeed * Time.deltaTime
            );
        }
        else
        {
            // Optional: keep a little path-following turn while moving
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                agent.angularSpeed * Time.deltaTime
            );
        }
    }

    private void ApplyMovementGate()
    {
        agent.isStopped = _isPreTurning;
    }

    private void UpdateLocomotionParams(Vector3 desiredDir)
    {
        Vector3 worldVel = _isPreTurning ? Vector3.zero : agent.velocity;
        worldVel.y = 0f;

        Vector3 localVel = locomotionSpace.InverseTransformDirection(worldVel);
        localVel *= speedMultiplier;

        float speedX = Mathf.Abs(localVel.x) < moveEpsilon ? 0f : localVel.x;
        float speedZ = Mathf.Abs(localVel.z) < moveEpsilon ? 0f : localVel.z;
        float speed = new Vector2(speedX, speedZ).magnitude;

        enemy.animator.SetFloat(speedParam, speed);
        enemy.animator.SetFloat(speedXParam, speedX);
        enemy.animator.SetFloat(speedZParam, speedZ);

        UpdateTurnParam(desiredDir);
    }

    private void UpdateTurnParam(Vector3 desiredDir)
    {
        float turn = 0f;

        if (desiredDir.sqrMagnitude > 0.0001f)
        {
            float signedAngle = Vector3.SignedAngle(
                locomotionSpace.forward,
                desiredDir,
                Vector3.up
            );

            if (Mathf.Abs(signedAngle) < turnDeadZone)
                signedAngle = 0f;

            turn = Mathf.Clamp(signedAngle / Mathf.Max(1f, fullTurnAngle), -1f, 1f);
        }

        enemy.animator.SetFloat(turnParam, turn, turnDampTime, Time.deltaTime);
    }

    private void UpdateLegBreakAnimatorAndMovement()
    {
        if (partBreaks == null)
        {
            SetLegAnimFlags(false, false, false);
            TrackAndApplySpeedMultiplier(1f);
            return;
        }

        partBreaks.TickLegBreakReact(Time.deltaTime);

        if (partBreaks.ConsumeLegBreakReactTriggerRequest() && enemy.animator != null)
        {
            enemy.animator.ResetTrigger("IsLongStun");
            enemy.animator.ResetTrigger("IsLegBreakReact");
            enemy.animator.SetTrigger("IsLegBreakReact");
        }

        var nextState = partBreaks.GetLegLocomotionState();

        bool hurtWalkL = partBreaks.GetIsHurtWalkL();
        bool hurtWalkR = partBreaks.GetIsHurtWalkR();
        bool isCrawl = partBreaks.GetIsCrawl();

        SetLegAnimFlags(hurtWalkL, hurtWalkR, isCrawl);
        TrackAndApplySpeedMultiplier(partBreaks.GetLegMoveSpeedMultiplier());

        if (logLegStateChanges && nextState != _currentLegState)
        {
            Debug.Log($"[{name}] Leg locomotion: {_currentLegState} -> {nextState} | L={partBreaks.GetLeftLegPercent():P0}, R={partBreaks.GetRightLegPercent():P0}");
        }

        _currentLegState = nextState;
    }

    private void SetLegAnimFlags(bool hurtL, bool hurtR, bool crawl)
    {
        enemy.animator.SetBool("IsHurtWalk_L", hurtL);
        enemy.animator.SetBool("IsHurtWalk_R", hurtR);
        enemy.animator.SetBool("IsCrawl", crawl);
    }

    private void TrackAndApplySpeedMultiplier(float movementMultiplier)
    {
        movementMultiplier = Mathf.Clamp(movementMultiplier, 0f, 1f);

        if (!_baseSpeedInitialized)
        {
            _baseRequestedAgentSpeed = agent.speed;
            _baseSpeedInitialized = true;
        }

        if (_lastAppliedAgentSpeed < 0f || !Mathf.Approximately(agent.speed, _lastAppliedAgentSpeed))
        {
            _baseRequestedAgentSpeed = agent.speed;
        }

        float targetSpeed = _baseRequestedAgentSpeed * movementMultiplier;

        if (!Mathf.Approximately(agent.speed, targetSpeed))
        {
            agent.speed = targetSpeed;
        }

        _lastAppliedAgentSpeed = targetSpeed;
    }
}