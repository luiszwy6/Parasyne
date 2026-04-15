using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Footstep Surface")]
public class FootstepSurface : ScriptableObject
{
    public string surfaceTag = "Untagged";

    [Header("Clips")]
    public AudioClip[] walkClips;
    public AudioClip[] runClips;
    public AudioClip[] crouchClips;

    [Header("Tuning")]
    [Range(0f, 1f)] public float volumeMul = 1f;
    public float pitchMin = -0.05f;
    public float pitchMax = 0.05f;
}
