using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dieses Script sitzt am HealthBar-Prefab (Canvas). 
/// Es aktualisiert die Füllung basierend auf der Health-Komponente der übergeordneten Einheit
/// und positioniert die Leiste über dem Kopf mit Billboard-Effekt.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("Referenzen")]
    [Tooltip("Link zur Health-Komponente der Einheit.")]
    public Health targetHealth;

    [Tooltip("Das Image-Objekt, dessen Fill Amount die aktuelle Lebensanzeige ist.")]
    public Image fillImage;

    [Header("Einstellungen")]
    [Tooltip("Lokaler Y‐Offset relativ zum Einheitssprungpunkt (z. B. 'über dem Kopf').")]
    public float yOffset = 2f;

    private Camera mainCamera;

    private void Awake()
    {
        if (targetHealth == null)
        {
            Debug.LogError($"{name}: Kein Health-Ziel zugewiesen!");
            enabled = false;
            return;
        }

        if (fillImage == null)
        {
            Debug.LogError($"{name}: Kein Fill-Image zugewiesen!");
            enabled = false;
            return;
        }

        // Referenz auf die Hauptkamera
        mainCamera = Camera.main;

        // Registriere uns am OnHealthChanged-Event
        targetHealth.OnHealthChanged += HandleHealthChanged;

        // Initiale Anzeige setzen
        UpdateHealthBar(targetHealth.CurrentHealth, targetHealth.maxHealth);
    }

    private void OnDestroy()
    {
        // Deregistrieren, damit keine NullReferenzen entstehen
        if (targetHealth != null)
            targetHealth.OnHealthChanged -= HandleHealthChanged;
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich der Health-Wert ändert.
    /// </summary>
    private void HandleHealthChanged(int current, int max)
    {
        UpdateHealthBar(current, max);
    }

    /// <summary>
    /// Setzt den Fill Amount des Images auf current/max (0..1).
    /// </summary>
    private void UpdateHealthBar(int current, int max)
    {
        float ratio = 0f;
        if (max > 0)
            ratio = (float)current / max;
        fillImage.fillAmount = ratio;
    }

    private void LateUpdate()
    {
        // 1) Billboard: Richte den Canvas so aus, dass er immer zur Kamera blickt
        Vector3 dirToCamera = transform.position - mainCamera.transform.position;
        Quaternion lookRotation = Quaternion.LookRotation(dirToCamera);
        // Wir drehen nur um Y-Achse, damit die HealthBar nicht kippt
        Vector3 euler = lookRotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0, euler.y, 0);

        // 2) Positioniere die HealthBar über dem Kopf der Einheit
        //    Die HealthBar ist als Child des Einheiten-GameObjects angelegt (s.u.),
        //    daher können wir hier die lokale Position setzen.
        Vector3 localPos = Vector3.up * yOffset;
        transform.localPosition = localPos;
    }
}
