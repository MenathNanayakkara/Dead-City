using System;

/// <summary>Per-owner state, independent of Unity, input, visuals and damage. Uses scaled seconds.</summary>
public sealed class WeaponAmmoState
{
    public int Magazine { get; private set; }
    public int Reserve { get; private set; }
    public int Capacity { get; }
    public bool IsReloading { get; private set; }
    public double ReloadEndsAt { get; private set; }
    public double NextFireTime { get; private set; }
    private readonly double fireInterval;
    private readonly double reloadDuration;

    public WeaponAmmoState(int capacity, int reserve, double shotsPerSecond, double reloadSeconds)
    {
        if (capacity < 1 || reserve < 0 || !FinitePositive(shotsPerSecond) || !FinitePositive(reloadSeconds))
            throw new ArgumentOutOfRangeException(nameof(capacity), "Invalid weapon ammo/timing settings.");
        Capacity = capacity;
        Magazine = capacity;
        Reserve = reserve;
        fireInterval = 1d / shotsPerSecond;
        reloadDuration = reloadSeconds;
    }

    public bool TryFire(double now)
    {
        if (IsReloading || Magazine <= 0 || now < NextFireTime) return false;
        Magazine--;
        // No burst of queued shots after a frame hitch. Cooldown survives weapon switching.
        NextFireTime = now + fireInterval;
        return true;
    }

    public bool TryStartReload(double now)
    {
        if (IsReloading || Magazine == Capacity || Reserve <= 0) return false;
        IsReloading = true;
        ReloadEndsAt = now + reloadDuration;
        return true;
    }

    public bool TickReload(double now)
    {
        if (!IsReloading || now < ReloadEndsAt) return false;
        int transferred = Math.Min(Capacity - Magazine, Reserve);
        Magazine += transferred;
        Reserve -= transferred;
        IsReloading = false;
        return true;
    }

    // Cancelling never deducts reserve rounds or fills a magazine.
    public void CancelReload() => IsReloading = false;
    public void AddReserve(int amount) { if (amount > 0) Reserve += amount; }
    private static bool FinitePositive(double value) => value > 0d && !double.IsInfinity(value) && !double.IsNaN(value);
}
