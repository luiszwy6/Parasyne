using UnityEngine;
using UnityEngine.AI;

// EnemyCheckState: Investigation state.
// - Enemy moves to the last investigation point (seen/heard/known) on the NavMesh.
// - If it reaches the point, it "checks" there for a short time, then returns to Patrol.
// - Can still escalate to Alert immediately if vision score/force alert triggers.

public class EnemyCheckState : EnemyState
{
    // Local timer for "standing and checking" after reaching destination.
    private float checkingTimer = 0f;

    // True once the agent has reached the point and started checking in place.
    private bool isCheckingHere = false;

    public EnemyCheckState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        // stateTimer can be used for generic time-in-state tracking (optional).
        enemy.stateTimer = 0f;
        checkingTimer = 0f;
        isCheckingHere = false;

        enemy.SetDebugColor(Color.blue);
        Debug.Log($"{enemy.name} -> CHECK");

        if (enemy.agent == null) return;

        // In Check we use NavMeshAgent for movement but rotate the enemy manually.
        enemy.agent.isStopped = false;
        enemy.agent.updatePosition = true;
        enemy.agent.updateRotation = false;

        // Investigation target comes from Enemy memory:
        // lastSeen > lastHeard > lastKnown (see Enemy.TryGetInvestigationPoint).
        if (enemy.agent.isOnNavMesh && enemy.TryGetInvestigationPoint(out var point))
        {
            Vector3 dest = ProjectToNavMesh(point);
            enemy.agent.SetDestination(dest);
        }
    }

    public override void Update()
    {
        enemy.stateTimer += Time.deltaTime;

        // --- Escalation to ALERT always has priority ---
        float score = enemy.GetVisionScore(out bool inOuter, out bool inInner);
        bool forceAlert = enemy.IsForceAlertTriggered();

        if (forceAlert || score >= enemy.scoreToAlert)
        {
            // Save a reliable "last seen" before switching.
            if (enemy.player != null) enemy.SetLastSeenPosition(enemy.player.position);
            enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));
            return;
        }

        // If we cannot navigate, fall back to Patrol.
        if (enemy.agent == null)
        {
            enemy.stateMachine.ChangeState(new EnemyPatrolState(enemy));
            return;
        }

        // --- Hearing can update investigation memory during Check ---
        bool canHearPlayer = enemy.IsPlayerInHearingRange();
        int noiseLevel = enemy.CurrentNoiseLevel();
        bool audioSuspicious = canHearPlayer && noiseLevel >= enemy.noiseSensitivity;

        if (audioSuspicious && enemy.player != null)
        {
            // This allows the investigation point to "move" if the player makes noise elsewhere.
            Vector3 heardPos = GetHeardEventPosition();
            enemy.SetLastHeardPosition(heardPos);
        }

        // Keep manual rotation behavior consistent every frame.
        enemy.agent.updatePosition = true;
        enemy.agent.updateRotation = false;

        if (!enemy.agent.isOnNavMesh)
            return;

        // --- Move to investigation point, then check in place ---
        if (enemy.TryGetInvestigationPoint(out var investigatePoint))
        {
            Vector3 dest = ProjectToNavMesh(investigatePoint);

            if (!isCheckingHere)
            {
                // Travel phase: keep moving toward the destination.
                enemy.agent.isStopped = false;
                enemy.agent.SetDestination(dest);

                // Manual facing: rotate toward desired velocity direction (not instant snap).
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

            // Arrival condition: close enough AND path is ready.
            bool reached =
                !enemy.agent.pathPending &&
                enemy.agent.remainingDistance <= enemy.agent.stoppingDistance;

            if (reached)
            {
                // Start the "check-in-place" phase once.
                if (!isCheckingHere)
                {
                    isCheckingHere = true;
                    checkingTimer = 0f;
                    enemy.agent.isStopped = true;
                }

                // If we waited long enough, clear memory and return to Patrol.
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
                // Not reached (or moved away) -> reset check timer.
                isCheckingHere = false;
                checkingTimer = 0f;
            }
        }
        else
        {
            // No investigation point -> nothing to check, go back to Patrol.
            enemy.stateMachine.ChangeState(new EnemyPatrolState(enemy));
        }
    }

    // For now, hearing points to the player's current position.
    // If you later add a "noise event system", this is where you'd plug it in.
    private Vector3 GetHeardEventPosition()
    {
        return enemy.player != null ? enemy.player.position : enemy.transform.position;
    }

    // Keeps destination reachable by sampling nearest point on NavMesh.
    private Vector3 ProjectToNavMesh(Vector3 p)
    {
        if (enemy.agent == null) return p;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(p, out hit, 2.0f, NavMesh.AllAreas))
            return hit.position;

        return p;
    }
}
