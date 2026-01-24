using UnityEngine;
using UnityEngine.InputSystem;

// PlayerCrossHairSettings: computes a world-space aim point and direction.
// Supports:
// - Mouse: screen raycast to ground
// - Gamepad: stick direction relative to camera
// Optional clamps and wall-stopping keep aim consistent.

public class PlayerCrossHairSettings : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Aim settings on the player.")]
    public PlayerAimSettings aimSettings;

    [Tooltip("World-space crosshair object (independent object).")]
    public Transform crosshair;

    [Tooltip("Optional: eye/head reference for crosshair height.")]
    public Transform playerEyePoint;

    [Header("Aim Ground Raycast (Mouse)")]
    public float crosshairDistance = 4f;
    public LayerMask crosshairGroundMask = ~0;

    [Header("Aim Range Limit (centered on Player)")]
    public bool limitAimRadius = true;
    public float maxAimRadius = 6f;

    [Header("Aim Pivot (camera follow target, centered on Player)")]
    public Transform aimPivot;            // usually on player, but you can still reference it here
    public float aimPivotHeight = 0.0f;
    public float pivotFollowSpeed = 20f;

    [Header("Stop By Layer (block aim point through walls)")]
    public bool stopByLayer = true;
    public LayerMask stopLayers = 0;
    public float stopRayStartHeight = 0.6f;
    public float stopRayEndHeight = 0.1f;
    public float stopPadding = 0.05f;

    [Header("External Override")]
    public bool forceHideCrosshair = false;



    public enum CrosshairHeightMode
    {
        Ground,          // use aim point y + offset (mouse hit y)
        EyePointHeight,  // force y = eye y + offset
        EyePointPlane    // same as EyePointHeight (kept for clarity)
    }

    [Header("Crosshair Height")]
    public CrosshairHeightMode crosshairHeightMode = CrosshairHeightMode.EyePointHeight;
    public float crosshairHeightOffset = 0.02f;

    [Header("Visibility")]
    public bool hideWhenNotAiming = true;

    [Header("Gizmos (Aim Radius)")]
    public bool drawAimRadiusGizmo = true;
    public float gizmoHeightOffset = 0.05f;
    [Range(12, 128)] public int gizmoSegments = 48;

    // Outputs
    public Vector3 AimPointClamped { get; private set; }
    public Vector3 AimWorldDir { get; private set; } // XZ dir normalized
    public bool HasMouseAimPoint { get; private set; }
    public Vector3 MouseAimPoint { get; private set; }

    // cached for gizmos (because gizmos can't take parameters)
    Transform _lastActor;

    void Reset()
    {
        if (aimSettings == null)
            aimSettings = FindFirstObjectByType<PlayerAimSettings>();
    }

    public void Tick(
        Transform actor,
        bool isCrouching,
        bool isAiming,
        Vector2 lookInput,
        bool usingMouseScheme,
        Camera aimCamera,
        Transform cameraTransform
    )
    {
        _lastActor = actor;

        AimWorldDir = Vector3.zero;
        HasMouseAimPoint = false;
        MouseAimPoint = Vector3.zero;

        if (!isAiming)
        {
            AimPointClamped = actor.position;
            UpdatePivot(actor, isAiming, Time.deltaTime);
            UpdateCrosshairVisual(actor, isAiming);
            return;
        }

        Vector3 desiredAimPoint = actor.position;

        // 1) mouse -> raycast ground
        if (usingMouseScheme && aimCamera != null && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = aimCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, 200f, crosshairGroundMask, QueryTriggerInteraction.Ignore))
            {
                MouseAimPoint = hit.point;
                HasMouseAimPoint = true;
                desiredAimPoint = MouseAimPoint;
            }
            else
            {
                desiredAimPoint = actor.position + actor.forward * crosshairDistance;
            }
        }
        else
        {
            // 2) gamepad -> right stick + camera basis
            Camera camForStick = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;

            if (lookInput.sqrMagnitude > 0.01f && camForStick != null)
            {
                Vector3 camForward = camForStick.transform.forward;
                Vector3 camRight   = camForStick.transform.right;
                camForward.y = 0f;
                camRight.y   = 0f;
                camForward.Normalize();
                camRight.Normalize();

                Vector3 dir = camForward * lookInput.y + camRight * lookInput.x;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.001f)
                {
                    dir.Normalize();
                    desiredAimPoint = actor.position + dir * crosshairDistance;
                }
                else
                {
                    desiredAimPoint = actor.position + actor.forward * crosshairDistance;
                }
            }
            else
            {
                desiredAimPoint = actor.position + actor.forward * crosshairDistance;
            }
        }

        // 3) clamp by radius around player (XZ)
        Vector3 aimPoint = ClampPointToRadius(actor.position, desiredAimPoint, maxAimRadius, limitAimRadius);

        // 4) stop by wall layer (ray from player towards aim point)
        aimPoint = StopAimPointByLayer(actor, aimPoint);

        AimPointClamped = aimPoint;

        // 5) compute aim dir on XZ
        Vector3 dirToAim = AimPointClamped - actor.position;
        dirToAim.y = 0f;
        if (dirToAim.sqrMagnitude > 0.001f)
            AimWorldDir = dirToAim.normalized;

        UpdatePivot(actor, isAiming, Time.deltaTime);
        UpdateCrosshairVisual(actor, isAiming);
    }

    void UpdatePivot(Transform actor, bool isAiming, float dt)
    {
        if (aimPivot == null) return;

        Vector3 targetPos;
        if (isAiming)
        {
            targetPos = AimPointClamped;
            targetPos.y = actor.position.y + aimPivotHeight;
        }
        else
        {
            targetPos = actor.position;
            targetPos.y = actor.position.y + aimPivotHeight;
        }

        float t = 1f - Mathf.Exp(-pivotFollowSpeed * Mathf.Max(0.0001f, dt));
        aimPivot.position = Vector3.Lerp(aimPivot.position, targetPos, t);
    }

    void UpdateCrosshairVisual(Transform actor, bool isAiming)
    {
        if (crosshair == null) return;

        // External override: hide crosshair regardless of aiming state.
        if (forceHideCrosshair)
        {
            if (crosshair.gameObject.activeSelf)
            crosshair.gameObject.SetActive(false);
            return;
        }


        if (hideWhenNotAiming && !isAiming)
        {
            if (crosshair.gameObject.activeSelf) crosshair.gameObject.SetActive(false);
            return;
        }

        if (!crosshair.gameObject.activeSelf) crosshair.gameObject.SetActive(true);

        Vector3 pos = AimPointClamped;

        float eyeY = (playerEyePoint != null) ? playerEyePoint.position.y : actor.position.y;

        switch (crosshairHeightMode)
        {
            case CrosshairHeightMode.Ground:
                pos.y = pos.y + crosshairHeightOffset;
                break;
            case CrosshairHeightMode.EyePointHeight:
            case CrosshairHeightMode.EyePointPlane:
                pos.y = eyeY + crosshairHeightOffset;
                break;
        }

        crosshair.position = pos;
    }

    Vector3 StopAimPointByLayer(Transform actor, Vector3 aimPoint)
    {
        if (!stopByLayer || stopLayers.value == 0) return aimPoint;

        Vector3 start = actor.position + Vector3.up * stopRayStartHeight;

        Vector3 end = aimPoint;
        end.y = actor.position.y + stopRayEndHeight;

        Vector3 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 0.001f) return aimPoint;

        dir /= dist;

        if (Physics.Raycast(start, dir, out RaycastHit hit, dist, stopLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 p = hit.point - dir * Mathf.Max(0f, stopPadding);
            p.y = aimPoint.y; // keep original y for later height mode
            return p;
        }

        return aimPoint;
    }

    static Vector3 ClampPointToRadius(Vector3 center, Vector3 point, float radius, bool enabled)
    {
        if (!enabled || radius <= 0f) return point;

        Vector3 v = point - center;
        v.y = 0f;

        float mag = v.magnitude;
        if (mag <= radius || mag < 0.0001f) return point;

        v = v / mag * radius;
        Vector3 clamped = center + v;
        clamped.y = point.y;
        return clamped;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawAimRadiusGizmo || !limitAimRadius || maxAimRadius <= 0f) return;

        Transform actor = _lastActor != null ? _lastActor : (aimSettings != null ? aimSettings.transform : null);
        if (actor == null) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);

        Vector3 center = actor.position;
        center.y += gizmoHeightOffset;

        int seg = Mathf.Max(12, gizmoSegments);
        float step = 2f * Mathf.PI / seg;

        Vector3 prev = center + new Vector3(maxAimRadius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * maxAimRadius, 0f, Mathf.Sin(a) * maxAimRadius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        if (Application.isPlaying && actor != null)
        {
            Vector3 p = AimPointClamped;
            p.y = center.y;
            Gizmos.DrawLine(center, p);
        }
    }
}
