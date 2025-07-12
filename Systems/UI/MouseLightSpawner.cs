using UnityEngine;
using UnityEngine.Tilemaps;

public class MouseFollowerLight : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Das Tilemap des Dungeons")]
    public Tilemap dungeonTilemap;

    [Tooltip("Kamera (falls leer wird automatisch gesucht)")]
    public Camera playerCamera;

    [Header("Light Settings")]
    [Tooltip("H�he �ber dem Boden")]
    public float lightHeight = 2f;

    [Tooltip("Farbe der Lichtquelle")]
    public Color lightColor = Color.white;

    [Tooltip("Intensit�t der Lichtquelle")]
    public float lightIntensity = 3f;

    [Tooltip("Reichweite der Lichtquelle")]
    public float lightRange = 6f;

    [Tooltip("Typ der Lichtquelle")]
    public LightType lightType = LightType.Point;

    [Header("Movement Settings")]
    [Tooltip("Geschwindigkeit der Licht-Bewegung (h�her = schneller)")]
    public float followSpeed = 15f;

    [Tooltip("Minimale Distanz f�r Bewegung (verhindert Zittern)")]
    public float minMoveDistance = 0.1f;

    [Tooltip("Smoothing f�r weichere Bewegung")]
    public bool useSmoothMovement = true;

    [Header("Raycast Settings")]
    [Tooltip("Layer f�r Raycast (sollte Tilemap-Collider enthalten)")]
    public LayerMask tilemapLayerMask = -1;

    [Tooltip("Fallback-H�he wenn kein Boden getroffen wird")]
    public float fallbackHeight = 1f;

    [Header("Visual Settings")]
    [Tooltip("Soll ein visueller Marker angezeigt werden?")]
    public bool showVisualMarker = true;

    [Tooltip("Gr��e des visuellen Markers")]
    public float markerSize = 0.1f;

    [Header("Performance")]
    [Tooltip("Update-Intervall (0 = jeden Frame, h�her = weniger oft)")]
    public float updateInterval = 0f;

    // Private Variablen
    private GameObject lightObject;
    private Light lightComponent;
    private GameObject visualMarker;
    private Vector3 targetPosition;
    private Vector3 currentVelocity;
    private float lastUpdateTime;

    void Start()
    {
        InitializeReferences();
        CreateFollowerLight();

        // Initiale Position setzen
        UpdateTargetPosition();
        if (lightObject != null)
        {
            lightObject.transform.position = targetPosition;
        }
    }

    void InitializeReferences()
    {
        // Auto-Setup der Referenzen
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
                playerCamera = FindObjectOfType<Camera>();
        }

        if (dungeonTilemap == null)
        {
            dungeonTilemap = FindObjectOfType<Tilemap>();
        }

        if (playerCamera == null)
        {
            Debug.LogError("[MouseFollowerLight] Keine Kamera gefunden!");
        }
    }

    void CreateFollowerLight()
    {
        // Haupt-Light-GameObject erstellen
        lightObject = new GameObject("Mouse Follower Light");
        lightObject.transform.position = Vector3.zero;

        // Light-Komponente hinzuf�gen
        lightComponent = lightObject.AddComponent<Light>();
        lightComponent.type = lightType;
        lightComponent.color = lightColor;
        lightComponent.intensity = lightIntensity;
        lightComponent.range = lightRange;
        lightComponent.shadows = LightShadows.Soft;

        // Visueller Marker (optional)
        if (showVisualMarker)
        {
            CreateVisualMarker();
        }

        // Target-Position initialisieren
        targetPosition = lightObject.transform.position;

        Debug.Log("[MouseFollowerLight] Follower Light erstellt und bereit");
    }

    void CreateVisualMarker()
    {
        visualMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualMarker.name = "Light Marker";
        visualMarker.transform.SetParent(lightObject.transform);
        visualMarker.transform.localPosition = Vector3.zero;
        visualMarker.transform.localScale = Vector3.one * markerSize;

        // Emissive Material f�r Sichtbarkeit
        Material emissiveMaterial = new Material(Shader.Find("Standard"));
        emissiveMaterial.EnableKeyword("_EMISSION");
        emissiveMaterial.SetColor("_EmissionColor", lightColor * lightIntensity * 0.3f);
        emissiveMaterial.SetColor("_Color", lightColor);

        Renderer markerRenderer = visualMarker.GetComponent<Renderer>();
        markerRenderer.material = emissiveMaterial;

        // Collider entfernen (nicht n�tig f�r visuellen Marker)
        Collider markerCollider = visualMarker.GetComponent<Collider>();
        if (markerCollider != null)
            DestroyImmediate(markerCollider);
    }

    void Update()
    {
        // Performance: Update-Intervall ber�cksichtigen
        if (updateInterval > 0f && Time.time - lastUpdateTime < updateInterval)
            return;

        lastUpdateTime = Time.time;

        // Target-Position basierend auf Mausposition aktualisieren
        UpdateTargetPosition();

        // Licht zur Target-Position bewegen
        MoveLightToTarget();
    }

    void UpdateTargetPosition()
    {
        if (playerCamera == null)
            return;

        Vector3? worldPosition = GetWorldPositionUnderMouse();

        if (worldPosition.HasValue)
        {
            Vector3 newTarget = worldPosition.Value + Vector3.up * lightHeight;

            // Nur aktualisieren wenn sich die Position signifikant ge�ndert hat
            if (Vector3.Distance(targetPosition, newTarget) > minMoveDistance)
            {
                targetPosition = newTarget;
            }
        }
        else
        {
            // Fallback: Berechne Position basierend auf Maus-Ray
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 fallbackPos = ray.origin + ray.direction * 10f; // 10 Units vor der Kamera
            fallbackPos.y = fallbackHeight;

            if (Vector3.Distance(targetPosition, fallbackPos) > minMoveDistance)
            {
                targetPosition = fallbackPos;
            }
        }
    }

    void MoveLightToTarget()
    {
        if (lightObject == null)
            return;

        Vector3 currentPos = lightObject.transform.position;

        if (useSmoothMovement)
        {
            // Smooth movement mit SmoothDamp
            lightObject.transform.position = Vector3.SmoothDamp(
                currentPos,
                targetPosition,
                ref currentVelocity,
                1f / followSpeed
            );
        }
        else
        {
            // Direkte Bewegung mit Lerp
            lightObject.transform.position = Vector3.Lerp(
                currentPos,
                targetPosition,
                followSpeed * Time.deltaTime
            );
        }
    }

    Vector3? GetWorldPositionUnderMouse()
    {
        if (playerCamera == null)
            return null;

        // Ray von Kamera zur Mausposition
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        // Raycast gegen Tilemap-Layer
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, tilemapLayerMask);

        if (hits.Length == 0)
            return null;

        // Finde n�chsten Treffer
        RaycastHit closestHit = hits[0];
        float closestDistance = closestHit.distance;

        for (int i = 1; i < hits.Length; i++)
        {
            if (hits[i].distance < closestDistance)
            {
                closestHit = hits[i];
                closestDistance = hits[i].distance;
            }
        }

        return closestHit.point;
    }

    // Alternative Methode f�r Tilemap-spezifische Positionierung
    Vector3? GetTilemapPositionUnderMouse()
    {
        if (playerCamera == null || dungeonTilemap == null)
            return null;

        Vector3? worldPos = GetWorldPositionUnderMouse();
        if (!worldPos.HasValue)
            return null;

        // Konvertiere zu Tile-Position und zur�ck f�r Snap-to-Grid Effekt
        Vector3Int tilePos = dungeonTilemap.WorldToCell(worldPos.Value);
        Vector3 tileCenterWorld = dungeonTilemap.GetCellCenterWorld(tilePos);

        return tileCenterWorld;
    }

    #region Public Methods

    /// <summary>
    /// �ndert die Lichtfarbe zur Laufzeit
    /// </summary>
    public void SetLightColor(Color newColor)
    {
        lightColor = newColor;
        if (lightComponent != null)
        {
            lightComponent.color = newColor;
        }

        // Marker-Material auch aktualisieren
        if (visualMarker != null)
        {
            Material mat = visualMarker.GetComponent<Renderer>().material;
            mat.SetColor("_EmissionColor", newColor * lightIntensity * 0.3f);
            mat.SetColor("_Color", newColor);
        }
    }

    /// <summary>
    /// �ndert die Lichtintensit�t zur Laufzeit
    /// </summary>
    public void SetLightIntensity(float newIntensity)
    {
        lightIntensity = newIntensity;
        if (lightComponent != null)
        {
            lightComponent.intensity = newIntensity;
        }
    }

    /// <summary>
    /// �ndert die Lichtreichweite zur Laufzeit
    /// </summary>
    public void SetLightRange(float newRange)
    {
        lightRange = newRange;
        if (lightComponent != null)
        {
            lightComponent.range = newRange;
        }
    }

    /// <summary>
    /// Ein-/Ausschalten des Lichts
    /// </summary>
    public void SetLightEnabled(bool enabled)
    {
        if (lightObject != null)
        {
            lightObject.SetActive(enabled);
        }
    }

    /// <summary>
    /// Snap-to-Grid Modus umschalten
    /// </summary>
    public void SetSnapToGrid(bool snapToGrid)
    {
        // Diese Funktionalit�t kann erweitert werden
        minMoveDistance = snapToGrid ? 1f : 0.1f;
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;

        // Zeichne Mouse Ray
        if (playerCamera != null)
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(ray.origin, ray.direction * 25f);
        }

        // Zeichne Target Position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPosition, 0.5f);

        // Zeichne Light Range
        if (lightObject != null)
        {
            Gizmos.color = lightColor;
            Gizmos.DrawWireSphere(lightObject.transform.position, lightRange);
        }

        // Zeige Maus-Hit-Punkt
        Vector3? hitPoint = GetWorldPositionUnderMouse();
        if (hitPoint.HasValue)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(hitPoint.Value, Vector3.one * 0.2f);
        }
    }

    void OnDestroy()
    {
        // Cleanup beim Zerst�ren der Komponente
        if (lightObject != null)
        {
            DestroyImmediate(lightObject);
        }
    }

    #endregion
}