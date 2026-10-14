using UnityEngine;

/// <summary>Cosmetic slide/bolt, pump and magazine motion. Ammo and impacts remain in the weapon controller.</summary>
[DefaultExecutionOrder(250), DisallowMultipleComponent]
public sealed class WeaponMechanism : MonoBehaviour
{
    [SerializeField] private Transform bolt;
    [SerializeField] private Transform magazine;
    [SerializeField] private bool pumpAction;
    [SerializeField, Min(0)] private float travel = .035f;
    private Vector3 boltRest, magazineRest;
    private float shotStart = -100, reloadStart = -100, reloadDuration;
    private bool reloading;
    public Transform Magazine => magazine;
    public bool IsPumpAction => pumpAction;

    private void Awake()
    {
        if (bolt != null) boltRest = bolt.localPosition;
        if (magazine != null) magazineRest = magazine.localPosition;
    }
    public void Fire() { shotStart = Time.time; }
    public void Reload(float seconds) { reloadStart = Time.time; reloadDuration = Mathf.Max(.01f, seconds); reloading = true; }
    public void StopReload() { reloading = false; if (magazine != null) magazine.localPosition = magazineRest; }
    private void LateUpdate()
    {
        if (bolt != null)
        {
            float t = (Time.time - shotStart - (pumpAction ? .16f : 0)) / (pumpAction ? .38f : .10f);
            float amount = t >= 0 && t <= 1 ? Mathf.Sin(t * Mathf.PI) : 0;
            Vector3 direction = bolt.parent.InverseTransformDirection(-transform.forward);
            bolt.localPosition = boltRest + direction * (travel * amount);
        }
        if (magazine != null && reloading)
        {
            float t = Mathf.Clamp01((Time.time - reloadStart) / reloadDuration);
            float amount = WeaponReloadMotion.MagazineTravel(t);
            magazine.localPosition = magazineRest + magazine.parent.InverseTransformDirection(-transform.up) * (amount * .16f);
            if (t >= 1) StopReload();
        }
    }
    private void OnDisable()
    {
        StopReload(); shotStart = -100;
        if (bolt != null) bolt.localPosition = boltRest;
    }
}
