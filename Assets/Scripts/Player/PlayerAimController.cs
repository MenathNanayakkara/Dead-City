using System;
using UnityEngine;

/// <summary>Owns aim intent. Camera and animation read this state; future UI can subscribe.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class PlayerAimController : MonoBehaviour
{
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private KeyCode aimKey = KeyCode.Mouse1;
    [SerializeField]
    private bool requireLockedCursor = true;
    [SerializeField, Min(0f)]
    private float aimMoveSpeed = 2.2f;
    public bool IsAiming { get; private set; }
    public float AimMoveSpeed => aimMoveSpeed;

    public event Action<bool> AimChanged;
    private bool inputSuppressed;
    public void SetInputSuppressed(bool suppressed)
    {
        inputSuppressed = suppressed;
        if (suppressed)
            SetAiming(false);
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        bool canAim = !inputSuppressed && Time.timeScale > 0f && (health == null || !health.IsDead) && Application.isFocused && (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);
        SetAiming(canAim && Input.GetKey(aimKey));
    }

    public void SetAiming(bool value)
    {
        value = value && !inputSuppressed && isActiveAndEnabled && (health == null || !health.IsDead);
        if (IsAiming == value)
            return;
        IsAiming = value;
        AimChanged?.Invoke(value);
    }

    private void OnDisable() => SetAiming(false);
}
