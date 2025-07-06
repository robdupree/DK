using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;
using DK;
using Unity.AI.Navigation;

public class NavMeshSafeObjectCulling : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap referenceTilemap;
    [SerializeField] private Camera playerCamera;

    [Header("Culling Settings")]
    [SerializeField] private float updateInterval = 1f; // PERFORMANCE: 1s statt 0.3s
    [SerializeField] private int cullingBuffer = 2; // PERFORMANCE: 2 statt 3
    [SerializeField] private bool debugMode = false; // PERFORMANCE: GUI AUS

    [Header("Performance Settings")]
    [SerializeField] private int maxObjectsPerFrame = 15; // PERFORMANCE: 15 statt 50
    [SerializeField] private bool useChunkedProcessing = true;
    [SerializeField] private bool forceImmediateProcessing = false;

    [Header("NavMesh Protection")]
    [SerializeField] private bool protectNavMeshObjects = false; // PERFORMANCE: AUS für große Maps
    [SerializeField] private float navMeshInfluenceRadius = 3f;

    [Header("Debug Options")]
    [SerializeField] private bool disableCulling = false;
    [SerializeField] private bool showCullingBounds = false; // PERFORMANCE: AUS
    [SerializeField] private bool showDetailedLogs = false; // PERFORMANCE: AUS

    // Core Data
    private float lastUpdateTime;
    private BoundsInt lastVisibleBounds;
    private List<CullableObject> allCullableObjects = new List<CullableObject>();
    private HashSet<CullableObject> navMeshProtectedObjects = new HashSet<CullableObject>();

    // Chunked Processing
    private Queue<CullableObject> objectsToProcess = new Queue<CullableObject>();

    // Performance Stats
    private int culledThisFrame = 0;
    private int visibleThisFrame = 0;
    private int totalProcessedThisFrame = 0;

    // Cached Imp Positions
    private List<Vector3> cachedImpPositions = new List<Vector3>();
    private float lastImpCacheTime = 0f;
    private const float IMP_CACHE_INTERVAL = 2f; // PERFORMANCE: 2s Cache

    [System.Serializable]
    public class CullableObject
    {
        public GameObject gameObject;
        public Vector3Int tilePosition;
        public bool isCurrentlyActive;
        public bool isNavMeshRelevant;
        public CullingMethod cullingMethod;

        // Cached Components
        public Renderer[] cachedRenderers;
        public bool hasComponentCache = false;

        public CullableObject(GameObject go, Vector3Int pos)
        {
            gameObject = go;
            tilePosition = pos;
            isCurrentlyActive = go.activeSelf;

            AnalyzeNavMeshRelevance();
            DetermineCullingMethod();
        }

        private void AnalyzeNavMeshRelevance()
        {
            if (gameObject == null)
            {
                isNavMeshRelevant = false;
                return;
            }

            bool hasNavMeshObstacle = gameObject.GetComponent<NavMeshObstacle>() != null;
            bool hasNavMeshModifier = gameObject.GetComponent<NavMeshModifier>() != null;

            Collider col = gameObject.GetComponent<Collider>();
            bool hasRelevantCollider = col != null && !col.isTrigger;

            isNavMeshRelevant = hasNavMeshObstacle || hasNavMeshModifier || hasRelevantCollider;
        }

        private void DetermineCullingMethod()
        {
            cullingMethod = isNavMeshRelevant ? CullingMethod.RendererOnly : CullingMethod.GameObject;
        }

        public void CacheComponents()
        {
            if (hasComponentCache || gameObject == null) return;

            cachedRenderers = gameObject.GetComponentsInChildren<Renderer>();
            hasComponentCache = true;
        }
    }

    public enum CullingMethod
    {
        GameObject,
        RendererOnly,
        None
    }

    void Start()
    {
        if (referenceTilemap == null)
            referenceTilemap = FindObjectOfType<Tilemap>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        RegisterAllChildObjects();
        CacheAllComponents();

        Debug.Log($"[NavMeshSafeCulling] Initialized with {allCullableObjects.Count} objects");
    }

    void CacheAllComponents()
    {
        foreach (CullableObject cullObj in allCullableObjects)
        {
            cullObj.CacheComponents();
        }
        Debug.Log($"[NavMeshSafeCulling] Cached components for {allCullableObjects.Count} objects");
    }

    void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval) return;

        lastUpdateTime = Time.time;
        ResetFrameStats();

        if (disableCulling)
        {
            SetAllObjectsVisible(true);
            return;
        }

        BoundsInt visibleBounds = GetVisibleTileBounds();

        bool boundsChanged = visibleBounds != lastVisibleBounds;

        if (boundsChanged || forceImmediateProcessing)
        {
            lastVisibleBounds = visibleBounds;

            if (protectNavMeshObjects)
            {
                UpdateNavMeshProtection();
            }

            if (useChunkedProcessing && !forceImmediateProcessing)
            {
                StartChunkedProcessing(visibleBounds);
            }
            else
            {
                ProcessAllObjectsImmediate(visibleBounds);
            }
        }
        else
        {
            // Chunked Processing fortsetzen
            ContinueChunkedProcessing();
        }
    }

    void ResetFrameStats()
    {
        culledThisFrame = 0;
        visibleThisFrame = 0;
        totalProcessedThisFrame = 0;
    }

    BoundsInt GetVisibleTileBounds()
    {
        if (referenceTilemap == null || playerCamera == null)
            return new BoundsInt(0, 0, 0, 0, 0, 0);

        // ORIGINALE BOUNDS-BERECHNUNG (die funktioniert hat)
        Vector3[] frustumCorners = new Vector3[4];

        // Hole die Ebene der Tilemap (meist Y=0, aber könnte auch anders sein)
        float tilemapY = referenceTilemap.transform.position.y;

        // Erstelle Strahlen von der Kamera durch die Viewport-Ecken
        Ray[] cornerRays = new Ray[4];
        cornerRays[0] = playerCamera.ViewportPointToRay(new Vector3(0, 0, 0)); // Unten-Links
        cornerRays[1] = playerCamera.ViewportPointToRay(new Vector3(1, 0, 0)); // Unten-Rechts
        cornerRays[2] = playerCamera.ViewportPointToRay(new Vector3(0, 1, 0)); // Oben-Links
        cornerRays[3] = playerCamera.ViewportPointToRay(new Vector3(1, 1, 0)); // Oben-Rechts

        // Berechne Schnittpunkte mit der Tilemap-Ebene
        Plane tilemapPlane = new Plane(Vector3.up, new Vector3(0, tilemapY, 0));

        for (int i = 0; i < 4; i++)
        {
            float distance;
            if (tilemapPlane.Raycast(cornerRays[i], out distance))
            {
                frustumCorners[i] = cornerRays[i].GetPoint(distance);
            }
            else
            {
                // Fallback: Verwende einen Punkt weit entfernt in Strahlrichtung
                frustumCorners[i] = cornerRays[i].GetPoint(100f);
            }
        }

        // Finde Min/Max Weltkoordinaten
        Vector3 minWorld = frustumCorners[0];
        Vector3 maxWorld = frustumCorners[0];

        foreach (Vector3 corner in frustumCorners)
        {
            minWorld = Vector3.Min(minWorld, corner);
            maxWorld = Vector3.Max(maxWorld, corner);
        }

        // Weltkoordinaten zu Tile-Koordinaten konvertieren
        Vector3Int minCell = referenceTilemap.WorldToCell(minWorld);
        Vector3Int maxCell = referenceTilemap.WorldToCell(maxWorld);

        // Buffer hinzufügen
        minCell.x -= cullingBuffer;
        minCell.y -= cullingBuffer;
        maxCell.x += cullingBuffer;
        maxCell.y += cullingBuffer;

        return new BoundsInt(minCell.x, minCell.y, 0,
                            maxCell.x - minCell.x + 1,
                            maxCell.y - minCell.y + 1, 1);
    }

    void UpdateNavMeshProtection()
    {
        if (Time.time - lastImpCacheTime > IMP_CACHE_INTERVAL)
        {
            UpdateImpPositionCache();
            lastImpCacheTime = Time.time;
        }

        navMeshProtectedObjects.Clear();
        float radiusSquared = navMeshInfluenceRadius * navMeshInfluenceRadius;

        foreach (CullableObject cullObj in allCullableObjects)
        {
            if (!cullObj.isNavMeshRelevant || cullObj.gameObject == null) continue;

            Vector3 objPos = cullObj.gameObject.transform.position;

            foreach (Vector3 impPos in cachedImpPositions)
            {
                if ((objPos - impPos).sqrMagnitude <= radiusSquared)
                {
                    navMeshProtectedObjects.Add(cullObj);
                    break;
                }
            }
        }
    }

    void UpdateImpPositionCache()
    {
        cachedImpPositions.Clear();
        UnitAI[] allUnits = FindObjectsOfType<UnitAI>();

        foreach (UnitAI unit in allUnits)
        {
            if (unit != null)
                cachedImpPositions.Add(unit.transform.position);
        }
    }

    void StartChunkedProcessing(BoundsInt visibleBounds)
    {
        objectsToProcess.Clear();

        // Füge alle Objekte zur Verarbeitungsqueue hinzu
        foreach (CullableObject cullObj in allCullableObjects)
        {
            objectsToProcess.Enqueue(cullObj);
        }

        // Sofort mit Verarbeitung beginnen
        ContinueChunkedProcessing();
    }

    void ContinueChunkedProcessing()
    {
        int processed = 0;

        while (objectsToProcess.Count > 0 && processed < maxObjectsPerFrame)
        {
            CullableObject cullObj = objectsToProcess.Dequeue();
            ProcessSingleObject(cullObj, lastVisibleBounds);
            processed++;
            totalProcessedThisFrame++;
        }
    }

    void ProcessAllObjectsImmediate(BoundsInt visibleBounds)
    {
        foreach (CullableObject cullObj in allCullableObjects)
        {
            ProcessSingleObject(cullObj, visibleBounds);
            totalProcessedThisFrame++;
        }
    }

    void ProcessSingleObject(CullableObject cullObj, BoundsInt visibleBounds)
    {
        if (cullObj.gameObject == null) return;

        bool shouldBeVisible = ShouldObjectBeVisible(cullObj, visibleBounds);

        if (shouldBeVisible != cullObj.isCurrentlyActive)
        {
            ApplyCulling(cullObj, shouldBeVisible);
            cullObj.isCurrentlyActive = shouldBeVisible;

            if (shouldBeVisible)
                visibleThisFrame++;
            else
                culledThisFrame++;
        }
    }

    bool ShouldObjectBeVisible(CullableObject cullObj, BoundsInt visibleBounds)
    {
        // Basis-Sichtbarkeits-Check
        bool inBounds = visibleBounds.Contains(cullObj.tilePosition);

        if (!inBounds && protectNavMeshObjects && navMeshProtectedObjects.Contains(cullObj))
        {
            // NavMesh-geschützte Objekte bleiben sichtbar
            return true;
        }

        return inBounds;
    }

    void ApplyCulling(CullableObject cullObj, bool shouldBeVisible)
    {
        switch (cullObj.cullingMethod)
        {
            case CullingMethod.GameObject:
                cullObj.gameObject.SetActive(shouldBeVisible);
                break;

            case CullingMethod.RendererOnly:
                ApplyRendererOnlyCulling(cullObj, shouldBeVisible);
                break;

            case CullingMethod.None:
                // Nicht cullen
                break;
        }
    }

    void ApplyRendererOnlyCulling(CullableObject cullObj, bool shouldBeVisible)
    {
        if (cullObj.cachedRenderers != null)
        {
            foreach (Renderer renderer in cullObj.cachedRenderers)
            {
                if (renderer != null)
                    renderer.enabled = shouldBeVisible;
            }
        }
    }

    void SetAllObjectsVisible(bool visible)
    {
        foreach (CullableObject cullObj in allCullableObjects)
        {
            if (cullObj.gameObject == null) continue;

            if (cullObj.isCurrentlyActive != visible)
            {
                ApplyCulling(cullObj, visible);
                cullObj.isCurrentlyActive = visible;
            }
        }
    }

    // Registration Methods
    public void RegisterObject(GameObject obj, Vector3 worldPosition)
    {
        if (referenceTilemap == null) return;
        Vector3Int tilePos = referenceTilemap.WorldToCell(worldPosition);
        RegisterObject(obj, tilePos);
    }

    public void RegisterObject(GameObject obj, Vector3Int tilePosition)
    {
        CullableObject cullObj = new CullableObject(obj, tilePosition);
        cullObj.CacheComponents();
        allCullableObjects.Add(cullObj);
    }

    public void RegisterAllChildObjects()
    {
        allCullableObjects.Clear();
        Transform[] children = GetComponentsInChildren<Transform>();
        int registeredCount = 0;

        foreach (Transform child in children)
        {
            if (child != transform)
            {
                Vector3 childWorldPos = child.position;
                Vector3Int tilePos = referenceTilemap.WorldToCell(childWorldPos);

                CullableObject cullObj = new CullableObject(child.gameObject, tilePos);
                allCullableObjects.Add(cullObj);
                registeredCount++;
            }
        }

        Debug.Log($"[NavMeshSafeCulling] Registered {registeredCount} child objects");
    }

    // Minimal Debug GUI - nur wenn debugMode = true
    void OnGUI()
    {
        if (!debugMode) return; // PERFORMANCE: GUI komplett AUS wenn debugMode false

        GUILayout.BeginArea(new Rect(10, 300, 300, 200));
        GUILayout.Label("=== Culling Debug ===", GUI.skin.box);
        GUILayout.Label($"Objects: {allCullableObjects.Count}");
        GUILayout.Label($"Queue: {objectsToProcess.Count}");
        GUILayout.Label($"Processed: {totalProcessedThisFrame}");
        GUILayout.Label($"Visible: {visibleThisFrame} | Culled: {culledThisFrame}");

        if (GUILayout.Button("Force Update"))
        {
            forceImmediateProcessing = true;
            lastUpdateTime = 0f;
        }

        GUILayout.EndArea();
    }

    // Minimal Gizmos
    void OnDrawGizmos()
    {
        if (!debugMode || !Application.isPlaying || !showCullingBounds) return;

        // Nur sichtbaren Bereich zeichnen
        if (referenceTilemap != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 worldMin = referenceTilemap.CellToWorld(new Vector3Int(lastVisibleBounds.xMin, lastVisibleBounds.yMin, 0));
            Vector3 worldMax = referenceTilemap.CellToWorld(new Vector3Int(lastVisibleBounds.xMax, lastVisibleBounds.yMax, 0));
            Vector3 center = (worldMin + worldMax) / 2f;
            Vector3 size = worldMax - worldMin;
            Gizmos.DrawWireCube(center, size);
        }
    }
}