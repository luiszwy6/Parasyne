using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCrossHairSettings : MonoBehaviour
{
    [Header("References")]
    public PlayerAimSettings aimSettings;
    public Transform physicalAimPoint;
    public Transform playerEyePoint;

    [Header("TPS")]
    public bool tpsMode = false;

    [Header("Aim Sampling (Mouse Raycast)")]
    public LayerMask mouseAimLayers = 0;
    public LayerMask mouseGroundLayers = ~0;
    public float mouseRayMaxDistance = 200f;

    [SerializeField] private QueryTriggerInteraction mouseRayTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Aim Distance (Gamepad Fallback)")]
    public float crosshairDistance = 4f;

    [Header("Aim Range Limit (centered on Player)")]
    public bool limitAimRadius = true;
    public float maxAimRadius = 6f;

    [Header("Aim Pivot (camera follow target, centered on Player)")]
    public Transform aimPivot;
    public float aimPivotHeight = 0.0f;
    public float pivotFollowSpeed = 20f;

    [Header("Stop By Layer (block aim point through walls)")]
    public bool stopByLayer = true;
    public LayerMask stopLayers = 0;
    public float stopRayStartHeight = 0.6f;
    public float stopRayEndHeight = 0.1f;
    public float stopPadding = 0.05f;

    [SerializeField] private QueryTriggerInteraction stopRayTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("External Override")]
    public bool forceHideCrosshair = false;

    public enum CrosshairHeightMode
    {
        Ground,
        EyePointHeight,
        EyePointPlane
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

    [Header("Gizmos (Aim Rays)")]
    public bool drawAimRaysGizmo = true;
    public Color gizmoPlayerToAimColor = new Color(1f, 0.35f, 0.1f, 0.9f);
    public Color gizmoScreenRayColor = new Color(0.1f, 1f, 0.2f, 0.9f);
    public bool gizmoOnlyWhenAiming = true;

    public Vector3 AimPointClamped { get; private set; }
    public Vector3 AimWorldDir { get; private set; }
    public bool HasMouseAimPoint { get; private set; }
    public Vector3 MouseAimPoint { get; private set; }

    Transform _lastActor;
    bool _lastIsAiming;

    bool _hasLastScreenRay;
    Vector3 _lastScreenRayOrigin;
    Vector3 _lastScreenRayDir;
    float _lastScreenRayDrawDist;

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
        _lastIsAiming = isAiming;

        AimWorldDir = Vector3.zero;
        HasMouseAimPoint = false;
        MouseAimPoint = Vector3.zero;

        _hasLastScreenRay = false;
        _lastScreenRayOrigin = Vector3.zero;
        _lastScreenRayDir = Vector3.forward;
        _lastScreenRayDrawDist = 0f;

        if (!isAiming)
        {
            AimPointClamped = actor.position;
            UpdatePivot(actor, isAiming, Time.deltaTime);
            UpdateCrosshairVisual(actor, isAiming);
            return;
        }

        Vector3 desiredAimPoint = actor.position;

        if (usingMouseScheme && aimCamera != null && Mouse.current != null)
        {
            Vector2 screenPos = tpsMode
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : Mouse.current.position.ReadValue();

            Ray ray = aimCamera.ScreenPointToRay(screenPos);

            _hasLastScreenRay = true;
            _lastScreenRayOrigin = ray.origin;
            _lastScreenRayDir = ray.direction.normalized;

            if (mouseAimLayers.value != 0 &&
                Physics.Raycast(ray, out RaycastHit hit, mouseRayMaxDistance, mouseAimLayers, mouseRayTriggerInteraction))
            {
                MouseAimPoint = hit.point;
                HasMouseAimPoint = true;
                desiredAimPoint = MouseAimPoint;
                _lastScreenRayDrawDist = hit.distance;
            }
            else if (mouseGroundLayers.value != 0 &&
                     Physics.Raycast(ray, out RaycastHit groundHit, mouseRayMaxDistance, mouseGroundLayers, mouseRayTriggerInteraction))
            {
                MouseAimPoint = groundHit.point;
                HasMouseAimPoint = true;
                desiredAimPoint = MouseAimPoint;
                _lastScreenRayDrawDist = groundHit.distance;
            }
            else
            {
                float d = tpsMode ? mouseRayMaxDistance : Mathf.Max(1f, crosshairDistance);
                desiredAimPoint = ray.origin + ray.direction * d;
                _lastScreenRayDrawDist = d;
            }
        }
        else
        {
            Camera camForStick = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;

            if (lookInput.sqrMagnitude > 0.01f && camForStick != null)
            {
                Vector3 camForward = camForStick.transform.forward;
                Vector3 camRight = camForStick.transform.right;
                camForward.y = 0f;
                camRight.y = 0f;
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

        Vector3 aimPoint = ClampPointToRadius(actor.position, desiredAimPoint, maxAimRadius, limitAimRadius);
        aimPoint = StopAimPointByLayer(actor, aimPoint);

        AimPointClamped = aimPoint;

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

        Vector3 targetPos = isAiming ? AimPointClamped : actor.position;
        targetPos.y = actor.position.y + aimPivotHeight;

        float t = 1f - Mathf.Exp(-pivotFollowSpeed * Mathf.Max(0.0001f, dt));
        aimPivot.position = Vector3.Lerp(aimPivot.position, targetPos, t);
    }

    void UpdateCrosshairVisual(Transform actor, bool isAiming)
    {
        if (physicalAimPoint == null) return;

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

        physicalAimPoint.position = pos;
    }

    Vector3 StopAimPointByLayer(Transform actor, Vector3 aimPoint)
    {
        if (!stopByLayer || stopLayers.value == 0) return aimPoint;

        Vector3 start = (playerEyePoint != null)
            ? playerEyePoint.position
            : actor.position + Vector3.up * stopRayStartHeight;

        Vector3 end = aimPoint;
        end.y = actor.position.y + stopRayEndHeight;

        Vector3 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 0.001f) return aimPoint;

        dir /= dist;

        if (Physics.Raycast(start, dir, out RaycastHit hit, dist, stopLayers, stopRayTriggerInteraction))
        {
            Vector3 p = hit.point - dir * Mathf.Max(0f, stopPadding);
            p.y = aimPoint.y;
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

    void OnDrawGizmos()
    {
        if (!drawAimRaysGizmo) return;

        if (gizmoOnlyWhenAiming && Application.isPlaying && !_lastIsAiming)
            return;

        if (_lastActor == null) return;

        Gizmos.color = gizmoPlayerToAimColor;
        Vector3 start = (playerEyePoint != null)
            ? playerEyePoint.position
            : _lastActor.position + Vector3.up * stopRayStartHeight;
        Vector3 end = AimPointClamped;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.08f);

        if (_hasLastScreenRay)
        {
            Gizmos.color = gizmoScreenRayColor;
            float d = Mathf.Max(0.1f, _lastScreenRayDrawDist);
            Vector3 rayEnd = _lastScreenRayOrigin + _lastScreenRayDir * d;
            Gizmos.DrawLine(_lastScreenRayOrigin, rayEnd);
            Gizmos.DrawWireSphere(rayEnd, 0.05f);
        }
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

        if (Application.isPlaying)
        {
            Vector3 p = AimPointClamped;
            p.y = center.y;
            Gizmos.DrawLine(center, p);
        }
    }
}