using UnityEngine;

// EnemyAwareState: Suspicion state.
// - Enemy stops and tries to confirm player presence.
// - Uses BOTH vision score (rings + LOS) and hearing (noise + range).
// - Escalation: Aware -> Alert (instant) OR Aware -> Check (timer) OR back to Patrol.

public class EnemyAwareState : EnemyState
{
    // Sound reaction: turn to the sound first, then wait, then CHECK.
    private bool reactingToSound = false;
    private float soundReactTimer = 0f;
    private Vector3 soundTargetPos;

    private bool soundLatchToCheck = false;

    public EnemyAwareState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        enemy.SetAnimAware(true);
        enemy.SetAnimRunning(false);

        // Timer counts continuous suspicion time.
        enemy.stateTimer = 0f;

        reactingToSound = false;
        soundReactTimer = 0f;
        soundTargetPos = default;
        soundLatchToCheck = false;

        enemy.SetDebugColor(Color.yellow);
        Debug.Log($"{enemy.name} -> AWARE");

        // Aware = pause movement and "listen/look".
        if (enemy.agent != null)
        {
            enemy.agent.isStopped = true;
            enemy.agent.updateRotation = false;
        }

        // If Patrol entered AWARE due to sound, Patrol should have set lastHeardPosition already.
        // Latch immediately based on memory, without requiring continued hearing.
        if (enemy.lastAwareTriggerWasSound)
        {
            StartSoundReaction(enemy.lastHeardPosition);
        }
    }

    public override void Update()
    {
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

            enemy.lastAwareTriggerWasSound = false;
            enemy.SetAnimAware(false);
            enemy.stateMachine.ChangeState(new EnemyAlertState(enemy));
            return;
        }

        // --- If sound was latched, do reaction then CHECK (no need to keep hearing) ---
        if (soundLatchToCheck)
        {
            TurnToward(soundTargetPos);

            soundReactTimer += Time.deltaTime;
            if (soundReactTimer >= enemy.soundReactionTime)
            {
                enemy.lastAwareTriggerWasSound = false;
                enemy.SetAnimAware(false);
                enemy.stateMachine.ChangeState(new EnemyCheckState(enemy));
                return;
            }

            // While reacting to sound, do not run vision timer logic.
            return;
        }

        // --- Perception inputs (used only to decide whether to latch sound) ---
        int perceivedNoise = enemy.GetPerceivedNoiseLevel();
        bool audioSuspicious = perceivedNoise >= enemy.noiseSensitivity;

        // --- Suspicion rules ---
        bool visionSuspicious = score >= enemy.scoreToAware;

        if (!visionSuspicious && !audioSuspicious)
        {
            // No signal -> calm down and return to Patrol.
            enemy.lastAwareTriggerWasSound = false;
            enemy.stateTimer = 0f;
            enemy.SetAnimAware(false);
            enemy.ClearInvestigationMemory();
            enemy.stateMachine.ChangeState(new EnemyPatrolState(enemy));
            return;
        }

        // Update investigation memory (vision has higher priority than hearing).
        if (visionSuspicious)
        {
            enemy.SetLastSeenPosition(enemy.GetPlayerVisionPoint());
        }
        else if (audioSuspicious && enemy.player != null)
        {
            // Aware hears sound -> latch and go CHECK after reaction time.
            enemy.lastAwareTriggerWasSound = true;
            enemy.SetLastHeardPosition(enemy.player.position);
            StartSoundReaction(enemy.lastHeardPosition);
            return;
        }

        // --- Escalation to CHECK if suspicion persists (vision-driven timer) ---
        enemy.stateTimer += Time.deltaTime;
        if (enemy.stateTimer >= enemy.awareToCheckTime)
        {
            enemy.lastAwareTriggerWasSound = false;
            enemy.SetAnimAware(false);
            enemy.stateMachine.ChangeState(new EnemyCheckState(enemy));
            return;
        }
    }

    private void StartSoundReaction(Vector3 heardPos)
    {
        reactingToSound = true;
        soundLatchToCheck = true;
        soundReactTimer = 0f;

        // Lock to the sound position at trigger time.
        soundTargetPos = heardPos;
    }

    private void TurnToward(Vector3 worldPos)
    {
        Vector3 dir = worldPos - enemy.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        enemy.transform.rotation = Quaternion.Slerp(
            enemy.transform.rotation,
            targetRot,
            enemy.soundTurnSpeed * Time.deltaTime
        );
    }
}
