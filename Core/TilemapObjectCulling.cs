using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapObjectCulling : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap referenceTilemap; // Tilemap als Positionsreferenz
    [SerializeField] private Camera playerCamera;

    [Header("Culling Settings")]
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField] private int cullingBuffer = 3; // Extra Tiles am Rand
    [SerializeField] private bool debugMode = false;

    private float lastUpdateTime;
    private BoundsInt lastVisibleBounds;
    private Dictionary<Vector3Int, List<GameObject>> tilemapObjects = new Dictionary<Vector3Int, List<GameObject>>();
    private List<TilemapObject> allTilemapObjects = new List<TilemapObject>();

    [System.Serializable]
    public class TilemapObject
    {
        public GameObject gameObject;
        public Vector3Int tilePosition;
        public bool isCurrentlyActive;

        public TilemapObject(GameObject go, Vector3Int pos)
        {
            gameObject = go;
            tilePosition = pos;
            isCurrentlyActive = go.activeSelf;
        }
    }

    void Start()
    {
        // Auto-finde Tilemap wenn nicht gesetzt
        if (referenceTilemap == null)
            referenceTilemap = FindObjectOfType<Tilemap>();

        // Registriere alle Kinder automatisch
        RegisterAllChildObjects();
    }
    void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval) return;

        lastUpdateTime = Time.time;
        UpdateObjectCulling();
    }

    void UpdateObjectCulling()
    {
        BoundsInt visibleBounds = GetVisibleTileBounds();

        if (debugMode)
        {
            // Debug.Log("Camera Position: " + playerCamera.transform.position);
            // Debug.Log("Visible Bounds: " + visibleBounds);
            // Debug.Log("Visible Bounds Size: " + visibleBounds.size);

            if (allTilemapObjects.Count > 0)
            {
               // Debug.Log("First Object Position: " + allTilemapObjects[0].tilePosition);
                // Debug.Log("First Object World Pos: " + allTilemapObjects[0].gameObject.transform.position);
            }
        }

        if (visibleBounds != lastVisibleBounds)
        {
            lastVisibleBounds = visibleBounds;
            CullObjects(visibleBounds);
        }
    }

    BoundsInt GetVisibleTileBounds()
    {
        if (referenceTilemap == null || playerCamera == null)
            return new BoundsInt(0, 0, 0, 0, 0, 0);

        // Berechne die 4 Frustum-Ecken der Kamera auf der Tilemap-Ebene
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

      /*  if (debugMode)
        {
            Debug.Log("Frustum corners world: " + minWorld + " to " + maxWorld);
            Debug.Log("Tile bounds: " + minCell + " to " + maxCell);
        }
      */
        return new BoundsInt(minCell.x, minCell.y, 0,
                            maxCell.x - minCell.x + 1,
                            maxCell.y - minCell.y + 1, 1);
    }

    [Header("Debug Options")]
    [SerializeField] private bool disableCulling = false; // Neue Variable hinzufügen

    void CullObjects(BoundsInt visibleBounds)
    {
        foreach (TilemapObject tilemapObj in allTilemapObjects)
        {
            if (tilemapObj.gameObject == null) continue;

            bool shouldBeVisible;

            if (disableCulling)
            {
                // Culling deaktiviert - alle Objekte sichtbar
                shouldBeVisible = true;
            }
            else
            {
                shouldBeVisible = visibleBounds.Contains(tilemapObj.tilePosition);
            }

            if (shouldBeVisible != tilemapObj.isCurrentlyActive)
            {
                tilemapObj.gameObject.SetActive(shouldBeVisible);
                tilemapObj.isCurrentlyActive = shouldBeVisible;

                if (debugMode)
                {
                    // Debug.Log("Object at " + tilemapObj.tilePosition + " set to " + shouldBeVisible);
                }
            }
        }

      /*  if (debugMode)
        {
            Debug.Log("Culling Update - Visible Objects: " + GetActiveObjectCount() + "/" + allTilemapObjects.Count);
        }
      */
    }

    int GetActiveObjectCount()
    {
        int count = 0;
        foreach (TilemapObject obj in allTilemapObjects)
        {
            if (obj.isCurrentlyActive) count++;
        }
        return count;
    }

    // Öffentliche Methoden zum Hinzufügen/Entfernen von Objekten

    public void RegisterObject(GameObject obj, Vector3 worldPosition)
    {
        if (referenceTilemap == null)
        {
            Debug.LogError("Reference Tilemap not assigned!");
            return;
        }

        Vector3Int tilePos = referenceTilemap.WorldToCell(worldPosition);

      /*  if (debugMode)
        {
            Debug.Log("Registering object: " + obj.name +
                     " | World Pos: " + worldPosition +
                     " | Tile Pos: " + tilePos);
        }
      */
        RegisterObject(obj, tilePos);
    }

    public void RegisterObject(GameObject obj, Vector3Int tilePosition)
    {
     /*   if (debugMode)
        {
            Debug.Log("Direct register: " + obj.name + " at tile " + tilePosition);
        }
     */
        TilemapObject tilemapObj = new TilemapObject(obj, tilePosition);
        allTilemapObjects.Add(tilemapObj);

        // Füge zur Dictionary hinzu für schnelleren Zugriff
        if (!tilemapObjects.ContainsKey(tilePosition))
        {
            tilemapObjects[tilePosition] = new List<GameObject>();
        }
        tilemapObjects[tilePosition].Add(obj);
    }

    public void UnregisterObject(GameObject obj)
    {
        for (int i = allTilemapObjects.Count - 1; i >= 0; i--)
        {
            if (allTilemapObjects[i].gameObject == obj)
            {
                Vector3Int pos = allTilemapObjects[i].tilePosition;
                allTilemapObjects.RemoveAt(i);

                // Aus Dictionary entfernen
                if (tilemapObjects.ContainsKey(pos))
                {
                    tilemapObjects[pos].Remove(obj);
                    if (tilemapObjects[pos].Count == 0)
                    {
                        tilemapObjects.Remove(pos);
                    }
                }
                break;
            }
        }
    }

    public void RegisterAllChildObjects()
    {
        // Registriere alle Kinder dieses GameObjects automatisch
        Transform[] children = GetComponentsInChildren<Transform>();

        int registeredCount = 0;

        foreach (Transform child in children)
        {
            if (child != transform) // Überspringe das Parent-Objekt selbst
            {
                Vector3 childWorldPos = child.position;

                /* if (debugMode)
                {
                    Debug.Log("Attempting to register child: " + child.name +
                             " at world position: " + childWorldPos);
                }
                */
                RegisterObject(child.gameObject, childWorldPos);
                registeredCount++;
            }
        }

        Debug.Log("RegisterAllChildObjects completed. Registered: " + registeredCount + " objects.");
        Debug.Log("Total objects in system: " + allTilemapObjects.Count);

        // Zeige erste paar Objekte zur Kontrolle
        for (int i = 0; i < Mathf.Min(3, allTilemapObjects.Count); i++)
        {
            var obj = allTilemapObjects[i];
           /* Debug.Log("Object " + i + ": " + obj.gameObject.name +
                     " | World: " + obj.gameObject.transform.position +
                     " | Tile: " + obj.tilePosition);
           */
        }
    }

    [ContextMenu("Test Single Object Registration")]
    public void TestSingleObjectRegistration()
    {
        if (transform.childCount > 0)
        {
            Transform firstChild = transform.GetChild(0);
            Debug.Log("=== TESTING SINGLE OBJECT ===");
            Debug.Log("Child name: " + firstChild.name);
            Debug.Log("Child world position: " + firstChild.position);

            if (referenceTilemap != null)
            {
                Vector3Int tilePos = referenceTilemap.WorldToCell(firstChild.position);
                Debug.Log("Calculated tile position: " + tilePos);

                // Test conversion back
                Vector3 backToWorld = referenceTilemap.CellToWorld(tilePos);
                Debug.Log("Tile back to world: " + backToWorld);
            }
            else
            {
                Debug.LogError("Reference tilemap is null!");
            }
        }
    }
    public void DebugCurrentState()
    {
        if (playerCamera == null)
        {
            Debug.LogError("Player Camera not assigned!");
            return;
        }

        if (referenceTilemap == null)
        {
            Debug.LogError("Reference Tilemap not assigned!");
            return;
        }

        BoundsInt bounds = GetVisibleTileBounds();
        Debug.Log("=== CULLING DEBUG ===");
        Debug.Log("Camera: " + playerCamera.transform.position);
        Debug.Log("Visible Bounds: " + bounds);
        Debug.Log("Registered Objects: " + allTilemapObjects.Count);

        if (allTilemapObjects.Count > 0)
        {
            var firstObj = allTilemapObjects[0];
            Debug.Log("First Object Tile Pos: " + firstObj.tilePosition);
            Debug.Log("First Object World Pos: " + firstObj.gameObject.transform.position);
            Debug.Log("Is in bounds: " + bounds.Contains(firstObj.tilePosition));
        }
    }
    void OnDrawGizmos()
    {
        if (debugMode && Application.isPlaying && referenceTilemap != null)
        {
            // Zeichne sichtbaren Bereich
            Gizmos.color = Color.yellow;
            Vector3 worldMin = referenceTilemap.CellToWorld(new Vector3Int(lastVisibleBounds.xMin, lastVisibleBounds.yMin, 0));
            Vector3 worldMax = referenceTilemap.CellToWorld(new Vector3Int(lastVisibleBounds.xMax, lastVisibleBounds.yMax, 0));

            Vector3 center = (worldMin + worldMax) / 2f;
            Vector3 size = worldMax - worldMin;

            Gizmos.DrawWireCube(center, size);

            // Zeichne registrierte Objekte
            Gizmos.color = Color.red;
            foreach (TilemapObject obj in allTilemapObjects)
            {
                if (obj.gameObject != null && !obj.isCurrentlyActive)
                {
                    Gizmos.DrawWireSphere(obj.gameObject.transform.position, 0.5f);
                }
            }

            Gizmos.color = Color.green;
            foreach (TilemapObject obj in allTilemapObjects)
            {
                if (obj.gameObject != null && obj.isCurrentlyActive)
                {
                    Gizmos.DrawWireSphere(obj.gameObject.transform.position, 0.3f);
                }
            }
        }
    }
}