using System;
using UnityEngine;

public sealed class PlayerEquipment : MonoBehaviour
{
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private Camera viewCamera;
    [SerializeField]
    private GrenadeProjectile grenadePrefab;
    [SerializeField, Min(1)]
    private float healAmount = 45;
    [SerializeField]
    private float throwSpeed = 14;
    private float nextThrow;
    private ThirdPersonCameraController cameraRig;
    public int Medkits { get; private set; }
    public int Grenades { get; private set; }

    public event Action<int, int> Changed;
    private void Awake()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) cameraRig = viewCamera.GetComponent<ThirdPersonCameraController>();
    }
    public void Configure(EquipmentPack pack)
    {
        Medkits = pack.medkits;
        Grenades = pack.grenades;
        Changed?.Invoke(Medkits, Grenades);
    }

    private void Update()
    {
        if (Time.timeScale <= 0 || health.IsDead || Cursor.lockState != CursorLockMode.Locked || !Application.isFocused)
            return;
        if (Input.GetKeyDown(KeyCode.Q))
            UseMedkit();
        if (Input.GetKeyDown(KeyCode.G))
            ThrowGrenade();
    }

    public bool UseMedkit()
    {
        if (Time.timeScale <= 0 || health.IsDead || Medkits <= 0 || health.CurrentHealth >= health.MaxHealth)
            return false;
        Medkits--;
        health.Heal(healAmount);
        Changed?.Invoke(Medkits, Grenades);
        return true;
    }

    public bool ThrowGrenade()
    {
        if (Time.timeScale <= 0 || health.IsDead || Grenades <= 0 || grenadePrefab == null || viewCamera == null || Time.time < nextThrow)
            return false;
        var origin = cameraRig != null && cameraRig.IsFirstPerson ? viewCamera.transform.position : transform.position + Vector3.up * 1.5f;
        var direction = (viewCamera.transform.forward + Vector3.up * .18f).normalized;
        // Spawn at the body, never beyond nearby cover, and ignore only the owner's colliders.
        var projectile = Instantiate(grenadePrefab, origin, Quaternion.identity);
        projectile.Launch(gameObject, direction * throwSpeed);
        Grenades--;
        nextThrow = Time.time + .6f;
        Changed?.Invoke(Medkits, Grenades);
        return true;
    }
}
