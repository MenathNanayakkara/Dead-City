using UnityEngine;

/// <summary>Camera selection and muzzle collision queries. Never applies damage itself.</summary>
public sealed class WeaponHitscan
{
    private readonly Transform owner;
    private readonly int layers;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Collider[] overlaps = new Collider[16];

    public WeaponHitscan(Transform owner, int layers) { this.owner = owner; this.layers = layers; }

    private bool IsExternal(Collider collider) => collider != null && !collider.transform.IsChildOf(owner);

    public bool Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, layers, QueryTriggerInteraction.Ignore);
        // NonAlloc order is undefined; a full buffer may have omitted the nearest hit.
        RaycastHit[] results = count == hits.Length ?
            Physics.RaycastAll(origin, direction, distance, layers, QueryTriggerInteraction.Ignore) : hits;
        if (results != hits) count = results.Length;
        nearest = default;
        float best = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (!IsExternal(results[i].collider) || results[i].distance >= best) continue;
            nearest = results[i];
            best = nearest.distance;
        }
        return best < float.PositiveInfinity;
    }

    public bool OverlapsWorld(Vector3 position, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(position, radius, overlaps, layers, QueryTriggerInteraction.Ignore);
        Collider[] results = count == overlaps.Length ?
            Physics.OverlapSphere(position, radius, layers, QueryTriggerInteraction.Ignore) : overlaps;
        if (results != overlaps) count = results.Length;
        for (int i = 0; i < count; i++) if (IsExternal(results[i])) return true;
        return false;
    }

    public Vector3 AimPoint(Camera camera, float range, Vector3 fallbackOrigin, out bool cameraBlocked)
    {
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        cameraBlocked = OverlapsWorld(ray.origin, 0.01f);
        if (cameraBlocked) return fallbackOrigin + camera.transform.forward * range;
        return Raycast(ray.origin, ray.direction, range, out RaycastHit hit) ? hit.point : ray.GetPoint(range);
    }

    public static Vector3 SpreadDirection(Vector3 forward, float degrees)
    {
        // Uniform solid-angle cone, not a square pattern or a centre-biased disk.
        float cosTheta = Mathf.Lerp(Mathf.Cos(degrees * Mathf.Deg2Rad), 1f, Random.value);
        float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));
        float phi = Random.value * Mathf.PI * 2f;
        return Quaternion.LookRotation(forward, Vector3.up) *
            new Vector3(sinTheta * Mathf.Cos(phi), sinTheta * Mathf.Sin(phi), cosTheta);
    }
}
