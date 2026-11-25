using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public sealed class DestructibleBarricade : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)]
    private float maxHealth = 100;
    [SerializeField]
    private GameObject debrisPrefab;
    public float Health { get; private set; }
    public bool IsDestroyed { get; private set; }

    public event Action Destroyed;
    private void Awake() => Health = maxHealth;
    public void TakeDamage(float amount)
    {
        if (amount <= 0 || IsDestroyed)
            return;
        Health = Mathf.Max(0, Health - amount);
        if (Health > 0)
            return;
        IsDestroyed = true;
        var obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle != null)
            obstacle.enabled = false;
        if (debrisPrefab != null)
            Instantiate(debrisPrefab, transform.position, transform.rotation);
        else
        {
            // The intact prop falls over visibly, then clears the route.
            var body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.None;
            body.AddTorque(transform.right * 80, ForceMode.Impulse);
        }

        Destroyed?.Invoke();
        Destroy(gameObject, 2f);
    }
}
