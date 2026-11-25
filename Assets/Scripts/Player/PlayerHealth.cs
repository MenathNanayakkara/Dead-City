using System;
using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)]
    private float maxHealth = 100f;
    public float MaxHealth
    {
        get
        {
            return maxHealth;
        }
    }

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>Raised with the new current health and the configured maximum health.</summary>
    public event Action<float, float> HealthChanged;
    public event Action Died;
    private void Awake()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        RaiseHealthChanged();
        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        RaiseHealthChanged();
    }

    public void Die()
    {
        if (IsDead)
        {
            return;
        }

        bool healthChanged = CurrentHealth > 0f;
        IsDead = true;
        CurrentHealth = 0f;
        if (healthChanged)
        {
            RaiseHealthChanged();
        }

        Died?.Invoke();
    }

    private void RaiseHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
    }
}
