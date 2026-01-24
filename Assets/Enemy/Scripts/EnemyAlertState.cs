using UnityEngine;
using UnityEngine.AI;

public class EnemyAlertState : EnemyState
{
    public EnemyAlertState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.stateTimer = 0f;
        enemy.SetDebugColor(Color.red);
        Debug.Log($"{enemy.name} -> ALERT!");

        // Animator flags
        enemy.SetAnimAware(false);
        enemy.SetAnimRunning(enemy.runOnAlert);

        if (enemy.agent != null)
        {
            enemy.agent.isStopped = false;

            // Keep your original approach (manual rotation)
            enemy.agent.updateRotation = false;

            // Speed depends on the toggle
            enemy.agent.speed = enemy.runOnAlert ? enemy.alertRunSpeed : enemy.alertWalkSpeed;
        }

        if (enemy.agent != null && enemy.player != null)
        {
            enemy.agent.SetDestination(enemy.player.position);
        }
    }

    public override void Update()
    {
        enemy.stateTimer += Time.deltaTime;

        // Keep chasing the player
        if (enemy.agent != null && enemy.player != null)
        {
            enemy.agent.SetDestination(enemy.player.position);

            // Keep your manual rotation (same as your current file)
            Vector3 vel = enemy.agent.desiredVelocity;
            vel.y = 0f;

            if (vel.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(vel);
                enemy.transform.rotation = Quaternion.Slerp(
                    enemy.transform.rotation,
                    targetRot,
                    10f * Time.deltaTime
                );
            }
        }
    }

    public override void Exit()
    {
        // When leaving ALERT, stop forcing run tree
        enemy.SetAnimRunning(false);

        if (enemy.agent != null)
        {
            enemy.agent.updateRotation = true;
        }
    }
}
