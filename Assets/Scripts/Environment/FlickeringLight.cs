using UnityEngine;

[RequireComponent(typeof(Light))]
public sealed class FlickeringLight : MonoBehaviour
{
    [SerializeField]
    private float minimum = .6f;
    [SerializeField]
    private float maximum = 1.3f;
    private Light lamp;
    private float next;
    private void Awake() => lamp = GetComponent<Light>();
    private void Update()
    {
        if (Time.time < next)
            return;
        next = Time.time + Random.Range(.08f, .35f);
        lamp.intensity = Random.Range(minimum, maximum);
    }
}
