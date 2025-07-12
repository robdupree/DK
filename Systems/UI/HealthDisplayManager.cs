using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manager zum Ein-/Ausblenden der Health-Anzeige auf Tiles
/// </summary>
public class HealthDisplayManager : MonoBehaviour
{
    [Header("Health Display Settings")]
    [Tooltip("Zeige Health-Werte auf den Tiles an")]
    public bool showHealthDisplay = true;

    [Header("UI References")]
    [Tooltip("Canvas oder Parent-Objekt aller Health-Displays")]
    public GameObject healthDisplayParent;

    [Header("Hotkey Settings")]
    [Tooltip("Taste zum Umschalten der Health-Anzeige")]
    public KeyCode toggleKey = KeyCode.H;

    // Cache für alle Health-Display Komponenten
    private List<Canvas> healthDisplayCanvases = new List<Canvas>();
    private List<TextMesh> healthDisplayTexts = new List<TextMesh>();
    private List<GameObject> healthDisplayObjects = new List<GameObject>();

    public static HealthDisplayManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Automatisch alle Health-Display Komponenten finden
        FindAllHealthDisplays();

        // Initial state setzen
        UpdateHealthDisplayVisibility();
    }

    void Update()
    {
        // Hotkey für Toggle
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleHealthDisplay();
        }
    }

    /// <summary>
    /// Findet automatisch alle Health-Display Komponenten in der Szene
    /// </summary>
    void FindAllHealthDisplays()
    {
        healthDisplayCanvases.Clear();
        healthDisplayTexts.Clear();
        healthDisplayObjects.Clear();

        // Methode 1: Über Parent-Objekt (falls gesetzt)
        if (healthDisplayParent != null)
        {
            Canvas[] canvases = healthDisplayParent.GetComponentsInChildren<Canvas>();
            TextMesh[] textMeshes = healthDisplayParent.GetComponentsInChildren<TextMesh>();

            healthDisplayCanvases.AddRange(canvases);
            healthDisplayTexts.AddRange(textMeshes);
        }

        // Methode 2: Über Tags (benötigt, dass Health-Displays getaggt sind)
        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag("HealthDisplay");
        healthDisplayObjects.AddRange(taggedObjects);

        // Methode 3: Über Namen-Pattern (z.B. alle GameObjects mit "Health" im Namen)
        TextMesh[] allTextMeshes = FindObjectsOfType<TextMesh>();
        foreach (var textMesh in allTextMeshes)
        {
            if (textMesh.gameObject.name.ToLower().Contains("health") ||
                textMesh.gameObject.name.ToLower().Contains("hp") ||
                textMesh.text.All(char.IsDigit)) // Nur Zahlen = wahrscheinlich Health
            {
                if (!healthDisplayTexts.Contains(textMesh))
                {
                    healthDisplayTexts.Add(textMesh);
                }
            }
        }

        Debug.Log($"[HealthDisplayManager] Gefunden: {healthDisplayCanvases.Count} Canvases, " +
                  $"{healthDisplayTexts.Count} TextMeshes, {healthDisplayObjects.Count} Tagged Objects");
    }

    /// <summary>
    /// Schaltet die Health-Anzeige um
    /// </summary>
    public void ToggleHealthDisplay()
    {
        showHealthDisplay = !showHealthDisplay;
        UpdateHealthDisplayVisibility();

        Debug.Log($"[HealthDisplayManager] Health Display: {(showHealthDisplay ? "EIN" : "AUS")}");
    }

    /// <summary>
    /// Setzt die Health-Anzeige explizit
    /// </summary>
    public void SetHealthDisplayVisible(bool visible)
    {
        showHealthDisplay = visible;
        UpdateHealthDisplayVisibility();
    }

    /// <summary>
    /// Aktualisiert die Sichtbarkeit aller Health-Displays
    /// </summary>
    void UpdateHealthDisplayVisibility()
    {
        // Canvas-basierte Displays
        foreach (var canvas in healthDisplayCanvases)
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(showHealthDisplay);
            }
        }

        // TextMesh-basierte Displays
        foreach (var textMesh in healthDisplayTexts)
        {
            if (textMesh != null)
            {
                textMesh.gameObject.SetActive(showHealthDisplay);
            }
        }

        // Tagged Objects
        foreach (var obj in healthDisplayObjects)
        {
            if (obj != null)
            {
                obj.SetActive(showHealthDisplay);
            }
        }

        // Parent-Objekt (falls gesetzt)
        if (healthDisplayParent != null)
        {
            healthDisplayParent.SetActive(showHealthDisplay);
        }
    }

    /// <summary>
    /// Registriert ein neues Health-Display zur Verwaltung
    /// </summary>
    public void RegisterHealthDisplay(GameObject healthDisplay)
    {
        if (!healthDisplayObjects.Contains(healthDisplay))
        {
            healthDisplayObjects.Add(healthDisplay);
            healthDisplay.SetActive(showHealthDisplay);
        }
    }

    /// <summary>
    /// Entfernt ein Health-Display aus der Verwaltung
    /// </summary>
    public void UnregisterHealthDisplay(GameObject healthDisplay)
    {
        healthDisplayObjects.Remove(healthDisplay);
    }

    /// <summary>
    /// Aktualisiert die Liste aller Health-Displays (nützlich wenn neue spawnen)
    /// </summary>
    [ContextMenu("Refresh Health Displays")]
    public void RefreshHealthDisplays()
    {
        FindAllHealthDisplays();
        UpdateHealthDisplayVisibility();
    }

    // GUI für Debug/Testing
    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 120, 300, 100));
        GUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("=== Health Display Manager ===");

        bool newState = GUILayout.Toggle(showHealthDisplay, "Health Display anzeigen");
        if (newState != showHealthDisplay)
        {
            SetHealthDisplayVisible(newState);
        }

        GUILayout.Label($"Gefunden: {healthDisplayCanvases.Count + healthDisplayTexts.Count + healthDisplayObjects.Count} Displays");
        GUILayout.Label($"Hotkey: {toggleKey}");

        if (GUILayout.Button("Refresh Displays"))
        {
            RefreshHealthDisplays();
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}