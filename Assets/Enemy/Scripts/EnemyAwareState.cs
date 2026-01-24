using UnityEngine;

// EnemyAwareState: Suspicion state.
// - Enemy stops and tries to confirm player presence.
// - Uses BOTH vision score (rings + LOS) and hearing (noise + range).
// - Escalation: Aware -> Alert (instant) OR Aware -> Check (timer) OR back to Patrol.

public class EnemyAwareState : EnemyState
{
    public EnemyAwareState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.SetAnimAware(true);
        enemy.SetAnimRunning(false);

        // Timer counts continuous suspicion time.
        enemy.stateTimer = 0f;

        enemy.SetDebugColor(Color.yellow);
        Debug.Log($"{enemy.name} -> AWARE");

        // Aware = pause movement and "listen/look".
        if (enemy.agent != null)
            enemy.agent.isStopped = true;
    }

    public override void Update()
    {
        // --- Perception inputs ---
        bool canHearPlayer = enemy.IsPlayerInHearingRange();
        int noiseLevel = enemy.CurrentNoiseLevel();

        // Vision score is ONLY > 0 if player is inside outer/inner cone (and not blocked by LOS).
        float score = enemy.GetVisionScore(out bool inOuter, out bool inInner);

        // Close-range inner cone can force instant alert (configured per lightness).
        bool forceAlert = enemy.IsForceAlertTriggered();

        // --- Instant escalation to ALERT ---
        if (forceAlert || score >= enemy.scoreToAlert)
        {
            // Save last seen point for investigation/chase logic.
            if (enemy.player != null)
                enemy.SetLastSeenPosition(enemy.GetPlayerVisionPoint());

            enemy.SetAnimAware(false);
            enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));
            return;
        }

        // --- Suspicion rules ---
        bool audioSuspicious = canHearPlayer && noiseLevel >= enemy.noiseSensitivity;
        bool visionSuspicious = score >= enemy.scoreToAware;

        if (!visionSuspicious && !audioSuspicious)
        {
            // No signal -> calm down and return to Patrol.
            enemy.stateTimer = 0f;
            enemy.SetAnimAware(false);
            enemy.ClearInvestigationMemory();
            enemy.stateMachine.ChangeState(new EnemyPatrolState(enemy));
            return;
        }

        // Update investigation memory (vision has higher priority than hearing).
        if (visionSuspicious)
            enemy.SetLastSeenPosition(enemy.GetPlayerVisionPoint());
        else
            enemy.SetLastHeardPosition(enemy.player.position);

        // --- Escalation to CHECK if suspicion persists ---
        enemy.stateTimer += Time.deltaTime;
        if (enemy.stateTimer >= enemy.awareToCheckTime)
        {
            enemy.SetAnimAware(false);
            enemy.stateMachine.ChangeState(new EnemyCheckState(enemy));
            return;
        }
    }
}
