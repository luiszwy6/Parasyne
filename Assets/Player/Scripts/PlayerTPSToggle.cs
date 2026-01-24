using UnityEngine;
using UnityEngine.InputSystem;

// PlayerTPSToggle: switches between top-down aiming and TPS aiming.
// Rules:
// - You can only enter TPS while already aiming in top-down.
// - Releasing Aim exits TPS.
// - Optionally, pressing Change View while aiming toggles back.

public class PlayerTPSToggle : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerTPS playerTPS;
    [SerializeField] private PlayerAimSettings playerAimSettings;

    [Header("Actions (PlayerInput)")]
    [SerializeField] private string aimActionName = "Aim";
    [SerializeField] private string changeViewActionName = "Change View";
    [SerializeField] private PlayerCrossHairSettings crosshairSettings;
    [SerializeField] private GameObject tpsCenterReticleUI; // a UI Image at screen center
    private bool cachedCrosshairActive;

    private InputAction aimAction;
    private InputAction changeViewAction;

    private bool enabledTPS;

    // Cache top-down aim line setting so we can restore it when leaving TPS
    private bool cachedShowAimLine = true;

    private void Reset()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerTPS == null) playerTPS = FindFirstObjectByType<PlayerTPS>();
        if (playerAimSettings == null) playerAimSettings = GetComponent<PlayerAimSettings>();
        if (crosshairSettings == null) crosshairSettings = GetComponent<PlayerCrossHairSettings>();
    }

    private void Awake()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            aimAction = playerInput.actions[aimActionName];
            changeViewAction = playerInput.actions[changeViewActionName];
        }

        // Cache initial value once
        if (playerAimSettings != null)
            cachedShowAimLine = playerAimSettings.showAimLine;
    }

    private void OnEnable()
    {
        aimAction?.Enable();
        changeViewAction?.Enable();
    }

    private void OnDisable()
    {
        aimAction?.Disable();
        changeViewAction?.Disable();
    }

    private void Start()
    {
        SetTPS(false);
    }

    private void Update()
    {
        bool aimPressed = aimAction != null && aimAction.IsPressed();
        bool changeViewPressed = changeViewAction != null && changeViewAction.WasPerformedThisFrame();

        // Feed TPS aiming state (used for TPS line + actor rotation)
        if (playerTPS != null && enabledTPS)
            playerTPS.SetAiming(aimPressed);

        // Only allow entering TPS when top-down has already entered aiming
        if (!enabledTPS)
        {
            if (changeViewPressed && playerAimSettings != null && playerAimSettings.IsAiming)
            {
                SetTPS(true);

                // Keep aiming state on entry
                if (playerTPS != null)
                    playerTPS.SetAiming(aimPressed);
            }
            return;
        }

        // If Aim released in TPS -> exit TPS back to default top-down
        if (!aimPressed)
        {
            SetTPS(false);
            return;
        }

        // Optional: while still aiming, pressing Change View toggles back to top-down immediately
        if (aimPressed && changeViewPressed)
        {
            SetTPS(false);
            return;
        }
    }

    
private void SetTPS(bool on)
{
    enabledTPS = on;

    // Toggle TPS system (camera + aiming ray).
    if (playerTPS != null)
    {
        playerTPS.SetActive(on);

        // Ensure TPS aim visuals stop when leaving TPS.
        if (!on)
            playerTPS.SetAiming(false);
    }

    // While TPS is active, disable the top-down aim line so we don't draw two aim indicators.
    if (playerAimSettings != null)
    {
        if (on)
        {
            // Cache runtime value so we can restore on exit.
            cachedShowAimLine = playerAimSettings.showAimLine;

            playerAimSettings.showAimLine = false;

            // Hide immediately this frame (optional).
            if (playerAimSettings.aimLine != null)
                playerAimSettings.aimLine.enabled = false;
        }
        else
        {
            playerAimSettings.showAimLine = cachedShowAimLine;
        }
    }

    // Hide world-space crosshair while TPS is active (TPS uses a center-screen reticle instead).
    if (crosshairSettings != null)
    {
        // Cache current state once when entering TPS.
        if (on && crosshairSettings.crosshair != null)
            cachedCrosshairActive = crosshairSettings.crosshair.gameObject.activeSelf;

        crosshairSettings.forceHideCrosshair = on;

        // Restore previous visibility when leaving TPS.
        if (!on && crosshairSettings.crosshair != null)
            crosshairSettings.crosshair.gameObject.SetActive(cachedCrosshairActive);
    }

    // Enable center-screen reticle UI in TPS.
    if (tpsCenterReticleUI != null)
        tpsCenterReticleUI.SetActive(on);
}

}
