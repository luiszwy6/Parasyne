using UnityEngine;
using UnityEngine.AI;

public class EnemyDeadState : EnemyState
{
    public EnemyDeadState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.SetDebugColor(Color.black);

        if (enemy.agent != null)
        {
            enemy.agent.isStopped = true;
            enemy.agent.ResetPath();
            enemy.agent.updateRotation = false;
            enemy.agent.updatePosition = false;
        }

        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsDead", true);

            enemy.animator.SetBool("IsRunning", false);
            enemy.animator.SetBool("IsAlert", false);
            enemy.animator.SetBool("IsCheck", false);
            enemy.animator.SetBool("IsAware", false);
            enemy.animator.SetFloat("Speed", 0f);

            enemy.animator.ResetTrigger("IsShortStun");
            enemy.animator.ResetTrigger("IsLongStun");
            enemy.animator.ResetTrigger("IsScream");
            enemy.animator.ResetTrigger("Attacking");
        }
    }

    public override void Update()
    {
        // stay dead
    }

    public override void Exit()
    {
        // do nothing
    }
}