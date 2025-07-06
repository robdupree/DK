using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapCullingManager : MonoBehaviour
{
    [Header("Tilemap References")]
    [SerializeField] private Tilemap[] tilemaps; // Alle deine Tilemaps (Boden, Wände, etc.)
    [SerializeField] private Camera playerCamera;

    [Header("Culling Settings")]
    [SerializeField] private float updateInterval = 0.1f; // Wie oft gecheckt wird
    [SerializeField] private int cullingBuffer = 2; // Extra Tiles am Rand
    [SerializeField] private bool debugMode = false;

    private float lastUpdateTime;
    private BoundsInt lastVisibleBounds;
    private Dictionary<Tilemap, TilemapRenderer> tilemapRenderers = new Dictionary<Tilemap, TilemapRenderer>();
    private Dictionary<Tilemap, BoundsInt> originalBounds = new Dictionary<Tilemap, BoundsInt>();

    void Start()
    {
        InitializeTilemaps();
    }

    void InitializeTilemaps()
    {
        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap != null)
            {
                TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
                if (renderer != null)
                {
                    tilemapRenderers[tilemap] = renderer;
                    originalBounds[tilemap] = tilemap.cellBounds;
                }
            }
        }
    }

    void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval) return;

        lastUpdateTime = Time.time;
        UpdateTilemapCulling();
    }

    void UpdateTilemapCulling()
    {
        // Berechne sichtbaren Bereich
        BoundsInt visibleBounds = GetVisibleTileBounds();

        // Nur updaten wenn sich der Bereich geändert hat
        if (visibleBounds != lastVisibleBounds)
        {
            lastVisibleBounds = visibleBounds;
            ApplyCullingToTilemaps(visibleBounds);
        }
    }

    BoundsInt GetVisibleTileBounds()
    {
        // Kamera Frustum Ecken in Weltkoordinaten
        Vector3[] frustumCorners = new Vector3[4];

        // Viewport Ecken zu Weltkoordinaten
        frustumCorners[0] = playerCamera.ViewportToWorldPoint(new Vector3(0, 0, playerCamera.nearClipPlane));
        frustumCorners[1] = playerCamera.ViewportToWorldPoint(new Vector3(1, 0, playerCamera.nearClipPlane));
        frustumCorners[2] = playerCamera.ViewportToWorldPoint(new Vector3(0, 1, playerCamera.nearClipPlane));
        frustumCorners[3] = playerCamera.ViewportToWorldPoint(new Vector3(1, 1, playerCamera.nearClipPlane));

        // Min/Max Weltkoordinaten finden
        Vector3 minWorld = frustumCorners[0];
        Vector3 maxWorld = frustumCorners[0];

        foreach (Vector3 corner in frustumCorners)
        {
            minWorld = Vector3.Min(minWorld, corner);
            maxWorld = Vector3.Max(maxWorld, corner);
        }

        // Weltkoordinaten zu Tile-Koordinaten (verwende erste Tilemap als Referenz)
        if (tilemaps.Length > 0 && tilemaps[0] != null)
        {
            Vector3Int minCell = tilemaps[0].WorldToCell(minWorld);
            Vector3Int maxCell = tilemaps[0].WorldToCell(maxWorld);

            // Buffer hinzufügen
            minCell.x -= cullingBuffer;
            minCell.y -= cullingBuffer;
            maxCell.x += cullingBuffer;
            maxCell.y += cullingBuffer;

            return new BoundsInt(minCell.x, minCell.y, 0,
                                maxCell.x - minCell.x + 1,
                                maxCell.y - minCell.y + 1, 1);
        }

        return new BoundsInt(0, 0, 0, 0, 0, 0);
    }

    void ApplyCullingToTilemaps(BoundsInt visibleBounds)
    {
        foreach (var kvp in tilemapRenderers)
        {
            Tilemap tilemap = kvp.Key;
            TilemapRenderer renderer = kvp.Value;

            if (tilemap != null && renderer != null)
            {
                // Beschränke die Rendering-Bounds auf den sichtbaren Bereich
                BoundsInt originalBound = originalBounds[tilemap];
                BoundsInt clampedBounds = ClampBounds(visibleBounds, originalBound);

                // Setze die Chunk Culling Bounds
                renderer.chunkCullingBounds = BoundsIntToVector3(clampedBounds);
            }
        }

        /* if (debugMode)
        {
            // Debug.Log($"Visible Bounds: {visibleBounds}");
        }
        */
    }

    BoundsInt ClampBounds(BoundsInt bounds, BoundsInt originalBounds)
    {
        // Stelle sicher, dass die Bounds innerhalb der originalen Tilemap liegen
        int minX = Mathf.Max(bounds.xMin, originalBounds.xMin);
        int minY = Mathf.Max(bounds.yMin, originalBounds.yMin);
        int maxX = Mathf.Min(bounds.xMax, originalBounds.xMax);
        int maxY = Mathf.Min(bounds.yMax, originalBounds.yMax);

        return new BoundsInt(minX, minY, 0, maxX - minX, maxY - minY, 1);
    }

    Vector3 BoundsIntToVector3(BoundsInt boundsInt)
    {
        return new Vector3(boundsInt.size.x, boundsInt.size.y, boundsInt.size.z);
    }

    void OnDrawGizmos()
    {
        if (debugMode && Application.isPlaying)
        {
            // Zeichne sichtbaren Bereich
            Gizmos.color = Color.green;
            if (tilemaps.Length > 0 && tilemaps[0] != null)
            {
                Vector3 worldMin = tilemaps[0].CellToWorld(new Vector3Int(lastVisibleBounds.xMin, lastVisibleBounds.yMin, 0));
                Vector3 worldMax = tilemaps[0].CellToWorld(new Vector3Int(lastVisibleBounds.xMax, lastVisibleBounds.yMax, 0));

                Vector3 center = (worldMin + worldMax) / 2f;
                Vector3 size = worldMax - worldMin;

                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}