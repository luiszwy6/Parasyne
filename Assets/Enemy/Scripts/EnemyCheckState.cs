using UnityEngine;
using UnityEngine.AI;

// EnemyCheckState: Investigation state.

public class EnemyCheckState : EnemyState
{
    private float checkingTimer = 0f;
    private bool isCheckingHere = false;

    public EnemyCheckState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.stateTimer = 0f;
        checkingTimer = 0f;
        isCheckingHere = false;

        enemy.SetDebugColor(Color.blue);
        Debug.Log($"{enemy.name} -> CHECK");

        if (enemy.agent == null) return;

        enemy.agent.isStopped = false;
        enemy.agent.updatePosition = true;
        enemy.agent.updateRotation = false;

        if (enemy.agent.isOnNavMesh && enemy.TryGetInvestigationPoint(out var point))
        {
            Vector3 dest = ProjectToNavMesh(point);
            enemy.agent.SetDestination(dest);
        }
    }

    public override void Update()
    {
        enemy.stateTimer += Time.deltaTime;

        float score = enemy.GetVisionScore(out bool inOuter, out bool inInner);
        bool forceAlert = enemy.IsForceAlertTriggered();

        if (forceAlert || score >= enemy.scoreToAlert)
        {
            if (enemy.player != null) enemy.SetLastSeenPosition(enemy.player.position);
            enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));
            return;
        }

        if (enemy.agent == null)
        {
            enemy.stateMachine.ChangeState(new EnemyPatrolState(enemy));
            return;
        }

        // Hearing can refresh investigation point during CHECK.
        int perceivedNoise = enemy.GetPerceivedNoiseLevel();
        bool audioSuspicious = perceivedNoise >= enemy.noiseSensitivity;

        if (audioSuspicious && enemy.player != null)
        {
            Vector3 heardPos = GetHeardEventPosition();
            enemy.SetLastHeardPosition(heardPos);
        }

        enemy.agent.updatePosition = true;
        enemy.agent.updateRotation = false;

        if (!enemy.agent.isOnNavMesh)
            return;

        if (enemy.TryGetInvestigationPoint(out var investigatePoint))
        {
            Vector3 dest = ProjectToNavMesh(investigatePoint);

            if (!isCheckingHere)
            {
                enemy.agent.isStopped = false;
                enemy.agent.SetDestination(dest);

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

            bool reached =
                !enemy.agent.pathPending &&
                enemy.agent.remainingDistance <= enemy.agent.stoppingDistance;

            if (reached)
            {
                if (!isCheckingHere)
                {
                    isCheckingHere = true;
                    checkingTimer = 0f;
                    enemy.agent.isStopped = true;
                }

                checkingTimer += Time.deltaTime;

                if (checkingTimer >= enemy.checkingTime)
                {
                    enemy.ClearInvestigationMemory();
                    enemy.stateMachine.ChangeState(new EnemyPatrolState(enemy));
                    return;
                }
            }
            else
            {
                isCheckingHere = false;
                checkingTimer = 0f;
            }
        }
        else
        {
            enemy.stateMachine.ChangeState(new EnemyPatrolState(enemy));
        }
    }

    private Vector3 GetHeardEventPosition()
    {
        return enemy.player != null ? enemy.player.position : enemy.transform.position;
    }

    private Vector3 ProjectToNavMesh(Vector3 p)
    {
        if (enemy.agent == null) return p;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(p, out hit, 2.0f, NavMesh.AllAreas))
            return hit.position;

        return p;
    }
}
