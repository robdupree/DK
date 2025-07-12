using UnityEngine;
using UnityEngine.Tilemaps;

public class AdvancedMouseFollowerLight : MonoBehaviour
{
    [Header("References")]
    public Tilemap dungeonTilemap;
    public Camera playerCamera;

    [Header("Light Settings")]
    public Color lightColor = Color.white;
    [Range(0f, 10f)]
    public float lightIntensity = 3f;
    [Range(1f, 20f)]
    public float lightRange = 6f;
    public LightType lightType = LightType.Point;
    [Range(0f, 5f)]
    public float lightHeight = 2f;

    [Header("Movement & Behavior")]
    [Range(1f, 50f)]
    public float followSpeed = 15f;
    [Range(0.01f, 1f)]
    public float minMoveDistance = 0.1f;
    public bool useSmoothMovement = true;
    public bool snapToTileCenter = false;

    [Header("Effects")]
    public bool enableFlickering = false;
    [Range(0.1f, 5f)]
    public float flickerSpeed = 1f;
    [Range(0f, 1f)]
    public float flickerIntensity = 0.2f;

    public bool enableColorShift = false;
    public Color[] colorShiftPalette = { Color.white, Color.yellow, Color.cyan };
    [Range(0.1f, 10f)]
    public float colorShiftSpeed = 2f;

    [Header("Visual Marker")]
    public bool showVisualMarker = true;
    [Range(0.05f, 0.5f)]
    public float markerSize = 0.1f;
    public bool animateMarker = true;
    [Range(0.5f, 3f)]
    public float markerPulseSpeed = 1.5f;

    [Header("Raycast Settings")]
    public LayerMask tilemapLayerMask = -1;
    public float fallbackHeight = 1f;
    [Range(0f, 0.1f)]
    public float updateInterval = 0.02f; // 50 FPS für smooth movement

    [Header("Input Controls")]
    [Tooltip("Taste zum Ein-/Ausschalten des Lichts")]
    public KeyCode toggleKey = KeyCode.F;
    [Tooltip("Taste zum Durchschalten der Lichttypen")]
    public KeyCode cycleLightTypeKey = KeyCode.G;

    // Private Variablen
    private GameObject lightObject;
    private Light lightComponent;
    private GameObject visualMarker;
    private Vector3 targetPosition;
    private Vector3 currentVelocity;
    private float lastUpdateTime;

    // Effect Variablen
    private float baseIntensity;
    private Color baseColor;
    private int currentColorIndex = 0;
    private float colorShiftTimer = 0f;
    private float markerBaseScale;
    private bool isLightEnabled = true;

    // Light Type Cycling
    private LightType[] availableLightTypes = {
        LightType.Point,
        LightType.Spot,
        LightType.Directional
    };
    private int currentLightTypeIndex = 0;

    void Start()
    {
        InitializeReferences();
        CreateFollowerLight();

        // Base-Werte speichern für Effekte
        baseIntensity = lightIntensity;
        baseColor = lightColor;

        // Initiale Position
        UpdateTargetPosition();
        if (lightObject != null)
        {
            lightObject.transform.position = targetPosition;
        }
    }

    void InitializeReferences()
    {
        if (playerCamera == null)
            playerCamera = Camera.main ?? FindObjectOfType<Camera>();

        if (dungeonTilemap == null)
            dungeonTilemap = FindObjectOfType<Tilemap>();

        if (playerCamera == null)
            Debug.LogError("[AdvancedMouseFollowerLight] Keine Kamera gefunden!");
    }

    void CreateFollowerLight()
    {
        // Haupt-Light-GameObject
        lightObject = new GameObject("Advanced Mouse Follower Light");

        // Light-Komponente
        lightComponent = lightObject.AddComponent<Light>();
        lightComponent.type = lightType;
        lightComponent.color = lightColor;
        lightComponent.intensity = lightIntensity;
        lightComponent.range = lightRange;
        lightComponent.shadows = LightShadows.Soft;

        // Spot Light spezifische Einstellungen
        if (lightType == LightType.Spot)
        {
            lightComponent.spotAngle = 60f;
            lightObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Nach unten zeigen
        }

        // Visueller Marker
        if (showVisualMarker)
        {
            CreateVisualMarker();
        }

        targetPosition = lightObject.transform.position;

        Debug.Log("[AdvancedMouseFollowerLight] Advanced Follower Light erstellt");
    }

    void CreateVisualMarker()
    {
        visualMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualMarker.name = "Advanced Light Marker";
        visualMarker.transform.SetParent(lightObject.transform);
        visualMarker.transform.localPosition = Vector3.zero;
        visualMarker.transform.localScale = Vector3.one * markerSize;
        markerBaseScale = markerSize;

        // Emissive Material
        Material emissiveMaterial = new Material(Shader.Find("Standard"));
        emissiveMaterial.EnableKeyword("_EMISSION");
        emissiveMaterial.SetColor("_EmissionColor", lightColor * lightIntensity * 0.5f);
        emissiveMaterial.SetColor("_Color", lightColor);
        emissiveMaterial.SetFloat("_Metallic", 0.5f);
        emissiveMaterial.SetFloat("_Smoothness", 0.8f);

        visualMarker.GetComponent<Renderer>().material = emissiveMaterial;

        // Collider entfernen
        DestroyImmediate(visualMarker.GetComponent<Collider>());
    }

    void Update()
    {
        HandleInput();

        // Performance: Update-Intervall
        if (updateInterval > 0f && Time.time - lastUpdateTime < updateInterval)
            return;
        lastUpdateTime = Time.time;

        if (!isLightEnabled) return;

        UpdateTargetPosition();
        MoveLightToTarget();
        UpdateEffects();
    }

    void HandleInput()
    {
        // Licht ein-/ausschalten
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleLight();
        }

        // Lichttyp wechseln
        if (Input.GetKeyDown(cycleLightTypeKey))
        {
            CycleLightType();
        }
    }

    void UpdateTargetPosition()
    {
        if (playerCamera == null) return;

        Vector3? worldPosition = snapToTileCenter ?
            GetTilemapPositionUnderMouse() :
            GetWorldPositionUnderMouse();

        if (worldPosition.HasValue)
        {
            Vector3 newTarget = worldPosition.Value + Vector3.up * lightHeight;

            if (Vector3.Distance(targetPosition, newTarget) > minMoveDistance)
            {
                targetPosition = newTarget;
            }
        }
        else
        {
            // Fallback
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 fallbackPos = ray.origin + ray.direction * 10f;
            fallbackPos.y = fallbackHeight;

            if (Vector3.Distance(targetPosition, fallbackPos) > minMoveDistance)
            {
                targetPosition = fallbackPos;
            }
        }
    }

    void MoveLightToTarget()
    {
        if (lightObject == null) return;

        Vector3 currentPos = lightObject.transform.position;

        if (useSmoothMovement)
        {
            lightObject.transform.position = Vector3.SmoothDamp(
                currentPos, targetPosition, ref currentVelocity, 1f / followSpeed);
        }
        else
        {
            lightObject.transform.position = Vector3.Lerp(
                currentPos, targetPosition, followSpeed * Time.deltaTime);
        }

        // Spot Light Rotation für nach unten zeigen
        if (lightComponent.type == LightType.Spot)
        {
            lightObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    void UpdateEffects()
    {
        if (lightComponent == null) return;

        // Flickering Effect
        if (enableFlickering)
        {
            float flicker = Mathf.Sin(Time.time * flickerSpeed * 10f) * flickerIntensity;
            lightComponent.intensity = baseIntensity + flicker;
        }
        else
        {
            lightComponent.intensity = baseIntensity;
        }

        // Color Shifting
        if (enableColorShift && colorShiftPalette.Length > 1)
        {
            colorShiftTimer += Time.deltaTime * colorShiftSpeed;

            if (colorShiftTimer >= 1f)
            {
                colorShiftTimer = 0f;
                currentColorIndex = (currentColorIndex + 1) % colorShiftPalette.Length;
            }

            int nextIndex = (currentColorIndex + 1) % colorShiftPalette.Length;
            Color currentColor = Color.Lerp(
                colorShiftPalette[currentColorIndex],
                colorShiftPalette[nextIndex],
                colorShiftTimer
            );

            lightComponent.color = currentColor;

            // Marker-Material aktualisieren
            if (visualMarker != null)
            {
                Material mat = visualMarker.GetComponent<Renderer>().material;
                mat.SetColor("_EmissionColor", currentColor * lightComponent.intensity * 0.5f);
                mat.SetColor("_Color", currentColor);
            }
        }

        // Marker Animation
        if (animateMarker && visualMarker != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * markerPulseSpeed * 5f) * 0.3f;
            visualMarker.transform.localScale = Vector3.one * markerBaseScale * pulse;
        }
    }

    Vector3? GetWorldPositionUnderMouse()
    {
        if (playerCamera == null) return null;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, tilemapLayerMask);

        if (hits.Length == 0) return null;

        RaycastHit closestHit = hits[0];
        for (int i = 1; i < hits.Length; i++)
        {
            if (hits[i].distance < closestHit.distance)
                closestHit = hits[i];
        }

        return closestHit.point;
    }

    Vector3? GetTilemapPositionUnderMouse()
    {
        Vector3? worldPos = GetWorldPositionUnderMouse();
        if (!worldPos.HasValue || dungeonTilemap == null) return worldPos;

        Vector3Int tilePos = dungeonTilemap.WorldToCell(worldPos.Value);
        return dungeonTilemap.GetCellCenterWorld(tilePos);
    }

    #region Public Methods

    public void ToggleLight()
    {
        isLightEnabled = !isLightEnabled;
        if (lightObject != null)
        {
            lightObject.SetActive(isLightEnabled);
        }
        Debug.Log($"[AdvancedMouseFollowerLight] Licht {(isLightEnabled ? "aktiviert" : "deaktiviert")}");
    }

    public void CycleLightType()
    {
        if (lightComponent == null) return;

        currentLightTypeIndex = (currentLightTypeIndex + 1) % availableLightTypes.Length;
        lightComponent.type = availableLightTypes[currentLightTypeIndex];

        // Spezielle Einstellungen für verschiedene Lichttypen
        switch (lightComponent.type)
        {
            case LightType.Point:
                lightObject.transform.rotation = Quaternion.identity;
                break;
            case LightType.Spot:
                lightComponent.spotAngle = 60f;
                lightObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            case LightType.Directional:
                lightObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                break;
        }

        Debug.Log($"[AdvancedMouseFollowerLight] Lichttyp gewechselt zu: {lightComponent.type}");
    }

    public void SetLightSettings(Color color, float intensity, float range)
    {
        baseColor = color;
        baseIntensity = intensity;
        lightRange = range;

        if (lightComponent != null)
        {
            if (!enableColorShift) lightComponent.color = color;
            if (!enableFlickering) lightComponent.intensity = intensity;
            lightComponent.range = range;
        }
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Mouse Ray
        if (playerCamera != null)
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(ray.origin, ray.direction * 25f);
        }

        // Target Position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPosition, 0.3f);

        // Light Range
        if (lightObject != null && lightComponent != null)
        {
            Gizmos.color = lightComponent.color;
            Gizmos.DrawWireSphere(lightObject.transform.position, lightComponent.range);

            // Spot Light Cone
            if (lightComponent.type == LightType.Spot)
            {
                float halfAngle = lightComponent.spotAngle * 0.5f * Mathf.Deg2Rad;
                float coneRadius = Mathf.Tan(halfAngle) * lightComponent.range;
                Gizmos.DrawWireCube(
                    lightObject.transform.position + Vector3.down * lightComponent.range * 0.5f,
                    new Vector3(coneRadius * 2f, lightComponent.range, coneRadius * 2f)
                );
            }
        }

        // Hit Point
        Vector3? hitPoint = GetWorldPositionUnderMouse();
        if (hitPoint.HasValue)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(hitPoint.Value, Vector3.one * 0.2f);
        }
    }

    void OnDestroy()
    {
        if (lightObject != null)
            DestroyImmediate(lightObject);
    }
}