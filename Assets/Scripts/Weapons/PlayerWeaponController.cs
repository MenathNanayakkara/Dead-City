using System;
using UnityEngine;

/// <summary>One shooter for all four weapons. LateUpdate fires after camera and animated hand updates.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAimController), typeof(PlayerHealth))]
public sealed class PlayerWeaponController : MonoBehaviour
{
    [Header("Phase 2 references")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private ThirdPersonCameraController cameraRig;
    [SerializeField] private PlayerAimController aim;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerAnimatorDriver animationDriver;
    [SerializeField] private Transform weaponSocket;
    [Tooltip("A point inside the player's upper body; checks for a barrel pushed through cover.")]
    [SerializeField] private Transform shotOrigin;
    [SerializeField] private Transform aimTarget;

    [Header("Fixed loadout: Rifle, SMG, Shotgun, Pistol")]
    [SerializeField] private WeaponData[] loadout = Array.Empty<WeaponData>();
    [SerializeField, Min(0)] private int startingWeapon;
    [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.001f)] private float muzzleClearance = 0.025f;
    [SerializeField, Min(0.01f)] private float firingActivitySeconds = 0.2f;
    [SerializeField] private bool drawShotRays = true;

    private WeaponAmmoState[] ammo;
    private WeaponView[] views;
    private float[] bloom;
    private WeaponHitscan hitscan;
    private PlayerStanceController stance;
    private FirstPersonWeaponRig firstPersonRig;
    public bool IsFirstPerson => cameraRig != null && cameraRig.IsFirstPerson;
    private bool FiringPoseAllowed => IsFirstPerson ? firstPersonRig != null && !firstPersonRig.IsSprinting : aim.IsAiming;
    private Vector3 GuardPosition => IsFirstPerson ? viewCamera.transform.position : shotOrigin.position-Vector3.up*(stance!=null?.38f*stance.Blend:0);
    private int selected = -1;
    private bool fireRequested;
    private bool triggerArmed;
    private bool initialized;

    public WeaponData CurrentWeapon => selected >= 0 ? loadout[selected] : null;
    public WeaponCategory CurrentWeaponCategory => CurrentWeapon != null ? CurrentWeapon.Category : WeaponCategory.None;
    public int CurrentWeaponIndex => selected;
    public int MagazineAmmo => CurrentAmmo != null ? CurrentAmmo.Magazine : 0;
    public int ReserveAmmo => CurrentAmmo != null ? CurrentAmmo.Reserve : 0;
    public bool IsReloading => CurrentAmmo != null && CurrentAmmo.IsReloading;
    public bool IsFiring => isActiveAndEnabled && health != null && !health.IsDead &&
        Time.timeAsDouble - LastShotTime <= firingActivitySeconds;
    public double LastShotTime { get; private set; } = double.NegativeInfinity;
    public Vector3 LastShotPosition { get; private set; }
    public Vector3 CurrentAimPoint { get; private set; }
    public bool LastShotBlocked { get; private set; }
    public event Action<WeaponData> WeaponChanged;
    public event Action<int, int> AmmoChanged;
    public event Action<bool> ReloadStateChanged;
    public event Action<WeaponShotInfo> ShotFired;
    public event Action<RaycastHit, float> HitDetected;
    private WeaponAmmoState CurrentAmmo => selected >= 0 && ammo != null ? ammo[selected] : null;
    private WeaponView CurrentView => selected >= 0 && views != null ? views[selected] : null;
    public WeaponView ActiveView => CurrentView;

    public bool ConfigureLoadout(WeaponData[] selection)
    {
        if (initialized || selection == null || selection.Length == 0) return false;
        foreach (var data in selection) if (data == null || data.WeaponPrefab == null) return false;
        loadout = (WeaponData[])selection.Clone(); startingWeapon = 0; return true;
    }

    public void GrantReserveMagazines(int magazines)
    {
        if (!initialized || magazines <= 0) return;
        for (int i=0;i<ammo.Length;i++) ammo[i].AddReserve(loadout[i].MagazineSize * magazines);
        AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
    }

    private void Awake()
    {
        stance=GetComponent<PlayerStanceController>();
        firstPersonRig=GetComponent<FirstPersonWeaponRig>();
        if (aim == null) aim = GetComponent<PlayerAimController>();
        if (health == null) health = GetComponent<PlayerHealth>();
        if (animationDriver == null) animationDriver = GetComponent<PlayerAnimatorDriver>();
        if (viewCamera == null) viewCamera = Camera.main;
        if (cameraRig == null && viewCamera != null) cameraRig = viewCamera.GetComponent<ThirdPersonCameraController>();
        if (weaponSocket == null || shotOrigin == null || viewCamera == null || aim == null || health == null || loadout.Length == 0)
        {
            Debug.LogError("PlayerWeaponController needs its Phase 2 references and a non-empty weapon loadout.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < loadout.Length; i++)
        {
            if (loadout[i] == null || loadout[i].WeaponPrefab == null || loadout[i].WeaponPrefab.Muzzle == null)
            {
                Debug.LogError("Weapon slot " + i + " needs WeaponData, a WeaponView prefab and a Muzzle reference.", this);
                enabled = false;
                return;
            }
        }
        hitscan = new WeaponHitscan(transform, hitLayers);
        ammo = new WeaponAmmoState[loadout.Length];
        views = new WeaponView[loadout.Length];
        bloom = new float[loadout.Length];
        for (int i = 0; i < loadout.Length; i++)
        {
            WeaponData data = loadout[i];
            ammo[i] = new WeaponAmmoState(data.MagazineSize, data.ReserveAmmo, data.FireRate, data.ReloadTime);
            views[i] = Instantiate(data.WeaponPrefab, weaponSocket, false);
            views[i].name = data.WeaponName;
            views[i].Initialize(data);
            if (IsFirstPerson && firstPersonRig != null) firstPersonRig.ConfigureView(views[i]);
            views[i].gameObject.SetActive(false);
        }
        initialized = true;
        SelectWeapon(Mathf.Clamp(startingWeapon, 0, loadout.Length - 1));
    }

    private void OnEnable()
    {
        if (health != null) health.Died += OnDeath;
        if (CurrentView != null && health != null && !health.IsDead) CurrentView.gameObject.SetActive(true);
        triggerArmed = false;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= OnDeath;
        CancelReload();
        if (CurrentView != null) CurrentView.gameObject.SetActive(false);
        fireRequested = false;
        triggerArmed = false;
    }

    private bool CanUseWeapons() => initialized && isActiveAndEnabled && !health.IsDead;
    private bool InputAllowed() => CanUseWeapons() && Application.isFocused &&
        Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f;

    private void Update()
    {
        fireRequested = false;
        if (!CanUseWeapons()) return;
        for (int i = 0; i < bloom.Length; i++)
            bloom[i] = Mathf.MoveTowards(bloom[i], 0f, loadout[i].SpreadRecovery * Time.deltaTime);
        if (CurrentAmmo.TickReload(Time.timeAsDouble))
        {
            FinishReloadPresentation();
            AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
            ReloadStateChanged?.Invoke(false);
        }
        if (!InputAllowed()) { triggerArmed = false; return; }
        bool held = Input.GetMouseButton(0);
        if (!held) triggerArmed = true;
        if (cameraRig != null && Time.frameCount <= cameraRig.CursorLockFrame) triggerArmed = false;

        for (int i = 0; i < Mathf.Min(loadout.Length, 9); i++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SelectWeapon(i);
        float wheel = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 0.01f)
            SelectWeapon((selected + (wheel > 0f ? 1 : loadout.Length - 1)) % loadout.Length);
        if (Input.GetKeyDown(KeyCode.R)) TryReload();
        fireRequested = triggerArmed && FiringPoseAllowed &&
            (CurrentWeapon.Automatic ? held : Input.GetMouseButtonDown(0));
    }

    private void LateUpdate()
    {
        if (!CanUseWeapons()) return;
        WeaponData data = CurrentWeapon;
        CurrentAimPoint = hitscan.AimPoint(viewCamera, data.Range, GuardPosition, out _);
        if (aimTarget != null) aimTarget.position = CurrentAimPoint;
        if (!IsFirstPerson) CurrentView.AimAt(CurrentAimPoint, aim.IsAiming && !IsReloading);
        if (!fireRequested || !InputAllowed() || !FiringPoseAllowed) return;
        fireRequested = false;
        TryFire();
    }

    /// <summary>Attempts one round. Enforces the firing stance, life, ammo and cooldown.</summary>
    public bool TryFire()
    {
        if (!CanUseWeapons() || !FiringPoseAllowed || Time.timeScale <= 0f) return false;
        CurrentAimPoint = hitscan.AimPoint(viewCamera, CurrentWeapon.Range, GuardPosition, out bool cameraBlocked);
        if (!IsFirstPerson) CurrentView.AimAt(CurrentAimPoint, !IsReloading);
        return Fire(CurrentWeapon, cameraBlocked);
    }

    public bool SelectWeapon(int index)
    {
        if (!CanUseWeapons() || index < 0 || index >= loadout.Length || index == selected) return false;
        CancelReload();
        if (animationDriver != null) animationDriver.CancelWeaponAction();
        if (CurrentView != null) CurrentView.gameObject.SetActive(false);
        selected = index;
        CurrentView.gameObject.SetActive(true);
        triggerArmed = false; // Release the trigger after switching; never fires an unexpected semi-auto shot.
        fireRequested = false;
        WeaponChanged?.Invoke(CurrentWeapon);
        AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
        return true;
    }

    public bool TryReload()
    {
        if (!CanUseWeapons() || Time.timeScale <= 0f || !CurrentAmmo.TryStartReload(Time.timeAsDouble)) return false;
        fireRequested = false;
        if (animationDriver != null) animationDriver.PlayReload(CurrentWeapon.ReloadTime);
        CurrentView.PlayReload();
        ReloadStateChanged?.Invoke(true);
        return true;
    }

    public void CancelReload()
    {
        if (!IsReloading) return;
        CurrentAmmo.CancelReload();
        FinishReloadPresentation();
        ReloadStateChanged?.Invoke(false);
    }

    private void FinishReloadPresentation()
    {
        if (CurrentView != null) CurrentView.StopReload();
        if (animationDriver != null) animationDriver.CancelWeaponAction();
    }

    private void OnDeath()
    {
        CancelReload();
        fireRequested = false;
        triggerArmed = false;
        if (CurrentView != null) CurrentView.gameObject.SetActive(false);
    }

    private bool Fire(WeaponData data, bool cameraBlocked)
    {
        if (!CurrentAmmo.TryFire(Time.timeAsDouble)) return false;
        Transform muzzle = CurrentView.Muzzle;
        Vector3 origin = muzzle.position;
        Vector3 direction = CurrentAimPoint - origin;
        Vector3 guard = origin - GuardPosition;
        // Raycasts starting inside colliders miss them. Also prevent a protruding barrel firing through a thin wall.
        LastShotBlocked = cameraBlocked || direction.sqrMagnitude < 0.0001f ||
            Vector3.Dot(direction, viewCamera.transform.forward) <= 0f ||
            hitscan.OverlapsWorld(origin, muzzleClearance) ||
            hitscan.OverlapsWorld(GuardPosition, muzzleClearance) ||
            (guard.sqrMagnitude > 0.0001f && hitscan.Raycast(GuardPosition, guard.normalized, guard.magnitude, out _));
        float spread = Mathf.Min(45f, data.Spread * (IsFirstPerson && !aim.IsAiming ? 1.8f : 1f) + bloom[selected]);
        if (!LastShotBlocked)
        {
            for (int pellet = 0; pellet < data.Pellets; pellet++)
            {
                Vector3 shot = WeaponHitscan.SpreadDirection(direction.normalized, spread);
                Vector3 endpoint = origin + shot * data.Range;
                if (hitscan.Raycast(origin, shot, data.Range, out RaycastHit hit))
                {
                    endpoint = hit.point;
                    IDamageable receiver = hit.collider.GetComponentInParent<IDamageable>();
                    receiver?.TakeDamage(data.Damage);
                    HitDetected?.Invoke(hit, data.Damage);
                }
                if (drawShotRays) Debug.DrawLine(origin, endpoint, Color.yellow, 0.15f);
            }
        }
        else if (drawShotRays) Debug.DrawLine(GuardPosition, origin, Color.red, 0.3f);

        bloom[selected] = Mathf.Min(data.MaximumExtraSpread, bloom[selected] + data.SpreadPerShot);
        CurrentView.PlayShot();
        if (animationDriver != null) animationDriver.PlayFire();
        if (cameraRig != null) cameraRig.ApplyRecoil(data.RecoilPitch, UnityEngine.Random.Range(-data.RecoilYaw, data.RecoilYaw));
        LastShotTime = Time.timeAsDouble;
        LastShotPosition = origin;
        AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
        ShotFired?.Invoke(new WeaponShotInfo(gameObject, data, origin, LastShotTime));
        return true;
    }

    [ContextMenu("Phase 3/Log Current Weapon and Ammo (Play Mode)")]
    private void LogAmmo()
    {
        if (Application.isPlaying && CurrentWeapon != null)
            Debug.Log($"{CurrentWeapon.WeaponName}: {MagazineAmmo}/{ReserveAmmo}; reload={IsReloading}; firing={IsFiring}; last shot blocked={LastShotBlocked}", this);
    }
}
