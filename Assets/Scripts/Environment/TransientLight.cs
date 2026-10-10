using UnityEngine;

[RequireComponent(typeof(Light))]
public sealed class TransientLight : MonoBehaviour
{
    [SerializeField, Min(.01f)]
    private float duration = .18f;
    private Light lamp;
    private float intensity, started;
    private void Awake()
    {
        lamp = GetComponent<Light>();
        intensity = lamp.intensity;
        started = Time.time;
    }

    private void Update()
    {
        float fraction = (Time.time - started) / duration;
        lamp.intensity = intensity * Mathf.Clamp01(1 - fraction);
        if (fraction >= 1)
        {
            lamp.enabled = false;
            enabled = false;
        }
    }
}
