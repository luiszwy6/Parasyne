using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerFootstepEvents : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animator animator;

    [Header("Surface")]
    [SerializeField] private FootstepLibrary library;
    [SerializeField] private PlayerFloorTagDetector floorTagDetector;

    [Header("Raycast Fallback")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float rayDistance = 1.6f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Animator Params (Optional)")]
    [SerializeField] private string isRunningParam = "IsRunning";
    [SerializeField] private string isCrouchingParam = "IsCrouching";
    [SerializeField] private string isRollingParam = "IsRolling";

    [Header("Aiming Hard Lock (Optional)")]
    [SerializeField] private string isAimingParam = "IsAiming";
    [SerializeField] private string isWalkingParam = "IsWalking";

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float baseVolume = 0.8f;
    [SerializeField] private float walkVolumeMul = 1.0f;
    [SerializeField] private float runVolumeMul = 1.1f;
    [SerializeField] private float crouchVolumeMul = 0.6f;

    [Header("Spam Guard")]
    [SerializeField] private float minInterval = 0.06f;
    private float lastPlayTime = -999f;

    [Header("Forced Surface Step (OnFloorEntered)")]
    [Tooltip("If the player enters any tag in this list, play 1 footstep immediately (one-shot feedback).")]
    [SerializeField] private List<string> forcedStepTags = new List<string>() { "BrokenGlass" };

    [Tooltip("Extra cooldown for forced step to avoid double-trigger with animation events.")]
    [SerializeField] private float forcedStepCooldown = 0.12f;

    private HashSet<string> forcedTagSet;

    private void Reset()
    {
        audioSource = GetComponent<AudioSource>();
        animator = GetComponentInChildren<Animator>();
        rayOrigin = transform;
        if (floorTagDetector == null) floorTagDetector = GetComponentInChildren<PlayerFloorTagDetector>();
    }

    private void Awake()
    {
        if (rayOrigin == null) rayOrigin = transform;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (floorTagDetector == null) floorTagDetector = GetComponentInChildren<PlayerFloorTagDetector>();

        // Cache tags for fast lookup
        forcedTagSet = new HashSet<string>();
        if (forcedStepTags != null)
        {
            for (int i = 0; i < forcedStepTags.Count; i++)
            {
                string t = forcedStepTags[i];
                if (!string.IsNullOrEmpty(t)) forcedTagSet.Add(t);
            }
        }
    }

    private void OnEnable()
    {
        if (floorTagDetector != null)
            floorTagDetector.OnFloorEntered.AddListener(HandleFloorEntered);
    }

    private void OnDisable()
    {
        if (floorTagDetector != null)
            floorTagDetector.OnFloorEntered.RemoveListener(HandleFloorEntered);
    }

    // --- Animation Events ---
    public void Footstep()  => TryPlayFootstep(fromForcedSurface: false);
    public void FootstepL() => TryPlayFootstep(fromForcedSurface: false);
    public void FootstepR() => TryPlayFootstep(fromForcedSurface: false);

    private void HandleFloorEntered(string newTag)
    {
        if (forcedTagSet == null || forcedTagSet.Count == 0) return;
        if (string.IsNullOrEmpty(newTag)) return;
        if (!forcedTagSet.Contains(newTag)) return;

        // One-shot feedback when stepping onto special surface (e.g., glass)
        TryPlayFootstep(fromForcedSurface: true, forcedTag: newTag);
    }

    private void TryPlayFootstep(bool fromForcedSurface, string forcedTag = null)
    {
        if (library == null || audioSource == null) return;

        // Rolling: no sound (keep consistent)
        if (IsRolling()) return;

        // Prevent double fire (animation event + forced surface)
        float interval = fromForcedSurface ? Mathf.Max(minInterval, forcedStepCooldown) : minInterval;
        if (Time.time - lastPlayTime < interval) return;

        // Hard lock for aiming idle ONLY applies to animation-driven footsteps.
        if (!fromForcedSurface)
        {
            if (IsAiming() && !IsWalking()) return;
        }

        string tag = !string.IsNullOrEmpty(forcedTag) ? forcedTag : GetSurfaceTag();
        FootstepSurface surface = library.GetByTag(tag);
        if (surface == null) return;

        AudioClip clip = PickClip(surface);
        if (clip == null) return;

        float vol = baseVolume * surface.volumeMul * GetStanceVolumeMul();
        float pitch = 1f + Random.Range(surface.pitchMin, surface.pitchMax);

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, vol);

        lastPlayTime = Time.time;
    }

    private string GetSurfaceTag()
    {
        // 1) Prefer PlayerFloorTagDetector
        if (floorTagDetector != null)
        {
            string t = floorTagDetector.CurrentFloorTag;
            if (!string.IsNullOrEmpty(t)) return t;
        }

        // 2) Raycast fallback
        if (Physics.Raycast(rayOrigin.position, Vector3.down, out RaycastHit hit, rayDistance, groundMask, triggerInteraction))
            return hit.collider != null ? hit.collider.tag : "Untagged";

        return "Untagged";
    }

    private float GetStanceVolumeMul()
    {
        if (IsRunning()) return runVolumeMul;
        if (IsCrouching()) return crouchVolumeMul;
        return walkVolumeMul;
    }

    private AudioClip PickClip(FootstepSurface s)
    {
        AudioClip[] arr = IsRunning() ? s.runClips : (IsCrouching() ? s.crouchClips : s.walkClips);
        if (arr == null || arr.Length == 0) return null;
        return arr[Random.Range(0, arr.Length)];
    }

    private bool IsRunning()
    {
        if (animator == null || string.IsNullOrEmpty(isRunningParam)) return false;
        return animator.GetBool(isRunningParam);
    }

    private bool IsCrouching()
    {
        if (animator == null || string.IsNullOrEmpty(isCrouchingParam)) return false;
        return animator.GetBool(isCrouchingParam);
    }

    private bool IsRolling()
    {
        if (animator == null || string.IsNullOrEmpty(isRollingParam)) return false;
        return animator.GetBool(isRollingParam);
    }

    private bool IsAiming()
    {
        if (animator == null || string.IsNullOrEmpty(isAimingParam)) return false;
        return animator.GetBool(isAimingParam);
    }

    private bool IsWalking()
    {
        if (animator == null || string.IsNullOrEmpty(isWalkingParam)) return false;
        return animator.GetBool(isWalkingParam);
    }
}
