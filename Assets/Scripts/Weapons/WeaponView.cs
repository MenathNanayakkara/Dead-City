using UnityEngine;

/// <summary>Visible weapon with a grip-origin root, +Z barrel, muzzle, flash and audio hooks.</summary>
[DisallowMultipleComponent]
public sealed class WeaponView : MonoBehaviour
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform supportHand;
    [SerializeField] private Transform primaryHand;
    [SerializeField] private ParticleSystem muzzleFlash;
    [Tooltip("A tiny emissive mesh used as a package-free greybox flash.")]
    [SerializeField] private GameObject greyboxFlash;
    [SerializeField] private AudioSource shotAudio;
    [SerializeField] private AudioSource reloadAudio;
    private WeaponData data;
    private float flashUntil;
    private Quaternion restRotation;
    private WeaponMechanism mechanism;
    private ParticleSystem[] flashParts = System.Array.Empty<ParticleSystem>();

    public Transform Muzzle => muzzle;
    public Transform SupportHand => supportHand;
    public Vector3 PrimaryHandPosition => primaryHand != null ? primaryHand.position :
        transform.TransformPoint(new Vector3(0, -.015f, -.025f));
    public Vector3 ReloadHandPosition => mechanism != null && mechanism.Magazine != null ?
        mechanism.Magazine.position + transform.TransformVector(new Vector3(-.025f, -.025f, 0)) :
        transform.TransformPoint(new Vector3(-.04f, -.08f, .15f));

    public void Initialize(WeaponData settings)
    {
        data = settings;
        mechanism = GetComponent<WeaponMechanism>();
        restRotation = transform.localRotation;
        if (data.MuzzleFlashPrefab != null && muzzle != null)
        {
            if (muzzleFlash != null) muzzleFlash.gameObject.SetActive(false);
            muzzleFlash = Instantiate(data.MuzzleFlashPrefab, muzzle, false);
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (greyboxFlash != null) greyboxFlash.SetActive(false);
        if (muzzleFlash != null)
            flashParts = muzzleFlash.GetComponentsInChildren<ParticleSystem>(true);
    }

    public void AimAt(Vector3 point, bool aiming)
    {
        if (!aiming) { transform.localRotation = restRotation; return; }
        Vector3 forward = point - transform.position;
        if (forward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    public void PlayShot()
    {
        if (mechanism != null) mechanism.Fire();
        if (muzzleFlash != null)
        {
            // Restart only the brief flash. Smoke already emitted by earlier
            // rounds can finish drifting instead of popping off on every shot.
            muzzleFlash.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play(false);
            foreach (var part in flashParts)
            {
                if (part == null || part == muzzleFlash) continue;
                if (!part.isPlaying) part.Play(false);
                part.Emit(1);
            }
        }
        else if (greyboxFlash != null)
        {
            greyboxFlash.SetActive(true);
            flashUntil = Time.time + 0.045f;
        }
        if (shotAudio != null && data.FireAudio != null)
            shotAudio.PlayOneShot(data.FireAudio, data.AudioVolume);
    }

    public void PlayReload()
    {
        if (mechanism != null && data != null) mechanism.Reload(data.ReloadTime);
        if (reloadAudio != null && data.ReloadAudio != null)
            reloadAudio.PlayOneShot(data.ReloadAudio, data.AudioVolume);
    }

    public void StopReload()
    {
        if (reloadAudio != null) reloadAudio.Stop();
        if (mechanism != null) mechanism.StopReload();
    }

    private void Update()
    {
        if (greyboxFlash != null && Time.time >= flashUntil) greyboxFlash.SetActive(false);
    }

    private void OnDisable()
    {
        StopReload();
        if (shotAudio != null) shotAudio.Stop();
        if (greyboxFlash != null) greyboxFlash.SetActive(false);
        if (muzzleFlash != null) muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
