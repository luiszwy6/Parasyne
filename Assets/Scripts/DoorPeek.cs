using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class DoorPeekHoldAction : MonoBehaviour
{
    [Header("Input (PlayerInput)")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string actionName = "Action"; 
    private InputAction action;

    [Header("Block Aim Input")]
    [SerializeField] private string aimActionName = "Aim";
    private InputAction aimAction;

    [Header("Hold Settings")]
    [SerializeField] private float holdSeconds = 0.20f;
    [SerializeField] private bool lockPlayerMovementWhilePeeking = true;

    [Header("Overlap Check")]
    [SerializeField] private Transform checkOrigin;
    [SerializeField] private Vector3 checkOffset = new Vector3(0f, 1.0f, 0.4f);
    [SerializeField] private bool offsetIsLocal = true;
    [SerializeField] private Vector3 halfExtents = new Vector3(0.6f, 1.0f, 0.6f);
    [SerializeField] private LayerMask playerMask;

    [Header("Peek Camera (Cinemachine 3)")]
    [SerializeField] private CinemachineCamera peekCam;
    [SerializeField] private int peekPriority = 100;
    [SerializeField] private int normalPriority = 0;

    [Header("Reveal Other Room (Optional)")]
    [SerializeField] private Room roomToReveal;
    [SerializeField] private bool pauseEnemyAIWhilePeeking = false;

    [Header("Debug")]
    [SerializeField] private bool debugGizmos = true;

    private Coroutine holdRoutine;
    private bool isPeeking;

    private void Reset()
    {
        if (checkOrigin == null) checkOrigin = transform;
        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInput>();
        if (peekCam == null) peekCam = GetComponentInChildren<CinemachineCamera>(true);
    }

    private void Awake()
    {
        if (checkOrigin == null) checkOrigin = transform;
        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInput>();

        if (playerInput != null)
            action = playerInput.actions[actionName];

        if (peekCam != null)
            peekCam.Priority = normalPriority;

        if (playerInput != null)
        aimAction = playerInput.actions[aimActionName];

    }

    private void OnEnable()
    {
        if (action == null) return;

        action.started += OnActionStarted;
        action.canceled += OnActionCanceled;

        action.Enable(); 
    }

    private void OnDisable()
    {
        if (action == null) return;

        action.started -= OnActionStarted;
        action.canceled -= OnActionCanceled;

        ForceExitPeek();
    }

    private void OnActionStarted(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerInZone()) return;

        if (holdRoutine != null) StopCoroutine(holdRoutine);
        holdRoutine = StartCoroutine(HoldToPeek());
    }

    private void OnActionCanceled(InputAction.CallbackContext ctx)
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        if (isPeeking)
            ExitPeek();
    }

    private IEnumerator HoldToPeek()
    {
        float t = 0f;
        while (t < holdSeconds)
        {
            if (!IsPlayerInZone())
            {
                holdRoutine = null;
                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        holdRoutine = null;
        EnterPeek();
    }

    private void EnterPeek()
    {
        if (isPeeking) return;
        isPeeking = true;

        if (roomToReveal != null)
        {
            roomToReveal.SetVisible(true);

            if (pauseEnemyAIWhilePeeking)
                BroadcastPause(roomToReveal, true);
        }

        if (peekCam != null)
            peekCam.Priority = peekPriority;

        if (lockPlayerMovementWhilePeeking)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player)
            {
                var pm = player.GetComponent<PlayerMovement>();
                if (pm != null) pm.externalMovementLock = true;
            }
        }

        if (aimAction != null) aimAction.Disable();
    }

    private void ExitPeek()
    {
        if (!isPeeking) return;
        isPeeking = false;

        if (peekCam != null)
            peekCam.Priority = normalPriority;

        if (roomToReveal != null)
        {
            if (pauseEnemyAIWhilePeeking)
                BroadcastPause(roomToReveal, false);

            roomToReveal.SetVisible(false);
        }

        if (lockPlayerMovementWhilePeeking)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player)
            {
                var pm = player.GetComponent<PlayerMovement>();
                if (pm != null) pm.externalMovementLock = false;
            }
        }

        if (aimAction != null) aimAction.Enable();


    }

    private void ForceExitPeek()
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        if (isPeeking)
            ExitPeek();
    }

    private void BroadcastPause(Room room, bool paused)
    {
        // 省事版：给房间内对象发 SetPaused(bool)（没有实现也不会报错）
        foreach (var tr in room.GetComponentsInChildren<Transform>(true))
            tr.gameObject.SendMessage("SetPaused", paused, SendMessageOptions.DontRequireReceiver);
    }

    private void GetOverlapPose(out Vector3 center, out Quaternion rotation)
    {
        Transform o = checkOrigin != null ? checkOrigin : transform;
        rotation = o.rotation;
        center = offsetIsLocal ? o.TransformPoint(checkOffset) : o.position + checkOffset;
    }

    private bool IsPlayerInZone()
    {
        GetOverlapPose(out Vector3 center, out Quaternion rot);

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rot,
            playerMask,
            QueryTriggerInteraction.Ignore
        );

        return hits != null && hits.Length > 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        GetOverlapPose(out Vector3 center, out Quaternion rot);
        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.DrawSphere(Vector3.zero, 0.05f);
    }
}
