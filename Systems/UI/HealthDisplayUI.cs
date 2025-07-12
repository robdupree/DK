using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Einfache UI-Komponente zum Umschalten der Health-Anzeige
/// Kann an einen Button oder Toggle in der UI angehängt werden
/// </summary>
public class HealthDisplayUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Button zum Umschalten (optional)")]
    public Button toggleButton;

    [Tooltip("Toggle zum Ein-/Ausschalten (optional)")]
    public Toggle healthToggle;

    [Tooltip("Text auf dem Button (wird automatisch aktualisiert)")]
    public Text buttonText;

    [Header("Settings")]
    [Tooltip("Text wenn Health Display aktiv ist")]
    public string enabledText = "Health: EIN";

    [Tooltip("Text wenn Health Display deaktiviert ist")]
    public string disabledText = "Health: AUS";

    void Start()
    {
        // Button Setup
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(OnToggleButtonClicked);
        }

        // Toggle Setup
        if (healthToggle != null)
        {
            healthToggle.onValueChanged.AddListener(OnToggleValueChanged);

            // Initial state setzen
            if (HealthDisplayManager.Instance != null)
            {
                healthToggle.isOn = HealthDisplayManager.Instance.showHealthDisplay;
            }
            else
            {
                healthToggle.isOn = DungeonTileData.GlobalHealthDisplayEnabled;
            }
        }

        // Initial button text setzen
        UpdateButtonText();
    }

    /// <summary>
    /// Button Click Handler
    /// </summary>
    public void OnToggleButtonClicked()
    {
        // Priorität: HealthDisplayManager > DungeonTileData static
        if (HealthDisplayManager.Instance != null)
        {
            HealthDisplayManager.Instance.ToggleHealthDisplay();
        }
        else
        {
            DungeonTileData.ToggleGlobalHealthDisplay();
        }

        // UI aktualisieren
        UpdateButtonText();
        UpdateToggleState();
    }

    /// <summary>
    /// Toggle Value Changed Handler
    /// </summary>
    public void OnToggleValueChanged(bool isOn)
    {
        // Priorität: HealthDisplayManager > DungeonTileData static
        if (HealthDisplayManager.Instance != null)
        {
            HealthDisplayManager.Instance.SetHealthDisplayVisible(isOn);
        }
        else
        {
            DungeonTileData.SetGlobalHealthDisplayEnabled(isOn);
        }

        // Button text aktualisieren
        UpdateButtonText();
    }

    /// <summary>
    /// Button Text aktualisieren
    /// </summary>
    void UpdateButtonText()
    {
        if (buttonText == null) return;

        bool isEnabled = GetCurrentHealthDisplayState();
        buttonText.text = isEnabled ? enabledText : disabledText;
    }

    /// <summary>
    /// Toggle State aktualisieren
    /// </summary>
    void UpdateToggleState()
    {
        if (healthToggle == null) return;

        bool isEnabled = GetCurrentHealthDisplayState();
        healthToggle.isOn = isEnabled;
    }

    /// <summary>
    /// Aktuellen Health Display Status ermitteln
    /// </summary>
    bool GetCurrentHealthDisplayState()
    {
        if (HealthDisplayManager.Instance != null)
        {
            return HealthDisplayManager.Instance.showHealthDisplay;
        }
        else
        {
            return DungeonTileData.GlobalHealthDisplayEnabled;
        }
    }

    void Update()
    {
        // UI-Synchronisation falls externe Änderungen passieren
        if (Time.frameCount % 60 == 0) // Nur alle 60 Frames prüfen
        {
            UpdateButtonText();
            UpdateToggleState();
        }
    }

    // Öffentliche Methoden für direkten Aufruf
    public void EnableHealthDisplay()
    {
        if (HealthDisplayManager.Instance != null)
        {
            HealthDisplayManager.Instance.SetHealthDisplayVisible(true);
        }
        else
        {
            DungeonTileData.SetGlobalHealthDisplayEnabled(true);
        }
        UpdateButtonText();
        UpdateToggleState();
    }

    public void DisableHealthDisplay()
    {
        if (HealthDisplayManager.Instance != null)
        {
            HealthDisplayManager.Instance.SetHealthDisplayVisible(false);
        }
        else
        {
            DungeonTileData.SetGlobalHealthDisplayEnabled(false);
        }
        UpdateButtonText();
        UpdateToggleState();
    }
}