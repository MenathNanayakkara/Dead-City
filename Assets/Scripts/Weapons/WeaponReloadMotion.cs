using UnityEngine;

/// <summary>Shared normalized timing for the weapon, magazine and support hand.</summary>
public static class WeaponReloadMotion
{
    public static float Ease(float value, float start, float end)
        => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));

    public static float Hold(float time)
        => Ease(time, 0f, .18f) * (1f - Ease(time, .82f, 1f));

    public static float HandContact(float time)
        => Ease(time, .06f, .22f) * (1f - Ease(time, .78f, .96f));

    public static float MagazineTravel(float time)
        => Ease(time, .24f, .44f) * (1f - Ease(time, .54f, .76f));
}
