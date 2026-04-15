using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerTakedownOffset : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform targetEnemy;
    [SerializeField] private CharacterController controller;
    [SerializeField] private MonoBehaviour movementScript;

    [Header("Takedown Targeting")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float takedownRange = 2f;
    [SerializeField] private LayerMask enemyMask = ~0;

    [Header("Animator Param")]
    [SerializeField] private string isTakingDownParam = "IsTakingDown";

    [Header("Alignment")]
    [SerializeField] private Vector3 enemyLocalPositionOffset = new Vector3(0f, 0f, 0.9f);
    [SerializeField] private float yawOffsetDeg = 180f;
    [SerializeField] private bool alignRotation = true;
    [SerializeField] private bool continuousAlign = false;

    [Header("Optional Smoothing")]
    [SerializeField] private float positionLerpSpeed = 0f;
    [SerializeField] private float rotationSlerpSpeed = 0f;

    [Header("Restore")]
    [SerializeField] private bool restoreOnEnd = false;

    [Header("Collision During Takedown")]
    [SerializeField] private bool ignorePlayerEnemyCollision = true;
    [SerializeField] private bool permanentIgnoreAfterTakedown = true;
    [SerializeField] private bool includeCharacterController = true;

    private bool lastTakingDown;
    private Vector3 savedPos;
    private Quaternion savedRot;

    private bool savedControllerEnabled;
    private bool savedMovementEnabled;

    private readonly List<Collider> playerCols = new List<Collider>(16);
    private readonly List<Collider> enemyCols = new List<Collider>(32);

    private Transform ignoredEnemyRoot;
    private readonly HashSet<int> permanentlyIgnoredEnemyIds = new HashSet<int>();

    private void Reset()
    {
        if (playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();
        if (controller == null) controller = GetComponent<CharacterController>();
    }

    private void Awake()
    {
        if (playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();
        if (controller == null) controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        bool takingDown = playerAnimator != null && playerAnimator.GetBool(isTakingDownParam);

        if (takingDown && !lastTakingDown)
        {
            if (restoreOnEnd)
            {
                savedPos = transform.position;
                savedRot = transform.rotation;
            }

            if (ignorePlayerEnemyCollision)
            {
                if (permanentIgnoreAfterTakedown) IgnorePlayerEnemyCollisionPermanently();
                else BeginIgnorePlayerEnemyCollision();
            }

            DisableMovementForTakedown();
            AlignNow();
        }

        if (takingDown && continuousAlign)
            AlignNow();

        if (!takingDown && lastTakingDown)
        {
            if (restoreOnEnd)
            {
                transform.position = savedPos;
                transform.rotation = savedRot;
            }

            RestoreMovementAfterTakedown();

            if (ignorePlayerEnemyCollision && !permanentIgnoreAfterTakedown)
                EndIgnorePlayerEnemyCollision();

            targetEnemy = null;
        }

        lastTakingDown = takingDown;
    }

    public void SetTargetEnemy(Transform enemyRoot)
    {
        targetEnemy = enemyRoot;
    }

    public void TryTakedown()
    {
        if (playerAnimator == null) return;
        if (playerAnimator.GetBool(isTakingDownParam)) return;

        Vector3 origin = rayOrigin ? rayOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 dir = rayOrigin ? rayOrigin.forward : transform.forward;

        if (!Physics.Raycast(origin, dir, out RaycastHit hit, takedownRange, enemyMask, QueryTriggerInteraction.Ignore))
            return;

        Enemy enemy = hit.transform.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        SetTargetEnemy(enemy.transform);
        playerAnimator.SetBool(isTakingDownParam, true);
    }

    public void EndTakedown()
    {
        if (playerAnimator != null)
            playerAnimator.SetBool(isTakingDownParam, false);
    }

    private void DisableMovementForTakedown()
    {
        if (movementScript != null)
        {
            savedMovementEnabled = movementScript.enabled;
            movementScript.enabled = false;
        }

        if (controller != null)
        {
            savedControllerEnabled = controller.enabled;
            controller.enabled = false;
        }
    }

    private void RestoreMovementAfterTakedown()
    {
        if (controller != null) controller.enabled = savedControllerEnabled;
        if (movementScript != null) movementScript.enabled = savedMovementEnabled;
    }

    private void AlignNow()
    {
        if (targetEnemy == null) return;

        Vector3 desiredPos = targetEnemy.TransformPoint(enemyLocalPositionOffset);
        Quaternion desiredRot = transform.rotation;

        if (alignRotation)
            desiredRot = ComputeDesiredRotation(desiredPos);

        if (positionLerpSpeed <= 0f)
            transform.position = desiredPos;
        else
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPos,
                1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime)
            );

        if (alignRotation)
        {
            if (rotationSlerpSpeed <= 0f)
                transform.rotation = desiredRot;
            else
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRot,
                    1f - Mathf.Exp(-rotationSlerpSpeed * Time.deltaTime)
                );
        }
    }

    private Quaternion ComputeDesiredRotation(Vector3 desiredPos)
    {
        Vector3 toEnemy = targetEnemy.position - desiredPos;
        toEnemy.y = 0f;

        if (toEnemy.sqrMagnitude > 0.0001f)
            return Quaternion.LookRotation(toEnemy.normalized, Vector3.up) * Quaternion.Euler(0f, yawOffsetDeg, 0f);

        Vector3 fwd = targetEnemy.forward; 
        fwd.y = 0f;

        if (fwd.sqrMagnitude > 0.0001f)
            return Quaternion.LookRotation(fwd.normalized, Vector3.up) * Quaternion.Euler(0f, yawOffsetDeg, 0f);

        return transform.rotation;
    }

    private void BeginIgnorePlayerEnemyCollision()
    {
        if (targetEnemy == null) return;

        ignoredEnemyRoot = targetEnemy;

        CollectPlayerColliders(playerCols);
        CollectEnemyColliders(ignoredEnemyRoot, enemyCols);

        ApplyIgnore(playerCols, enemyCols, true);
    }

    private void EndIgnorePlayerEnemyCollision()
    {
        if (ignoredEnemyRoot == null) return;

        CollectPlayerColliders(playerCols);
        CollectEnemyColliders(ignoredEnemyRoot, enemyCols);

        ApplyIgnore(playerCols, enemyCols, false);

        ignoredEnemyRoot = null;
        playerCols.Clear();
        enemyCols.Clear();
    }

    private void IgnorePlayerEnemyCollisionPermanently()
    {
        if (targetEnemy == null) return;

        int id = targetEnemy.gameObject.GetInstanceID();
        if (permanentlyIgnoredEnemyIds.Contains(id)) return;

        permanentlyIgnoredEnemyIds.Add(id);

        CollectPlayerColliders(playerCols);
        CollectEnemyColliders(targetEnemy, enemyCols);

        ApplyIgnore(playerCols, enemyCols, true);

        playerCols.Clear();
        enemyCols.Clear();
    }

    private void CollectPlayerColliders(List<Collider> outList)
    {
        outList.Clear();
        GetComponentsInChildren(true, outList);

        if (includeCharacterController && controller != null)
        {
            Collider cc = controller;
            if (cc != null && !outList.Contains(cc))
                outList.Add(cc);
        }
    }

    private void CollectEnemyColliders(Transform enemyRoot, List<Collider> outList)
    {
        outList.Clear();
        if (enemyRoot == null) return;
        enemyRoot.GetComponentsInChildren(true, outList);
    }

    private static void ApplyIgnore(List<Collider> aList, List<Collider> bList, bool ignore)
    {
        for (int i = 0; i < aList.Count; i++)
        {
            Collider a = aList[i];
            if (a == null) continue;

            for (int j = 0; j < bList.Count; j++)
            {
                Collider b = bList[j];
                if (b == null) continue;
                if (a == b) continue;

                Physics.IgnoreCollision(a, b, ignore);
            }
        }
    }
}
