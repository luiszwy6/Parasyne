using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("UI Reticles")]
    [SerializeField] private GameObject topDownAimReticleUI;
    [SerializeField] private GameObject topDownActualReticleUI;
    [SerializeField] private GameObject tpsCenterReticleUI;

    private InputAction aimAction;
    private InputAction changeViewAction;

    private bool enabledTPS;

    private bool cachedShowAimLine = true;
    private bool cachedStopByLayer;
    private bool cachedLimitAimRadius;

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

        if (playerAimSettings != null)
            cachedShowAimLine = playerAimSettings.showAimLine;

        if (crosshairSettings != null)
        {
            cachedStopByLayer = crosshairSettings.stopByLayer;
            cachedLimitAimRadius = crosshairSettings.limitAimRadius;
        }
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

        if (playerTPS != null && enabledTPS)
            playerTPS.SetAiming(aimPressed);

        if (!enabledTPS)
        {
            if (changeViewPressed && playerAimSettings != null && playerAimSettings.IsAiming)
            {
                SetTPS(true);

                if (playerTPS != null)
                    playerTPS.SetAiming(aimPressed);
            }
            return;
        }

        if (!aimPressed)
        {
            SetTPS(false);
            return;
        }

        if (aimPressed && changeViewPressed)
        {
            SetTPS(false);
            return;
        }
    }

    private void SetTPS(bool on)
    {
        enabledTPS = on;

        if (playerTPS != null)
        {
            playerTPS.SetActive(on);
            if (on) playerTPS.ZeroLookDeltaOnce();
            if (!on) playerTPS.SetAiming(false);
        }

        if (playerAimSettings != null)
        {
            if (on)
            {
                cachedShowAimLine = playerAimSettings.showAimLine;
                playerAimSettings.showAimLine = false;

                if (playerAimSettings.aimLine != null)
                    playerAimSettings.aimLine.enabled = false;
            }
            else
            {
                playerAimSettings.showAimLine = cachedShowAimLine;
            }
        }

        if (crosshairSettings != null)
        {
            if (on)
            {
                cachedStopByLayer = crosshairSettings.stopByLayer;
                cachedLimitAimRadius = crosshairSettings.limitAimRadius;

                crosshairSettings.tpsMode = true;
                crosshairSettings.stopByLayer = false;
                crosshairSettings.limitAimRadius = false;

                crosshairSettings.forceHideCrosshair = true;
            }
            else
            {
                crosshairSettings.tpsMode = false;
                crosshairSettings.stopByLayer = cachedStopByLayer;
                crosshairSettings.limitAimRadius = cachedLimitAimRadius;

                crosshairSettings.forceHideCrosshair = false;
            }
        }

        if (tpsCenterReticleUI != null)
            tpsCenterReticleUI.SetActive(on);

        if (topDownAimReticleUI != null)
            topDownAimReticleUI.SetActive(!on);

        if (topDownActualReticleUI != null)
            topDownActualReticleUI.SetActive(!on);
    }
}