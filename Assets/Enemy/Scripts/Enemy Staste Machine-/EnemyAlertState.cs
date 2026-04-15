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

        enemy.SetAnimAware(false);
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsAlert", true);
            enemy.animator.SetBool("IsCheck", false);
            enemy.animator.SetBool("IsAware", false);
        }

        enemy.SetAnimRunning(enemy.runOnAlert);

        if (enemy.agent != null)
        {
            enemy.agent.isStopped = false;
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

        if (enemy.agent != null && enemy.player != null)
        {
            enemy.agent.SetDestination(enemy.player.position);
        }

        EnemyAttackSettings atk = enemy.GetComponent<EnemyAttackSettings>();
        if (atk != null && enemy.player != null && atk.CanAttackTargetNow(enemy.player))
        {
            enemy.stateMachine.ChangeState(new EnemyAttackState(enemy));
            return;
        }
    }

    public override void Exit()
    {
        enemy.SetAnimRunning(false);

        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsAlert", false);
        }
    }
}