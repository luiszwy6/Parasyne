using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class FinishDebugger : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator playerAnimator;

    [Header("Target Enemy (debug)")]
    [SerializeField] private Enemy targetEnemy;

    [Header("Input")]
    [SerializeField] private string finishActionName = "Finish";

    [Header("Animator")]
    [SerializeField] private string finishTriggerName = "SetFinish";
    [SerializeField] private bool resetTriggerBeforeSet = true;

    [Header("Damage")]
    [SerializeField] private float finishDamage = 100f;
    [Min(0f)]
    [SerializeField] private float damageDelay = 0.25f;

    private InputAction finishAction;
    private int finishTriggerHash;

    private Coroutine pendingDamageRoutine;

    private void Reset()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();

        if (playerInput != null && playerInput.actions != null)
            finishAction = playerInput.actions.FindAction(finishActionName, true);

        finishTriggerHash = Animator.StringToHash(finishTriggerName);
    }

    private void OnEnable()
    {
        if (finishAction != null)
        {
            finishAction.Enable();
            finishAction.performed += OnFinishPerformed;
        }
    }

    private void OnDisable()
    {
        if (finishAction != null)
        {
            finishAction.performed -= OnFinishPerformed;
            finishAction.Disable();
        }

        if (pendingDamageRoutine != null)
        {
            StopCoroutine(pendingDamageRoutine);
            pendingDamageRoutine = null;
        }
    }

    private void OnFinishPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.ReadValueAsButton())
            return;

        // 1) Player animation trigger
        if (playerAnimator != null)
        {
            if (resetTriggerBeforeSet) playerAnimator.ResetTrigger(finishTriggerHash);
            playerAnimator.SetTrigger(finishTriggerHash);
        }

        // 2) Schedule damage with delay
        if (pendingDamageRoutine != null)
        {
            StopCoroutine(pendingDamageRoutine);
            pendingDamageRoutine = null;
        }

        pendingDamageRoutine = StartCoroutine(DealDamageAfterDelay());
    }

    private IEnumerator DealDamageAfterDelay()
    {
        float d = Mathf.Max(0f, damageDelay);
        if (d > 0f)
            yield return new WaitForSeconds(d);

        if (targetEnemy == null)
        {
            Debug.LogWarning("[FinishDebugger] Target enemy is not assigned.");
            pendingDamageRoutine = null;
            yield break;
        }

        EnemyHealth hp = targetEnemy.GetComponent<EnemyHealth>();
        if (hp == null) hp = targetEnemy.GetComponentInParent<EnemyHealth>();

        if (hp != null)
        {
            hp.TakeDamage(finishDamage);
        }
        else
        {
            Debug.LogWarning($"[FinishDebugger] EnemyHealth not found on '{targetEnemy.name}'.");
        }

        pendingDamageRoutine = null;
    }

    public void SetTargetEnemy(Enemy e)
    {
        targetEnemy = e;
    }
}