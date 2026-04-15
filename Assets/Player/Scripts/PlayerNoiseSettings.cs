using System;
using System.Collections.Generic;
using UnityEngine;

//For now, the player noise levels are depending on player's posture.
// implement more complex logic later if needed.
// use CC or Rigidbody to detect movement speed.
public class PlayerNoiseSettings : MonoBehaviour
{
    [Serializable]
    public class FloorNoiseRule
    {
        [Tooltip("Floor collider tag (e.g., Wood, BrokenGlass)")]
        public string floorTag;

        [Tooltip("Modifier added when player is crouch-moving")]
        public int addOnCrouch;

        [Tooltip("Modifier added when player is standing-walking movement")]
        public int addOnWalk;

        [Tooltip("Modifier added when player is running")]
        public int addOnRun;
    }

    [Header("References")]
    [SerializeField] private PlayerAwareness playerAwareness;
    [SerializeField] private PlayerFloorTagDetector floorDetector;
    [SerializeField] private Animator playerAnimator;

    [Header("Animator Params")]
    [SerializeField] private string crouchParam = "IsCrouching";
    [SerializeField] private string runParam = "IsRunning";
    [Tooltip("If your roll animation has a bool, set it here. Otherwise leave empty.")]
    [SerializeField] private string rollParam = "IsRolling";

    [Header("Movement Detection")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private float movingSpeedThreshold = 0.05f;

    [Header("Floor Noise Rules")]
    [Tooltip("If a tag is not in the list, modifier = 0.")]
    [SerializeField] private List<FloorNoiseRule> floorRules = new List<FloorNoiseRule>()
    {
        new FloorNoiseRule(){ floorTag = "Wood",        addOnCrouch = 0, addOnWalk = 1, addOnRun = 1 },
        new FloorNoiseRule(){ floorTag = "BrokenGlass", addOnCrouch = 1, addOnWalk = 1, addOnRun = 1 },
    };

    [Header("Noise Rings (Player-Centered Attenuation)")]
    [Tooltip("Ring 1 radius: attenuation 0")]
    public float ring1Radius = 4f;

    [Tooltip("Ring 2 radius: attenuation -1")]
    public float ring2Radius = 8f;

    [Tooltip("Ring 3 radius: attenuation -2")]
    public float ring3Radius = 12f;

    [Header("Noise Rings Gizmos")]
    public bool drawNoiseRingGizmos = true;


    [Header("Debug")]
    [SerializeField] private bool logNoiseChanges = false;

    private readonly Dictionary<string, FloorNoiseRule> ruleMap = new Dictionary<string, FloorNoiseRule>();
    private int lastNoise = -1;

    private void Awake()
    {
        if (playerAwareness == null) playerAwareness = GetComponent<PlayerAwareness>();
        if (floorDetector == null) floorDetector = GetComponentInChildren<PlayerFloorTagDetector>();
        if (playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (characterController == null) characterController = GetComponent<CharacterController>();

        ruleMap.Clear();
        foreach (var r in floorRules)
        {
            if (r == null) continue;
            if (string.IsNullOrWhiteSpace(r.floorTag)) continue;
            ruleMap[r.floorTag] = r;
        }
    }

    private void Update()
    {
        if (playerAwareness == null) return;

        int baseNoise = ComputeBaseNoise();
        int mod = ComputeFloorModifier();
        int finalNoise = Mathf.Clamp(baseNoise + mod, 0, 2);

        playerAwareness.noisiness = finalNoise;

        if (logNoiseChanges && finalNoise != lastNoise)
        {
            Debug.Log($"[PlayerNoiseSettings] noise={finalNoise} (base={baseNoise}, mod={mod}, floor={(floorDetector ? floorDetector.CurrentFloorTag : "<none>")})");
            lastNoise = finalNoise;
        }
    }

    private int ComputeBaseNoise()
    {
        if (!IsMoving()) return 0;
        if (IsRolling()) return 0;

        bool crouching = GetAnimBoolSafe(crouchParam);
        bool running = GetAnimBoolSafe(runParam);

        if (crouching) return 0;
        if (running) return 2;
        return 1;
    }

    private int ComputeFloorModifier()
    {
        if (!IsMoving()) return 0;
        if (IsRolling()) return 0;

        if (floorDetector == null) return 0;
        string tag = floorDetector.CurrentFloorTag;
        if (string.IsNullOrEmpty(tag)) return 0;

        if (!ruleMap.TryGetValue(tag, out var rule) || rule == null) return 0;

        bool crouching = GetAnimBoolSafe(crouchParam);
        bool running = GetAnimBoolSafe(runParam);

        if (crouching) return rule.addOnCrouch;
        if (running) return rule.addOnRun;
        return rule.addOnWalk;
    }

    private bool IsRolling()
    {
        if (string.IsNullOrEmpty(rollParam)) return false;
        return GetAnimBoolSafe(rollParam);
    }

    private bool GetAnimBoolSafe(string param)
    {
        if (playerAnimator == null) return false;
        if (string.IsNullOrEmpty(param)) return false;
        try { return playerAnimator.GetBool(param); }
        catch { return false; }
    }

    private bool IsMoving()
    {
        if (characterController != null)
            return characterController.velocity.sqrMagnitude > movingSpeedThreshold * movingSpeedThreshold;

        if (rb != null)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            return v.sqrMagnitude > movingSpeedThreshold * movingSpeedThreshold;
        }

        return true;
    }

    public int GetRingAttenuation(Vector3 enemyPos, float enemyHearingRadius)
    {
        float d = Vector3.Distance(transform.position, enemyPos);

        // closest ring wins (strongest signal)
        if (d <= enemyHearingRadius + ring1Radius) return 0;
        if (d <= enemyHearingRadius + ring2Radius) return 1;
        if (d <= enemyHearingRadius + ring3Radius) return 2;

        return int.MaxValue; // no overlap -> cannot hear
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawNoiseRingGizmos) return;

        Vector3 c = transform.position;

        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(c, Mathf.Max(0f, ring1Radius));

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(c, Mathf.Max(0f, ring2Radius));

        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawWireSphere(c, Mathf.Max(0f, ring3Radius));
    }
}
