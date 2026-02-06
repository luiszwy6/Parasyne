using UnityEngine;

// ENHANCED TAKEDOWN DEBUGGER
// This will tell us exactly what's happening with the animator

public class TakedownDebugger : MonoBehaviour
{
    private Enemy enemy;
    private Animator animator;

    public Animator playerAnimator; // Assign the player's animator in the Inspector for comparison
    void Start()
    {
        enemy = GetComponent<Enemy>();
        if (enemy == null)
        {
            Debug.LogError("TakedownDebugger: No Enemy component found!");
            return;
        }

        animator = enemy.animator;
        if (animator == null)
        {
            Debug.LogError("TakedownDebugger: Enemy.animator is NULL! Assign it in the Inspector!");
        }
    }

    void Update()
    {
        // Press 'T' to test the takedown
        if (playerAnimator.GetBool("IsTakingDown")==true)
        {
            Debug.Log("=== TAKEDOWN DEBUG START ===");
            
            if (enemy == null)
            {
                Debug.LogError("Enemy is NULL");
                return;
            }

            if (animator == null)
            {
                Debug.LogError("Animator is NULL - check Enemy component Inspector!");
                return;
            }

            // Check if animator is enabled
            Debug.Log($"Animator enabled: {animator.enabled}");
            Debug.Log($"Animator gameObject active: {animator.gameObject.activeInHierarchy}");

            // Check if the parameter exists
            bool hasParameter = false;
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == "BeingTakenDown")
                {
                    hasParameter = true;
                    Debug.Log($"✓ Found parameter 'BeingTakenDown' (Type: {param.type})");
                    break;
                }
            }

            if (!hasParameter)
            {
                Debug.LogError("✗ Parameter 'BeingTakenDown' NOT FOUND in Animator Controller!");
                Debug.Log("Available parameters:");
                foreach (AnimatorControllerParameter param in animator.parameters)
                {
                    Debug.Log($"  - {param.name} ({param.type})");
                }
                return;
            }

            // Get current value before setting
            bool beforeValue = animator.GetBool("BeingTakenDown");
            Debug.Log($"BeingTakenDown value BEFORE: {beforeValue}");

            // Trigger the state change
            enemy.stateMachine.ChangeState(new EnemyTakeDownState(enemy));

            // Check value after setting
            bool afterValue = animator.GetBool("BeingTakenDown");
            Debug.Log($"BeingTakenDown value AFTER: {afterValue}");

            // Check current state info
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"Current Animator State: {stateInfo.fullPathHash}");
            Debug.Log($"Current State normalized time: {stateInfo.normalizedTime}");

            Debug.Log("=== TAKEDOWN DEBUG END ===");
        }
       
    }
}