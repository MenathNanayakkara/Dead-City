using UnityEngine;

public enum MapMarkerKind
{
    PlayerSpawn,
    ZombieEntrance,
    Ambush,
    Cover,
    EliteArena
}

/// <summary>Authored positions consumed by spawning and tactical decisions.</summary>
public sealed class MapMarker : MonoBehaviour
{
    public MapMarkerKind kind;
    public float radius = 1.5f;
    private void OnDrawGizmos()
    {
        Gizmos.color = kind == MapMarkerKind.ZombieEntrance ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.DrawRay(transform.position, transform.forward * 2);
    }
}
