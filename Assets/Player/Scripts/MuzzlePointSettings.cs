using UnityEngine;

//This scipt should be attached to the weapon object, and use an empty child transform as the muzzle point. 
//It will compute the shot raycast based on the attached muzzle point and the aim point from PlayerCrossHairSettings (The 3d crosshair object in world), 
//then cache the results for PlayerShootSettings to read and draw debug info. 
//Use this to apply for weapons' shooting logic and bullet trajectory. 
//It can also be used as a share referenence for both TPS and top-down shooting.

[DisallowMultipleComponent]
public class MuzzlePointSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private PlayerCrossHairSettings crosshairSettings;
    [SerializeField] private Transform physicalBulletLandPoint;

    // In here, we would use the same physical bullet land point(an object) as the aim point from aim settings, so we can have the bullet trajectory raycast hit the same point as the crosshair aim point.
    // The reason to do this is to make sure the bullet trajectory raycast can hit the same point in both top-down and TPS mode, so we can have the same shooting logic and bullet trajectory for both modes.
    [Header("Aim Source")]
    [SerializeField] private bool usePhysicalBulletLandPointAsAimPoint = true;

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

    [SerializeField] private Color bulletRayColor = Color.red; 
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
    // It computes the shot raycast and caches results, then draws debug info for a short duration.
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
        // we need a muzzle point to compute the shot raycast
        if (muzzlePoint == null)
            return;
        // they raycast is origined from the muzzle point, and aimed for the aim point resolved from previous settings.
        AimPoint = ResolveAimPoint();
        Vector3 origin = muzzlePoint.position;
        //  Compute direction vector from muzzle to aim point, find distance and compute to normalize
        Vector3 toAim = AimPoint - origin;
        float distToAim = toAim.magnitude;
        
        Vector3 dir = (distToAim > 0.0001f) ? (toAim / distToAim) : muzzlePoint.forward;
        // To simulate the bullet projection better, we need to add some extra distance to raycast beyond the aim point.
        // However, since the bullet land point is attach to the object hits, this distance is mostly not that useful, but still add as a realistic factor for bullet trajectory.
        float maxDist = Mathf.Max(0.01f, rangeOfProjectile);

        LastRay = new Ray(origin, dir);
        // perfrm physical raycast, detect hits.
        if (Physics.Raycast(LastRay, out RaycastHit hit, maxDist, hitLayers, triggerInteraction))
        {
            HasHit = true;
            LastHit = hit;
        }
    }

    private Vector3 ResolveAimPoint()
    {
        if (usePhysicalBulletLandPointAsAimPoint && physicalBulletLandPoint != null)
            return physicalBulletLandPoint.position;

        if (crosshairSettings != null)
            return crosshairSettings.AimPointClamped;

        if (physicalBulletLandPoint != null)
            return physicalBulletLandPoint.position;

        if (muzzlePoint != null)
            return muzzlePoint.position + muzzlePoint.forward * 10f;

        return Vector3.zero;
    }

    private void DrawDebugLineGame()
    {
        Vector3 a = LastRay.origin;
        Vector3 b = HasHit ? LastHit.point : AimPoint;

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

    // Extra setters for TPS mode, not used yet.
    public void SetMuzzle(Transform t) => muzzlePoint = t;
    public void SetCrosshairSettings(PlayerCrossHairSettings s) => crosshairSettings = s;
    public void SetPhysicalBulletLandPoint(Transform t) => physicalBulletLandPoint = t;
}