using UnityEngine;
using UnityEngine.AI;

public class EnemySurpriseState : EnemyState
{
    private float timer;
    private bool hasScreamed;

    public EnemySurpriseState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        timer = 0f;
        hasScreamed = false;

        enemy.SetDebugColor(new Color(1f, 0.5f, 0f)); // orange

        // stop moving and scream
        if (enemy.agent != null)
        {
            enemy.agent.isStopped = true;
            enemy.agent.ResetPath();
            enemy.agent.updateRotation = false;
        }

        if (enemy.animator != null)
        {
            // Go into Alert.
            enemy.animator.SetBool("IsAlert", true);
            enemy.animator.SetBool("IsCheck", false);

           // Aware is just a transition for aware -> check, so we turn it off immediately when entering Surprise.
            enemy.animator.SetBool("IsAware", false);

            enemy.animator.SetBool("IsFirstFind", false);
            enemy.animator.SetBool("IsRunning", false);
            enemy.animator.SetFloat("Speed", 0f);

            enemy.animator.ResetTrigger("IsScream");
            enemy.animator.SetTrigger("IsScream");
        }

        DoScreamOnce();
    }

    public override void Update()
    {
        if (enemy.IsDeadPlaceholder())
        {
            //enemy.stateMachine.ChangeState(new EnemyDeadState(enemy));
            return;
        }

        timer += Time.deltaTime;

        float duration = (enemy.surpriseDuration > 0f) ? enemy.surpriseDuration : 0.6f;
        if (timer < duration) return;

        // Surprise enter alert state
        enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));
    }

    public override void Exit()
    {
        if (enemy.agent != null)
        {
            enemy.agent.isStopped = false;
        }
    }

    private void DoScreamOnce()
    {
        if (hasScreamed) return;
        hasScreamed = true;

        Debug.Log($"{enemy.name} scream before Alert");

        // put alert nearby logic here
        // enemy.alertNearBy?.BroadcastAlert(enemy);
    }
}