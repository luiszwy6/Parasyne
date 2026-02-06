using UnityEngine;
using UnityEngine.AI;

// EnemyPatrolState: Default roaming state.
// Key behavior:
// - Walk between waypoints (with optional wait at each point).
// - Continuously monitors perception.
// Transition priority:
// 1) -> ALERT if force alert triggers OR score >= scoreToAlert.
// 2) -> AWARE if vision/hearing indicates suspicion (scoreToAware or noise threshold).
// Otherwise keep patrolling.

public class EnemyPatrolState : EnemyState
{
    // Time spent waiting after reaching a waypoint.
    private float waitTimer = 0f;

    public EnemyPatrolState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.stateTimer = 0f;
        waitTimer = 0f;

        enemy.SetDebugColor(Color.green);
        Debug.Log($"{enemy.name} -> PATROL");

        // Patrol is "calm": no aware flag and no running by default.
        enemy.SetAnimAware(false);
        enemy.SetAnimRunning(false);

        if (enemy.agent != null)
        {
            // In Patrol we let NavMeshAgent rotate the character normally.
            enemy.agent.isStopped = false;
            enemy.agent.updateRotation = true;
        }

        // Start moving toward the current waypoint (keeps patrolIndex persistent across states).
        TrySetDestinationToCurrentWaypoint();
    }

    public override void Update()
    {
        enemy.stateTimer += Time.deltaTime;

        // --- Perception checks (can interrupt patrol) ---
        int perceivedNoise = enemy.GetPerceivedNoiseLevel();

        // Vision score is only non-zero if player is inside view cone and LOS is clear.
        float score = enemy.GetVisionScore(out bool inOuter, out bool inInner);
        bool forceAlert = enemy.IsForceAlertTriggered();

        // --- Highest priority: immediate ALERT ---
        if (forceAlert || score >= enemy.scoreToAlert)
        {
            // Store a memory point so Alert/Check can react meaningfully.
            if (enemy.player != null) enemy.SetLastKnownPosition(enemy.player.position);
            enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));
            return;
        }

        // --- Suspicion: enter AWARE ---
        bool audioSuspicious = perceivedNoise >= enemy.noiseSensitivity;
        bool visionAware = score >= enemy.scoreToAware;

        if (visionAware || audioSuspicious)
        {
            // Sound always goes to AWARE first (reaction time is handled in AWARE).
        if (audioSuspicious && enemy.player != null)
        {
            enemy.lastAwareTriggerWasSound = true;
            enemy.SetLastHeardPosition(enemy.player.position);
        }
        else
        {
            enemy.lastAwareTriggerWasSound = false;
            if (enemy.player != null) enemy.SetLastKnownPosition(enemy.player.position);
        }

        enemy.stateMachine.ChangeState(new EnemyAwareState(enemy));
        return;
        }


        // --- Waypoint patrol loop ---
        if (enemy.agent == null) return;
        if (!enemy.agent.isOnNavMesh) return;

        if (enemy.patrolWaypoints == null || enemy.patrolWaypoints.Length == 0)
            return;

        // Clamp index so patrol never goes out of range.
        if (enemy.patrolIndex < 0) enemy.patrolIndex = 0;
        if (enemy.patrolIndex >= enemy.patrolWaypoints.Length)
            enemy.patrolIndex = enemy.patrolLoop ? 0 : enemy.patrolWaypoints.Length - 1;

        // If reached the waypoint, wait briefly, then advance.
        bool reached =
            !enemy.agent.pathPending &&
            enemy.agent.remainingDistance <= enemy.agent.stoppingDistance;

        if (reached)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= enemy.patrolWaitTime)
            {
                waitTimer = 0f;
                AdvanceWaypoint();
                TrySetDestinationToCurrentWaypoint();
            }
        }
        else
        {
            // Moving again -> reset wait timer.
            waitTimer = 0f;
        }
    }

    // Advance patrolIndex with optional looping.
    private void AdvanceWaypoint()
    {
        if (enemy.patrolWaypoints == null || enemy.patrolWaypoints.Length == 0) return;

        int next = enemy.patrolIndex + 1;

        if (next >= enemy.patrolWaypoints.Length)
        {
            if (enemy.patrolLoop) next = 0;
            else next = enemy.patrolWaypoints.Length - 1;
        }

        enemy.patrolIndex = next;
    }

    // Set NavMeshAgent destination to the current waypoint (projected onto NavMesh).
    private void TrySetDestinationToCurrentWaypoint()
    {
        if (enemy.agent == null) return;
        if (!enemy.agent.isOnNavMesh) return;
        if (enemy.patrolWaypoints == null || enemy.patrolWaypoints.Length == 0) return;

        Transform wp = enemy.patrolWaypoints[enemy.patrolIndex];
        if (wp == null) return;

        Vector3 dest = ProjectToNavMesh(wp.position);
        enemy.agent.isStopped = false;
        enemy.agent.SetDestination(dest);
    }

    // Keeps waypoint targets reachable even if the transform is slightly off the NavMesh.
    private Vector3 ProjectToNavMesh(Vector3 p)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(p, out hit, 2.0f, NavMesh.AllAreas))
            return hit.position;
        return p;
    }
}
