using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class GrenadeProjectile : MonoBehaviour
{
    public static event System.Action<Vector3, float> Detonated;
    [SerializeField, Min(.1f)]
    private float fuse = 2.5f;
    [SerializeField, Min(.1f)]
    private float radius = 6;
    [SerializeField, Min(0)]
    private float damage = 100;
    [SerializeField, Min(0)]
    private float explosionForce = 850;
    [SerializeField]
    private ParticleSystem explosionEffect;
    [SerializeField]
    private AudioClip explosionAudio;
    private float detonateAt;
    private bool exploded;
    private readonly Collider[] buffer = new Collider[128];
    private readonly HashSet<EnemyHealth> damaged = new HashSet<EnemyHealth>();
    private readonly HashSet<Rigidbody> bodies = new HashSet<Rigidbody>();
    private void Awake() => detonateAt = Time.time + fuse;
    public void Launch(GameObject owner, Vector3 velocity)
    {
        var own = GetComponent<Collider>();
        foreach (var c in owner.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(own, c);
        GetComponent<Rigidbody>().linearVelocity = velocity;
    }

    private void Update()
    {
        if (Time.time >= detonateAt)
            Explode();
    }

    public void Explode()
    {
        if (exploded)
            return;
        exploded = true;
        Detonated?.Invoke(transform.position, radius * 7);
        GetComponent<Collider>().enabled = false;
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Collider[] hits = buffer;
        if (count == buffer.Length)
        {
            hits = Physics.OverlapSphere(transform.position, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        for (int i = 0; i < count; i++)
        {
            var c = hits[i];
            var enemy = c.GetComponentInParent<EnemyHealth>();
            bool occluded = Physics.Linecast(transform.position, c.bounds.center, 1 << 8, QueryTriggerInteraction.Ignore);
            if (enemy != null && !occluded && damaged.Add(enemy))
                enemy.TakeDamage(damage * Mathf.Lerp(1, .25f, Mathf.Clamp01(Vector3.Distance(transform.position, c.bounds.center) / radius)));
            var barricade = c.GetComponentInParent<DestructibleBarricade>();
            if (barricade != null)
                barricade.TakeDamage(damage);
            var body = c.attachedRigidbody;
            if (body != null && !body.isKinematic && bodies.Add(body))
                body.AddExplosionForce(explosionForce, transform.position, radius, 1, ForceMode.Impulse);
        }

        if (explosionEffect != null)
        {
            var effect = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, 5);
        }

        if (explosionAudio != null)
            AudioSource.PlayClipAtPoint(explosionAudio, transform.position, .7f);
        Destroy(gameObject);
    }
}
