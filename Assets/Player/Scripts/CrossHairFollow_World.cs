using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class UICrosshairFollowWorld : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform ui;
    [SerializeField] private Transform worldTarget;
    [SerializeField] private Camera cam;
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
        cam = Camera.main;
        playerInput = FindFirstObjectByType<PlayerInput>();
    }

    private void Awake()
    {
        if (ui == null) ui = GetComponent<RectTransform>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (cam == null) cam = Camera.main;

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
        if (ui == null || worldTarget == null || cam == null) return;

        if (onlyShowWhileAiming)
        {
            bool aiming = aimAction != null && aimAction.IsPressed();
            if (!aiming)
            {
                SetVisible(false);
                return;
            }
        }

        Vector3 sp = cam.WorldToScreenPoint(worldTarget.position);

        bool behind = sp.z <= 0f;
        bool offscreen = sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height;

        if (hideWhenOffscreen && (behind || offscreen))
        {
            if (clampToScreenWhenOffscreen && !behind)
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

    public void SetTarget(Transform t) => worldTarget = t;
}