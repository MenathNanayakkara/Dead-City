using UnityEngine;

/// <summary>A virtual camera pose; only Main Camera renders or owns an AudioListener.</summary>
[DisallowMultipleComponent]
public sealed class ThirdPersonCameraMode : MonoBehaviour
{
    [SerializeField, Min(0.3f)]
    private float distance = 4.5f;
    [SerializeField]
    private Vector2 shoulderOffset = new Vector2(0.2f, 0.05f);
    [SerializeField, Range(20f, 100f)]
    private float fieldOfView = 60f;
    [SerializeField, Min(0f)]
    private float sensitivityMultiplier = 1f;
    public float Distance { get => distance; set => distance = Mathf.Max(0.3f, value); }
    public Vector2 ShoulderOffset => shoulderOffset;
    public float FieldOfView => fieldOfView;
    public float SensitivityMultiplier => sensitivityMultiplier;
}
