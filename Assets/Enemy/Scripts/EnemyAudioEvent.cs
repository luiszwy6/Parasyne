using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAudioEvents : MonoBehaviour
{
    [System.Serializable]
    public class AudioEventGroup
    {
        public string label;

        [Header("Clips")]
        public AudioClip[] clips;

        [Header("Playback")]
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 2f)] public float pitchMin = 1f;
        [Range(0.5f, 2f)] public float pitchMax = 1f;

        [Tooltip("Minimum time between plays of this group.")]
        public float cooldown = 0f;

        [HideInInspector] public float lastPlayTime = -999f;
    }

    [Header("Audio Source (optional)")]
    public AudioSource voiceSource;

    [Header("World Offset")]
    [Tooltip("Added to the playback position (world space).")]
    public Vector3 playOffset = Vector3.zero;

    [Tooltip("If set, audio plays at this transform position + offset instead of this component's transform.")]
    public Transform playOriginOverride;

    [Header("Groups")]
    public AudioEventGroup scream = new AudioEventGroup { label = "Scream" };
    public AudioEventGroup footstep = new AudioEventGroup { label = "Footstep" };
    public AudioEventGroup hurt = new AudioEventGroup { label = "Hurt" };
    public AudioEventGroup attack = new AudioEventGroup { label = "Attack" };
    public AudioEventGroup death = new AudioEventGroup { label = "Death" };

    [Header("Break Groups")]
    public AudioEventGroup armBreak = new AudioEventGroup { label = "ArmBreak" };
    public AudioEventGroup legBreak = new AudioEventGroup { label = "LegBreak" };
    public AudioEventGroup crawlEnter = new AudioEventGroup { label = "CrawlEnter" };

    // Animation Event API (existing)
    public void Enemy_PlayScreamSfx()   => PlayGroup(scream);
    public void Enemy_PlayFootstepSfx() => PlayGroup(footstep);
    public void Enemy_PlayHurtSfx()     => PlayGroup(hurt);
    public void Enemy_PlayAttackSfx()   => PlayGroup(attack);
    public void Enemy_PlayDeathSfx()    => PlayGroup(death);

    // Break API (new)
    public void Enemy_PlayArmBreakSfx()   => PlayGroup(armBreak);
    public void Enemy_PlayLegBreakSfx()   => PlayGroup(legBreak);
    public void Enemy_PlayCrawlEnterSfx() => PlayGroup(crawlEnter);

    private void PlayGroup(AudioEventGroup group)
    {
        if (group == null) return;
        if (group.clips == null || group.clips.Length == 0) return;

        if (Time.time < group.lastPlayTime + group.cooldown)
            return;

        AudioClip clip = PickRandomClip(group.clips);
        if (clip == null) return;

        float pitch = Random.Range(
            Mathf.Min(group.pitchMin, group.pitchMax),
            Mathf.Max(group.pitchMin, group.pitchMax)
        );

        Vector3 origin = playOriginOverride != null ? playOriginOverride.position : transform.position;
        Vector3 pos = origin + playOffset;

        if (voiceSource != null)
        {
            voiceSource.pitch = pitch;
            voiceSource.transform.position = pos;
            voiceSource.PlayOneShot(clip, group.volume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, pos, group.volume);
        }

        group.lastPlayTime = Time.time;
    }

    private AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        int i = Random.Range(0, clips.Length);
        return clips[i];
    }
}