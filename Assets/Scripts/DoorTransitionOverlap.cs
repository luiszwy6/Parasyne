using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class DoorTransitionOverlap : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private Room targetRoom;
    [SerializeField] private Transform targetSpawnPoint;

    [Header("Input (PlayerInput)")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string interactActionName = "Interact";
    private InputAction interactAction;

    [Header("Lock Input During Transition")]
    [SerializeField] private bool lockAllPlayerInputDuringTransition = true;
    [SerializeField] private bool reactivateInputAfterTransition = true;

    [Header("Overlap Check")]
    [SerializeField] private Transform checkOrigin;
    [SerializeField] private Vector3 checkOffset = new Vector3(0f, 1.0f, 0.4f);
    [SerializeField] private bool offsetIsLocal = true;
    [SerializeField] private Vector3 halfExtents = new Vector3(0.6f, 1.0f, 0.6f);
    [SerializeField] private LayerMask playerMask;

    [Header("Timing")]
    [SerializeField] private float blackHoldSeconds = 0.10f;
    [SerializeField] private float postTeleportIgnoreSeconds = 0.25f;

    [Header("Teleport Safety")]
    [SerializeField] private float spawnForwardOffset = 0.8f;
    [SerializeField] private bool raycastSnapToGround = true;
    [SerializeField] private float groundRayStartUp = 2.0f;
    [SerializeField] private float groundRayLength = 6.0f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool debugGizmos = true;

    private static bool IsTransitioning;
    private float ignoreUntilUnscaledTime;

    private void Reset()
    {
        if (checkOrigin == null) checkOrigin = transform;
        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInput>();
    }

    private void Awake()
    {
        if (checkOrigin == null) checkOrigin = transform;
        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInput>();

        if (playerInput != null)
            interactAction = playerInput.actions[interactActionName];
    }

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.performed += OnInteractPerformed;
            interactAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.performed -= OnInteractPerformed;
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (IsTransitioning) return;
        if (Time.unscaledTime < ignoreUntilUnscaledTime) return;
        if (!IsPlayerInZone()) return;

        StartCoroutine(DoTransition());
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

    private IEnumerator DoTransition()
    {
        if (IsTransitioning) yield break;
        IsTransitioning = true;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (!player)
        {
            IsTransitioning = false;
            yield break;
        }

        Transform playerTf = player.transform;

        // Cache player input (prefer the one on player)
        PlayerInput pi = playerInput;
        if (pi == null) pi = player.GetComponent<PlayerInput>();
        bool inputWasActive = true;

        // Lock all input
        if (lockAllPlayerInputDuringTransition && pi != null)
        {
            inputWasActive = pi.inputIsActive;
            pi.DeactivateInput();
        }

        // Lock movement logic (if you still want it)
        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.externalMovementLock = true;

        // Fade out
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut();

        // Pause world
        float prevScale = Time.timeScale;
        Time.timeScale = 0f;

        // Switch room visuals
        if (RoomManager.Instance != null && targetRoom != null)
            RoomManager.Instance.SwitchTo(targetRoom);

        // -------------------- Teleport (CC safe + snap BEFORE enabling CC) --------------------
        if (targetSpawnPoint != null)
        {
            Vector3 safePos = targetSpawnPoint.position + targetSpawnPoint.forward * spawnForwardOffset;
            Quaternion safeRot = targetSpawnPoint.rotation;

            CharacterController cc = player.GetComponent<CharacterController>();

            // 1) Disable CC first
            if (cc != null) cc.enabled = false;

            // 2) Move to approximate spawn
            playerTf.SetPositionAndRotation(safePos, safeRot);

            // 3) Compute ground-snapped Y while CC is disabled
            if (cc != null && raycastSnapToGround)
            {
                Vector3 rayStart = playerTf.position + Vector3.up * groundRayStartUp;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRayLength, groundMask, QueryTriggerInteraction.Ignore))
                {
                    float y = hit.point.y + (cc.height * 0.5f) + cc.skinWidth + 0.02f;
                    playerTf.position = new Vector3(playerTf.position.x, y, playerTf.position.z);
                }
            }

            // 4) Enable CC after position is correct
            if (cc != null) cc.enabled = true;

            // 5) Push down a bit to “catch” ground reliably
            if (cc != null) cc.Move(Vector3.down * 0.5f);
        }
        // --------------------------------------------------------------------------------------

        yield return null;

        // optional hold on black (unscaled)
        if (blackHoldSeconds > 0f)
        {
            float t = 0f;
            while (t < blackHoldSeconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // Resume world
        Time.timeScale = prevScale;

        // Fade in
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeIn();

        // Ignore immediate retrigger
        ignoreUntilUnscaledTime = Time.unscaledTime + postTeleportIgnoreSeconds;

        // Unlock movement logic
        if (pm != null) pm.externalMovementLock = false;

        // Reactivate input
        if (lockAllPlayerInputDuringTransition && reactivateInputAfterTransition && pi != null && inputWasActive)
            pi.ActivateInput();

        IsTransitioning = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        GetOverlapPose(out Vector3 center, out Quaternion rot);
        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.DrawSphere(Vector3.zero, 0.05f);
    }
}
