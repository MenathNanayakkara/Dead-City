using TMPro;
using UnityEngine;

/// <summary>Event-driven HUD. Serialized references only; no Update, polling or scene searches.</summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class PlayerHudPresenter : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private PlayerWeaponController weapons;
    [SerializeField]
    private PlayerAimController aim;
    [SerializeField]
    private GameUIController screens;
    [Header("Views")]
    [SerializeField]
    private GameObject crosshair;
    [SerializeField]
    private UnityEngine.UI.Image healthFill;
    [SerializeField]
    private TMP_Text healthText;
    [SerializeField]
    private TMP_Text weaponText;
    [SerializeField]
    private TMP_Text magazineText;
    [SerializeField]
    private TMP_Text reserveText;
    [SerializeField]
    private GameObject reloadingIndicator;
    [SerializeField]
    private CanvasGroup hudGroup;
    [Header("Health colours")]
    [SerializeField]
    private Color healthyColor = new Color(0.28f, 0.88f, 0.72f);
    [SerializeField]
    private Color lowHealthColor = new Color(1f, 0.32f, 0.3f);
    [SerializeField, Range(0f, 1f)]
    private float lowHealthFraction = 0.25f;
    private bool subscribed;
    private void OnEnable()
    {
        if (health == null || weapons == null || aim == null || screens == null || crosshair == null || healthFill == null || healthText == null || weaponText == null || magazineText == null || reserveText == null || reloadingIndicator == null || hudGroup == null)
        {
            Debug.LogError("PlayerHudPresenter is missing a source or UI reference. Run Phase 4 setup.", this);
            enabled = false;
            return;
        }

        health.HealthChanged += OnHealthChanged;
        health.Died += OnDied;
        weapons.WeaponChanged += OnWeaponChanged;
        weapons.AmmoChanged += OnAmmoChanged;
        weapons.ReloadStateChanged += OnReloadChanged;
        aim.AimChanged += OnAimChanged;
        screens.StateChanged += OnScreenChanged;
        subscribed = true;
        RefreshAll();
    }

    // Read again after every scene object's Awake. Covers initial values emitted before subscription.
    private void Start() => RefreshAll();
    private void OnDisable()
    {
        if (!subscribed)
            return;
        health.HealthChanged -= OnHealthChanged;
        health.Died -= OnDied;
        weapons.WeaponChanged -= OnWeaponChanged;
        weapons.AmmoChanged -= OnAmmoChanged;
        weapons.ReloadStateChanged -= OnReloadChanged;
        aim.AimChanged -= OnAimChanged;
        screens.StateChanged -= OnScreenChanged;
        subscribed = false;
    }

    public void RefreshAll()
    {
        if (!subscribed)
            return;
        OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        OnWeaponChanged(weapons.CurrentWeapon);
        OnAmmoChanged(weapons.MagazineAmmo, weapons.ReserveAmmo);
        OnReloadChanged(weapons.IsReloading);
        OnScreenChanged(screens.State);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        float fraction = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        healthFill.fillAmount = fraction;
        healthFill.color = fraction <= lowHealthFraction ? lowHealthColor : healthyColor;
        healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
        RefreshCrosshair();
    }

    private void OnWeaponChanged(WeaponData data)
    {
        weaponText.text = data != null ? data.WeaponName.ToUpperInvariant() : "UNARMED";
        OnAmmoChanged(weapons.MagazineAmmo, weapons.ReserveAmmo);
        OnReloadChanged(weapons.IsReloading);
    }

    private void OnAmmoChanged(int magazine, int reserve)
    {
        magazineText.text = magazine.ToString("00");
        reserveText.text = reserve.ToString("000");
    }

    private void OnReloadChanged(bool reloading)
    {
        reloadingIndicator.SetActive(reloading);
        RefreshCrosshair();
    }
    private void OnAimChanged(bool aiming) => RefreshCrosshair();
    private void OnDied() => RefreshCrosshair();
    private void OnScreenChanged(GameScreenState state)
    {
        hudGroup.alpha = state == GameScreenState.Playing ? 1f : 0f;
        RefreshCrosshair();
    }

    private void RefreshCrosshair()
    {
        crosshair.SetActive((aim.IsAiming || weapons.IsFirstPerson) && weapons.CurrentWeapon != null &&
            !weapons.IsReloading && !health.IsDead && health.CurrentHealth > 0f && screens.IsPlaying);
    }
}
