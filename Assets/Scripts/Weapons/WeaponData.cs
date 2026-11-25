using UnityEngine;

/// <summary>Shared, read-only-at-runtime weapon tuning. Ammo belongs to WeaponAmmoState.</summary>
[CreateAssetMenu(menuName = "Dead city/Weapon Data", fileName = "NewWeapon")]
public sealed class WeaponData : ScriptableObject
{
    [SerializeField] private string weaponName = "Rifle";
    [SerializeField] private WeaponCategory category = WeaponCategory.Rifle;
    [SerializeField, Min(0f)] private float damage = 25f;
    [Tooltip("Shots per second, not rounds per minute.")]
    [SerializeField, Min(0.01f)] private float fireRate = 8f;
    [SerializeField, Min(1)] private int magazineSize = 30;
    [Tooltip("Starting loose rounds. The initial full magazine is additional.")]
    [SerializeField, Min(0)] private int reserveAmmo = 120;
    [SerializeField, Min(0.01f)] private float reloadTime = 2.2f;
    [SerializeField, Min(1f)] private float range = 100f;
    [SerializeField] private bool automatic = true;
    [Tooltip("Cone half-angle in degrees, per pellet.")]
    [SerializeField, Range(0f, 30f)] private float spread = 0.7f;
    [SerializeField, Range(1, 32)] private int pellets = 1;

    [Header("Presentation hooks")]
    [SerializeField] private WeaponView weaponPrefab;
    [SerializeField] private ParticleSystem muzzleFlashPrefab;
    [SerializeField] private AudioClip fireAudio;
    [SerializeField] private AudioClip reloadAudio;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 0.35f;
    [SerializeField, Min(0f)] private float recoilPitch = 0.7f;
    [SerializeField, Min(0f)] private float recoilYaw = 0.25f;
    [Tooltip("Extra spread added per shot, recovers over time.")]
    [SerializeField, Min(0f)] private float spreadPerShot = 0.25f;
    [SerializeField, Range(0f, 15f)] private float maximumExtraSpread = 2f;
    [SerializeField, Min(0f)] private float spreadRecovery = 3f;

    [Header("Future AI observation")]
    [Tooltip("Gameplay hearing radius in metres; independent of AudioSource volume.")]
    [SerializeField, Min(0f)] private float noiseRadius = 35f;

    public string WeaponName => weaponName;
    public WeaponCategory Category => category;
    public float Damage => Mathf.Max(0f, damage);
    public float FireRate => Mathf.Max(0.01f, fireRate);
    public int MagazineSize => Mathf.Max(1, magazineSize);
    public int ReserveAmmo => Mathf.Max(0, reserveAmmo);
    public float ReloadTime => Mathf.Max(0.01f, reloadTime);
    public float Range => Mathf.Max(1f, range);
    public bool Automatic => automatic;
    public float Spread => Mathf.Clamp(spread, 0f, 30f);
    public int Pellets => Mathf.Clamp(pellets, 1, 32);
    public WeaponView WeaponPrefab => weaponPrefab;
    public ParticleSystem MuzzleFlashPrefab => muzzleFlashPrefab;
    public AudioClip FireAudio => fireAudio;
    public AudioClip ReloadAudio => reloadAudio;
    public float AudioVolume => Mathf.Clamp01(audioVolume);
    public float RecoilPitch => Mathf.Max(0f, recoilPitch);
    public float RecoilYaw => Mathf.Max(0f, recoilYaw);
    public float SpreadPerShot => Mathf.Max(0f, spreadPerShot);
    public float MaximumExtraSpread => Mathf.Clamp(maximumExtraSpread, 0f, 15f);
    public float SpreadRecovery => Mathf.Max(0f, spreadRecovery);
    public float NoiseRadius => Mathf.Max(0f, noiseRadius);
}
