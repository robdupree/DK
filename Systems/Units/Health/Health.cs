using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Lebenspunkte")]
    [Tooltip("Maximale Lebenspunkte dieser Einheit.")]
    public int maxHealth = 10;

    // Aktuelle Lebenspunkte
    public int CurrentHealth { get; private set; }

    // Wird bei jeder Änderung der Health (Heal oder Damage) ausgelöst
    public event Action<int, int> OnHealthChanged;
    // Wird nur bei Schaden ausgelöst, gibt den Angreifer weiter
    public event Action<GameObject> OnDamagedBy;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    /// <summary>
    /// Zieht Schaden ab. Löst OnHealthChanged und OnDamagedBy aus.
    /// </summary>
    public void TakeDamage(int amount, GameObject source = null)
    {
        if (amount <= 0) return;

        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        // Angreifer event
        if (source != null)
            OnDamagedBy?.Invoke(source);

        Debug.Log($"{name} hat {amount} Schaden erhalten von {source?.name ?? "Unbekannt"}. HP: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth == 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;

        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        Debug.Log($"{name} wurde um {amount} geheilt. HP: {CurrentHealth}/{maxHealth}");
    }

    private void Die()
    {
        Debug.Log($"{name} ist gestorben.");
        Destroy(gameObject);
    }
}
