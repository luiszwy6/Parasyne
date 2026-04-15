using UnityEngine;

[DisallowMultipleComponent]
public class WeaponEffects : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip gunshotClip;
    [Range(0f, 1f)] [SerializeField] private float gunshotVolume = 1f;
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private float pitchMin = 0.95f;
    [SerializeField] private float pitchMax = 1.05f;

    [Header("VFX")]
    [SerializeField] private ParticleSystem muzzleFlash;

    private void Reset()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = GetComponentInParent<AudioSource>();
    }

    public void PlayGunshot()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play(true);
        }

        if (audioSource != null && gunshotClip != null)
        {
            float oldPitch = audioSource.pitch;

            if (randomizePitch)
            {
                float lo = Mathf.Min(pitchMin, pitchMax);
                float hi = Mathf.Max(pitchMin, pitchMax);
                audioSource.pitch = Random.Range(lo, hi);
            }

            audioSource.PlayOneShot(gunshotClip, gunshotVolume);

            audioSource.pitch = oldPitch;
        }
    }
}