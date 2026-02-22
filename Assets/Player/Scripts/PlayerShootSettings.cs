using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerShootSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MuzzlePointSettings muzzlePointSettings;

    [Header("Input (optional)")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string shootActionName = "Shoot";

    private InputAction shootAction;

    private void Awake()
    {
        if (playerInput != null)
            shootAction = playerInput.actions[shootActionName];
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

        if (muzzlePointSettings != null)
            muzzlePointSettings.RequestDebugDraw();
    }
}