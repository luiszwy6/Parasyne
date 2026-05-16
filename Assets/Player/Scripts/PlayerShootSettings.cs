using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerShootSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MuzzlePointSettings muzzlePointSettings;
    [SerializeField] private PlayerWeaponDmg weaponDmg;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerCrossHairSettings crosshairSettings;
    [SerializeField] private CameraNoiseByMovement cameraNoiseByMovement;
    [SerializeField] private AmmoSettings ammoSettings; 

    [Header("Weapon SFX/VFX")]
    [SerializeField] private WeaponEffects weaponEffects;

    [Header("Aim Gate")]
    [SerializeField] private PlayerAimSettings aimSettings;

    [Header("Projectile (real logic)")]
    [SerializeField] private BulletProjectile bulletProjectilePrefab;
    [SerializeField] private Transform projectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float bulletSpeed = 120f;
    [Min(0.01f)] [SerializeField] private float bulletMaxDistance = 50f;
    [Min(0.001f)] [SerializeField] private float bulletRadius = 0.04f;
    [SerializeField] private LayerMask bulletHitMask = ~0;
    [SerializeField] private QueryTriggerInteraction projectileTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Projectile (visual only)")]
    [SerializeField] private BulletProjectileVisual visualBulletProjectilePrefab;
    [SerializeField] private bool spawnVisualProjectile = true;
    [SerializeField] private Transform visualProjectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float visualBulletSpeed = 150f;

    [Header("Fire Rate")]
    [Min(0f)]
    [SerializeField] private float shootCooldown = 0.12f;

    [Header("Animator")]
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private bool resetTriggerBeforeSet = true;

    [Header("Input (optional)")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string shootActionName = "Shoot";

    private InputAction shootAction;
    private InputAction reloadAction;
    private float nextShootTime;
    private int shootTriggerHash;

    private void Reset()
    {
        if (weaponDmg == null) weaponDmg = GetComponent<PlayerWeaponDmg>();
        if (aimSettings == null) aimSettings = GetComponent<PlayerAimSettings>();
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (crosshairSettings == null) crosshairSettings = FindFirstObjectByType<PlayerCrossHairSettings>();
        if (weaponEffects == null) weaponEffects = GetComponentInChildren<WeaponEffects>();
        if (ammoSettings == null) ammoSettings = GetComponent<AmmoSettings>(); 
    }

    private void Awake()
    {
        if (weaponDmg == null) weaponDmg = GetComponent<PlayerWeaponDmg>();
        if (aimSettings == null) aimSettings = GetComponent<PlayerAimSettings>();
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (weaponEffects == null) weaponEffects = GetComponentInChildren<WeaponEffects>();
        if (ammoSettings == null) ammoSettings = GetComponent<AmmoSettings>(); 

        if (playerInput != null && playerInput.actions != null)
            shootAction = playerInput.actions[shootActionName];
        if (playerInput != null && playerInput.actions != null)
            reloadAction = playerInput.actions["Reload"];

        shootTriggerHash = Animator.StringToHash(shootTriggerName);
        nextShootTime = 0f;
    }

    private void OnEnable()
    {
        if (shootAction != null)
        {
            shootAction.Enable();
            shootAction.performed += OnShootPerformed;
        }
        if (reloadAction != null)
        {
            reloadAction.Enable();
            reloadAction.performed += OnReloadPerformed;
        }
    }

    private void OnDisable()
    {
        if (shootAction != null)
        {
            shootAction.performed -= OnShootPerformed;
            shootAction.Disable();
        }
        if (reloadAction != null)
        {
            reloadAction.performed -= OnReloadPerformed;
            reloadAction.Disable();
        }
    }

    private void OnShootPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.ReadValueAsButton())
            return;

        Shoot();
    }

    private void OnReloadPerformed(InputAction.CallbackContext ctx)
    {
        ammoSettings.reload();
    }



    public void Shoot()
    {   
        if (ammoSettings.getBulletsInMag() <= 0 )
            return;
        if (shootCooldown > 0f && Time.time < nextShootTime)
            return;

        if (aimSettings != null && !aimSettings.IsAiming)
            return;

        if (muzzlePointSettings == null || bulletProjectilePrefab == null)
            return;

        ammoSettings.shoot(); 
        nextShootTime = Time.time + Mathf.Max(0f, shootCooldown);

        if (crosshairSettings != null)
            crosshairSettings.AddRecoil();

        if (animator != null)
        {
            if (resetTriggerBeforeSet) animator.ResetTrigger(shootTriggerHash);
            animator.SetTrigger(shootTriggerHash);
        }

        muzzlePointSettings.RequestDebugDraw();

        bool useTPSCenterRay = crosshairSettings != null && crosshairSettings.tpsMode;

        Ray shotRay;
        Vector3 origin;

        if (useTPSCenterRay && Camera.main != null)
        {
            shotRay = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            origin = shotRay.origin;
        }
        else
        {
            shotRay = muzzlePointSettings.LastRay;
            origin = projectileSpawnOverride != null ? projectileSpawnOverride.position : shotRay.origin;
        }

        Vector3 dir = shotRay.direction;

        float partDmg = (weaponDmg != null) ? weaponDmg.part_dmg : 0f;
        float baseDmg = (weaponDmg != null) ? weaponDmg.base_dmg : 0f;

        BulletProjectile bullet = Instantiate(bulletProjectilePrefab, origin, Quaternion.identity);
        bullet.Init(
            origin,
            dir,
            bulletSpeed,
            bulletMaxDistance,
            bulletRadius,
            partDmg,
            baseDmg,
            bulletHitMask,
            projectileTriggerInteraction
        );

        SpawnVisualProjectile(shotRay);

        if (weaponEffects != null)
            weaponEffects.PlayGunshot();
    }

    private void SpawnVisualProjectile(Ray shotRay)
    {
        if (!spawnVisualProjectile || visualBulletProjectilePrefab == null)
            return;

        Vector3 visualOrigin;

        if (visualProjectileSpawnOverride != null)
            visualOrigin = visualProjectileSpawnOverride.position;
        else if (projectileSpawnOverride != null)
            visualOrigin = projectileSpawnOverride.position;
        else
            visualOrigin = shotRay.origin;

        Vector3 targetPoint;
        if (Physics.Raycast(
            shotRay,
            out RaycastHit hit,
            bulletMaxDistance,
            bulletHitMask,
            projectileTriggerInteraction))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = shotRay.origin + shotRay.direction * bulletMaxDistance;
        }

        BulletProjectileVisual visualBullet =
            Instantiate(visualBulletProjectilePrefab, visualOrigin, Quaternion.identity);

        visualBullet.Init(visualOrigin, targetPoint, visualBulletSpeed);
    }
}