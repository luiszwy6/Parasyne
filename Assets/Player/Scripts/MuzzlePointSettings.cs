using UnityEngine;
//This scipt should be attached to the weapon object, and use an empty child transform as the muzzle point. 
//It will compute the shot raycast based on the attached muzzle point and the aim point from PlayerCrossHairSettings (The 3d crosshair object in world), then cache the results for PlayerShootSettings to read and draw debug info.
//Use this to apply for weapons' shooting logic and bullet trajectory. It can also be used as a share referenence for both TPS and top-down shooting.
[DisallowMultipleComponent]
public class MuzzlePointSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private PlayerCrossHairSettings crosshairSettings;
    [SerializeField] private Transform crosshairTransform;

    [Header("Aim Source")]
    [SerializeField] private bool useCrosshairTransformAsAimPoint = true;

    [Header("Raycast (Bullet Trajectory Debug)")]
    [Tooltip("Layers that can be hit by the shot ray.")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Tooltip("Max extra distance beyond aim point distance (for safety).")]
    [Min(0f)]
    [SerializeField] private float extraDistance = 2f;

    [Min(0.01f)]
    [SerializeField] private float rangeOfProjectile = 50f;

    [Tooltip("Ignore trigger colliders.")]
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Debug Draw")]
    [Tooltip("How long to draw debug line after Shoot() is called.")]
    [Min(0.01f)]
    [SerializeField] private float debugDrawDuration = 0.35f;

    [Tooltip("Width is for Gizmos only; Debug.DrawLine has no width.")]
    [Min(0.0001f)]
    [SerializeField] private float gizmoSphereRadius = 0.06f;

    [SerializeField] private Color bulletRayColor = Color.red;      // muzzle -> hit/aim
    [SerializeField] private Color aimPointColor = Color.yellow;
    [SerializeField] private bool drawInGameView = true;
    [SerializeField] private bool drawInSceneGizmos = true;

    // Outputs (last shot)
    public bool HasHit { get; private set; }
    public RaycastHit LastHit { get; private set; }
    public Vector3 AimPoint { get; private set; }
    public Ray LastRay { get; private set; }

    private float _debugTimer;

    private void Reset()
    {
        if (muzzlePoint == null) muzzlePoint = transform;
    }

    private void Update()
    {
        if (_debugTimer > 0f)
        {
            _debugTimer -= Time.deltaTime;

            if (drawInGameView)
                DrawDebugLineGame();
        }
    }

    // Call this from PlayerShootSettings.Shoot().
    // It will compute the shot raycast and cache the results, then draw debug info for a short duration.
    public void RequestDebugDraw()
    {
        ComputeShotRaycast();
        _debugTimer = debugDrawDuration;

        if (drawInGameView)
            DrawDebugLineGame();
    }

    private void ComputeShotRaycast()
    {
        HasHit = false;
        LastHit = default;

        if (muzzlePoint == null)
            return;

        AimPoint = ResolveAimPoint();
        Vector3 origin = muzzlePoint.position;

        Vector3 toAim = AimPoint - origin;
        float distToAim = toAim.magnitude;

        Vector3 dir = (distToAim > 0.0001f) ? (toAim / distToAim) : muzzlePoint.forward;
        float maxDist = Mathf.Max(0.01f, rangeOfProjectile);

        LastRay = new Ray(origin, dir);

        if (Physics.Raycast(LastRay, out RaycastHit hit, maxDist, hitLayers, triggerInteraction))
        {
            HasHit = true;
            LastHit = hit;
        }
    }

    private Vector3 ResolveAimPoint()
    {
        if (useCrosshairTransformAsAimPoint && crosshairTransform != null)
            return crosshairTransform.position;

        if (crosshairSettings != null)
            return crosshairSettings.AimPointClamped;

        if (crosshairTransform != null)
            return crosshairTransform.position;

        if (muzzlePoint != null)
            return muzzlePoint.position + muzzlePoint.forward * 10f;

        return Vector3.zero;
    }

    private void DrawDebugLineGame()
    {
        Vector3 a = LastRay.origin;
        Vector3 b;

        if (HasHit)
            b = LastHit.point;
        else
            b = AimPoint;

        Debug.DrawLine(a, b, bulletRayColor, 0f, false);
        Debug.DrawLine(AimPoint, AimPoint + Vector3.up * 0.15f, aimPointColor, 0f, false);
    }

    private void OnDrawGizmos()
    {
        if (!drawInSceneGizmos) return;

        if (!Application.isPlaying)
            ComputeShotRaycast();

        if (Application.isPlaying && _debugTimer <= 0f)
            return;

        Gizmos.color = bulletRayColor;

        Vector3 a = LastRay.origin;
        Vector3 b = HasHit ? LastHit.point : AimPoint;

        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(b, gizmoSphereRadius);

        Gizmos.color = aimPointColor;
        Gizmos.DrawWireSphere(AimPoint, gizmoSphereRadius * 0.9f);
    }

    // extra setters for TPS mode, not use yet.
    public void SetMuzzle(Transform t) => muzzlePoint = t;
    public void SetCrosshairSettings(PlayerCrossHairSettings s) => crosshairSettings = s;
    public void SetCrosshairTransform(Transform t) => crosshairTransform = t;
}