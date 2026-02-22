using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class UICrosshairFollowMouse : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform ui;
    [SerializeField] private Canvas canvas;

    [Header("Aim Gate (Optional)")]
    [SerializeField] private bool onlyShowWhileAiming = false;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string aimActionName = "Aim";

    [Header("Options")]
    [SerializeField] private bool hideWhenOffscreen = true;
    [SerializeField] private bool clampToScreenWhenOffscreen = true;
    [SerializeField] private float screenPadding = 8f;

    private CanvasGroup cg;
    private InputAction aimAction;

    private void Reset()
    {
        ui = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        playerInput = FindFirstObjectByType<PlayerInput>();
    }

    private void Awake()
    {
        if (ui == null) ui = GetComponent<RectTransform>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();

        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        ResolveAimAction();
    }

    private void OnEnable()
    {
        ResolveAimAction();
    }

    private void ResolveAimAction()
    {
        if (playerInput != null && playerInput.actions != null && !string.IsNullOrEmpty(aimActionName))
            aimAction = playerInput.actions.FindAction(aimActionName, true);
    }

    private void LateUpdate()
    {
        if (ui == null) return;

        if (onlyShowWhileAiming)
        {
            bool aiming = aimAction != null && aimAction.IsPressed();
            if (!aiming)
            {
                SetVisible(false);
                return;
            }
        }

        Vector2 sp = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        bool offscreen = sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height;

        if (hideWhenOffscreen && offscreen)
        {
            if (clampToScreenWhenOffscreen)
            {
                sp.x = Mathf.Clamp(sp.x, screenPadding, Screen.width - screenPadding);
                sp.y = Mathf.Clamp(sp.y, screenPadding, Screen.height - screenPadding);
                SetVisible(true);
            }
            else
            {
                SetVisible(false);
                return;
            }
        }
        else
        {
            SetVisible(true);
        }

        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            ui.position = new Vector3(sp.x, sp.y, 0f);
            return;
        }

        Camera uiCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? canvas.worldCamera : null;
        RectTransform parent = ui.parent as RectTransform;

        if (parent != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, uiCam, out var local))
        {
            ui.anchoredPosition = local;
        }
        else
        {
            ui.position = new Vector3(sp.x, sp.y, 0f);
        }
    }

    private void SetVisible(bool v)
    {
        if (cg == null) return;
        cg.alpha = v ? 1f : 0f;
        cg.blocksRaycasts = v;
        cg.interactable = v;
    }
}