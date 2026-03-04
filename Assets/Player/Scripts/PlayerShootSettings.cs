using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerShootSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MuzzlePointSettings muzzlePointSettings;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip gunshotClip;
    [SerializeField] private ParticleSystem muzzleFlash;
    
    [Header("Input (optional)")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string shootActionName = "Shoot";

    [Header("Animator Param")]

    private InputAction shootAction;
    public PlayerAimSettings aimSettings;
    private void Awake()
    {
        if (playerInput != null)
            shootAction = playerInput.actions[shootActionName];
        if (aimSettings == null)
            aimSettings = GetComponent<PlayerAimSettings>(); 
    }

    private void OnEnable()
    {
        if (shootAction != null)
            shootAction.performed += OnShootPerformed;
    }

    private void OnDisable()
    {
        if (shootAction != null)
            shootAction.performed -= OnShootPerformed;
    }

    private void OnShootPerformed(InputAction.CallbackContext ctx)
    {
        Shoot();
    }

    public void Shoot()
    {
        
        //
        if (audioSource != null && gunshotClip != null && aimSettings.IsAiming == true )
        {
            muzzleFlash.Stop();
            muzzleFlash.Clear();
            muzzleFlash.Play();
            audioSource.PlayOneShot(gunshotClip);
        }
    }
}