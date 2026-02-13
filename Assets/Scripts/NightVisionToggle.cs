using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class NightVisionToggle : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Volume volume;

    [Header("Profiles")]
    [SerializeField] private VolumeProfile normalProfile;
    [SerializeField] private VolumeProfile nightVisionProfile;

    [Header("Night Vision Light (Child)")]
    [SerializeField] private Light nvDirectionalLight;
    [SerializeField] private bool autoFindChildLight = true;

    [Header("Input")]
    [SerializeField] private string nightVisionActionName = "NightVision";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip nvOnClip;
    [SerializeField] private AudioClip nvOffClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    private InputAction nightVisionAction;
    private bool enabledNV;

    private void Awake()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (autoFindChildLight && nvDirectionalLight == null)
        {
            var lights = GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                if (l.name.Contains("NV"))
                {
                    nvDirectionalLight = l;
                    break;
                }
            }
        }

        if (playerInput == null || volume == null)
        {
            Debug.LogError("[NightVisionToggle] Missing PlayerInput or Volume reference.");
            enabled = false;
            return;
        }

        nightVisionAction = playerInput.actions[nightVisionActionName];
        if (nightVisionAction == null)
        {
            Debug.LogError($"[NightVisionToggle] Action '{nightVisionActionName}' not found.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (nightVisionAction == null) return;
        nightVisionAction.performed += OnNightVisionPerformed;
        nightVisionAction.Enable();
    }

    private void OnDisable()
    {
        if (nightVisionAction == null) return;
        nightVisionAction.performed -= OnNightVisionPerformed;
        nightVisionAction.Disable();
    }

    private void Start()
    {
        ApplyProfileAndAudio(playSound: false);
    }

    private void OnNightVisionPerformed(InputAction.CallbackContext _)
    {
        enabledNV = !enabledNV;
        ApplyProfileAndAudio(playSound: true);
    }

    private void ApplyProfileAndAudio(bool playSound)
    {
        volume.profile = enabledNV ? nightVisionProfile : normalProfile;

        if (nvDirectionalLight != null)
            nvDirectionalLight.enabled = enabledNV;

        if (!playSound || audioSource == null) return;

        AudioClip clip = enabledNV ? nvOnClip : nvOffClip;
        if (clip != null)
            audioSource.PlayOneShot(clip, sfxVolume);
    }
}
