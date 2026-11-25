using UnityEngine;

/// <summary>One event per spent round, including a shotgun discharge, for future AI hearing.</summary>
public readonly struct WeaponShotInfo
{
    public GameObject Source { get; }
    public WeaponData Weapon { get; }
    public WeaponCategory Category => Weapon.Category;
    public Vector3 Position { get; }
    public float NoiseRadius { get; }
    public double Time { get; }

    public WeaponShotInfo(GameObject source, WeaponData weapon, Vector3 position, double time)
    {
        Source = source;
        Weapon = weapon;
        Position = position;
        NoiseRadius = weapon.NoiseRadius;
        Time = time;
    }
}
